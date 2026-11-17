# Corrige as proporções dos corpos (Jogador.fbx / Goleiro.fbx) para as de uma pessoa normal,
# usando como referência o esqueleto dos Universal Base Characters / Universal Animation Library
# da Quaternius (CC0, Assets/Craquez/Animations/Quaternius): quadril a ~51% da altura, joelho a ~29%,
# ombros na mesma altura e pelve mais estreita. Os modelos antigos tinham tronco ~30% comprido e pernas
# curtas (sobretudo a canela).
#
# Como funciona: na pose de repouso, cada osso é escalado ao longo do próprio comprimento (canela, coxa,
# coluna) e a pelve é estreitada; os filhos não herdam a escala (pés, mãos, cabeça e braços mantêm o tamanho).
# A malha deformada vira a nova malha de repouso e a pose vira o novo repouso. UVs, pesos, materiais e nomes
# dos ossos ficam iguais: uniformes, rostos e o avatar Humanoid continuam valendo; a altura total é mantida.
#
# Uso: python -c "import bpy" disponível (Blender 4.x como módulo ou blender -b -P):
#   blender -b -P tools/blender/corpo_proporcoes.py -- ENTRADA.fbx SAIDA.fbx [larguraPelve]
import bpy, sys

args = sys.argv[sys.argv.index('--') + 1:]
src, dst = args[0], args[1]
pelvis_target = float(args[2]) if len(args) > 2 else 0.105   # distância entre as articulações do quadril / altura

# alvos (fração da altura), referência Quaternius (UAL): quadril 0,51 · joelho 0,29
HIP_T, KNEE_T = 0.51, 0.29

for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=src, ignore_leaf_bones=False)  # mãos, cabeça e dedos dos pés são folhas reais do Humanoid
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
body = [o for o in bpy.data.objects if o.type == 'MESH'][0]
if arm.animation_data: arm.animation_data.action = None
for pb in arm.pose.bones: pb.matrix_basis.identity()
bpy.context.view_layer.update()

B = lambda n: arm.data.bones['mixamorig:' + n]
W = lambda v: arm.matrix_world @ v
def z(n): return W(B(n).head_local).z

zs = [(body.matrix_world @ v.co).z for v in body.data.vertices]
zmin, H = min(zs), max(zs) - min(zs)
ankle = (z('LeftFoot') + z('RightFoot')) / 2 - zmin
knee = (z('LeftLeg') + z('RightLeg')) / 2 - zmin
hip = (z('LeftUpLeg') + z('RightUpLeg')) / 2 - zmin
shoulder = (z('LeftArm') + z('RightArm')) / 2 - zmin
hipsZ = z('Hips') - zmin
spread = (W(B('LeftUpLeg').head_local) - W(B('RightUpLeg').head_local)).length

shin = (KNEE_T * H - ankle) / (knee - ankle)
thigh = ((HIP_T - KNEE_T) * H) / (hip - knee)
new_hip = ankle + (knee - ankle) * shin + (hip - knee) * thigh
# tronco: os ombros ficam na mesma altura (e a cabeça, logo a altura total também)
torso = (shoulder - (new_hip + (hipsZ - hip))) / (shoulder - hipsZ)
width = max(0.7, min(1.0, pelvis_target * H / spread))
print('ALTURA %.3f  tornozelo %.3f joelho %.3f quadril %.3f ombro %.3f  ->  canela x%.3f coxa x%.3f tronco x%.3f pelve x%.3f'
      % (H, ankle / H, knee / H, hip / H, shoulder / H, shin, thigh, torso, width))

# eixo local do osso Hips que aponta para os lados (para estreitar a pelve)
hm = B('Hips').matrix_local.to_3x3()
side_axis = max(range(3), key=lambda i: abs(hm.col[i].normalized().x))

scale = {}
for s in ('Left', 'Right'):
    scale[s + 'Leg'] = (1.0, shin, 1.0)
    scale[s + 'UpLeg'] = (1.0, thigh, 1.0)
hs = [1.0, torso, 1.0]; hs[side_axis] = width
scale['Hips'] = tuple(hs)
for n in ('Spine', 'Spine1', 'Spine2'): scale[n] = (1.0, torso, 1.0)

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
for b in arm.data.edit_bones: b.inherit_scale = 'NONE'   # só o osso escalado muda; filhos mantêm o tamanho
bpy.ops.object.mode_set(mode='POSE')
for n, sc in scale.items(): arm.pose.bones['mixamorig:' + n].scale = sc
bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode='OBJECT')

# malha deformada -> nova malha de repouso
mods = [m for m in body.modifiers if m.type == 'ARMATURE']
bpy.context.view_layer.objects.active = body
for m in mods: bpy.ops.object.modifier_apply(modifier=m.name)
# pose -> novo repouso
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.pose.select_all(action='SELECT')
bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode='EDIT')
for b in arm.data.edit_bones: b.inherit_scale = 'FULL'
# o quadril ficou no lugar e as pernas cresceram para baixo: sobe esqueleto e malha juntos até os pés voltarem
# à origem (o Humanoid da Unity assume pés na origem no repouso; senão o corpo flutua nas animações)
from mathutils import Vector
mw = body.matrix_world
dz = -min((mw @ v.co).z for v in body.data.vertices)
aw, awi = arm.matrix_world, arm.matrix_world.inverted()
# calcula tudo antes (em ossos conectados, mover a cauda do pai já move a cabeça do filho)
alvo = {b.name: (awi @ ((aw @ b.head) + Vector((0, 0, dz))), awi @ ((aw @ b.tail) + Vector((0, 0, dz)))) for b in arm.data.edit_bones}
for _ in range(2):  # segunda passada corrige o que a primeira mexeu pelas conexões
    for b in arm.data.edit_bones:
        b.head, b.tail = alvo[b.name]
bpy.ops.object.mode_set(mode='OBJECT')
mwi = mw.inverted()
for v in body.data.vertices:
    v.co = mwi @ ((mw @ v.co) + Vector((0, 0, dz)))
print('PES NA ORIGEM: subiu %.3f m' % dz)
mod = body.modifiers.new('Armature', 'ARMATURE'); mod.object = arm
if body.parent != arm:
    body.parent = arm

zs = [(body.matrix_world @ v.co).z for v in body.data.vertices]
H2 = max(zs) - min(zs)
bpy.context.view_layer.update()
print('RESULTADO altura %.3f (pés em %.3f)  quadril %.3f joelho %.3f ombro %.3f' % (H2, min(zs), (z('LeftUpLeg') - min(zs)) / H2, (z('LeftLeg') - min(zs)) / H2, (z('LeftArm') - min(zs)) / H2))

for m in body.data.materials:
    if m is not None and '.' in m.name: m.name = m.name.split('.')[0]   # mantém o nome do material original
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); body.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS',
    mesh_smooth_type='FACE', path_mode='STRIP', primary_bone_axis='Y', secondary_bone_axis='X')
print('EXPORTADO', dst)
