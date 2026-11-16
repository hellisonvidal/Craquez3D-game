"""Uso: python3 tools/make_kits.py [pasta_texturas] [pasta_saida] [tamanho]

Gera as variações de uniforme (times, goleiros e árbitros) a partir das texturas dos dois modelos.
Mantém pele, cabelo, rosto e o sombreado do tecido; troca só as cores do uniforme."""
from PIL import Image, ImageFilter
import numpy as np, sys, os

def rgb2hsv(im):
    r,g,b=im[...,0],im[...,1],im[...,2]
    mx=im.max(-1); mn=im.min(-1); d=mx-mn
    s=np.where(mx>0,d/np.maximum(mx,1e-6),0); v=mx
    dd=np.maximum(d,1e-6)
    h=np.where(mx==r, ((g-b)/dd)%6, np.where(mx==g,(b-r)/dd+2,(r-g)/dd+4))*60
    h=np.where(d<1e-6,0,h)
    return h,s,v
def hsv2rgb(h,s,v):
    h=(h%360)/60.0
    c=v*s; x=c*(1-np.abs(h%2-1)); m=v-c
    z=np.zeros_like(h)
    conds=[(h<1),(h>=1)&(h<2),(h>=2)&(h<3),(h>=3)&(h<4),(h>=4)&(h<5),(h>=5)]
    rs=[c,x,z,z,x,c]; gs=[x,c,c,x,z,z]; bs=[z,z,x,c,c,x]
    r=np.select(conds,rs); g=np.select(conds,gs); b=np.select(conds,bs)
    return np.stack([r+m,g+m,b+m],-1)


T = sys.argv[1] if len(sys.argv) > 1 else '/home/user/craquez3d-game/Assets/Craquez/Textures/'
OUT = sys.argv[2] if len(sys.argv) > 2 else T + 'Uniformes/'
SIZE = int(sys.argv[3]) if len(sys.argv) > 3 else 2048
os.makedirs(OUT, exist_ok=True)

def load(n):
    return np.asarray(Image.open(T + n).convert('RGB').resize((SIZE, SIZE), Image.LANCZOS)).astype(np.float32) / 255

def smooth(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)

def skin_mask(h, s, v):
    hue = smooth(h, 2, 9) * (1 - smooth(h, 40, 50))
    return hue * smooth(s, 0.10, 0.18) * (1 - smooth(s, 0.62, 0.75)) * smooth(v, 0.18, 0.3)

def density(mask, radius):
    im = Image.fromarray((np.clip(mask, 0, 1) * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.BoxBlur(radius))).astype(np.float32) / 255

def paint(img, mask, color, shade, gain=1.0):
    """Pinta a região com a cor alvo preservando o sombreado (shade ~ 1 no tom médio do tecido)."""
    c = np.array(color, np.float32)
    lit = np.clip(c[None, None, :] * shade[..., None] * gain, 0, 1)
    return img * (1 - mask[..., None]) + lit * mask[..., None]

def shade_of(v, region):
    ref = np.median(v[region > 0.5]) if (region > 0.5).any() else 0.5
    return np.clip(v / max(ref, 1e-3), 0.35, 1.7) ** 0.65

def model_a(img, base, accent, white=None):
    h, s, v = rgb2hsv(img)
    skin = skin_mask(h, s, v)
    r = SIZE / 1024.0
    face = smooth(density(skin, 10 * r), 0.25, 0.45)  # perto da pele (rosto/mãos): lábios não viram uniforme
    red = (smooth(h, 325, 338) + (1 - smooth(h, 6, 12))).clip(0, 1) * smooth(s, 0.55, 0.66) * smooth(v, 0.12, 0.2) * (1 - skin) * (1 - face)
    warm = ((h < 60) | (h >= 300)).astype(np.float32)
    hair0 = warm * smooth(s, 0.17, 0.24) * (1 - smooth(v, 0.3, 0.45))  # castanho escuro = cabelo/barba
    hair = np.maximum(hair0, smooth(density(hair0, 11 * r), 0.1, 0.22))  # fios pretos no meio do cabelo também
    dark = (1 - smooth(v, 0.16, 0.26)) * (1 - skin) * (1 - red) * (1 - hair)
    wht = smooth(v, 0.72, 0.85) * (1 - smooth(s, 0.12, 0.2)) * (1 - skin)
    out = img.copy()
    out = paint(out, dark, base, shade_of(v, dark))
    out = paint(out, red, accent, shade_of(v, red))
    if white is not None: out = paint(out, wht, white, shade_of(v, wht))
    return out

