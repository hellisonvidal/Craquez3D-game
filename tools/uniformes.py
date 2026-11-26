"""Gerador de uniformes (camisa, calção, meiões e chuteiras) pintados em 3D direto na malha revisada.

Uso:  python3 -I tools/uniformes.py CORPO_JOGADOR.npz CORPO_GOLEIRO.npz tools/uniformes_ref/Tecido_Fabric026_NormalGL.jpg
      (CORPO_*.npz: tools/blender/corpo_uv.py; tecido: ambientCG Fabric026, CC0; referências originais em tools/uniformes_ref)

A separação entre camisa, calção, meião, chuteira, luva e pele usa diretamente a geometria 3D das bordas esculpidas
na malha e os ossos dominantes (sem depender de máscaras de cor na textura antiga, eliminando manchas ciano/cinza,
bordas serrilhadas e fantasmas de escudos antigos). O sombreamento de dobras vem das normais esculpidas da malha.

Grava em Assets/Craquez/Textures/Uniformes: A_Casa, A_Visitante, A_Arbitro, B_GoleiroCasa, B_GoleiroVisitante (2048)
e em Assets/Craquez/Resources/Visuais: A_{Casa|Visitante}_{1..7} (1024: tom de pele e cor da chuteira de cada visual).
Também acrescenta a trama do tecido ao normal map do corpo (Jogador_Normal / Goleiro_Normal) nas regiões de pano.
"""
import sys, os
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from raster import raster

ROOT = os.path.join(HERE, '..', 'Assets', 'Craquez')
TEX = os.path.join(ROOT, 'Textures')
OUT = os.path.join(TEX, 'Uniformes')
VIS = os.path.join(ROOT, 'Resources', 'Visuais')
N = 2048


def hexc(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32) / 255


