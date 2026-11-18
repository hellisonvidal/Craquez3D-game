"""Gerador de uniformes (camisa, calção, meiões e chuteiras) pintados direto na textura do corpo.

Uso:  python3 -I tools/uniformes.py CORPO_JOGADOR.npz CORPO_GOLEIRO.npz tools/uniformes_ref/Tecido_Fabric026_NormalGL.jpg
      (CORPO_*.npz: tools/blender/corpo_uv.py; tecido: ambientCG Fabric026, CC0; referências originais em tools/uniformes_ref)

Cada texel do corpo sabe a posição 3D (pose T) e o osso dominante; as regiões vêm daí e da textura original de
referência, que marca onde a malha tem pano e onde tem pele (mangas, joelhos, mãos). O desenho é feito em 3D
(listras verticais, faixa diagonal, gola, punhos, faixa lateral do calção, barras do meião), então fica contínuo
entre as ilhas da UV. Sombras suaves da textura original são mantidas; logotipos e marcas, não.

Grava em Assets/Craquez/Textures/Uniformes: A_Casa, A_Visitante, A_Arbitro, B_GoleiroCasa, B_GoleiroVisitante (2048)
e em Assets/Craquez/Resources/Visuais: A_{Casa|Visitante}_{1..7} (1024: tom de pele e cor da chuteira de cada visual).
Também acrescenta a trama do tecido ao normal map do corpo (Jogador_Normal / Goleiro_Normal) nas regiões de pano.
"""
import sys, os, json
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from raster import raster, sample

ROOT = os.path.join(HERE, '..', 'Assets', 'Craquez')
TEX = os.path.join(ROOT, 'Textures')
OUT = os.path.join(TEX, 'Uniformes')
VIS = os.path.join(ROOT, 'Resources', 'Visuais')
N = 2048


def hexc(h):
    h = h.lstrip('#'); return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32) / 255


def rgb2hsv(im):
    r, g, b = im[..., 0], im[..., 1], im[..., 2]
    mx = im.max(-1); mn = im.min(-1); d = mx - mn
    s = np.where(mx > 0, d / np.maximum(mx, 1e-6), 0)
    dd = np.maximum(d, 1e-6)
    h = np.where(mx == r, ((g - b) / dd) % 6, np.where(mx == g, (b - r) / dd + 2, (r - g) / dd + 4)) * 60
    return np.where(d < 1e-6, 0, h), s, mx


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)


def skin_mask(h, s, v):
    hue = smooth(2, 9, h) * (1 - smooth(40, 50, h))
    return hue * smooth(0.10, 0.18, s) * (1 - smooth(0.62, 0.75, s)) * smooth(0.18, 0.3, v)