def model_b(img, base, accent, white=None):
    h, s, v = rgb2hsv(img)
    skin = skin_mask(h, s, v)
    green = smooth(h, 110, 125) * (1 - smooth(h, 178, 190)) * smooth(s, 0.2, 0.3) * (1 - skin)
    neon = smooth(h, 45, 55) * (1 - smooth(h, 95, 105)) * smooth(s, 0.25, 0.35) * smooth(v, 0.35, 0.5) * (1 - skin)
    wht = smooth(v, 0.68, 0.8) * (1 - smooth(s, 0.12, 0.2)) * (1 - skin)
    out = img.copy()
    out = paint(out, green, base, shade_of(v, green))
    out = paint(out, neon, accent, shade_of(v, neon))
    if white is not None: out = paint(out, wht, white, shade_of(v, wht))
    return out

# ---------------------------------------------------------------- aparências (pele e cabelo)

# (pele, cabelo, barba); barba: 'cheia', 'limpo', 'cavanhaque', 'bigode', 'por_fazer'
CLARO, MORENO, NEGRO = (0.95, 0.80, 0.68), (0.74, 0.53, 0.38), (0.33, 0.215, 0.155)
LOOKS = {
    # modelo A (original: pele clara, cabelo castanho escuro, barba cheia)
    'A': [None,
          (MORENO, (0.05, 0.04, 0.035), 'cheia'),
          (NEGRO, (0.035, 0.03, 0.028), 'cavanhaque'),
          (CLARO, (0.78, 0.62, 0.32), 'limpo'),
          (None, (0.2, 0.13, 0.08), 'limpo'),
          (MORENO, (0.05, 0.04, 0.035), 'bigode'),
          (NEGRO, (0.035, 0.03, 0.028), 'limpo'),
          (CLARO, (0.55, 0.16, 0.08), 'por_fazer')],
    # modelo B (original: pele parda, cabelo preto, barba cheia)
    'B': [None,
          ((0.27, 0.175, 0.125), (0.03, 0.03, 0.03), 'cheia'),
          ((0.93, 0.77, 0.65), (0.42, 0.26, 0.14), 'limpo'),
          ((0.80, 0.60, 0.45), (0.55, 0.16, 0.08), 'cavanhaque'),
          (None, (0.045, 0.04, 0.04), 'limpo'),
          ((0.27, 0.175, 0.125), (0.03, 0.03, 0.03), 'bigode'),
          ((0.93, 0.77, 0.65), (0.78, 0.62, 0.32), 'por_fazer'),
          (None, (0.045, 0.04, 0.04), 'cavanhaque')],
}

_REGIONS = {}
def regions(model, size):
    """Rótulos do rosto (tools/face_regions/*.png, gerados por tools/blender/face_regions.py)."""
    key = (model, size)
    if key not in _REGIONS:
        f = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'face_regions', 'Jogador.png' if model == 'A' else 'Goleiro.png')
        im = Image.open(f).resize((size, size), Image.NEAREST)
        _REGIONS[key] = np.asarray(im)
    return _REGIONS[key]

def zone(lab, codes, grow):
    m = np.isin(lab, codes).astype(np.float32)
    im = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(grow * 2 + 1)).filter(ImageFilter.BoxBlur(1))
    return np.asarray(im).astype(np.float32) / 255

def recolor(img, mask, target, by_luma=False):
    """Leva a região à cor alvo preservando as variações relativas (lábios, sombras, fios)."""
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

def look(img, model, idx):
    spec = LOOKS[model][idx]
    if spec is None:
        return img
    skin_t, hair_t, beard = spec
    h, s, v = rgb2hsv(img)
    r = img.shape[0] / 1024.0
    warm = ((h < 60) | (h >= 300)).astype(np.float32)
    skin0 = skin_mask(h, s, v)
    # rosto inteiro: tons quentes claros perto da pele também (bochechas pálidas, nariz, orelhas)
    skin = np.maximum(skin0, smooth(density(skin0, 6 * r), 0.35, 0.6) * warm * smooth(v, 0.3, 0.42) * (1 - smooth(s, 0.62, 0.75)))
    brown = (smooth(h, 4, 9) * (1 - smooth(h, 50, 60))).astype(np.float32)  # castanho (não o vermelho escuro do uniforme)
    hair0 = brown * smooth(s, 0.15, 0.22) * (1 - smooth(v, 0.3, 0.45)) * (1 - skin)
    near = smooth(density(hair0, 11 * r), 0.1, 0.22)
    # fios pretos no meio do cabelo (não o tecido azulado do uniforme nem o fundo do atlas)
    hair = np.maximum(hair0, near * warm * smooth(s, 0.06, 0.12) * (1 - smooth(v, 0.35, 0.5)) * smooth(v, 0.03, 0.06))
    hair *= 1 - skin
    out = recolor(img, skin, skin_t) if skin_t is not None else img.copy()
    out = recolor(out, hair, hair_t, by_luma=True)
    return apply_beard(out, img, model, beard, skin, h, s, v)