def rgb2hsv(im):
    r, g, b = im[..., 0], im[..., 1], im[..., 2]
    mx = im.max(-1); mn = im.min(-1); d = mx - mn
    s = np.where(mx > 0, d / np.maximum(mx, 1e-6), 0)
    dd = np.maximum(d, 1e-6)
    h = np.where(mx == r, ((g - b) / dd) % 6, np.where(mx == g, (b - r) / dd + 2, (r - g) / dd + 4)) * 60
    return np.where(d < 1e-6, 0, h), s, mx


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def corpo(npz, ref, ref_normal, modelo):
    """Mapa por texel: posição 3D, osso, normais, regiões exatas das peças de roupa e sombreamento limpo."""
    d = np.load(npz)
    nomes = list(d['nomes'])
    J = dict(zip(d['juntas_nomes'], d['juntas']))
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
    NX, NY, NZ = P[..., 4], P[..., 5], P[..., 6]
    bone = lambda *ns: np.isin(B, [nomes.index(n) for n in ns if n in nomes])

    R = np.asarray(Image.open(ref).convert('RGB').resize((N, N), Image.LANCZOS)).astype(np.float32) / 255
    Nb = np.asarray(Image.open(ref_normal).convert('RGB').resize((N, N), Image.LANCZOS)).astype(np.float32) / 255
    nb = Nb * 2.0 - 1.0

    zq, zj, zt, zo = J['LeftUpLeg'][2], J['LeftLeg'][2], J['LeftFoot'][2], J['LeftArm'][2]
    z_neck = J['Neck'][2]
    ax = np.abs(X)

    is_gk = (modelo == 'goleiro')
    leg_bones = bone('LeftUpLeg', 'RightUpLeg', 'LeftLeg', 'RightLeg', 'LeftFoot', 'RightFoot', 'LeftToeBase', 'RightToeBase')
    arm_bones = bone('LeftShoulder', 'RightShoulder', 'LeftArm', 'RightArm', 'LeftForeArm', 'RightForeArm', 'LeftHand', 'RightHand')

    neck_y = J['Neck'][1]
    r_neck = np.sqrt(X ** 2 + ((Y - neck_y) * 0.92) ** 2)

    if is_gk:
        pe = leg_bones & (Z < 0.146)
        meiao = leg_bones & (Z >= 0.146) & (Z <= 0.586)
        bainha = 1.088
        shorts_z0 = 0.802
        calcao = (Z >= shorts_z0) & (Z <= bainha) & (ax < 0.29) & ~arm_bones
        luva = arm_bones & (ax > 0.618)
        # Gola abraça a base do pescoço (raio > 6,6 cm) sem deixar decote canoa largo
        in_collar = (Z <= z_neck + 0.012) & ((Z <= z_neck - 0.032) | (r_neck >= 0.066))
        tronco_cam = (Z > bainha) & in_collar & ~leg_bones & ~luva & (ax <= 0.24)
        manga_cam = (Z > 1.26) & (ax > 0.14) & (ax <= 0.618) & ~luva & ~leg_bones
        camisa = tronco_cam | manga_cam
    else:
        pe = leg_bones & (Z < 0.144)
        meiao = leg_bones & (Z >= 0.144) & (Z <= 0.564)
        bainha = 1.038
        shorts_z0 = 0.746
        calcao = (Z >= shorts_z0) & (Z <= bainha) & (ax < 0.29) & ~arm_bones
        luva = np.zeros((N, N), dtype=bool)
        in_collar = (Z <= z_neck + 0.016) & ((Z <= z_neck - 0.028) | (r_neck >= 0.066))
        tronco_cam = (Z > bainha) & in_collar & ~leg_bones & (ax <= 0.24)
        manga_cam = (Z > 1.24) & (Z <= z_neck + 0.03) & (ax > 0.14) & (ax <= 0.396) & ~leg_bones
        camisa = tronco_cam | manga_cam

    pele = ~(camisa | calcao | meiao | pe | luva)

    # Limpa manchas residuais de tinta/gola antiga nas bordas de pele (pescoço, joelho, braço)
    h_r, s_r, v_r = rgb2hsv(R)
    non_skin_stain = pele & (((h_r > 70) & (h_r < 320) & (s_r > 0.20)) | ((s_r < 0.12) & (v_r > 0.68)) | (v_r < 0.24))
    good_skin = pele & ~non_skin_stain & (v_r > 0.30) & (v_r < 0.86) & (s_r > 0.15) & (s_r < 0.58)
    if good_skin.sum() > 200 and non_skin_stain.any():
        med_skin = np.median(R[good_skin], axis=0)
        R_clean = R.copy()
        shade_p = np.clip(0.90 + 0.10 * (-NY) + 0.05 * nb[..., 1], 0.76, 1.10)[..., None]
        R_clean[non_skin_stain] = np.clip(med_skin * shade_p[non_skin_stain], 0, 1)
        R = R_clean

    # Sombreamento físico limpo de dobras e volume a partir das normais 3D + normal map esculpido (zero logos antigos!)
    fold = 0.96 + 0.07 * (-NY) + 0.04 * NZ + 0.08 * (nb[..., 1] * 0.6 + (nb[..., 2] - 0.85))
    hem_ao = 1.0 - 0.10 * np.exp(-((Z - bainha) / 0.028) ** 2) - 0.07 * np.exp(-((Z - shorts_z0) / 0.022) ** 2)
    groin_ao = np.where((calcao) & (ax < 0.055), 0.90, 1.0)
    sombra = np.clip(ndimage.gaussian_filter(fold * hem_ao * groin_ao, 1.2), 0.78, 1.12).astype(np.float32)

    lum = R.mean(-1)
    return dict(
        P=P, X=X, Y=Y, Z=Z, NX=NX, NY=NY, NZ=NZ, B=B, bone=bone, J=J, R=R,
        camisa=camisa, calcao=calcao, meiao=meiao, pe=pe, luva=luva, pele=pele,
        sombra=sombra, bainha=bainha, shorts_z0=shorts_z0, zj=zj, zt=zt, zo=zo,
        z_neck=z_neck, r_neck=r_neck, lum=lum, is_gk=is_gk
    )


