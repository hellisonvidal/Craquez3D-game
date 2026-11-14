# Transfere as animações nativas do Meshy (mesmo esqueleto do jogador) para o rig corrigido (T-pose canônica),
# reproduzindo exatamente a mesma deformação da malha: R_alvo(f) = R_fonte(f) · R_fonte_rep⁻¹ · R_alvo_rep.
# Exporta cada clipe só com o esqueleto, com a T-pose gravada como repouso.
# Uso: SRC=meshy_animado.fbx TGT=Jogador.fbx OUTDIR=... MAP="Running:Correr,Walking:Andar"
import bpy, os
from mathutils import Matrix, Vector
def load(path):
    before=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=path)
    new=[o for o in bpy.data.objects if o not in before]
    return [o for o in new if o.type=='ARMATURE'][0], [o for o in new if o.type!='ARMATURE']
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
src,junk=load(os.environ['SRC'])
for o in junk: bpy.data.objects.remove(o)
acts={a.name.split('|')[1] if '|' in a.name else a.name: a for a in bpy.data.actions}
print("ações", list(acts))
tgt,junk=load(os.environ['TGT'])
for o in junk: bpy.data.objects.remove(o)
if tgt.animation_data: tgt.animation_data.action=None
sc=bpy.context.scene
def worldmats(arm):
    return {pb.name:(arm.matrix_world@pb.matrix).copy() for pb in arm.pose.bones}
src.animation_data_create(); src.animation_data.action=None
for pb in src.pose.bones: pb.matrix_basis=Matrix()
for pb in tgt.pose.bones: pb.matrix_basis=Matrix()
bpy.context.view_layer.update()
SR=worldmats(src); TR=worldmats(tgt)
names=[b.name for b in tgt.data.bones if b.name in SR]
for pb in tgt.pose.bones: pb.rotation_mode='QUATERNION'
Mi=tgt.matrix_world.inverted()
for pair in os.environ['MAP'].split(','):
    an,out=pair.split(':')
    act=acts[an]
    src.animation_data.action=act
    f0,f1=[int(round(x)) for x in act.frame_range]
    new=bpy.data.actions.new(out); tgt.animation_data_create(); tgt.animation_data.action=new
    prev={}
    for f in range(f0,f1+1):
        sc.frame_set(f); bpy.context.view_layer.update()
        S=worldmats(src)
        for n in names:  # pais antes dos filhos
            pb=tgt.pose.bones[n]
            W=S[n]@SR[n].inverted()@TR[n]
            pb.matrix=Mi@W
            bpy.context.view_layer.update()
        for n in names:
            pb=tgt.pose.bones[n]
            q=pb.rotation_quaternion.copy()
            if n in prev and prev[n].dot(q)<0: q.negate()
            pb.rotation_quaternion=q; prev[n]=q
            pb.keyframe_insert('rotation_quaternion', frame=f-f0)
            if pb.parent is None or n.endswith('Hips'): pb.keyframe_insert('location', frame=f-f0)
    sc.frame_start=0; sc.frame_end=f1-f0
    # o exportador grava a pose atual como repouso e exporta "todas as ações": deixa só a nova
    tgt.animation_data.action=None
    for pb in tgt.pose.bones: pb.matrix_basis=Matrix()
    src.animation_data.action=None
    for a in list(bpy.data.actions):
        if a!=new: bpy.data.actions.remove(a)
    # mesmo nome de esqueleto do modelo (a Unity compara a hierarquia ao reaproveitar o avatar)
    bpy.data.objects.remove(src)
    tgt.name=os.environ.get('RIGNAME','SmartRigArmature')
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT'); tgt.select_set(True); bpy.context.view_layer.objects.active=tgt
    bpy.ops.export_scene.fbx(filepath=os.path.join(os.environ['OUTDIR'], out+'.fbx'), use_selection=True, object_types={'ARMATURE'},
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', path_mode='STRIP')
    print("CLIP", an, "->", out, f1-f0, "quadros")
    break  # uma ação por execução (as do Meshy foram removidas)
