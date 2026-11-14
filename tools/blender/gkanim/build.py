# Gera as animações do goleiro (FBX só com o esqueleto) a partir das poses-chave.
import bpy, os, sys
sys.path.insert(0,'/home/user/tools/gkanim')
import posekit, poses
OUT=os.environ.get('OUTDIR','/home/user/work/gkanim')
os.makedirs(OUT, exist_ok=True)
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=os.environ.get('GK','/home/user/Craquez3D-game/Assets/Craquez/Models/Goleiro.fbx'))
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
for o in list(bpy.data.objects):
    if o.type!='ARMATURE': bpy.data.objects.remove(o)
rig=posekit.Rig(arm)
sc=bpy.context.scene; sc.render.fps=30

def P(name): return getattr(poses,name), getattr(poses,name+'_H',(0,0,0))
def M(name):
    s,h=P(name); return posekit.mirror(s,h)

CLIPS={
 'GK_Pronto':      [(0,P('READY')),(24,P('READY2')),(48,P('READY'))],
 'GK_DeslocarDir': [(0,P('SHUF_A')),(10,P('SHUF_B')),(20,P('SHUF_A'))],
 'GK_DeslocarEsq': [(0,M('SHUF_A')),(10,M('SHUF_B')),(20,M('SHUF_A'))],
 'GK_Mergulho':    [(0,P('READY')),(4,P('D_LOAD')),(9,P('D_LAUNCH')),(15,P('D_FLY')),(24,P('D_LAND')),(34,P('D_LIE')),(42,P('D_LIE'))],
 'GK_Caido':       [(0,P('D_LIE')),(30,P('D_LIE'))],
 'GK_Levantar':    [(0,P('D_LIE')),(10,P('G_PUSH')),(22,P('G_KNEEL')),(36,P('READY'))],
 'GK_Salto':       [(0,P('READY')),(5,P('J_CROUCH')),(10,P('J_TAKE')),(15,P('J_PEAK')),(21,P('J_CATCH')),(30,P('J_LAND'))],
 'GK_Defesa':      [(0,P('READY')),(6,P('B_REACH')),(14,P('B_HUG')),(24,P('B_HUG'))],
}
only=os.environ.get('ONLY')
for name,keys in CLIPS.items():
    if only and name not in only.split(','): continue
    act=bpy.data.actions.new(name)
    arm.animation_data_create(); arm.animation_data.action=act
    prev=None
    for f,(spec,off) in keys:
        rig.pose(spec,off)
        prev=rig.key(f,prev)
    sc.frame_start=0; sc.frame_end=keys[-1][0]
    # o exportador grava a pose ATUAL como repouso: volta à T-pose (sem ação ativa) e exporta "todas as ações" (só esta existe)
    arm.animation_data.action=None
    rig.reset()
    for a in list(bpy.data.actions):
        if a!=act: bpy.data.actions.remove(a)
    bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active=arm
    bpy.ops.export_scene.fbx(filepath=f"{OUT}/{name}.fbx", use_selection=True, object_types={'ARMATURE'},
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', path_mode='STRIP')
    print("CLIP", name, keys[-1][0], "quadros")
