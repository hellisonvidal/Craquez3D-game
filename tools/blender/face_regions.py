"""Mapa de regiões do rosto na textura (atlas) de um modelo rigado.

Uso: python face_regions.py modelo.fbx saida.png [tamanho]
(precisa do Blender como módulo: pip install bpy)

Cada pixel do atlas recebe um rótulo pela posição 3D em que cai, medida no referencial da cabeça
(origem no osso da cabeça, frente = -Y do Blender, normalizado pela altura do personagem):
  0 = fora do rosto, 1 = queixo/cavanhaque, 2 = buço/bigode, 3 = bochechas e maxilar (barba),
  4 = pescoço sob o queixo (barba), 5 = resto da cabeça.
Usado por tools/make_kits.py para os estilos de barba.
"""
import sys
import bpy
import numpy as np
from PIL import Image

fbx, out = sys.argv[-2], sys.argv[-1]
size = 512
try:
    size = int(sys.argv[-3]) if sys.argv[-3].isdigit() else 512
except Exception:
    pass

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
mesh = [o for o in bpy.data.objects if o.type == 'MESH'][0]
me = mesh.data
me.calc_loop_triangles()
co = np.array([mesh.matrix_world @ v.co for v in me.vertices], np.float64)
H = co[:, 2].max() - co[:, 2].min()
hb = [b for b in arm.data.bones if b.name.split(':')[-1].lower() == 'head'][0]
hp = np.array(arm.matrix_world @ hb.head_local)
# referencial da cabeça: x = direita, y = frente (-Y Blender), z = cima, em frações da altura
rel = (co - hp) / H
rel = np.stack([rel[:, 0], -rel[:, 1], rel[:, 2]], 1)
top = rel[:, 2].max()

uv = me.uv_layers.active.data
tris = [(t.vertices[:], t.loops[:]) for t in me.loop_triangles]
lab = np.zeros((size, size), np.uint8)
pos = np.full((size, size, 3), np.nan, np.float32)
for (vi, li) in tris:
    p = np.array([uv[l].uv for l in li]) * size
    P = rel[list(vi)]
    x0, y0 = np.floor(p.min(0)).astype(int)
    x1, y1 = np.ceil(p.max(0)).astype(int)
    x0, y0 = max(x0, 0), max(y0, 0)
    x1, y1 = min(x1, size - 1), min(y1, size - 1)
    if x1 < x0 or y1 < y0:
        continue
    xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    a, b, c = p
    den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
    if abs(den) < 1e-12:
        continue
    w0 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / den
    w1 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / den
    w2 = 1 - w0 - w1
    inside = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
    if not inside.any():
        continue
    q = w0[..., None] * P[0] + w1[..., None] * P[1] + w2[..., None] * P[2]
    iy = (size - 1 - (ys - 0.5)).astype(int)   # v cresce para cima
    ix = (xs - 0.5).astype(int)
    pos[iy[inside], ix[inside]] = q[inside]

x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
valid = ~np.isnan(x)
x = np.nan_to_num(x); y = np.nan_to_num(y); z = np.nan_to_num(z)
head = valid & (z > -0.03)
face = head & (y > 0.0)                   # metade da frente da cabeça
# alturas do rosto (frações da altura, a partir do osso da cabeça): medidas nos dois modelos
zz = z / max(top, 1e-6)                    # 0 = osso da cabeça, 1 = topo
chin = face & (zz < 0.30) & (np.abs(x) < 0.022)
mouth_up = face & (zz >= 0.30) & (zz < 0.40) & (np.abs(x) < 0.02) & (y > 0.04)
# maxilar/bochechas: toda a parte de baixo da cabeça (abaixo dos olhos), da frente até perto das orelhas
jaw = head & (zz < 0.5) & (y > -0.025) & ~chin & ~mouth_up
neck = valid & (z <= -0.03) & (z > -0.09) & (y > -0.01)
lab[head] = 5
lab[jaw] = 3
lab[neck] = 4
lab[chin] = 1
lab[mouth_up] = 2
Image.fromarray(lab).save(out)
np.save(out.replace('.png', '_pos.npy'), pos.astype(np.float16))
print('ok', out, 'top', round(float(top), 4), 'rótulos', np.bincount(lab.ravel(), minlength=6).tolist())
