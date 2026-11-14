import bpy, os
from mathutils import Vector
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=os.environ['INP'])
me=[o for o in bpy.data.objects if o.type=='MESH'][0]; me.name='Bola'
bpy.context.view_layer.objects.active=me; me.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.remove_doubles(threshold=0.0001); bpy.ops.object.mode_set(mode='OBJECT')
d=me.modifiers.new('d','DECIMATE'); d.ratio=8000/len(me.data.polygons); bpy.ops.object.modifier_apply(modifier='d')
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
vs=[v.co.copy() for v in me.data.vertices]; c=sum(vs,Vector())/len(vs)
r=sum((v-c).length for v in vs)/len(vs); s=0.11/r
for v in me.data.vertices: v.co=(v.co-c).normalized()*0.11
for p in me.data.polygons: p.use_smooth=True
me.data.materials.clear(); me.data.materials.append(bpy.data.materials.new('Bola_Mat'))
rs=[v.co.length for v in me.data.vertices]; print("tris",sum(len(p.vertices)-2 for p in me.data.polygons),"radius min/max",min(rs),max(rs))
bpy.ops.export_scene.fbx(filepath='/home/user/work/out/Bola.fbx', use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP')