def corpo(npz, ref, cloth_fn):
    """Mapa por texel: posição, osso, regiões (camisa/calção/meião/chuteira/pele) e sombra da referência."""
    d = np.load(npz)
    nomes = list(d['nomes']); J = dict(zip(d['juntas_nomes'], d['juntas']))
    uvpx = np.stack([d['uv'][..., 0] * N, (1 - d['uv'][..., 1]) * N], -1)
    bmaj = np.array([np.bincount(b).argmax() for b in d['bone']], np.float32)
    nrm = np.cross(d['pos'][:, 1] - d['pos'][:, 0], d['pos'][:, 2] - d['pos'][:, 0])
    nrm /= np.linalg.norm(nrm, axis=1, keepdims=True) + 1e-9
    vals = np.concatenate([d['pos'], np.repeat(bmaj[:, None, None], 3, 1), np.repeat(nrm[:, None, :], 3, 1)], -1)
    P, M = raster(uvpx, vals, N, N)
    idx = ndimage.distance_transform_edt(~M, return_distances=False, return_indices=True)
    P = P[idx[0], idx[1]]
    X, Y, Z = P[..., 0], P[..., 1], P[..., 2]
    B = np.rint(P[..., 3]).astype(int)
    bone = lambda *ns: np.isin(B, [nomes.index(n) for n in ns if n in nomes])
    R = np.asarray(Image.open(ref).convert('RGB').resize((N, N), Image.LANCZOS)).astype(np.float32) / 255
    h, s, v = rgb2hsv(R)
    pano = cloth_fn(h, s, v)
    pano = ndimage.binary_closing(pano, iterations=6) & ~(skin_mask(h, s, v) > 0.6)   # tapa logotipos/manchas
    pele = (skin_mask(h, s, v) > 0.5) & ~pano
    zq, zj, zt, zo = J['LeftUpLeg'][2], J['LeftLeg'][2], J['LeftFoot'][2], J['LeftArm'][2]
    pe = bone('LeftFoot', 'RightFoot', 'LeftToeBase', 'RightToeBase') & (Z < zt + 0.04) & ~pele
    pano &= ~pe
    bainha = zq + 0.15                                   # barra da camisa
    camisa = pano & (Z > bainha) & ~bone('LeftUpLeg', 'RightUpLeg', 'LeftLeg', 'RightLeg')
    meiao = pano & (Z < zj + 0.13) & bone('LeftLeg', 'RightLeg', 'LeftFoot', 'RightFoot')
    calcao = pano & ~camisa & ~meiao & (Z > zj)
    camisa |= pano & ~calcao & ~meiao & (Z > bainha - 0.02)
    mao = bone('LeftHand', 'RightHand') & ~camisa
    lum = R.mean(-1)
    sombra = np.ones((N, N), np.float32)
    for reg in (camisa, calcao, meiao, pe):
        if reg.sum() < 100: continue
        l = ndimage.gaussian_filter(np.where(reg, lum, np.median(lum[reg])), 5)
        sombra = np.where(reg, np.clip(l / max(np.median(lum[reg]), 1e-3), 0.8, 1.12), sombra)
    return dict(P=P, X=X, Y=Y, Z=Z, NY=P[..., 7], B=B, bone=bone, J=J, R=R, camisa=camisa, calcao=calcao, meiao=meiao,
                pe=pe, pele=pele, mao=mao, sombra=sombra, bainha=bainha, zj=zj, zt=zt, zo=zo, lum=lum)


def pintar(c, kit):
    """kit: cores e desenho. Devolve a textura 2048 (0..1)."""
    X, Y, Z = c['X'], c['Y'], c['Z']
    out = c['R'].copy()
    # ---- camisa
    cam = np.zeros((N, N, 3), np.float32) + kit['camisa']
    tronco = c['camisa'] & ~c['bone']('LeftArm', 'RightArm', 'LeftForeArm', 'RightForeArm')
    if kit.get('listras'):
        lar, per, cor = kit['listras']
        m = tronco & (np.mod(X + per / 2, per) < lar)
        cam[m] = cor
    if kit.get('faixa'):
        cor, lar = kit['faixa']
        # do ombro direito (x<0) ao quadril esquerdo (x>0), na frente e nas costas
        x0, z0, x1, z1 = -0.17, c['zo'] - 0.01, 0.15, c['bainha'] + 0.03
        k = (z1 - z0) / (x1 - x0)
        dist = np.abs((Z - z0) - k * (X - x0)) / np.sqrt(1 + k * k)
        cam[tronco & (dist < lar / 2)] = cor
    if kit.get('ombros'):
        cam[c['camisa'] & (Z > c['zo'] - 0.05) & (np.abs(X) > 0.11)] = kit['ombros']
    # gola e punhos
    gola = c['camisa'] & (Z > c['J']['Neck'][2] - 0.035)
    cam[gola] = kit['gola']
    braco = c['camisa'] & c['bone']('LeftArm', 'RightArm', 'LeftForeArm', 'RightForeArm', 'LeftHand', 'RightHand')
    if braco.any():
        xmax = np.abs(X[braco]).max()
        cam[braco & (np.abs(X) > xmax - 0.025)] = kit['gola']
    # laterais (faixa sob o braço, até a barra)
    if kit.get('lateral'):
        side = tronco & (np.abs(c['P'][..., 6]) > 0.75) & (Z < c['zo'] - 0.08)
        cam[side] = kit['lateral']
    out[c['camisa']] = cam[c['camisa']]
    # ---- calção
    cal = np.zeros((N, N, 3), np.float32) + kit['calcao']
    reg = c['calcao']
    if reg.any() and kit.get('calcao_faixa') is not None:
        lado = reg & (np.abs(c['P'][..., 6]) > 0.8)
        cal[lado] = kit['calcao_faixa']
        cal[reg & (Z < c['zj'] + 0.27) & (Z > c['zj'] + 0.255)] = kit['calcao_faixa']   # barra
    out[reg] = cal[reg]
    # ---- meiões
    mei = np.zeros((N, N, 3), np.float32) + kit['meiao']
    reg = c['meiao']
    if reg.any():
        top = Z[reg].max()
        for a, b in kit.get('meiao_barras', []):
            mei[reg & (Z < top - a) & (Z > top - b)] = kit['meiao_barra']
    out[reg] = mei[reg]
    # ---- chuteiras: cor do cabedal + sola/travas
    reg = c['pe']
    if reg.any():
        cor, sola = kit['chuteira']
        l = c['lum']
        det = np.clip(l / max(np.median(l[reg]), 1e-3), 0.6, 1.5)[..., None]
        ch = np.where((Z < 0.022)[..., None], sola, cor) * det
        out[reg] = ch[reg]
    # sombras suaves da textura original (dobras) sobre as cores novas
    pano = c['camisa'] | c['calcao'] | c['meiao']
    out = np.where(pano[..., None], out * c['sombra'][..., None], out)
    return np.clip(out, 0, 1)


