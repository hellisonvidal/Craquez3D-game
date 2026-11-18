# Gera dribles, fintas, arrancada e freada do jogador (FBX só com o esqueleto do Jogador.fbx, 30 fps).
# Uso: python -I build.py -- MODELO.fbx PASTA_SAIDA   (bpy 4.x; com o Blender: blender -b -P build.py -- ...)
import bpy, os, sys, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'gkanim')); sys.path.insert(0, HERE)
import posekit, poses
args = sys.argv[sys.argv.index('--') + 1:]
MODEL, OUT = args[0], args[1]
os.makedirs(OUT, exist_ok=True)
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=MODEL, ignore_leaf_bones=False)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
for o in list(bpy.data.objects):
    if o.type != 'ARMATURE': bpy.data.objects.remove(o)
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
rig = posekit.Rig(arm)
sc = bpy.context.scene; sc.render.fps = 30

def P(n): return getattr(poses, n), getattr(poses, n + '_H', (0, 0, 0))
def M(n): s, h = P(n); return posekit.mirror(s, h)

CLIPS = {
    'FintaCorpoDir': [(0, P('BASE')), (6, P('F_AMEACA')), (11, P('F_CARGA')), (17, P('F_SAIDA')), (24, P('F_CORRIDA')), (30, P('BASE'))],
    'PedaladaDir':   [(0, P('BASE')), (5, P('P_ERGUE')), (10, P('P_VOLTA')), (14, P('P_APOIO')), (20, P('P_LEVA')), (27, P('F_CORRIDA')), (33, P('BASE'))],
    'PuxadaDir':     [(0, P('BASE')), (6, P('U_SOLA')), (12, P('U_PUXA')), (18, P('U_GIRA')), (25, P('BASE'))],
    'Arrancada':     [(0, P('BASE')), (4, P('A_IMPULSO')), (9, P('A_TROCA')), (14, P('A_IMPULSO')), (19, P('A_CORRE'))],
    'Freada':        [(0, P('R_CORRE')), (6, P('R_FREIA')), (12, P('R_FREIA')), (19, P('R_ASSENTA')), (26, P('BASE'))],
}
# versões espelhadas (para a esquerda)
for base in ('FintaCorpo', 'Pedalada', 'Puxada'):
    CLIPS[base + 'Esq'] = [(f, posekit.mirror(s, h)) for f, (s, h) in CLIPS[base + 'Dir']]

amostras = {}
for name, keys in CLIPS.items():
    act = bpy.data.actions.new(name)
    arm.animation_data_create(); arm.animation_data.action = act
    prev = None
    for f, (spec, off) in keys:
        rig.pose(spec, off)
        prev = rig.key(f, prev)
    end = keys[-1][0]
    sc.frame_start = 0; sc.frame_end = end
    # amostras para conferência (posição das articulações em quadros espaçados)
    fr = []
    for k in range(9):
        sc.frame_set(round(end * k / 8))
        bones = {}
        for pb in arm.pose.bones:
            h = arm.matrix_world @ pb.head
            bones[pb.name.replace('mixamorig:', '')] = [h.x, -h.y, h.z]
        fr.append({'t': end * k / 8 / 30, 'bones': bones})
    amostras[name] = fr
    arm.animation_data.action = None
    rig.reset()
    for a in list(bpy.data.actions):
        if a != act: bpy.data.actions.remove(a)
    bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, object_types={'ARMATURE'},
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', path_mode='STRIP')
    bpy.data.actions.remove(act)
    print('CLIP', name, end, 'quadros')
json.dump(amostras, open(os.path.join(OUT, 'amostras.json'), 'w'))
