import bpy, os, mathutils, math
path=os.environ['INP']; out=os.environ['OUT']; views=os.environ.get('VIEWS','front').split(',')
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=path)
meshes=[o for o in bpy.data.objects if o.type=='MESH']
bpy.context.view_layer.update()
pts=[o.matrix_world @ mathutils.Vector(c) for o in meshes for c in o.bound_box]
mn=mathutils.Vector([min(p[i] for p in pts) for i in range(3)]); mx=mathutils.Vector([max(p[i] for p in pts) for i in range(3)])
ctr=(mn+mx)/2; size=(mx-mn).length
sc=bpy.context.scene
sc.render.engine='CYCLES'; sc.cycles.samples=16; sc.cycles.device='CPU'
sc.render.resolution_x=900; sc.render.resolution_y=700
w=bpy.data.worlds.new('w'); sc.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs[1].default_value=1.5
cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera=cam
dirs={'front':(0,-1,0.35),'side':(1,0,0.3),'top':(0,-0.3,1),'back':(0,1,0.35),'iso':(1,-1,0.8)}
for v in views:
    d=mathutils.Vector(dirs[v]).normalized()
    cam.location=ctr+d*size*1.3
    cam.rotation_euler=(ctr-cam.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=f"{out}_{v}.png"
    bpy.ops.render.render(write_still=True)
