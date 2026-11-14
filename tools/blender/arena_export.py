import bpy, bmesh, statistics
from mathutils import Vector, Matrix
bpy.ops.wm.open_mainfile(filepath='/home/user/work/arena_dec.blend')
me=bpy.data.objects['Estadio']
cx,cy=(-0.787+0.776)/2,(0.565-0.531)/2; L=0.776+0.787; s=105.0/L
zs=[v.co.z for v in me.data.vertices if -0.7<v.co.x<0.7 and -0.45<v.co.y<0.45]
g=statistics.median(zs); print("ground z", g, "count", len(zs), "p10/p90", sorted(zs)[len(zs)//10], sorted(zs)[9*len(zs)//10])
bm=bmesh.new(); bm.from_mesh(me.data)
low=g+0.45/s
dele=[]
for f in bm.faces:
    c=f.calc_center_median()
    inner = -0.905<c.x<0.905 and -0.665<c.y<0.665
    bench = abs(c.x-cx)<0.26 and abs(c.y-cy)>0.53
    goal = (0.74<abs(c.x-cx)<0.90) and abs(c.y-cy)<0.20
    if goal or (inner and not bench and c.z<low): dele.append(f)
print("delete faces", len(dele), "of", len(bm.faces))
bmesh.ops.delete(bm, geom=dele, context='FACES')
bm.to_mesh(me.data); bm.free()
me.data.update()
d=me.modifiers.new('d','DECIMATE'); d.ratio=min(1.0,160000/len(me.data.polygons)); 
bpy.context.view_layer.objects.active=me; me.select_set(True); bpy.ops.object.modifier_apply(modifier='d')
for v in me.data.vertices: v.co=Vector(((v.co.x-cx)*s,(v.co.y-cy)*s,(v.co.z-g)*s))
me.data.update()
zs=[v.co.z for v in me.data.vertices]; print("tris", sum(len(p.vertices)-2 for p in me.data.polygons), "height range m", min(zs), max(zs))
xs=[v.co.x for v in me.data.vertices]; ys=[v.co.y for v in me.data.vertices]; print("x", min(xs), max(xs), "y", min(ys), max(ys))
for p in me.data.polygons: p.use_smooth=True
me.data.materials.clear(); me.data.materials.append(bpy.data.materials.new('Estadio_Mat'))
bpy.ops.wm.save_as_mainfile(filepath='/home/user/work/arena_final.blend')
bpy.ops.export_scene.fbx(filepath='/home/user/work/out/Estadio.fbx', use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP')
# preview
import math
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=12; sc.render.resolution_x=1200; sc.render.resolution_y=700
cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera=cam
cam.location=(40,-70,30); cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler()
sc.render.filepath='/home/user/work/arena_cut.png'; bpy.ops.render.render(write_still=True)
