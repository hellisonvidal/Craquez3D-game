import bpy, os, math
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=os.environ['INP'])
me=[o for o in bpy.data.objects if o.type=='MESH'][0]; me.name='Estadio'
bpy.context.view_layer.objects.active=me; me.select_set(True)
d=me.modifiers.new('d','DECIMATE'); d.ratio=400000/len(me.data.polygons); bpy.ops.object.modifier_apply(modifier='d')
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.wm.save_as_mainfile(filepath='/home/user/work/arena_dec.blend')
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=8; sc.render.resolution_x=2000; sc.render.resolution_y=2000
w=bpy.data.worlds.new('w'); sc.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs[1].default_value=3
cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type='ORTHO'; cam.data.ortho_scale=2.0; cam.location=(0,0,5); cam.rotation_euler=(0,0,0)
sc.render.filepath='/home/user/work/arena_top.png'; bpy.ops.render.render(write_still=True)
cam.location=(0,-5,0); cam.rotation_euler=(math.pi/2,0,0); sc.render.resolution_y=600; cam.data.ortho_scale=2.0
sc.render.filepath='/home/user/work/arena_front.png'; bpy.ops.render.render(write_still=True)