def pintar(c, kit):
    """kit: cores e desenho 3D contínuo. Devolve a textura 2048 (0..1)."""
    X, Y, Z = c['X'], c['Y'], c['Z']
    ax = np.abs(X)
    out = c['R'].copy()

    # Recolore detalhes verdes originais da luva do goleiro para combinar com a camisa do goleiro
    if c['is_gk'] and c['luva'].any():
        hl, sl, vl = rgb2hsv(out)
        green_glove = c['luva'] & (hl > 75) & (hl < 175) & (sl > 0.22)
        if green_glove.any():
            sh = np.clip(vl / max(float(np.median(vl[green_glove])), 1e-3), 0.65, 1.25)[..., None]
            out[green_glove] = np.clip(kit['camisa'] * sh[green_glove], 0, 1)

    # ---- camisa
    cam = np.zeros((N, N, 3), np.float32) + kit['camisa']
    manga = c['camisa'] & (ax > (0.175 if c['is_gk'] else 0.195))
    tronco = c['camisa'] & ~manga

    if kit.get('listras'):
        lar, per, cor = kit['listras']
        phase = np.abs(np.mod(X + per * 0.5, per) - per * 0.5)
        w = (1.0 - smooth(lar * 0.45, lar * 0.58, phase))[..., None]
        cam = np.where(tronco[..., None], cam * (1 - w) + cor * w, cam)

    if kit.get('faixa'):
        cor, lar = kit['faixa']
        x0, z0, x1, z1 = -0.17, c['zo'] - 0.01, 0.15, c['bainha'] + 0.03
        k = (z1 - z0) / (x1 - x0)
        dist = np.abs((Z - z0) - k * (X - x0)) / np.sqrt(1 + k * k)
        w = (1.0 - smooth(lar * 0.46, lar * 0.54, dist))[..., None]
        cam = np.where(tronco[..., None], cam * (1 - w) + cor * w, cam)

    if kit.get('ombros') is not None:
        raglan_line = c['zo'] + 0.045 - 0.55 * (ax - 0.11)
        raglan = c['camisa'] & ((ax > 0.195) | ((ax > 0.11) & (Z > raglan_line)))
        cam[raglan] = kit['ombros']

    if kit.get('lateral') is not None:
        side = tronco & (np.abs(c['NX']) > 0.76) & (Z < c['zo'] - 0.06)
        cam[side] = kit['lateral']

    # Gola anatômica ao redor do pescoço (anel de 2,2 cm de largura acompanhando o contorno cervical)
    gola = c['camisa'] & (Z > c['z_neck'] - 0.055) & (c['r_neck'] < 0.090)
    cam[gola] = kit['gola']

    cuff_end = 0.618 if c['is_gk'] else 0.396
    punho = manga & (ax > cuff_end - 0.022)
    cam[punho] = kit['gola']

    # Barra discreta na cintura da camisa
    barra_cam = c['camisa'] & (Z < c['bainha'] + 0.012)
    cam[barra_cam] = cam[barra_cam] * 0.90

    out[c['camisa']] = cam[c['camisa']]

    # ---- calção
    cal = np.zeros((N, N, 3), np.float32) + kit['calcao']
    reg = c['calcao']
    if reg.any() and kit.get('calcao_faixa') is not None:
        lado = reg & (ax > 0.155) & (np.abs(c['NX']) > 0.74)
        cal[lado] = kit['calcao_faixa']
        barra = reg & (Z < c['shorts_z0'] + 0.020)
        cal[barra] = kit['calcao_faixa']
    # Cós elástico escuro sutil no topo do calção
    cos = reg & (Z > c['bainha'] - 0.016)
    cal[cos] = cal[cos] * 0.86
    out[reg] = cal[reg]

    # ---- meiões
    mei = np.zeros((N, N, 3), np.float32) + kit['meiao']
    reg = c['meiao']
    if reg.any():
        top = 0.586 if c['is_gk'] else 0.564
        for a, b in kit.get('meiao_barras', []):
            mei[reg & (Z < top - a) & (Z > top - b)] = kit['meiao_barra']
    out[reg] = mei[reg]

    # ---- chuteiras: cabedal estruturado + sola/travas
    reg = c['pe']
    if reg.any():
        cor, sola = kit['chuteira']
        boot_sh = np.clip(0.85 + 0.22 * c['NZ'] + 0.14 * (-c['NY']), 0.62, 1.22)[..., None]
        ch = np.where((Z < 0.026)[..., None], sola, cor) * boot_sh
        out[reg] = ch[reg]

    # Aplica o sombreamento 3D limpo das dobras do tecido
    pano = c['camisa'] | c['calcao'] | c['meiao']
    out = np.where(pano[..., None], out * c['sombra'][..., None], out)
    return np.clip(out, 0, 1)


