"""
Pack FBX -> Unity-ready FBX.
Unity expects: metres, Y up, -Z forward, pivot at ground centre, no 0.01-scale wrappers.
usage: blender -b --python export_unity_fbx.py -- <src> <dst> <kind> [target_len]
kind: character | car | static
"""
import bpy, sys, os, json, math


from mathutils import Vector
argv = sys.argv[sys.argv.index('--')+1:]
src, dst = argv[0], argv[1]
kind = argv[2] if len(argv) > 2 else 'static'
target_len = float(argv[3]) if len(argv) > 3 else 0.0

bpy.ops.wm.read_factory_settings(use_empty=True)
if src.endswith('.fbx'):
    bpy.ops.import_scene.fbx(filepath=src, use_anim=True, automatic_bone_orientation=True)
elif src.endswith('.blend'):
    with bpy.data.libraries.load(src) as (s, d):
        d.objects = s.objects
        for o in d.objects:
            if o: bpy.context.scene.collection.objects.link(o)
else:
    bpy.ops.import_scene.gltf(filepath=src)

def bounds(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    mn = Vector((1e9,)*3); mx = Vector((-1e9,)*3)
    for o in objs:
        if o.type != 'MESH': continue
        ev = o.evaluated_get(dg); me = ev.to_mesh()
        for v in me.vertices:
            w = o.matrix_world @ v.co
            for i in range(3):
                mn[i] = min(mn[i], w[i]); mx[i] = max(mx[i], w[i])
        ev.to_mesh_clear()
    return mn, mx

meshes = [o for o in bpy.data.objects if o.type == 'MESH']

def mesh_bounds(obj):
    """габариты по вершинам локального меша (без ops и кешей)"""
    import numpy as np
    me = obj.data
    n = len(me.vertices)
    co = np.empty(n * 3, dtype=np.float64)
    me.vertices.foreach_get('co', co)
    co = co.reshape(-1, 3)
    return co.min(axis=0), co.max(axis=0), co

if kind in ('car', 'static'):
    from mathutils import Matrix
    import numpy as np

    for o in bpy.data.objects: o.select_set(False)
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    for o in list(bpy.data.objects):
        if o.type != 'MESH': bpy.data.objects.remove(o, do_unlink=True)
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    if len(meshes) > 1:
        bpy.ops.object.select_all(action='DESELECT')
        for o in meshes: o.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    # запекаем мировую матрицу в меш — дальше работаем только с вершинами
    obj.data.transform(obj.matrix_world)
    obj.matrix_world = Matrix.Identity(4)

    mn, mx, co = mesh_bounds(obj)
    dim = mx - mn
    print("  raw dims: %.2f x %.2f x %.2f" % tuple(dim))
    # длинная горизонтальная ось должна стать X
    if dim[1] > dim[0] * 1.02:
        obj.data.transform(Matrix.Rotation(math.radians(90), 4, 'Z'))
        mn, mx, co = mesh_bounds(obj)
        dim = mx - mn
        print("  rotated -> %.2f x %.2f x %.2f" % tuple(dim))
    # ставим днищем на ноль и центрируем по XZ
    if target_len > 0:
        s_ = target_len / max(dim[0], 1e-6)
        obj.data.transform(Matrix.Scale(s_, 4))
        mn, mx, co = mesh_bounds(obj)
        dim = mx - mn
    offset = Matrix.Translation((-((mn[0]+mx[0])/2), -((mn[1]+mx[1])/2), -mn[2]))
    obj.data.transform(offset)
    mn, mx, co = mesh_bounds(obj)
    obj.name = os.path.splitext(os.path.basename(dst))[0]
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={'MESH'},
        apply_scale_options='FBX_SCALE_ALL', global_scale=1.0, axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, use_mesh_modifiers=True, mesh_smooth_type='FACE',
        bake_anim=False, path_mode='COPY', embed_textures=False, use_tspace=True)
else:
    mn, mx = bounds(meshes)
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=False,
        object_types={'MESH', 'ARMATURE'},
        apply_scale_options='FBX_SCALE_ALL', global_scale=1.0, axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, use_mesh_modifiers=False, mesh_smooth_type='FACE',
        bake_anim=True, bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=False, add_leaf_bones=False,
        primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
        path_mode='COPY', embed_textures=False, use_tspace=True)

mn2, mx2 = bounds([o for o in bpy.data.objects if o.type == "MESH"])
size = mx2 - mn2
print("UNITY_FBX_JSON " + json.dumps({
    "dst": dst, "bytes": os.path.getsize(dst),
    "min": [round(v,3) for v in mn2], "max": [round(v,3) for v in mx2],
    "size": [round(v,3) for v in size],
    "actions": [a.name for a in bpy.data.actions],
    "mats": len(bpy.data.materials),
}))
