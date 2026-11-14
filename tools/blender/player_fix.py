# Jogador: passa os pesos dos ossos extras das mãos para LeftHand/RightHand (pose de repouso inalterada).
import bpy, os
from mathutils import Matrix
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=os.environ['INP'])
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; body=[o for o in bpy.data.objects if o.type=='MESH'][0]
if arm.animation_data: arm.animation_data.action=None
for pb in arm.pose.bones: pb.matrix_basis=Matrix()
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
exec(open('/home/user/tools/rigfix.py').read())
L='mixamorig:LeftHand'; R='mixamorig:RightHand'
merge_weights(body, arm, {'Bone_019':L,'Bone_020':L,'Bone_021':L,'Bone_022':L,'Bone_023':L,'Bone_035':L,'Bone_036':R})
body.data.materials.clear(); body.data.materials.append(bpy.data.materials.new('Jogador_Mat'))
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath='/home/user/work/out/Jogador.fbx', use_selection=True, object_types={'ARMATURE','MESH'},
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP')
print("PLAYER ok")
