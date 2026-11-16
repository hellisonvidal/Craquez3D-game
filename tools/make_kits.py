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

def save(img, name):
    Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)).save(OUT + name + '.jpg', quality=93, subsampling=0)
    print('ok', name)

A = load('Jogador_Albedo.png')
B = load('Goleiro_Albedo.png')

# Craquez FC (casa): preto com detalhes vermelhos  -> modelo A já é assim; B recolorido
save(model_b(B, (0.07, 0.07, 0.08), (0.86, 0.1, 0.1)), 'B_Casa')
# Atlético Celeste (visitante): branco com azul-celeste
save(model_a(A, (0.92, 0.93, 0.95), (0.12, 0.48, 0.95), white=(0.1, 0.25, 0.6)), 'A_Visitante')
save(model_b(B, (0.92, 0.93, 0.95), (0.12, 0.48, 0.95)), 'B_Visitante')
# Goleiros: casa = verde original (modelo B); visitante = laranja com preto (modelo A)
save(model_a(A, (1.0, 0.5, 0.08), (0.08, 0.08, 0.08), white=(0.1, 0.1, 0.1)), 'A_GoleiroVisitante')
# Arbitragem: amarelo-limão com preto
save(model_a(A, (0.86, 0.95, 0.18), (0.06, 0.06, 0.06), white=(0.06, 0.06, 0.06)), 'A_Arbitro')
save(model_b(B, (0.86, 0.95, 0.18), (0.06, 0.06, 0.06)), 'B_Arbitro')
