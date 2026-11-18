# Separa a cabeça do corpo (objeto "Cabeca", com UV própria) e gera cabelos 3D ajustados ao crânio.
#
# Por quê: o rosto fazia parte da textura do corpo/uniforme, então todos os jogadores tinham o rosto do modelo
# importado (só mudavam pele e barba). Com a cabeça separada, cada jogador recebe uma textura de rosto própria
# (fotos sintéticas projetadas por tools/rostos.py) com qualquer uniforme; e os cabelos deixam de ser esferas/cubos.
#
# Saída: o FBX do personagem com Body (UV original) + Cabeca (UV "cilíndrica" com a frente do rosto no centro)
# + Cabelo_<estilo> (malhas fechadas presas 100% ao osso Head; o jogo liga uma por jogador)
# e um .npz com os triângulos da cabeça (posição de repouso, UV nova e UV antiga) para o projetor de rostos.
#
#   python -I cabeca_cabelos.py -- ENTRADA.fbx SAIDA.fbx SAIDA.npz
import bpy, bmesh, sys, math
import numpy as np
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
src, dst, npz = args[0], args[1], args[2]
LIMIAR = 0.5        # peso mínimo no osso Head para a face ir para a cabeça

for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=src, ignore_leaf_bones=False)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
body = [o for o in bpy.data.objects if o.type == 'MESH'][0]
if arm.animation_data: arm.animation_data.action = None
for pb in arm.pose.bones: pb.matrix_basis.identity()
bpy.context.view_layer.update()
body.name = 'Body'
mat_body = body.data.materials[0].name if body.data.materials else 'Corpo_Mat'

H = body.vertex_groups['mixamorig:Head'].index
mw = body.matrix_world
wh = np.zeros(len(body.data.vertices))
for v in body.data.vertices:
    for g in v.groups:
        if g.group == H: wh[v.index] = g.weight

# ---------------------------------------------------------------- referencial da cabeça (mundo, personagem olhando para -Y)
P = np.array([(mw @ v.co)[:] for v in body.data.vertices])
hm = wh >= LIMIAR
top = P[hm, 2].max()
cx = 0.0
cy = float(np.median(P[hm & (P[:, 2] > P[hm, 2].min() + 0.6 * (top - P[hm, 2].min())), 1]))   # centro do crânio (frente-trás)
# queixo: na linha do meio, de baixo para cima, onde a frente "salta" da garganta para o queixo
mid = np.abs(P[:, 0] - cx) < 0.03
zs = np.arange(top - 0.40, top, 0.005)
fr = []
for z0 in zs:
    m = mid & (P[:, 1] < cy) & (P[:, 2] >= z0) & (P[:, 2] < z0 + 0.005)
    fr.append(P[m, 1].min() if m.any() else np.nan)
fr = np.array(fr)
jump = [i for i in range(1, len(fr)) if not np.isnan(fr[i]) and not np.isnan(fr[i - 1]) and fr[i] < fr[i - 1] - 0.03]
chin = float(zs[jump[0]]) if jump else float(P[hm, 2].min())
y_chin = float(np.nanmin(fr[jump[0]:jump[0] + 6])) if jump else cy - 0.11
# nuca: atrás, onde o peso da cabeça passa de 0,5
back = mid & (P[:, 1] > cy + 0.03)
nape = float(P[back & hm, 2].min()) if (back & hm).any() else chin + 0.07
y_back = float(P[back, 1].max()) if back.any() else cy + 0.09
def zcut(y):   # plano do corte: sob o queixo na frente, na nuca atrás
    t = min(max((y - y_chin) / max(1e-3, (y_back - y_chin)), 0.0), 1.0)
    return chin + (nape - chin) * t
inhead = np.array([hm[i] or P[i, 2] >= zcut(P[i, 1]) - 0.002 for i in range(len(P))])
HH = top - chin
print('CABECA queixo %.3f (y %.3f) nuca %.3f topo %.3f altura %.3f centro y %.3f' % (chin, y_chin, nape, top, HH, cy))

def angle(p):  # 0 = frente (-Y), +pi/2 = esquerda do personagem (+X)
    return math.atan2(p[0] - cx, -(p[1] - cy))