def apply_beard(out, orig, model, style, skin, h, s, v):
    """Barba: tira (rosto limpo), mantém só o queixo (cavanhaque) ou o buço (bigode), ou deixa rala (por fazer)."""
    if style == 'cheia':
        return out
    size = out.shape[0]
    lab = regions(model, size)
    g = max(1, size // 256)
    face = zone(lab, [1, 2, 3, 4], g)
    # pelos da barba: mais escuros que a pele, dentro da parte de baixo do rosto
    lum = orig.mean(-1)
    sk = skin > 0.5
    skin_lum = np.median(lum[sk]) if sk.any() else 0.6
    sk_s = np.median(s[sk]) if sk.any() else 0.35
    dark = 1 - smooth(lum, skin_lum * 0.62, skin_lum * 0.95)        # fio escuro
    gray = 1 - smooth(s, sk_s * 0.45, sk_s * 0.8)                    # fio acinzentado (pele é mais saturada)
    beard0 = face * np.clip(np.maximum(dark, gray * smooth(lum, 0.08, 0.15)), 0, 1)
    grown = np.asarray(Image.fromarray((beard0 * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3 if g < 3 else 5)).filter(ImageFilter.BoxBlur(g))).astype(np.float32) / 255
    beard = np.clip(np.maximum(beard0, grown * face * 0.9), 0, 1)
    keep = np.zeros_like(beard)
    if style == 'cavanhaque': keep = zone(lab, [1, 2], 2 * g)
    elif style == 'bigode': keep = zone(lab, [2], 5 * g)
    remove = beard * (1 - keep)
    if style == 'por_fazer': remove *= 0.55
    else: remove = np.clip(remove * 1.25, 0, 1)
    # pele "por baixo" da barba: média local da pele vizinha (convolução normalizada), sem remendo chapado
    wsk = np.clip(skin * (1 - beard), 0, 1)
    rad = 9 * g
    num = np.stack([density(out[..., c] * wsk, rad) for c in range(3)], -1)
    den = density(wsk, rad)[..., None]
    local = num / np.maximum(den, 1e-3)
    oskin = out[sk] if sk.any() else out.reshape(-1, 3)
    base = np.median(oskin, axis=0)
    local = np.where(den > 0.02, local, base[None, None, :])
    # textura fina dos poros: um pouco do detalhe original, bem suave
    detail = np.clip(1 + (lum - density(lum, 2 * g)) * 0.6, 0.9, 1.08)[..., None]
    clean = np.clip(local * detail, 0, 1)
    return out * (1 - remove[..., None]) + clean * remove[..., None]

def save(img, name):
    Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)).save(OUT + name + '.jpg', quality=93, subsampling=0)
    print('ok', name)

A = load('Jogador_Albedo.png')
B = load('Goleiro_Albedo.png')
kits = {}

# Craquez FC (casa): preto com detalhes vermelhos  -> modelo A já é assim; B recolorido
kits['B_Casa'] = model_b(B, (0.07, 0.07, 0.08), (0.86, 0.1, 0.1)); save(kits['B_Casa'], 'B_Casa')
# Atlético Celeste (visitante): branco com azul-celeste
kits['A_Visitante'] = model_a(A, (0.92, 0.93, 0.95), (0.12, 0.48, 0.95), white=(0.1, 0.25, 0.6)); save(kits['A_Visitante'], 'A_Visitante')
kits['B_Visitante'] = model_b(B, (0.92, 0.93, 0.95), (0.12, 0.48, 0.95)); save(kits['B_Visitante'], 'B_Visitante')
# Goleiros: casa = verde original (modelo B); visitante = laranja com preto (modelo A)
save(model_a(A, (1.0, 0.5, 0.08), (0.08, 0.08, 0.08), white=(0.1, 0.1, 0.1)), 'A_GoleiroVisitante')
# Arbitragem: amarelo-limão com preto
save(model_a(A, (0.86, 0.95, 0.18), (0.06, 0.06, 0.06), white=(0.06, 0.06, 0.06)), 'A_Arbitro')
save(model_b(B, (0.86, 0.95, 0.18), (0.06, 0.06, 0.06)), 'B_Arbitro')

# aparências por jogador (1024 px, carregadas pela partida via Resources/Visuais): {modelo}_{time}_{n}
kits['A_Casa'] = A
VIS = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'Craquez', 'Resources', 'Visuais') + '/'
if len(sys.argv) > 4: VIS = sys.argv[4]
os.makedirs(VIS, exist_ok=True)
for key, base in kits.items():
    model = key[0]
    small = np.asarray(Image.fromarray((np.clip(base, 0, 1) * 255).astype(np.uint8)).resize((1024, 1024), Image.LANCZOS)).astype(np.float32) / 255
    for i in range(1, len(LOOKS[model])):
        Image.fromarray((np.clip(look(small, model, i), 0, 1) * 255 + 0.5).astype(np.uint8)).save(VIS + '%s_%d.jpg' % (key, i), quality=90)
        print('ok visual', key, i)
