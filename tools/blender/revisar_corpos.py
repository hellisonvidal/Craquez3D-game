# Revisa e alinha os esqueletos de Jogador.fbx e Goleiro.fbx sem alterar a malha de repouso, UVs ou pesos:
# - Corrige a coluna vertebral em zigue-zague do Goleiro (Spine/Spine1/Spine2 oscilavam ±10 cm em Y,
#   causando cisalhamento na torção do tronco e dobramento fora de centro no Humanoid da Unity);
# - Centraliza o pivô anteroposterior (Y) de coxa/joelho no centro anatômico da perna;
# - Simetriza perfeitamente os pares Left/Right (ombros, braços, mãos, quadris, joelhos, tornozelos e dedos);
# - Remove sufixos .001 de materiais exportados.
#
# Uso:
#   blender -b --factory-startup -P tools/blender/revisar_corpos.py -- Assets/Craquez/Models/Goleiro.fbx
#   blender -b --factory-startup -P tools/blender/revisar_corpos.py -- Assets/Craquez/Models/Jogador.fbx
import bpy, sys
import numpy as np
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
path = args[0]

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)

bpy.ops.import_scene.fbx(filepath=path, ignore_leaf_bones=False)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
body = [o for o in meshes if o.name == 'Body'][0]

if arm.animation_data:
    arm.animation_data.action = None
for pb in arm.pose.bones:
    pb.matrix_basis.identity()
bpy.context.view_layer.update()

# Captura posições originais dos vértices no mundo para garantir 0.0000 mm de alteração na malha de repouso
orig_verts = {m.name: np.array([(m.matrix_world @ v.co)[:] for v in m.data.vertices]) for m in meshes}
body_pts = orig_verts['Body']

aw = arm.matrix_world
awi = aw.inverted()

def slab_mid_y(z, half_x=0.13, dz=0.022):
    sl = body_pts[(np.abs(body_pts[:, 2] - z) < dz) & (np.abs(body_pts[:, 0]) < half_x)]
    if len(sl) < 6:
        return None
    return float(0.5 * (sl[:, 1].min() + sl[:, 1].max()))

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
P = 'mixamorig:'

def W(v):
    return aw @ v

def L(v):
    return awi @ v

# 1. Alinhamento suave e monotônico da coluna vertebral no plano sagital (x = 0)
hips_h = W(eb[P + 'Hips'].head)
head_h = W(eb[P + 'Head'].head)
head_t = W(eb[P + 'Head'].tail)

spine_names = ['Hips', 'Spine', 'Spine1', 'Spine2', 'Neck', 'Head']
new_heads = {}
new_heads['Hips'] = Vector((0.0, hips_h.y, hips_h.z))
new_heads['Head'] = Vector((0.0, head_h.y, head_h.z))

# Calcula curva anatômica centralizada dentro do tronco para Spine, Spine1, Spine2 e Neck
raw_y = {}
for nm in ('Spine', 'Spine1', 'Spine2', 'Neck'):
    bh = W(eb[P + nm].head)
    mid = slab_mid_y(bh.z, 0.09 if nm == 'Neck' else 0.14)
    t = (bh.z - hips_h.z) / max(1e-4, head_h.z - hips_h.z)
    line_y = hips_h.y * (1.0 - t) + head_h.y * t
    # Combina a linha suave Hips->Head com o centro real da seção transversal do tronco (sem zigue-zague)
    y_val = 0.55 * line_y + 0.45 * (mid if mid is not None else line_y)
    # Leve cifose torácica fisiológica (+8 mm em Spine1/Spine2)
    if nm in ('Spine1', 'Spine2'):
        y_val += 0.008
    raw_y[nm] = y_val

for nm in ('Spine', 'Spine1', 'Spine2', 'Neck'):
    bh = W(eb[P + nm].head)
    new_heads[nm] = Vector((0.0, raw_y[nm], bh.z))

# 2. Simetrização Left/Right e centralização anteroposterior das pernas
pairs = [
    'Shoulder', 'Arm', 'ForeArm', 'Hand', 'HandEnd',
    'UpLeg', 'Leg', 'Foot', 'ToeBase', 'Toe_End'
]
# Descobre também dedos das mãos ou folhas extras se existirem
for b in eb:
    s = b.name.replace(P, '')
    if s.startswith('Left'):
        suf = s[4:]
        if suf not in pairs and (P + 'Right' + suf) in eb:
            pairs.append(suf)