def head_uv(p):
    a = angle(p)
    # frente comprimida para ganhar resolução no rosto: |a|<=pi -> u em [0,1], com a frente ocupando ~60% da largura
    s = math.copysign(abs(a / math.pi) ** 0.62, a)
    u = 0.5 + 0.5 * s
    v = (p[2] - chin) / HH
    return u, min(max(v, 0.0), 1.0)

# ---------------------------------------------------------------- separa a cabeça
bpy.context.view_layer.objects.active = body
bpy.ops.object.mode_set(mode='EDIT')
bm = bmesh.from_edit_mesh(body.data)
bm.faces.ensure_lookup_table()
for f in bm.faces:
    f.select = all(inhead[v.index] for v in f.verts)
bmesh.update_edit_mesh(body.data)
bpy.ops.mesh.separate(type='SELECTED')
bpy.ops.object.mode_set(mode='OBJECT')
head = [o for o in bpy.data.objects if o.type == 'MESH' and o != body][0]
head.name = 'Cabeca'

# ---------------------------------------------------------------- crânio: tira o volume do cabelo esculpido do modelo
def hairline(a):
    # altura (fração queixo->topo) da linha do cabelo: alta na testa, acima das orelhas nos lados, baixa na nuca
    return 0.60 + 0.19 * math.cos(a)

def smooth(e0, e1, x):
    t = min(max((x - e0) / (e1 - e0), 0.0), 1.0)
    return t * t * (3 - 2 * t)

C = Vector((cx, cy, chin + 0.52 * HH))      # centro do crânio
def bulk(p):     # quanto do cabelo esculpido sai em cada ponto (radial): ~2,8 cm no topo, ~1,6 cm nos lados
    a = angle(p); h = (p.z - chin) / HH
    return (0.016 + 0.012 * smooth(0.75, 1.0, h)) * smooth(hairline(a) - 0.06, hairline(a) + 0.08, h)
hm_ = head.matrix_world; hinv = hm_.inverted()
for v in head.data.vertices:
    w = hm_ @ v.co
    d = w - C
    w = C + d.normalized() * max(0.02, d.length - bulk(w))
    v.co = hinv @ w
head.data.update()
# mandíbula um pouco mais fina (o modelo base tem o rosto largo); a borda do corte no pescoço fica fixa (sem fresta)
for v in head.data.vertices:
    w = hm_ @ v.co
    h = (w.z - chin) / HH
    k = 1.0 - 0.08 * (1.0 - smooth(0.2, 0.5, h)) * smooth(0.0, 0.012, w.z - zcut(w.y))
    w.x = cx + (w.x - cx) * k
    v.co = hinv @ w
head.data.update()
top = max((hm_ @ v.co).z for v in head.data.vertices)
print('CRANIO topo %.3f -> cabeça (queixo-topo) %.3f m' % (top, top - chin))

# UV antiga (para amostrar a textura original no projetor) e UV nova
me = head.data
old_uv = me.uv_layers[0]
tris_pos, tris_uv, tris_old = [], [], []
me.calc_loop_triangles()
hmw = head.matrix_world
new_uv = {}
for poly in me.polygons:
    pts = [hmw @ me.vertices[me.loops[li].vertex_index].co for li in poly.loop_indices]
    uvs = [head_uv(p) for p in pts]
    us = [u for u, _ in uvs]
    if max(us) - min(us) > 0.5:   # face na costura de trás: evita a volta inteira na textura (repetição em U)
        uvs = [((u + 1.0) if u < 0.5 else u, v) for u, v in uvs]
    for li, uv in zip(poly.loop_indices, uvs): new_uv[li] = uv
for tri in me.loop_triangles:
    tris_pos.append([(hmw @ me.vertices[me.loops[li].vertex_index].co)[:] for li in tri.loops])
    tris_uv.append([new_uv[li] for li in tri.loops])
    tris_old.append([old_uv.data[li].uv[:] for li in tri.loops])
for li, uv in new_uv.items(): old_uv.data[li].uv = uv
head.data.materials.clear(); head.data.materials.append(bpy.data.materials.new('Rosto_Mat'))

