"""
Asset pipeline: FBX (Renderpeople / Sketchfab) -> optimized GLB for the web game.

Usage:
  blender -b --factory-startup -noaudio --python export_asset.py -- \
      --in <file.fbx> --out <file.glb> [--kind character|car|static] \
      [--tex 1024] [--decimate 0.35] [--fps 30] [--no-anim]

What it does:
  * imports FBX, strips helper empties that carry 0.01 scale where it is safe
  * character: keeps armature + skin, resamples/strips animations, renames the
    action to a stable name (walk / idle), downscales the 8K skin texture
  * car/static: bakes the whole hierarchy into one mesh in world space,
    normalizes the pivot to the ground-centre, applies a real-world size,
    decimates, cleans up broken/duplicate materials
  * always exports GLB with embedded textures (+Y up, metres)
  * prints a JSON block with the resulting bounds so the game manifest can use it
"""
import bpy, sys, os, json, math, argparse
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--in", dest="src", required=True)
ap.add_argument("--out", dest="dst", required=True)
ap.add_argument("--kind", default="static")
ap.add_argument("--tex", type=int, default=1024)
ap.add_argument("--decimate", type=float, default=0.0)
ap.add_argument("--fps", type=int, default=30)
ap.add_argument("--anim-name", default="")
ap.add_argument("--target-length", type=float, default=0.0)
ap.add_argument("--no-anim", action="store_true")
ap.add_argument("--frames", default="", help="trim animation, e.g. 1-250")
ap.add_argument("--strip-anims", action="store_true", help="drop all clips (skeleton kept)")
ap.add_argument("--canon", action="store_true", help="rename bones to canonical names (Root, Hip, Spine1...) so clips retarget between characters")
ap.add_argument("--join", action="store_true")
args = ap.parse_args(argv)


def log(*a):
    print("[pipeline]", *a, flush=True)


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def world_bounds(objs):
    mn = Vector((1e9, 1e9, 1e9))
    mx = Vector((-1e9, -1e9, -1e9))
    dg = bpy.context.evaluated_depsgraph_get()
    for o in objs:
        if o.type != "MESH":
            continue
        ev = o.evaluated_get(dg)
        mesh = ev.to_mesh()
        mw = o.matrix_world
        for v in mesh.vertices:
            w = mw @ v.co
            for i in range(3):
                mn[i] = min(mn[i], w[i])
                mx[i] = max(mx[i], w[i])
        ev.to_mesh_clear()
    return mn, mx


# ---------------------------------------------------------------- import
clear()
scene = bpy.context.scene
scene.render.fps = args.fps
log("importing", args.src)
bpy.ops.import_scene.fbx(filepath=args.src, use_anim=not args.no_anim,
                         ignore_leaf_bones=False, automatic_bone_orientation=True,
                         global_scale=1.0)

meshes = [o for o in bpy.data.objects if o.type == "MESH"]
armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
log(f"imported: {len(meshes)} mesh(es), {len(armatures)} armature(s), "
    f"{len(bpy.data.materials)} material(s), {len(bpy.data.images)} image(s)")

# ---------------------------------------------------------------- textures
for img in bpy.data.images:
    if img.size[0] == 0:
        continue
    w, h = img.size
    if max(w, h) > args.tex:
        s = args.tex / max(w, h)
        img.scale(max(1, int(w * s)), max(1, int(h * s)))
        log(f"resized {img.name} -> {img.size[0]}x{img.size[1]}")
    img.pack()

# ---------------------------------------------------------------- animation
if args.kind == "character" and not args.no_anim:
    fps = args.fps
    for act in bpy.data.actions:
        start, end = act.frame_range
        # retime: Renderpeople ships 24 fps takes, we export at args.fps
        ratio = fps / 24.0
        for fc in act.fcurves:
            for kp in fc.keyframe_points:
                kp.co.x = 1 + (kp.co.x - 1) * ratio
                kp.handle_left.x = 1 + (kp.handle_left.x - 1) * ratio
                kp.handle_right.x = 1 + (kp.handle_right.x - 1) * ratio
            fc.update()
        if args.anim_name:
            act.name = args.anim_name
        log(f"action {act.name!r} frames {start:.0f}-{end:.0f} -> "
            f"{1:.0f}-{1 + (end - start) * ratio:.0f}")
    if args.frames and bpy.data.actions:
        f0, f1 = [float(x) for x in args.frames.split("-")]
        for a in bpy.data.actions:
            for fc in list(a.fcurves):
                try:
                    for i in range(len(fc.keyframe_points) - 1, -1, -1):
                        if not (f0 <= fc.keyframe_points[i].co.x <= f1):
                            fc.keyframe_points.remove(fc.keyframe_points[i])
                    fc.update()
                except Exception as e:
                    log("trim skip", e)
    if bpy.data.actions:
        a = bpy.data.actions[0]
        scene.frame_start = int(a.frame_range[0])
        scene.frame_end = int(a.frame_range[1])
        scene.frame_set(scene.frame_start)

# ---------------------------------------------------------------- canonical bone names
_LCP = [""]

def lcp(seq):
    seq = list(seq)
    if not seq:
        return ""
    a, b = min(seq), max(seq)
    i = 0
    while i < min(len(a), len(b)) and a[i] == b[i]:
        i += 1
    return a[:i]

def canon(name: str) -> str:
    n = name
    if _LCP[0] and n.startswith(_LCP[0]):
        n = n[len(_LCP[0]):]
    out = "".join(w.capitalize() for w in n.replace("-", "_").split("_") if w)
    return out or name

