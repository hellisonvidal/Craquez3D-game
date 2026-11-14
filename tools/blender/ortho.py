import bpy, os, mathutils, math
bpy.ops.wm.open_mainfile(filepath=os.environ['BLEND'])
sc=bpy.context.scene
sc.render.engine='CYCLES'; sc.cycles.samples=8
R=int(os.environ.get('RES','1000')); sc.render.resolution_x=R; sc.render.resolution_y=R
w=bpy.data.worlds.new('w'); sc.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs[1].default_value=2.5
cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type='ORTHO'; S=float(os.environ.get('OS','2.0')); cam.data.ortho_scale=S
cz=float(os.environ.get('CZ','0.94'))
for name,loc,rot in [('front',(0,-5,cz),(math.pi/2,0,0)),('side',(5,0,cz),(math.pi/2,0,math.pi/2))]:
    cam.location=loc; cam.rotation_euler=rot
    sc.render.filepath=os.environ['OUT']+'_'+name+'.png'
    bpy.ops.render.render(write_still=True)