# ---------------------------------------------------------------- cabelos (deslocamento radial a partir do centro do crânio)
def estilo_offset(nome, p):
    a = angle(p); h = (p.z - chin) / HH
    base = smooth(hairline(a) - 0.015, hairline(a) + 0.03, h)      # cresce a partir da linha do cabelo
    topo = smooth(0.7, 1.0, h)
    frente = max(0.0, math.cos(a))
    lado = abs(p.x - cx)
    if nome == 'Raspado':    return 0.003 * base
    if nome == 'Curto':      return (0.010 + 0.008 * topo) * base
    if nome == 'Topete':     return (0.010 + 0.008 * topo + 0.028 * topo * frente ** 2) * base
    if nome == 'Cacheado':   return (0.020 + 0.006 * math.sin(p.x * 160) * math.sin(p.y * 150) * math.sin(p.z * 170)) * base
    if nome == 'BlackPower':   # sobe aos poucos desde a linha do cabelo: redondo, sem "aba"
        return (0.060 + 0.040 * topo) * smooth(hairline(a) - 0.015, hairline(a) + 0.16, h)
    if nome == 'Moicano':    return (0.003 + 0.035 * topo * (1 - smooth(0.012, 0.03, lado))) * base
    return 0.0

ESTILOS = ['Raspado', 'Curto', 'Topete', 'Cacheado', 'BlackPower', 'Moicano']
hair_mat = bpy.data.materials.new('Cabelo_Mat')
for nome in ESTILOS:
    ob = head.copy(); ob.data = head.data.copy(); ob.name = 'Cabelo_' + nome
    bpy.context.collection.objects.link(ob)
    bm = bmesh.new(); bm.from_mesh(ob.data)
    bm.verts.ensure_lookup_table(); bm.normal_update()
    wmat = ob.matrix_world; inv = wmat.inverted()
    # remove o que fica abaixo da linha do cabelo
    kill = []
    for f in bm.faces:
        ws = [wmat @ v.co for v in f.verts]
        if all((w.z - chin) / HH < hairline(angle(w)) - 0.12 for w in ws): kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.normal_update()
    for v in bm.verts:
        w = wmat @ v.co
        d = w - C
        # um fio acima da pele (sem disputar profundidade com a cabeça) + volume do estilo
        # abaixo da linha do cabelo a casca afunda sob a pele: a borda visível segue a curva da linha, sem serrilhado
        h = (w.z - chin) / HH
        sob = -0.006 * (1.0 - smooth(hairline(angle(w)) - 0.03, hairline(angle(w)), h))
        v.co = inv @ (C + d.normalized() * (d.length + 0.0012 + sob + estilo_offset(nome, w)))
    bm.to_mesh(ob.data); bm.free()
    # borda fechada (casca com espessura para dentro)
    mod = ob.modifiers.new('casca', 'SOLIDIFY'); mod.thickness = 0.004; mod.offset = -1.0; mod.use_rim = True
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier='casca')
    # 100% no osso da cabeça
    for g in list(ob.vertex_groups):
        if g.name != 'mixamorig:Head': ob.vertex_groups.remove(g)
    g = ob.vertex_groups.get('mixamorig:Head') or ob.vertex_groups.new(name='mixamorig:Head')
    g.add([v.index for v in ob.data.vertices], 1.0, 'REPLACE')
    ob.data.materials.clear(); ob.data.materials.append(hair_mat)
    print('CABELO', nome, len(ob.data.vertices), 'vértices')

np.savez(npz, pos=np.array(tris_pos, np.float32), uv=np.array(tris_uv, np.float32), old_uv=np.array(tris_old, np.float32),
         chin=chin, top=top, cy=cy, cx=cx, HH=HH, y_chin=y_chin, y_back=y_back, nape=nape)

# modificadores de armadura em todos (o separar já copia; os cabelos vêm da cópia da cabeça)
for ob in [o for o in bpy.data.objects if o.type == 'MESH']:
    if not any(m.type == 'ARMATURE' for m in ob.modifiers):
        m = ob.modifiers.new('Armature', 'ARMATURE'); m.object = arm
    if ob.parent != arm: ob.parent = arm

bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
for ob in [o for o in bpy.data.objects if o.type == 'MESH']: ob.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS',
    mesh_smooth_type='FACE', path_mode='STRIP', primary_bone_axis='Y', secondary_bone_axis='X')
print('EXPORTADO', dst, 'material do corpo', mat_body)