sym_heads = {}
sym_tails = {}
for suf in pairs:
    bl = eb.get(P + 'Left' + suf)
    br = eb.get(P + 'Right' + suf)
    if not bl or not br:
        continue
    lh, rh = W(bl.head), W(br.head)
    lt, rt = W(bl.tail), W(br.tail)
    hx = 0.5 * (abs(lh.x) + abs(rh.x))
    hy = 0.5 * (lh.y + rh.y)
    hz = 0.5 * (lh.z + rh.z)
    tx = 0.5 * (abs(lt.x) + abs(rt.x))
    ty = 0.5 * (lt.y + rt.y)
    tz = 0.5 * (lt.z + rt.z)

    # Centraliza UpLeg e Leg mais perto do eixo central da coxa/joelho quando estavam muito atrás (> +4.5 cm)
    if suf in ('UpLeg', 'Leg'):
        sl = body_pts[(np.abs(body_pts[:, 2] - hz) < 0.02) & (body_pts[:, 0] > 0.01) & (body_pts[:, 0] < hx + 0.12)]
        if len(sl) >= 8:
            mid_leg_y = float(0.5 * (sl[:, 1].min() + sl[:, 1].max()))
            hy = 0.5 * hy + 0.5 * mid_leg_y

    sym_heads['Left' + suf] = Vector((+hx, hy, hz))
    sym_heads['Right' + suf] = Vector((-hx, hy, hz))
    sym_tails['Left' + suf] = Vector((+tx, ty, tz))
    sym_tails['Right' + suf] = Vector((-tx, ty, tz))

# Aplica desconectando temporariamente para que caudas não arrastem cabeças fora de ordem
saved_conn = {b.name: b.use_connect for b in eb}
for b in eb:
    b.use_connect = False

# Aplica cabeças e caudas da coluna
chain_next = {'Hips': 'Spine', 'Spine': 'Spine1', 'Spine1': 'Spine2', 'Spine2': 'Neck', 'Neck': 'Head'}
for nm, nxt in chain_next.items():
    b = eb[P + nm]
    b.head = L(new_heads[nm])
    if nm == 'Hips':
        b.tail = L(Vector((0.0, new_heads['Hips'].y, new_heads['Spine'].z)))
    else:
        b.tail = L(new_heads[nxt])

b_head = eb[P + 'Head']
b_head.head = L(new_heads['Head'])
b_head.tail = L(Vector((0.0, new_heads['Head'].y, head_t.z)))
if (P + 'HeadTop_End') in eb:
    hte = eb[P + 'HeadTop_End']
    hte_len = max(0.05, (W(hte.tail) - W(hte.head)).length)
    hte.head = b_head.tail
    hte.tail = L(Vector((0.0, new_heads['Head'].y, head_t.z + hte_len)))

# Aplica pares simétricos mantendo continuidade pai->filho nas cadeias de membros
limb_next = {
    'Shoulder': 'Arm', 'Arm': 'ForeArm', 'ForeArm': 'Hand',
    'UpLeg': 'Leg', 'Leg': 'Foot', 'Foot': 'ToeBase'
}
for side in ('Left', 'Right'):
    for suf in pairs:
        key = side + suf
        if key not in sym_heads:
            continue
        b = eb[P + key]
        b.head = L(sym_heads[key])
        nxt = limb_next.get(suf)
        if nxt and (side + nxt) in sym_heads:
            b.tail = L(sym_heads[side + nxt])
        elif suf == 'ToeBase' and (side + 'Toe_End') in sym_heads:
            b.tail = L(sym_heads[side + 'Toe_End'])
        elif suf == 'Hand' and (side + 'HandEnd') in sym_heads:
            b.tail = L(sym_heads[side + 'HandEnd'])
        else:
            b.tail = L(sym_tails[key])

# Restaura use_connect apenas onde head == parent.tail
for b in eb:
    if b.parent and saved_conn.get(b.name, False):
        if (b.head - b.parent.tail).length < 1e-4:
            b.use_connect = True

# Recalcula roll consistente com o pipeline do projeto (GLOBAL_POS_Y)
bpy.ops.armature.select_all(action='SELECT')
bpy.ops.armature.calculate_roll(type='GLOBAL_POS_Y')
bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.update()

# Limpa nomes de materiais (.001 -> original) e verifica integridade da malha de repouso
for m in meshes:
    for slot in m.material_slots:
        if slot.material and '.' in slot.material.name:
            base_name = slot.material.name.split('.')[0]
            existing = bpy.data.materials.get(base_name)
            if existing and existing != slot.material:
                slot.material = existing
            else:
                slot.material.name = base_name
    cur = np.array([(m.matrix_world @ v.co)[:] for v in m.data.vertices])
    max_drift = float(np.max(np.abs(cur - orig_verts[m.name])))
    assert max_drift < 1e-5, f'ERRO: malha {m.name} deslocou {max_drift}'

print('REVISADO COM SUCESSO:', path)
for nm in ('Hips', 'Spine', 'Spine1', 'Spine2', 'Neck', 'Head', 'LeftUpLeg', 'RightUpLeg', 'LeftLeg', 'LeftFoot', 'LeftArm', 'RightArm'):
    b = arm.data.bones.get(P + nm)
    if b:
        wh = aw @ b.head_local
        wt = aw @ b.tail_local
        print(f'  {nm:12s} ({wh.x:+.4f}, {wh.y:+.4f}, {wh.z:.4f}) -> ({wt.x:+.4f}, {wt.y:+.4f}, {wt.z:.4f})')

bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
for m in meshes:
    m.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=path,
    use_selection=True,
    object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False,
    bake_anim=False,
    axis_forward='-Z',
    axis_up='Y',
    apply_scale_options='FBX_SCALE_UNITS',
    mesh_smooth_type='FACE',
    path_mode='STRIP',
    primary_bone_axis='Y',
    secondary_bone_axis='X'
)
print('EXPORTADO:', path)
