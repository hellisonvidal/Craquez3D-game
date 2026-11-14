import bpy, os, math
from mathutils import Vector
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=os.environ['INP'])
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=4; sc.render.resolution_x=360; sc.render.resolution_y=360
w=bpy.data.worlds.new('w'); sc.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs[1].default_value=2
cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type='ORTHO'; cam.data.ortho_scale=3.4; cam.location=(0,-6,1.0); cam.rotation_euler=(math.pi/2,0,0)
for f in [int(x) for x in os.environ['FR'].split(',')]:
    sc.frame_set(f); sc.render.filepath=f"{os.environ['OUT']}_{f:03d}.png"; bpy.ops.render.render(write_still=True)