def recolor_pele(img, c, alvo):
    """Troca o tom da pele (braços, mãos, joelhos, pescoço) preservando o sombreamento natural."""
    if alvo is None:
        return img
    sk = c['pele']
    if not sk.any():
        return img
    med = np.median(img[sk], axis=0) + 1e-3
    ratio = np.clip(img / med, 0.35, 1.65)
    novo = np.clip(np.array(alvo, np.float32)[None, None, :] * ratio, 0, 1)
    m = ndimage.gaussian_filter(sk.astype(np.float32), 1.0)[..., None]
    return img * (1 - m) + novo * m


def tecido_normal(ref_normal, base_normal, c, tecido, periodo=48):
    """Trama do tecido (normal map CC0 repetido na UV) somada ao normal map original nas regiões de pano."""
    Nb = np.asarray(Image.open(ref_normal).convert('RGB')).astype(np.float32) / 255
    T0 = np.asarray(Image.open(tecido).convert('RGB').resize((periodo, periodo), Image.LANCZOS)).astype(np.float32) / 255
    reps = int(np.ceil(Nb.shape[0] / periodo))
    Tt = np.tile(T0, (reps, reps, 1))[:Nb.shape[0], :Nb.shape[1]]
    pano = np.asarray(Image.fromarray(((c['camisa'] | c['calcao'] | c['meiao']) * 255).astype(np.uint8)).resize((Nb.shape[1], Nb.shape[0]))) > 127
    nb = Nb * 2 - 1
    nt = Tt * 2 - 1
    n = np.stack([nb[..., 0] + nt[..., 0] * 0.55, nb[..., 1] + nt[..., 1] * 0.55, nb[..., 2] * nt[..., 2]], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True) + 1e-6
    out = np.where(pano[..., None], n * 0.5 + 0.5, Nb)
    Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)).save(base_normal)
    print('normal map com tecido:', os.path.relpath(base_normal))


def salvar(img, path, size=None, q=92):
    im = Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8))
    if size:
        im = im.resize((size, size), Image.LANCZOS)
    im.save(path, quality=q, subsampling=0)
    print('ok', os.path.relpath(path))


PELES = [None, (0.27, 0.175, 0.125), (0.86, 0.68, 0.55), (0.80, 0.60, 0.45), None, (0.45, 0.30, 0.21), (0.86, 0.68, 0.55), (0.62, 0.44, 0.32)]
CHUTEIRAS = [('#111111', '#d81f26'), ('#f2f2f2', '#111111'), ('#e8f31c', '#1b1b1b'), ('#ff6a13', '#1b1b1b'),
             ('#151515', '#d4a017'), ('#1f6fff', '#f2f2f2'), ('#c8102e', '#f2f2f2'), ('#3ee0a6', '#1b1b1b')]

