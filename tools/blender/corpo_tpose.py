# Braços em T (coluna e pernas como no modelo), altura final, exporta o FBX com esqueleto Mixamo.
# Uso: argv = [..., nome, altura, material, saida.fbx]
import bpy, sys
from mathutils import Vector, Matrix
name, H, matname, fbxout = sys.argv[-4], float(sys.argv[-3]), sys.argv[-2], sys.argv[-1]
bpy.ops.wm.open_mainfile(filepath=name+'_rig.blend')
arm=bpy.data.objects['Rig']; body=bpy.data.objects['Body']
P='mixamorig:'
T={'LeftShoulder':(1,0,0),'LeftArm':(1,0,0),'LeftForeArm':(1,0,0),'LeftHand':(1,0,0),
   'LeftUpLeg':(0.03,0,-1),'LeftLeg':(0.0,0.0,-1)}
for kk in list(T):
    if kk.startswith('Left'): x,y,z=T[kk]; T['Right'+kk[4:]]=(-x,y,z)
order=['LeftShoulder','LeftArm','LeftForeArm','LeftHand','RightShoulder','RightArm','RightForeArm','RightHand',
       'LeftUpLeg','LeftLeg','RightUpLeg','RightLeg']
bpy.context.view_layer.objects.active=arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='POSE')
for n in order:
    pb=arm.pose.bones[P+n]; bpy.context.view_layer.update()
    head=pb.head.copy(); q=(pb.tail-pb.head).normalized().rotation_difference(Vector(T[n]).normalized())
    pb.matrix=Matrix.Translation(head)@q.to_matrix().to_4x4()@Matrix.Translation(-head)@pb.matrix
bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); bpy.context.view_layer.objects.active=body
mn=[m for m in body.modifiers if m.type=='ARMATURE'][0].name
bpy.ops.object.modifier_copy(modifier=mn); bpy.ops.object.modifier_apply(modifier=mn)
for m in body.modifiers:
    if m.type=='ARMATURE': m.name='Armature'; m.object=arm
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.armature_apply(selected=False); bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.update()
zs=[(body.matrix_world@v.co).z for v in body.data.vertices]; zmin,zmax=min(zs),max(zs); s=H/(zmax-zmin)
hb=arm.matrix_world@arm.data.bones[P+'Hips'].head_local
arm.matrix_world=Matrix.Translation((-hb.x*s,-hb.y*s,-zmin*s))@Matrix.Scale(s,4)@arm.matrix_world
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
for p in body.data.polygons: p.use_smooth=True
body.data.materials.clear(); body.data.materials.append(bpy.data.materials.new(matname))
for b in arm.data.bones:
    if b.name.endswith(('Hips','Head','LeftHand','LeftFoot','LeftArm')): print("B",b.name,tuple(round(c,3) for c in b.head_local))
bpy.ops.wm.save_as_mainfile(filepath=name+'_final.blend')
bpy.ops.export_scene.fbx(filepath=fbxout, use_selection=True, object_types={'ARMATURE','MESH'},
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP')
print("EXPORT", fbxout)