if args.kind == "character" and args.canon:
    for arm in [o for o in bpy.data.objects if o.type == "ARMATURE"]:
        _LCP[0] = lcp([b.name for b in arm.data.bones])
        log("bone prefix stripped:", repr(_LCP[0]))
        mapping = {}
        for b in arm.data.bones:
            mapping[b.name] = canon(b.name)
        for b in arm.data.bones:
            b.name = mapping[b.name]
        # чиним пути в действиях
        for act in bpy.data.actions:
            for fc in act.fcurves:
                dp = fc.data_path
                if 'pose.bones["' in dp:
                    for old, new in mapping.items():
                        if f'pose.bones["{old}"]' in dp:
                            fc.data_path = dp.replace(f'pose.bones["{old}"]', f'pose.bones["{new}"]')
                            break
        log("bones renamed:", list(mapping.values())[:6], "...")

# ---------------------------------------------------------------- character mesh decimate
if args.kind == "character" and args.decimate and args.decimate < 1.0:
    for o in [x for x in bpy.data.objects if x.type == "MESH"]:
        mod = o.modifiers.new("dec", "DECIMATE")
        mod.ratio = args.decimate
        mod.use_collapse_triangulate = True
        # декъимация до арматуры — веса сохраняются в вершинных группах
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=mod.name)
        log("character decimated", o.name, "polys:", len(o.data.polygons))

if args.strip_anims:
    for act in list(bpy.data.actions):
        bpy.data.actions.remove(act)
    log("animations stripped")

# ---------------------------------------------------------------- flatten car/static
if args.kind in ("car", "static") and args.join:
    # drop pure empties, keep the mesh children of the deepest scales
    for o in list(bpy.data.objects):
        if o.type == "EMPTY" and len(o.children) == 0:
            bpy.data.objects.remove(o, do_unlink=True)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    # bake transforms
    for o in bpy.data.objects:
        o.select_set(False)
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.data.objects):
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    if len(meshes) > 1:
        for o in bpy.data.objects:
            o.select_set(o.type == "MESH")
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = os.path.splitext(os.path.basename(args.dst))[0]

    # normalize orientation: longest horizontal axis -> +X forward
    mn, mx = world_bounds([obj])
    dim = mx - mn
    log("raw bounds", [round(v, 3) for v in mn], [round(v, 3) for v in mx])
    if dim.y > dim.x:
        obj.rotation_euler[2] = math.radians(90)
        bpy.ops.object.transform_apply(rotation=True)
        mn, mx = world_bounds([obj])
        dim = mx - mn

    if args.target_length > 0:
        s = args.target_length / max(dim.x, 1e-6)
        obj.scale = (s, s, s)
        bpy.ops.object.transform_apply(scale=True)
        mn, mx = world_bounds([obj])
        dim = mx - mn

    # pivot: ground centre
    obj.location.x -= (mn.x + mx.x) / 2
    obj.location.y -= (mn.y + mx.y) / 2
    obj.location.z -= mn.z
    bpy.ops.object.transform_apply(location=True)
    log("normalized dims", [round(v, 3) for v in (mx - mn)])

    # decimate
    if args.decimate and args.decimate < 1.0:
        mod = obj.modifiers.new("dec", "DECIMATE")
        mod.ratio = args.decimate
        mod.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
        _b = world_bounds([obj]); log("bounds after decimate", [round(v,2) for v in _b[0]], [round(v,2) for v in _b[1]])
        log("after decimate tris:", len(obj.data.loop_triangles) or "n/a",
            "polys:", len(obj.data.polygons))

    # clean materials that lost their texture
    for mat in list(bpy.data.materials):
        if not mat.use_nodes:
            continue
        for n in mat.node_tree.nodes:
            if n.type == "TEX_IMAGE" and (n.image is None or n.image.size[0] == 0):
                mat.node_tree.nodes.remove(n)
        bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf:
            c = bsdf.inputs["Base Color"]
            if not c.is_linked and c.default_value[3] == 0:
                c.default_value = (0.5, 0.5, 0.5, 1)

# ---------------------------------------------------------------- export
mn, mx = world_bounds([o for o in bpy.data.objects if o.type == "MESH"])
os.makedirs(os.path.dirname(args.dst), exist_ok=True)
bpy.ops.export_scene.gltf(
    filepath=args.dst,
    export_format="GLB",
    use_selection=False,
    export_apply=True,
    export_yup=True,
    export_texcoords=True,
    export_normals=True,
    export_materials="EXPORT",
    export_image_format="JPEG",
    export_jpeg_quality=88,
    export_animations=not args.no_anim,
    export_animation_mode="ACTIONS",
    export_frame_range=True,
    export_force_sampling=True,
    export_optimize_animation_size=True,
    export_bake_animation=False,
    export_anim_slide_to_zero=True,
    export_skins=True,
    export_def_bones=False,
    export_morph=False,
)

# re-read bounds in three.js space (glTF: Y up, Z forward-ish)
size = mx - mn
info = {
    "out": args.dst,
    "bytes": os.path.getsize(args.dst),
    "blender_bounds_min": [round(v, 3) for v in mn],
    "blender_bounds_max": [round(v, 3) for v in mx],
    "size_xyz": [round(v, 3) for v in size],
    "tris": sum(len(o.data.polygons) for o in bpy.data.objects if o.type == "MESH"),
}
print("PIPELINE_JSON " + json.dumps(info))
log("done", args.dst)