def recolor_pele(img, c, alvo):
    """Troca o tom da pele do corpo (braços, mãos, joelhos, pescoço) mantendo as sombras."""
    if alvo is None: return img
    h, s, v = rgb2hsv(img)
    m = np.clip(skin_mask(h, s, v) * (~(c['camisa'] | c['calcao'] | c['meiao'] | c['pe'])), 0, 1)
    sk = m > 0.5
    if not sk.any(): return img
    med = np.median(img[sk], 0) + 1e-3
    novo = np.clip(np.array(alvo, np.float32)[None, None, :] * np.clip(img / med, 0.2, 2.2), 0, 1)
    return img * (1 - m[..., None]) + novo * m[..., None]


def tecido_normal(ref_normal, base_normal, c, tecido, periodo=48):
    """Trama do tecido (normal map CC0 repetido na UV) somada ao normal map original do corpo nas regiões de pano."""
    Nb = np.asarray(Image.open(ref_normal).convert('RGB')).astype(np.float32) / 255
    T0 = np.asarray(Image.open(tecido).convert('RGB').resize((periodo, periodo), Image.LANCZOS)).astype(np.float32) / 255
    reps = int(np.ceil(Nb.shape[0] / periodo))
    Tt = np.tile(T0, (reps, reps, 1))[:Nb.shape[0], :Nb.shape[1]]
    pano = np.asarray(Image.fromarray(((c['camisa'] | c['calcao'] | c['meiao']) * 255).astype(np.uint8)).resize((Nb.shape[1], Nb.shape[0]))) > 127
    nb = Nb * 2 - 1; nt = Tt * 2 - 1
    # "whiteout": soma as inclinações
    n = np.stack([nb[..., 0] + nt[..., 0] * 0.6, nb[..., 1] + nt[..., 1] * 0.6, nb[..., 2] * nt[..., 2]], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True) + 1e-6
    out = np.where(pano[..., None], n * 0.5 + 0.5, Nb)
    Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)).save(base_normal)
    print('normal map com tecido:', os.path.relpath(base_normal))


def salvar(img, path, size=None, q=92):
    im = Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8))
    if size: im = im.resize((size, size), Image.LANCZOS)
    im.save(path, quality=q, subsampling=0); print('ok', os.path.relpath(path))


# tons de pele dos visuais (mesma ordem de tools/make_kits.py / tools/tons_visuais.json); None = pele original
PELES = [None, (0.27, 0.175, 0.125), (0.86, 0.68, 0.55), (0.80, 0.60, 0.45), None, (0.45, 0.30, 0.21), (0.86, 0.68, 0.55), (0.62, 0.44, 0.32)]
# chuteiras de cada visual (cabedal, sola)
CHUTEIRAS = [('#111111', '#d81f26'), ('#f2f2f2', '#111111'), ('#e8f31c', '#1b1b1b'), ('#ff6a13', '#1b1b1b'),
             ('#151515', '#d4a017'), ('#1f6fff', '#f2f2f2'), ('#c8102e', '#f2f2f2'), ('#3ee0a6', '#1b1b1b')]

