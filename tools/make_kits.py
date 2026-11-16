"""Uso: python3 tools/make_kits.py PASTA_ORIGEM [pasta_texturas] [pasta_visuais]

Gera as texturas dos personagens a partir dos modelos novos (corpo do jogador = modelo A, corpo do goleiro = modelo B):

  PASTA_ORIGEM/Jogador_Albedo.png      textura original do jogador (2048)
  PASTA_ORIGEM/Goleiro_Albedo.png      textura original do goleiro (2048)
  PASTA_ORIGEM/Kit_Casa.png            uniforme da casa (azul) transferido para a UV do jogador
  PASTA_ORIGEM/Kit_Visitante.png       uniforme visitante (branco) transferido para a UV do jogador
                                       (os dois por tools/blender/kit_bake.py, a partir dos modelos de uniforme)
  tools/face_regions/{Jogador,Goleiro}.png e *_pos.npz  rótulos do rosto e posição 3D de cada pixel
                                       (tools/blender/face_regions.py)

Saída: Uniformes/A_Casa, A_Visitante, B_GoleiroVisitante, A_Arbitro (2048) e Resources/Visuais/A_{Casa|Visitante}_{n}
(1024): cada jogador do mesmo time veste exatamente o mesmo uniforme; muda só pele, cabelo e barba.
"""
from PIL import Image, ImageFilter
import numpy as np, sys, os

try:
    from scipy import ndimage
    from scipy.spatial import cKDTree
except ImportError:  # pragma: no cover
    sys.exit('precisa de scipy (pip install scipy)')

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = sys.argv[1] if len(sys.argv) > 1 else '.'
TEX = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, '..', 'Assets', 'Craquez', 'Textures')
VIS = sys.argv[3] if len(sys.argv) > 3 else os.path.join(HERE, '..', 'Assets', 'Craquez', 'Resources', 'Visuais')
OUT = os.path.join(TEX, 'Uniformes')
SIZE = 2048
os.makedirs(OUT, exist_ok=True); os.makedirs(VIS, exist_ok=True)


def rgb2hsv(im):
    r, g, b = im[..., 0], im[..., 1], im[..., 2]
    mx = im.max(-1); mn = im.min(-1); d = mx - mn
    s = np.where(mx > 0, d / np.maximum(mx, 1e-6), 0); v = mx
    dd = np.maximum(d, 1e-6)
    h = np.where(mx == r, ((g - b) / dd) % 6, np.where(mx == g, (b - r) / dd + 2, (r - g) / dd + 4)) * 60
    h = np.where(d < 1e-6, 0, h)
    return h, s, v


def hsv2rgb(h, s, v):
    h = (h % 360) / 60.0
    c = v * s; x = c * (1 - np.abs(h % 2 - 1)); m = v - c
    z = np.zeros_like(h)
    conds = [(h < 1), (h >= 1) & (h < 2), (h >= 2) & (h < 3), (h >= 3) & (h < 4), (h >= 4) & (h < 5), (h >= 5)]
    r = np.select(conds, [c, x, z, z, x, c]); g = np.select(conds, [x, c, c, x, z, z]); b = np.select(conds, [z, z, x, c, c, x])
    return np.stack([r + m, g + m, b + m], -1)


def load(path, size=SIZE):
    return np.asarray(Image.open(path).convert('RGB').resize((size, size), Image.LANCZOS)).astype(np.float32) / 255


def smooth(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def blur(m, r):
    im = Image.fromarray((np.clip(m, 0, 1) * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.BoxBlur(r))).astype(np.float32) / 255


def skin_mask(h, s, v):
    hue = smooth(h, 2, 9) * (1 - smooth(h, 40, 50))
    return hue * smooth(s, 0.10, 0.18) * (1 - smooth(s, 0.62, 0.75)) * smooth(v, 0.18, 0.3)


def regions(model, size):
    """Rótulos do rosto e posição 3D (referencial da cabeça, em frações da altura) de cada pixel."""
    lab = np.asarray(Image.open(os.path.join(HERE, 'face_regions', model + '.png')).resize((size, size), Image.NEAREST))
    pos = np.load(os.path.join(HERE, 'face_regions', model + '_pos.npz'))['pos'].astype(np.float32)
    if pos.shape[0] != size:
        k = size // pos.shape[0]
        pos = pos.repeat(k, 0).repeat(k, 1)
    return lab, pos


def fill_gaps(img, valid):
    """Pixels fora das ilhas da UV copiam o vizinho válido mais próximo (sem vazar cor nos mipmaps)."""
    idx = ndimage.distance_transform_edt(~valid, return_distances=False, return_indices=True)
    return img[idx[0], idx[1]]


def save(img, path, q=93):
    Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)).save(path, quality=q, subsampling=0)
    print('ok', os.path.relpath(path))


# ---------------------------------------------------------------- uniformes do time (modelo A)

