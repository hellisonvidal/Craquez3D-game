# Endireita a pose de repouso (T-pose canônica igual à do X Bot do Mixamo): pernas retas, pés para frente,
# coluna/pescoço/cabeça e braços alinhados. A Unity (Humanoid) usa a pose de repouso como referência;
# pernas inclinadas para trás no repouso viram pés "tortos para trás" em todas as animações.
# Uso: INP=modelo.fbx OUT=saida.fbx REF="X Bot.fbx"
import bpy, os
from mathutils import Vector, Matrix, Quaternion
P='mixamorig:'
def load(path):
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new=[o for o in bpy.data.objects if o not in before]
    return [o for o in new if o.type=='ARMATURE'][0], [o for o in new if o.type=='MESH']
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
ref,refm=load(os.environ['REF'])
bpy.context.view_layer.update()
def head(a,n): return a.matrix_world @ a.pose.bones[P+n].head
SP=float(os.environ.get('SPINE','1'))
CH=[('Spine','Spine1'),('Spine1','Spine2'),('Spine2','Neck'),('Neck','Head'),
    ('LeftShoulder','LeftArm'),('LeftArm','LeftForeArm'),('LeftForeArm','LeftHand'),
    ('RightShoulder','RightArm'),('RightArm','RightForeArm'),('RightForeArm','RightHand'),
    ('LeftUpLeg','LeftLeg'),('LeftLeg','LeftFoot'),('LeftFoot','LeftToeBase'),
    ('RightUpLeg','RightLeg'),('RightLeg','RightFoot'),('RightFoot','RightToeBase')]
target={a:(head(ref,b)-head(ref,a)).normalized() for a,b in CH}
for o in [ref]+refm: bpy.data.objects.remove(o)
arm,meshes=load(os.environ['INP'])
if arm.animation_data: arm.animation_data.action=None
for pb in arm.pose.bones: pb.matrix_basis=Matrix()
bpy.context.view_layer.update()
bpy.context.view_layer.objects.active=arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='POSE')
M=arm.matrix_world; Mi=M.inverted()
for it in range(2):
    for a,b in CH:
        bpy.context.view_layer.update()
        ha=head(arm,a); d=(head(arm,b)-ha).normalized()
        q=d.rotation_difference(target[a])
        if a in ('Spine','Spine1','Spine2','Neck'): q=Quaternion().slerp(q,SP)
        if it==0: print(f"{a:14s} desvio {d.angle(target[a])*57.2958:5.1f} graus")
        pb=arm.pose.bones[P+a]
        W=Matrix.Translation(ha)@q.to_matrix().to_4x4()@Matrix.Translation(-ha)@(M@pb.matrix)
        pb.matrix=Mi@W
bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode='OBJECT')
for body in meshes:
    bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); bpy.context.view_layer.objects.active=body
    mn=[m for m in body.modifiers if m.type=='ARMATURE'][0].name
    bpy.ops.object.modifier_copy(modifier=mn); bpy.ops.object.modifier_apply(modifier=mn)
    for m in body.modifiers:
        if m.type=='ARMATURE': m.object=arm
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.armature_apply(selected=False); bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.update()
# pés no chão
zs=[(b.matrix_world@v.co).z for b in meshes for v in b.data.vertices]; zmin=min(zs)
arm.location.z-=zmin
bpy.context.view_layer.update()
for a,b in CH:
    d=(head(arm,b)-head(arm,a)).normalized()
    if d.angle(target[a])>0.02 and a not in ('Spine','Spine1','Spine2','Neck'): print("AINDA DESALINHADO", a, d.angle(target[a])*57.3)
print("altura", max(zs)-zmin)
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True)
for b in meshes: b.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.environ['OUT'], use_selection=True, object_types={'ARMATURE','MESH'},
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP')
print("OK", os.environ['OUT'])