BRANCO, PRETO = hexc('#f4f4f2'), hexc('#141619')
KITS = {
    # Craquez FC: azul royal com listras finas brancas, calção marinho e meiões azuis
    'A_Casa': dict(camisa=hexc('#1d4ed8'), listras=(0.013, 0.056, BRANCO), gola=BRANCO, lateral=hexc('#15399e'),
                   calcao=hexc('#0f1f56'), calcao_faixa=BRANCO, meiao=hexc('#1d4ed8'), meiao_barras=[(0.025, 0.042), (0.055, 0.070)],
                   meiao_barra=BRANCO),
    # Atlético Celeste: branco com faixa diagonal celeste
    'A_Visitante': dict(camisa=BRANCO, faixa=(hexc('#4bb3e8'), 0.088), gola=hexc('#4bb3e8'), lateral=hexc('#e2eef7'), calcao=BRANCO,
                        calcao_faixa=hexc('#4bb3e8'), meiao=BRANCO, meiao_barras=[(0.030, 0.058)], meiao_barra=hexc('#4bb3e8')),
    # Arbitragem: preto com detalhes amarelo-limão
    'A_Arbitro': dict(camisa=hexc('#16181c'), gola=hexc('#e8f31c'), ombros=hexc('#23262c'), lateral=hexc('#101215'), calcao=hexc('#141619'),
                      calcao_faixa=hexc('#e8f31c'), meiao=hexc('#141619'), meiao_barras=[(0.028, 0.048)], meiao_barra=hexc('#e8f31c'),
                      chuteira=(PRETO, PRETO)),
}
KITS_GK = {
    'B_GoleiroCasa': dict(camisa=hexc('#1c9c4e'), ombros=hexc('#105e2e'), gola=PRETO, lateral=hexc('#126c35'), calcao=hexc('#136f37'),
                          calcao_faixa=BRANCO, meiao=hexc('#1c9c4e'), meiao_barras=[(0.032, 0.055)], meiao_barra=BRANCO,
                          chuteira=(PRETO, hexc('#3ee0a6'))),
    'B_GoleiroVisitante': dict(camisa=hexc('#f57416'), ombros=hexc('#1c1e22'), gola=PRETO, lateral=hexc('#c4560c'), calcao=hexc('#181a1e'),
                               calcao_faixa=hexc('#f57416'), meiao=hexc('#f57416'), meiao_barras=[(0.032, 0.055)], meiao_barra=PRETO,
                               chuteira=(PRETO, hexc('#f57416'))),
}

if __name__ == '__main__':
    npz_j, npz_g, tecido = sys.argv[1:4]
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(VIS, exist_ok=True)
    REF = os.path.join(HERE, 'uniformes_ref')
    cj = corpo(npz_j, os.path.join(REF, 'A_Casa_ref.jpg'), os.path.join(REF, 'Jogador_Normal_ref.png'), 'jogador')
    for nome, kit in KITS.items():
        k = dict(kit)
        k.setdefault('chuteira', (hexc(CHUTEIRAS[0][0]), hexc(CHUTEIRAS[0][1])))
        base = pintar(cj, k)
        salvar(base, os.path.join(OUT, nome + '.jpg'))
        if nome == 'A_Arbitro':
            continue
        for v in range(1, 8):
            kv = dict(kit)
            kv['chuteira'] = (hexc(CHUTEIRAS[v][0]), hexc(CHUTEIRAS[v][1]))
            img = recolor_pele(pintar(cj, kv), cj, PELES[v])
            salvar(img, os.path.join(VIS, '%s_%d.jpg' % (nome, v)), 1024, 90)
    cg = corpo(npz_g, os.path.join(REF, 'Goleiro_Albedo_ref.png'), os.path.join(REF, 'Goleiro_Normal_ref.png'), 'goleiro')
    for nome, kit in KITS_GK.items():
        salvar(pintar(cg, kit), os.path.join(OUT, nome + '.jpg'))
    tecido_normal(os.path.join(REF, 'Jogador_Normal_ref.png'), os.path.join(TEX, 'Jogador_Normal.png'), cj, tecido)
    tecido_normal(os.path.join(REF, 'Goleiro_Normal_ref.png'), os.path.join(TEX, 'Goleiro_Normal.png'), cg, tecido)
