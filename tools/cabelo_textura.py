"""Textura de cabelo (fios) para os cabelos 3D: albedo claro em tons de cinza (a cor de cada jogador vem do
material) e normal map com os fios na vertical da UV da cabeça.  python3 tools/cabelo_textura.py PASTA_TEXTURAS"""
import sys, os, numpy as np
from PIL import Image
from scipy import ndimage
N = 512
rng = np.random.default_rng(7)
ruido = rng.random((N, N))
# fios: ruído esticado na vertical (direção do crescimento na UV cilíndrica) em duas escalas
fios = ndimage.gaussian_filter(ruido, (14, 0.7)) * 0.65 + ndimage.gaussian_filter(rng.random((N, N)), (5, 0.5)) * 0.35
fios = (fios - fios.min()) / (fios.max() - fios.min())
mechas = ndimage.gaussian_filter(rng.random((N, N)), (40, 9))
mechas = (mechas - mechas.min()) / (mechas.max() - mechas.min())
alb = np.clip(0.55 + 0.35 * fios + 0.18 * (mechas - 0.5), 0, 1)
Image.fromarray((alb * 255).astype(np.uint8)).convert('RGB').save(os.path.join(sys.argv[1], 'Cabelo_Albedo.png'))
alt = ndimage.gaussian_filter(fios, 0.8) * 3.0
gx = np.gradient(alt, axis=1); gy = np.gradient(alt, axis=0)
n = np.stack([-gx, gy, np.ones_like(alt)], -1); n /= np.linalg.norm(n, axis=-1, keepdims=True)
Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8)).save(os.path.join(sys.argv[1], 'Cabelo_Normal.png'))
print('ok')