def compose_kit(base, kit, pos, lab):
    """Troca a roupa do jogador pela do uniforme transferido; pele, cabelo, rosto e chuteiras ficam."""
    valid = ~np.isnan(pos[..., 0])
    z = np.nan_to_num(pos[..., 2], nan=9)
    h, s, v = rgb2hsv(base)
    skin = skin_mask(h, s, v) > 0.5
    x = np.nan_to_num(pos[..., 0])
    # roupa: abaixo do pescoço; nos ombros (fora do pescoço) sobe até a gola; rosto/barba nunca
    cloth = valid & (z > -0.816) & ~skin & ~np.isin(lab, [1, 2, 3]) & ((z < -0.072) | ((z < -0.02) & ((np.abs(x) > 0.05) | (lab != 4) | (s > 0.45))))
    # uniforme válido: não é "manequim" (cinza) nem área sem projeção (preto)
    kh, ks, kv = rgb2hsv(kit)
    manequim = (ks < 0.08) & (kv > 0.2) & (kv < 0.8)
    good = cloth & ~manequim & (kv > 0.24)  # muito escuro = avesso do tecido ou sem projeção
    # o que faltou (mangas e barras mais compridas que as do uniforme) pega a cor do ponto 3D mais próximo
    miss = cloth & ~good
    if miss.any() and good.any():
        gi = np.argwhere(good)
        tree = cKDTree(pos[good])
        # procura um pouco mais para dentro do corpo: copia o tecido, não a barra/acabamento da borda
        q = pos[miss]
        c = np.array([0.0, 0.0, -0.35], np.float32)
        d = c[None, :] - q
        q = q + 0.05 * d / np.maximum(np.linalg.norm(d, axis=1, keepdims=True), 1e-6)
        _, nn = tree.query(q, k=1)
        src = gi[nn]
        kit = kit.copy()
        kit[miss] = kit[src[:, 0], src[:, 1]]
    m = blur(cloth.astype(np.float32), 1)
    out = base * (1 - m[..., None]) + kit * m[..., None]
    return fill_gaps(out, valid)


def recolor_hue(img, mask, hue, sat_gain=1.0, val_gain=1.0):
    h, s, v = rgb2hsv(img)
    new = hsv2rgb(np.full_like(h, hue), np.clip(s * sat_gain, 0, 1), np.clip(v * val_gain, 0, 1))
    return img * (1 - mask[..., None]) + new * mask[..., None]


# ---------------------------------------------------------------- aparências (pele, cabelo, barba)

# modelo A (original: pele morena clara, cabelo preto, barba por fazer/cavanhaque)
# (pele, cabelo, barba); barba: 'cheia' (original), 'limpo', 'cavanhaque', 'bigode', 'por_fazer'
LOOKS = [None,
         ((0.27, 0.175, 0.125), (0.03, 0.03, 0.03), 'cheia'),
         ((0.86, 0.68, 0.55), (0.42, 0.26, 0.14), 'limpo'),
         ((0.80, 0.60, 0.45), (0.55, 0.16, 0.08), 'cavanhaque'),
         (None, (0.045, 0.04, 0.04), 'limpo'),
         ((0.45, 0.30, 0.21), (0.03, 0.03, 0.03), 'bigode'),
         ((0.86, 0.68, 0.55), (0.78, 0.62, 0.32), 'por_fazer'),
         ((0.62, 0.44, 0.32), (0.06, 0.05, 0.045), 'cavanhaque')]


def recolor(img, mask, target, by_luma=False):
    m = mask > 0.5
    if not m.any():
        return img
    if by_luma:
        lum = img.mean(-1)
        ratio = np.clip(lum / (np.median(lum[m]) + 1e-3), 0.2, 2.6)[..., None]
    else:
        med = np.median(img[m], axis=0) + 1e-3
        ratio = np.clip(img / med[None, None, :], 0.2, 2.2)
    new = np.clip(np.array(target, np.float32)[None, None, :] * ratio, 0, 1)
    return img * (1 - mask[..., None]) + new * mask[..., None]


def look(img, lab, pos, idx):
    spec = LOOKS[idx]
    if spec is None:
        return img
    skin_t, hair_t, beard = spec
    size = img.shape[0]
    g = max(1, size // 256)
    valid = ~np.isnan(pos[..., 0])
    x, y, z = [np.nan_to_num(pos[..., i]) for i in range(3)]
    head = valid & (z > -0.072)
    h, s, v = rgb2hsv(img)
    lum = img.mean(-1)
    skin = skin_mask(h, s, v)
    sk = (skin > 0.5) & valid
    skin_lum = np.median(lum[sk & head]) if (sk & head).any() else 0.5
    # cabelo: parte de cima/de trás da cabeça, bem mais escuro que a pele
    top = head & ((z > 0.055) | ((y < -0.035) & (z > 0.0)))
    hair = (top & (lum < skin_lum * 0.6)).astype(np.float32)
    hair = np.clip(blur(hair, 1) * 1.3, 0, 1) * top
    out = recolor(img, np.clip(skin * (1 - hair), 0, 1), skin_t) if skin_t is not None else img.copy()
    out = recolor(out, hair, hair_t, by_luma=True)
    return apply_beard(out, img, lab, beard, np.clip(skin * (1 - hair), 0, 1), s, g)


def zone(lab, codes, grow):
    m = np.isin(lab, codes).astype(np.float32)
    im = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(grow * 2 + 1)).filter(ImageFilter.BoxBlur(1))
    return np.asarray(im).astype(np.float32) / 255


