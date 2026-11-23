import bpy, sys, math, mathutils
argv = sys.argv[sys.argv.index('--')+1:]
src, out, mode = argv[0], argv[1], (argv[2] if len(argv) > 2 else 'orbit')
bpy.ops.wm.read_factory_settings(use_empty=True)
if src.endswith('.fbx'):
    bpy.ops.import_scene.fbx(filepath=src)
elif src.endswith('.blend'):
    with bpy.data.libraries.load(src) as (s, d):
        d.objects = s.objects
    for o in d.objects:
        if o is not None and hasattr(o, 'name'):
            bpy.context.scene.collection.objects.link(o)
else:
    bpy.ops.import_scene.gltf(filepath=src)
mn=mathutils.Vector((1e9,)*3); mx=mathutils.Vector((-1e9,)*3)
for o in bpy.data.objects:
    if o.type=='MESH':
        for c in o.bound_box:
            w=o.matrix_world@mathutils.Vector(c)
            for i in range(3): mn[i]=min(mn[i],w[i]); mx[i]=max(mx[i],w[i])
center=(mn+mx)/2; size=(mx-mn)
cam_data=bpy.data.cameras.new("c"); cam=bpy.data.objects.new("c",cam_data)
bpy.context.scene.collection.objects.link(cam)
if mode=='top':
    s=max(size.x,size.y)
    cam.location=center+mathutils.Vector((0.001, 0, s*0.75))
    cam.rotation_euler=(0,0,0)
else:
    s=max(size.x,size.y,size.z)
    cam.location=center+mathutils.Vector((s*0.35,-s*0.42,s*0.28))
    cam.rotation_euler=((center-cam.location).normalized()).to_track_quat('-Z','Y').to_euler()
cam_data.clip_start = max(0.1, size.length*0.001)
cam_data.clip_end = size.length*8
bpy.context.scene.camera=cam
bpy.data.objects.new("s", bpy.data.lights.new("s",'SUN')).data.energy=3.5
sun=bpy.data.objects["s"]; bpy.context.scene.collection.objects.link(sun); sun.data=bpy.data.lights['s']; sun.data.energy=3.5
sun.rotation_euler=(math.radians(50),0,math.radians(30))
w=bpy.data.worlds.new("W"); bpy.context.scene.world=w; w.use_nodes=True
w.node_tree.nodes["Background"].inputs[0].default_value=(0.3,0.35,0.45,1)
w.node_tree.nodes["Background"].inputs[1].default_value=1.3
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=18
sc.render.resolution_x=900; sc.render.resolution_y=560; sc.render.filepath=out
sc.render.image_settings.file_format='PNG'
bpy.ops.render.render(write_still=True)
print("PREVIEW_DONE", out)
