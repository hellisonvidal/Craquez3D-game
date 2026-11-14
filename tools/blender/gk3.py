# Goleiro v3 (modelo limpo do Meshy): reduz polígonos, abre os braços em T-pose, 1,90 m, exporta.
import bpy, os
from mathutils import Vector, Matrix
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=os.environ['INP'])
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; body=[o for o in bpy.data.objects if o.type=='MESH'][0]
arm.name='GK_Rig'; body.name='GK_Body'
if arm.animation_data: arm.animation_data.action=None
for pb in arm.pose.bones: pb.matrix_basis=Matrix()
exec(open('/home/user/tools/rigfix.py').read())
merge_weights(body, arm, {'Bone_030':'mixamorig:LeftHand','Bone_031':'mixamorig:RightHand'})
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); bpy.context.view_layer.objects.active=body
d=body.modifiers.new('dec','DECIMATE'); d.ratio=32000/len(body.data.polygons)
bpy.ops.object.modifier_move_to_index(modifier='dec', index=0); bpy.ops.object.modifier_apply(modifier='dec')
print("tris", sum(len(p.vertices)-2 for p in body.data.polygons))
bpy.context.view_layer.objects.active=arm; bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True)
bpy.ops.object.mode_set(mode='POSE')
P='mixamorig:'
T={'LeftShoulder':(1,0,0),'LeftArm':(1,0,0),'LeftForeArm':(1,0,0),'LeftHand':(1,0,0)}
for k in list(T): x,y,z=T[k]; T['Right'+k[4:]]=(-x,y,z)
for n in ['LeftShoulder','LeftArm','LeftForeArm','LeftHand','RightShoulder','RightArm','RightForeArm','RightHand']:
    pb=arm.pose.bones[P+n]; bpy.context.view_layer.update()
    head=pb.head.copy(); q=(pb.tail-pb.head).normalized().rotation_difference(Vector(T[n]).normalized())
    pb.matrix=Matrix.Translation(head)@q.to_matrix().to_4x4()@Matrix.Translation(-head)@pb.matrix
bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.objects.active=body; bpy.ops.object.select_all(action='DESELECT'); body.select_set(True)
mn=[m for m in body.modifiers if m.type=='ARMATURE'][0].name
bpy.ops.object.modifier_copy(modifier=mn); bpy.ops.object.modifier_apply(modifier=mn)
for m in body.modifiers:
    if m.type=='ARMATURE': m.name='Armature'; m.object=arm
bpy.context.view_layer.objects.active=arm; bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True)
bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.armature_apply(selected=False); bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.update()
zs=[(body.matrix_world@v.co).z for v in body.data.vertices]; zmin,zmax=min(zs),max(zs); s=1.90/(zmax-zmin)
hb=arm.matrix_world@arm.data.bones[P+'Hips'].head_local
arm.matrix_world=Matrix.Translation((-hb.x*s,-hb.y*s,-zmin*s))@Matrix.Scale(s,4)@arm.matrix_world
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
zs=[(body.matrix_world@v.co).z for v in body.data.vertices]; print("height",max(zs)-min(zs),"zmin",min(zs))
for p in body.data.polygons: p.use_smooth=True
body.data.materials.clear(); body.data.materials.append(bpy.data.materials.new('GK_Mat'))
bpy.ops.wm.save_as_mainfile(filepath='/home/user/work/gk3_final.blend')
bpy.ops.export_scene.fbx(filepath='/home/user/work/out/Goleiro.fbx', use_selection=True, object_types={'ARMATURE','MESH'},
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP')