def apply_beard(out, orig, lab, style, skin, s, g):
    """Barba: tira (rosto limpo), mantém só o queixo (cavanhaque) ou o buço (bigode), ou deixa rala (por fazer)."""
    if style == 'cheia':
        return out
    face = np.isin(lab, [1, 2, 3]).astype(np.float32)
    lum = orig.mean(-1)
    sk = skin > 0.5
    skin_lum = np.median(lum[sk]) if sk.any() else 0.6
    sk_s = np.median(s[sk]) if sk.any() else 0.35
    dark = 1 - smooth(lum, skin_lum * 0.62, skin_lum * 0.95)
    gray = 1 - smooth(s, sk_s * 0.45, sk_s * 0.8)
    beard0 = face * np.clip(np.maximum(dark, gray * smooth(lum, 0.08, 0.15)), 0, 1)
    grown = np.asarray(Image.fromarray((beard0 * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3 if g < 3 else 5)).filter(ImageFilter.BoxBlur(g))).astype(np.float32) / 255
    beard = np.clip(np.maximum(beard0, grown * face * 0.9), 0, 1)
    keep = np.zeros_like(beard)
    if style == 'cavanhaque': keep = zone(lab, [1, 2], 2 * g)
    elif style == 'bigode': keep = zone(lab, [2], 5 * g)
    remove = beard * (1 - keep)
    remove = remove * 0.55 if style == 'por_fazer' else np.clip(remove * 1.25, 0, 1)
    wsk = np.clip(skin * (1 - beard), 0, 1)
    rad = 9 * g
    num = np.stack([blur(out[..., c] * wsk, rad) for c in range(3)], -1)
    den = blur(wsk, rad)[..., None]
    local = num / np.maximum(den, 1e-3)
    base = np.median(out[sk], axis=0) if sk.any() else np.array([0.6, 0.45, 0.35])
    local = np.where(den > 0.02, local, base[None, None, :])
    detail = np.clip(1 + (lum - blur(lum, 2 * g)) * 0.6, 0.9, 1.08)[..., None]
    clean = np.clip(local * detail, 0, 1)
    return out * (1 - remove[..., None]) + clean * remove[..., None]


# ---------------------------------------------------------------- geração

A = load(os.path.join(SRC, 'Jogador_Albedo.png'))
B = load(os.path.join(SRC, 'Goleiro_Albedo.png'))
labA, posA = regions('Jogador', SIZE)
labB, posB = regions('Goleiro', SIZE)

kits = {
    'A_Casa': compose_kit(A, load(os.path.join(SRC, 'Kit_Casa.png')), posA, labA),
    'A_Visitante': compose_kit(A, load(os.path.join(SRC, 'Kit_Visitante.png')), posA, labA),
}
for k, im in kits.items():
    save(im, os.path.join(OUT, k + '.jpg'))

# goleiro visitante: o verde do goleiro vira laranja (luvas, pele e cabelo ficam)
hB, sB, vB = rgb2hsv(B)
zB = np.nan_to_num(posB[..., 2], nan=9)
green = smooth(hB, 85, 100) * (1 - smooth(hB, 175, 190)) * smooth(sB, 0.18, 0.28) * (zB < -0.06)
save(recolor_hue(B, blur(green, 1), 26, 1.25, 1.12), os.path.join(OUT, 'B_GoleiroVisitante.jpg'))

# arbitragem: o uniforme original do jogador (preto e vermelho) com o vermelho trocado por amarelo-limão
hA, sA, vA = rgb2hsv(A)
zA = np.nan_to_num(posA[..., 2], nan=9)
red = (smooth(hA, 325, 338) + (1 - smooth(hA, 8, 14))).clip(0, 1) * smooth(sA, 0.55, 0.66) * smooth(vA, 0.12, 0.2) * (zA < -0.072) * (1 - skin_mask(hA, sA, vA))
save(recolor_hue(A, blur(red, 1), 62, 1.0, 1.25), os.path.join(OUT, 'A_Arbitro.jpg'))

# aparências por jogador (1024): {modelo}_{time}_{n}
lab1, pos1 = regions('Jogador', 1024)
for key, base in kits.items():
    small = np.asarray(Image.fromarray((np.clip(base, 0, 1) * 255).astype(np.uint8)).resize((1024, 1024), Image.LANCZOS)).astype(np.float32) / 255
    for i in range(1, len(LOOKS)):
        save(look(small, lab1, pos1, i), os.path.join(VIS, '%s_%d.jpg' % (key, i)), 90)
