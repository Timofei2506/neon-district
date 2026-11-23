"""
Манхэттен -> Unity-готовый FBX + JSON-навигация.

Что делаем:
  * разворачиваем город так, чтобы главные улицы шли «ровно» (иначе трафик и
    пешеходы будут ездить по диагонали);
  * сажаем на землю и центрируем по XZ;
  * сохраняем FBX с относительными путями к текстурам (папка Textures рядом);
  * считаем границы и масштаб для Unity.
"""
import bpy, sys, os, json, math
import numpy as np

argv = sys.argv[sys.argv.index('--')+1:]
src, dst_fbx, dst_json = argv[0], argv[1], argv[2]
angle = float(argv[3]) if len(argv) > 3 else 0.0

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)

# корневой объект: поворачиваем всё вокруг Z (в Blender Z — вверх)
root = bpy.data.objects.new("ManhattanRoot", None)
bpy.context.scene.collection.objects.link(root)
for o in list(bpy.data.objects):
    if o.parent is None and o is not root and o.type in {'MESH', 'EMPTY'}:
        o.parent = root
root.rotation_euler[2] = math.radians(-angle)
bpy.context.view_layer.update()

# габариты и посадка на землю
dg = bpy.context.evaluated_depsgraph_get()
mn = np.array([1e18]*3); mx = np.array([-1e18]*3)
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    for c in o.bound_box:
        w = np.array(o.matrix_world @ __import__('mathutils').Vector(c))
        mn = np.minimum(mn, w); mx = np.maximum(mx, w)
size = mx - mn
root.location[0] = -(mn[0] + mx[0]) / 2
root.location[1] = -(mn[1] + mx[1]) / 2
root.location[2] = -mn[2]
bpy.context.view_layer.update()

for o in bpy.data.objects: o.select_set(o.type in {'MESH', 'EMPTY'})
bpy.ops.export_scene.fbx(
    filepath=dst_fbx, use_selection=True,
    apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
    axis_forward='-Z', axis_up='Y', bake_space_transform=False,
    use_mesh_modifiers=False, mesh_smooth_type='FACE',
    bake_anim=False, path_mode='RELATIVE', embed_textures=False,
    use_tspace=True,
)

info = {
    "size": [round(float(v), 1) for v in size],
    "bounds_unrotated": {"min": [round(float(v), 1) for v in mn], "max": [round(float(v), 1) for v in mx]},
    "rotation_deg": -angle,
    "center": [0.0, 0.0],
    "playable_radius": round(float(max(size[0], size[1]) * 0.45), 1),
}
with open(dst_json, 'w') as f:
    json.dump(info, f, indent=1)
print("MANHATTAN_DONE", json.dumps(info))