BRANCO, PRETO = hexc('#f4f4f2'), hexc('#121212')
KITS = {
    # Craquez FC: azul com listras finas brancas
    'A_Casa': dict(camisa=hexc('#1f4fd1'), listras=(0.012, 0.055, BRANCO), gola=BRANCO, lateral=hexc('#16388f'),
                   calcao=hexc('#0f1f5c'), calcao_faixa=BRANCO, meiao=hexc('#1f4fd1'), meiao_barras=[(0.03, 0.045), (0.06, 0.075)],
                   meiao_barra=BRANCO),
    # Atlético Celeste: branco com faixa diagonal celeste
    'A_Visitante': dict(camisa=BRANCO, faixa=(hexc('#5bb7e8'), 0.085), gola=hexc('#5bb7e8'), calcao=BRANCO,
                        calcao_faixa=hexc('#5bb7e8'), meiao=BRANCO, meiao_barras=[(0.035, 0.065)], meiao_barra=hexc('#5bb7e8')),
    # arbitragem: preto com detalhes amarelo-limão
    'A_Arbitro': dict(camisa=hexc('#141414'), gola=hexc('#e8f31c'), ombros=hexc('#e8f31c'), calcao=hexc('#141414'),
                      calcao_faixa=hexc('#e8f31c'), meiao=hexc('#141414'), meiao_barras=[(0.03, 0.05)], meiao_barra=hexc('#e8f31c'),
                      chuteira=(PRETO, PRETO)),
}
KITS_GK = {
    'B_GoleiroCasa': dict(camisa=hexc('#1e9e4f'), ombros=hexc('#0e5a2b'), gola=PRETO, lateral=hexc('#0e5a2b'), calcao=hexc('#14773a'),
                          calcao_faixa=PRETO, meiao=hexc('#1e9e4f'), meiao_barras=[(0.04, 0.06)], meiao_barra=PRETO,
                          chuteira=(PRETO, hexc('#3ee0a6'))),
    'B_GoleiroVisitante': dict(camisa=hexc('#ff7a1a'), ombros=PRETO, gola=PRETO, lateral=hexc('#c25510'), calcao=PRETO,
                               calcao_faixa=hexc('#ff7a1a'), meiao=hexc('#ff7a1a'), meiao_barras=[(0.04, 0.06)], meiao_barra=PRETO,
                               chuteira=(PRETO, hexc('#ff7a1a'))),
}

if __name__ == '__main__':
    npz_j, npz_g, tecido = sys.argv[1:4]
    os.makedirs(OUT, exist_ok=True); os.makedirs(VIS, exist_ok=True)
    azul = lambda h, s, v: (h > 190) & (h < 260) & (s > 0.3) & (v > 0.12)
    verde = lambda h, s, v: (h > 85) & (h < 175) & (s > 0.25) & (v > 0.1)
    REF = os.path.join(HERE, 'uniformes_ref')   # texturas originais (referência de onde há pano/pele), nunca sobrescritas
    cj = corpo(npz_j, os.path.join(REF, 'A_Casa_ref.jpg'), azul)
    for nome, kit in KITS.items():
        k = dict(kit); k.setdefault('chuteira', (hexc(CHUTEIRAS[0][0]), hexc(CHUTEIRAS[0][1])))
        base = pintar(cj, k)
        salvar(base, os.path.join(OUT, nome + '.jpg'))
        if nome == 'A_Arbitro': continue
        for v in range(1, 8):
            kv = dict(kit); kv['chuteira'] = (hexc(CHUTEIRAS[v][0]), hexc(CHUTEIRAS[v][1]))
            img = recolor_pele(pintar(cj, kv), cj, PELES[v])
            salvar(img, os.path.join(VIS, '%s_%d.jpg' % (nome, v)), 1024, 90)
    cg = corpo(npz_g, os.path.join(REF, 'Goleiro_Albedo_ref.png'), verde)
    for nome, kit in KITS_GK.items():
        salvar(pintar(cg, kit), os.path.join(OUT, nome + '.jpg'))
    tecido_normal(os.path.join(REF, 'Jogador_Normal_ref.png'), os.path.join(TEX, 'Jogador_Normal.png'), cj, tecido)
    tecido_normal(os.path.join(REF, 'Goleiro_Normal_ref.png'), os.path.join(TEX, 'Goleiro_Normal.png'), cg, tecido)
