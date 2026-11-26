# Gera dribles, fintas, arrancada, freada, parado (pés plantados) e quedas/levantar de futebol
# (FBX só com o esqueleto do Jogador.fbx, 30 fps, garantindo altura mínima acima do gramado).
# Uso: blender -b --factory-startup -P tools/blender/jogadoranim/build.py -- MODELO.fbx PASTA_SAIDA
import bpy, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'gkanim'))
sys.path.insert(0, HERE)
import posekit, poses

args = sys.argv[sys.argv.index('--') + 1:]
MODEL, OUT = args[0], args[1]
os.makedirs(OUT, exist_ok=True)
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=MODEL, ignore_leaf_bones=False)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
for o in list(bpy.data.objects):
    if o.type != 'ARMATURE':
        bpy.data.objects.remove(o)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)

rig = posekit.Rig(arm)
sc = bpy.context.scene
sc.render.fps = 30

# Altura de repouso do tornozelo para travar os pés 100% imóveis no clipe Parado
rig.reset()
rest_ankle_z = 0.5 * ((arm.matrix_world @ arm.pose.bones['mixamorig:LeftFoot'].head).z +
                      (arm.matrix_world @ arm.pose.bones['mixamorig:RightFoot'].head).z)

TORSO_CLEARANCE = {'Hips': 0.125, 'Spine': 0.135, 'Spine1': 0.145, 'Spine2': 0.150, 'Neck': 0.145, 'Head': 0.145}
LIMB_CLEARANCE = 0.055


def P(n):
    return getattr(poses, n), getattr(poses, n + '_H', (0, 0, 0))


def M(n):
    s, h = P(n)
    return posekit.mirror(s, h)


def apply_grounded_pose(clip_name, spec, off):
    rig.pose(spec, off)
    if clip_name == 'Parado':
        # Mantém os tornozelos cravados na exata altura de repouso (0,0 mm de oscilação nos pés!)
        cur_z = 0.5 * ((arm.matrix_world @ arm.pose.bones['mixamorig:LeftFoot'].head).z +
                       (arm.matrix_world @ arm.pose.bones['mixamorig:RightFoot'].head).z)
        dz = rest_ankle_z - cur_z
        if abs(dz) > 1e-6:
            rig.pose(spec, (off[0], off[1], off[2] + dz))
    elif clip_name in ('Queda', 'QuedaCostas', 'CaidoDor', 'Levantar'):
        # Garante que tronco, quadril, cabeça, cotovelos, joelhos e pés nunca penetrem abaixo do gramado (z=0)
        needed_dz = 0.0
        for pb in arm.pose.bones:
            short = pb.name.replace('mixamorig:', '')
            hz = (arm.matrix_world @ pb.head).z
            min_allowed = TORSO_CLEARANCE.get(short, LIMB_CLEARANCE)
            if hz < min_allowed:
                needed_dz = max(needed_dz, min_allowed - hz)
        if needed_dz > 1e-5:
            rig.pose(spec, (off[0], off[1], off[2] + needed_dz))


CLIPS = {
    'Parado':        [(0, P('IDLE_A')), (45, P('IDLE_B')), (90, P('IDLE_A'))],
    'FintaCorpoDir': [(0, P('BASE')), (6, P('F_AMEACA')), (11, P('F_CARGA')), (17, P('F_SAIDA')), (24, P('F_CORRIDA')), (30, P('BASE'))],
    'PedaladaDir':   [(0, P('BASE')), (5, P('P_ERGUE')), (10, P('P_VOLTA')), (14, P('P_APOIO')), (20, P('P_LEVA')), (27, P('F_CORRIDA')), (33, P('BASE'))],
    'PuxadaDir':     [(0, P('BASE')), (6, P('U_SOLA')), (12, P('U_PUXA')), (18, P('U_GIRA')), (25, P('BASE'))],
    'Arrancada':     [(0, P('BASE')), (4, P('A_IMPULSO')), (9, P('A_TROCA')), (14, P('A_IMPULSO')), (19, P('A_CORRE'))],
    'Freada':        [(0, P('R_CORRE')), (6, P('R_FREIA')), (12, P('R_FREIA')), (19, P('R_ASSENTA')), (26, P('BASE'))],
    'Queda':         [(0, P('BASE')), (5, P('Q_IMPACTO')), (11, P('Q_DESCIDA')), (18, P('Q_SOLO')), (28, P('Q_REPOUSO'))],
    'QuedaCostas':   [(0, P('BASE')), (5, P('QC_IMPACTO')), (13, P('QC_SOLO')), (24, P('QC_REPOUSO'))],
    'CaidoDor':      [(0, P('DOR_A')), (30, P('DOR_B')), (60, P('DOR_A'))],
    'Levantar':      [(0, P('Q_REPOUSO')), (10, P('LEV_APOIO')), (21, P('LEV_JOELHO')), (34, P('IDLE_A'))],
}
for base in ('FintaCorpo', 'Pedalada', 'Puxada'):
    CLIPS[base + 'Esq'] = [(f, posekit.mirror(s, h)) for f, (s, h) in CLIPS[base + 'Dir']]

for name, keys in CLIPS.items():
    act = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = act
    prev = None
    for f, (spec, off) in keys:
        apply_grounded_pose(name, spec, off)
        prev = rig.key(f, prev)
    end = keys[-1][0]
    sc.frame_start = 0
    sc.frame_end = end

    # Verifica estabilidade dos pés / altura mínima
    lf_zs, rf_zs, min_zs = [], [], []
    for f in range(0, end + 1):
        sc.frame_set(f)
        bpy.context.view_layer.update()
        lf_zs.append((arm.matrix_world @ arm.pose.bones['mixamorig:LeftFoot'].head).z)
        rf_zs.append((arm.matrix_world @ arm.pose.bones['mixamorig:RightFoot'].head).z)
        min_zs.append(min((arm.matrix_world @ pb.head).z for pb in arm.pose.bones))

    arm.animation_data.action = None
    rig.reset()
    for a in list(bpy.data.actions):
        if a != act:
            bpy.data.actions.remove(a)
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, name + '.fbx'),
        use_selection=True,
        object_types={'ARMATURE'},
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        axis_forward='-Z',
        axis_up='Y',
        apply_scale_options='FBX_SCALE_UNITS',
        path_mode='STRIP',
    )
    bpy.data.actions.remove(act)
    print(f'CLIP {name:14s}: {end:2d}f | LF_Z=[{min(lf_zs):.4f}..{max(lf_zs):.4f}] RF_Z=[{min(rf_zs):.4f}..{max(rf_zs):.4f}] minBoneZ={min(min_zs):.4f}')
