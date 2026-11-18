"""Rostos dos jogadores a partir de fotos sintéticas (gerador gratuito: thispersondoesnotexist.com, StyleGAN2).

Uso (Python com mediapipe, numpy, scipy, pillow):
  python -I tools/rostos.py CABECA.npz ALBEDO_ORIGINAL.png PASTA_FOTOS LISTA(ids,...) SAIDA_DIR PREFIXO [tamanho]

CABECA.npz vem de tools/blender/cabeca_cabelos.py (triângulos da cabeça: posição de repouso, UV nova e UV antiga).
Passos:
 1) rasteriza a cabeça no espaço da UV nova (posição 3D, normal e UV antiga de cada texel);
 2) renderiza a cabeça de frente com a textura original e acha os 468 pontos do FaceMesh do MediaPipe;
 3) em cada foto acha os mesmos 468 pontos e projeta a foto na cabeça por triângulos (afim por partes);
 4) fora do rosto (orelhas, nuca, couro cabeludo) usa a pele da própria foto; o tom é casado com o do corpo
    escolhido (o pescoço é do corpo) e a parte de baixo funde com ele, sem costura.
Grava SAIDA_DIR/PREFIXO_<id>.jpg e SAIDA_DIR/PREFIXO.json (tom de pele, visual do corpo e cor do cabelo de cada rosto).
LISTA: ids separados por vírgula, cada um com o visual do corpo (tom de pele) conferido à mão: '297:5,05:0,...'.
"""
import sys, os, json
import numpy as np
from PIL import Image, ImageFilter
from scipy.spatial import Delaunay
from scipy import ndimage
import mediapipe as mp

FM = mp.solutions.face_mesh.FaceMesh(static_image_mode=True, max_num_faces=1, refine_landmarks=False, min_detection_confidence=0.3)


def landmarks(img):
    r = FM.process(np.asarray(img.convert('RGB')))
    if not r.multi_face_landmarks: return None
    h, w = img.size[1], img.size[0]
    return np.array([(l.x * w, l.y * h) for l in r.multi_face_landmarks[0].landmark], np.float32)


def raster(tris_xy, values, W, H, depth=None):
    """Rasteriza triângulos (coords em pixels) interpolando 'values' (n,3,k); com depth faz z-buffer (maior = mais perto)."""
    k = values.shape[2]
    out = np.zeros((H, W, k), np.float32); mask = np.zeros((H, W), bool)
    zb = np.full((H, W), -1e9, np.float32)
    for i in range(len(tris_xy)):
        p = tris_xy[i]
        x0, x1 = int(max(0, np.floor(p[:, 0].min()))), int(min(W - 1, np.ceil(p[:, 0].max())))
        y0, y1 = int(max(0, np.floor(p[:, 1].min()))), int(min(H - 1, np.ceil(p[:, 1].max())))
        if x0 > x1 or y0 > y1: continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        d = (p[1, 1] - p[2, 1]) * (p[0, 0] - p[2, 0]) + (p[2, 0] - p[1, 0]) * (p[0, 1] - p[2, 1])
        if abs(d) < 1e-12: continue
        a = ((p[1, 1] - p[2, 1]) * (gx - p[2, 0]) + (p[2, 0] - p[1, 0]) * (gy - p[2, 1])) / d
        b = ((p[2, 1] - p[0, 1]) * (gx - p[2, 0]) + (p[0, 0] - p[2, 0]) * (gy - p[2, 1])) / d
        c = 1 - a - b
        m = (a >= -1e-4) & (b >= -1e-4) & (c >= -1e-4)
        if not m.any(): continue
        if depth is not None:
            z = a * depth[i, 0] + b * depth[i, 1] + c * depth[i, 2]
            sub = zb[y0:y1 + 1, x0:x1 + 1]
            m = m & (z > sub); sub[m] = z[m]
        v = a[..., None] * values[i, 0] + b[..., None] * values[i, 1] + c[..., None] * values[i, 2]
        out[y0:y1 + 1, x0:x1 + 1][m] = v[m]
        mask[y0:y1 + 1, x0:x1 + 1][m] = True
    return out, mask


def sample(img, xy):
    """Amostragem bilinear de img (H,W,3 float) em coordenadas xy (n,2) de pixel."""
    H, W = img.shape[:2]
    x = np.clip(xy[:, 0] - 0.5, 0, W - 1.001); y = np.clip(xy[:, 1] - 0.5, 0, H - 1.001)
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int); fx = (x - x0)[:, None]; fy = (y - y0)[:, None]
    return (img[y0, x0] * (1 - fx) * (1 - fy) + img[y0, x0 + 1] * fx * (1 - fy) + img[y0 + 1, x0] * (1 - fx) * fy + img[y0 + 1, x0 + 1] * fx * fy)


def frente(pos, chin, top, R):
    """coordenadas de pixel da vista frontal ortográfica (personagem olhando para -Y; direita da imagem = +X)."""
    s = R / (top - chin) * 0.78
    return np.stack([R / 2 + pos[..., 0] * s, R * 0.9 - (pos[..., 2] - chin) * s], -1), s


if __name__ == '__main__':
    cab, albedo, fotos, ids, saida, prefixo = sys.argv[1:7]
    T = int(sys.argv[7]) if len(sys.argv) > 7 else 512
    os.makedirs(saida, exist_ok=True)
    d = np.load(cab)
    pos, uv, old = d['pos'], d['uv'], d['old_uv']
    chin, top = float(d['chin']), float(d['top'])
    A = np.asarray(Image.open(albedo).convert('RGB')).astype(np.float32)
    # 1) vista frontal da cabeça com a textura original (para os pontos do MediaPipe no modelo)
    R = 768
    fxy, s = frente(pos, chin, top, R)
    oldpx = np.stack([old[..., 0] * A.shape[1], (1 - old[..., 1]) * A.shape[0]], -1)
    o, m = raster(fxy, oldpx, R, R, depth=-pos[..., 1])
    rend = np.full((R, R, 3), 120, np.float32)
    rend[m] = sample(A, o[m][:, :2])
    Image.fromarray(rend.astype(np.uint8)).save(os.path.join(saida, prefixo + '_modelo_frente.png'))
    Lm = landmarks(Image.fromarray(rend.astype(np.uint8)))
    if Lm is None: sys.exit('MediaPipe não achou o rosto no modelo renderizado')
    print('modelo: 468 pontos ok')
    tri = Delaunay(Lm)

    # 2) cabeça no espaço da UV nova: posição 3D e normal de cada texel (a costura de trás passa de u=1: repete em U)
    nrm = np.cross(pos[:, 1] - pos[:, 0], pos[:, 2] - pos[:, 0]); nrm /= np.linalg.norm(nrm, axis=1, keepdims=True) + 1e-9
    vals = np.concatenate([pos, np.repeat(nrm[:, None, :], 3, 1)], -1)
    uvpx = np.stack([uv[..., 0] * T, (1 - uv[..., 1]) * T], -1)
    P1, M1 = raster(uvpx, vals, T, T)
    P2, M2 = raster(uvpx - np.array([T, 0], np.float32), vals, T, T)   # parte que passou de u = 1
    P = np.where(M1[..., None], P1, P2); M = M1 | M2
    # preenche um pouco além das ilhas (sem vazar cor nos mipmaps)
    idx = ndimage.distance_transform_edt(~M, return_distances=False, return_indices=True)
    P = P[idx[0], idx[1]]
    X, Z, NY = P[..., 0], P[..., 2], P[..., 4]
    fxy_t, _ = frente(P[..., :3], chin, top, R)
    flat = fxy_t.reshape(-1, 2)
    simp = tri.find_simplex(flat)
    Tm = tri.transform[simp]
    bary2 = np.einsum('nij,nj->ni', Tm[:, :2], flat - Tm[:, 2])
    bary = np.concatenate([bary2, 1 - bary2.sum(1, keepdims=True)], 1)
    dentro = (simp >= 0).reshape(T, T) & (NY < -0.05)
    # peso do rosto: some suavemente perto da borda dos pontos e nas partes que não olham para a frente
    dist = ndimage.distance_transform_edt(dentro)
    wface = np.clip(dist / (T * 0.025), 0, 1) * np.clip((-NY - 0.05) / 0.3, 0, 1)
    # textura original (sombras das orelhas, do pescoço etc.), amostrada pela UV antiga
    vals_old = np.concatenate([oldpx, np.zeros_like(oldpx[..., :1])], -1)
    O1, _ = raster(uvpx, vals_old, T, T); O2, _ = raster(uvpx - np.array([T, 0], np.float32), vals_old, T, T)
    O = np.where(M1[..., None], O1, O2)[idx[0], idx[1]]
    orig = sample(A, O[..., :2].reshape(-1, 2)).reshape(T, T, 3)
    lum = orig.mean(-1)
    lum_ref = np.median(lum[wface > 0.9]) if (wface > 0.9).any() else lum.mean()
    sombra = np.clip(lum / max(lum_ref, 1e-3), 0.55, 1.25)[..., None]
    # altura relativa (queixo->topo) e linha do cabelo, como no Blender
    HH, cx, cy = float(d['HH']), float(d['cx']), float(d['cy'])
    ang = np.arctan2(X - cx, -(P[..., 1] - cy))
    h = (Z - chin) / HH
    linha = 0.60 + 0.19 * np.cos(ang)
    couro = np.clip((h - (linha - 0.02)) / 0.05, 0, 1)
    zc = chin + (float(d['nape']) - chin) * np.clip((P[..., 1] - float(d['y_chin'])) / max(1e-3, float(d['y_back']) - float(d['y_chin'])), 0, 1)
    pescoco = 1 - np.clip((Z - zc) / 0.03, 0, 1)        # perto do corte do pescoço: cor do corpo

    tons = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'tons_visuais.json')))
    manifest = {}
    def blur(x, sg):
        return np.stack([ndimage.gaussian_filter(x[..., c], sg) for c in range(x.shape[-1])], -1) if x.ndim == 3 else ndimage.gaussian_filter(x, sg)

    for item in ids.split(','):
        fid, vis = item.split(':') if ':' in item else (item, None)
        if fid.startswith('M'):
            # rosto do próprio modelo (com a pele/barba do visual v do corpo), amostrado pela UV antiga
            v = int(fid[1:])
            base = os.path.join(os.path.dirname(albedo), 'Uniformes', 'A_Casa.jpg') if v == 0 else \
                   os.path.join(os.path.dirname(os.path.dirname(albedo)), 'Resources', 'Visuais', 'A_Casa_%d.jpg' % v)
            B = np.asarray(Image.open(base).convert('RGB')).astype(np.float32)
            Bs = B.shape[0] / A.shape[0]
            out = sample(B, O[..., :2].reshape(-1, 2) * Bs).reshape(T, T, 3)
            alvo = np.array(tons[str(v)]) * 255
            Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(os.path.join(saida, '%s_%s.jpg' % (prefixo, fid)), quality=90)
            manifest[fid] = {'visual': v, 'pele': [round(float(x) / 255, 3) for x in alvo], 'cabelo': [0.05, 0.04, 0.035]}
            print('rosto', fid, 'visual', v)
            continue
        f = os.path.join(fotos, 'r%s.jpg' % fid)
        im = Image.open(f).convert('RGB')
        Lp = landmarks(im)
        if Lp is None: print('sem rosto:', fid); continue
        F = np.asarray(im).astype(np.float32)
        # pele da foto (bochechas e testa); o tom do corpo vem da tabela (as fotos têm iluminação muito variada)
        pts = [50, 280, 101, 330, 9, 151]
        pele = np.median(np.concatenate([F[int(Lp[i, 1]) - 8:int(Lp[i, 1]) + 8, int(Lp[i, 0]) - 8:int(Lp[i, 0]) + 8].reshape(-1, 3) for i in pts]), 0)
        best = vis if vis is not None else min(tons, key=lambda k: np.sum((np.array(tons[k]) * 255 - pele) ** 2))
        alvo = np.array(tons[str(best)]) * 255
        ganho = np.clip(alvo / np.maximum(pele, 1), 0.45, 2.2)
        # cor do cabelo: bem acima da testa (ponto 10); se parecer pele, castanho-escuro
        up = Lp[10] - Lp[152]; hp = Lp[10] + up * 0.32
        y0, x0 = int(np.clip(hp[1], 12, F.shape[0] - 12)), int(np.clip(hp[0], 30, F.shape[1] - 30))
        cab = F[y0 - 10:y0 + 10, x0 - 30:x0 + 30].reshape(-1, 3)
        cab = cab[cab.mean(1) <= np.percentile(cab.mean(1), 35)].mean(0)
        if cab.mean() > 110 or (cab[0] - cab[2] > 45 and cab.mean() > 80): cab = np.array([42, 30, 24.])
        if str(best) in ('1', '5', '7'): cab = np.minimum(cab, 38) * np.array([1.0, 0.85, 0.75])   # pele escura: cabelo preto
        # rosto: afim por partes (triângulos dos pontos do modelo -> mesmos pontos na foto)
        V = Lp[tri.simplices[np.maximum(simp, 0)]]
        src_xy = np.einsum('ni,nij->nj', bary, V)
        face = sample(F, src_xy).reshape(T, T, 3) * ganho
        fundo = alvo * sombra[..., 0:1] * np.array([1, 1, 1])
        # tira a luz de um lado da foto (mantém os detalhes): leva a luminância de baixa frequência do rosto à do fundo
        wf = wface[..., None]
        lf = blur((face * wf).mean(-1), T * 0.035) / np.maximum(blur(wface, T * 0.035), 1e-3)
        lt = blur((fundo * wf).mean(-1), T * 0.035) / np.maximum(blur(wface, T * 0.035), 1e-3)
        face = face * np.clip((lt / np.maximum(lf, 1)) ** 0.75, 0.6, 1.6)[..., None]
        cabelo_px = cab * (0.75 + 0.25 * sombra)
        fundo = fundo * (1 - couro[..., None]) + cabelo_px * couro[..., None]
        out = face * wface[..., None] + fundo * (1 - wface[..., None])
        out = out * (1 - pescoco[..., None]) + (alvo * sombra) * pescoco[..., None]
        img = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))
        img.save(os.path.join(saida, '%s_%s.jpg' % (prefixo, fid)), quality=90)
        manifest[fid] = {'visual': int(best), 'pele': [round(float(x) / 255, 3) for x in alvo],
                         'cabelo': [round(float(x) / 255, 3) for x in cab]}
        print('rosto', fid, 'visual', best)
    # lista (JsonUtility da Unity não lê dicionários)
    json.dump({'rostos': [dict(id=k, **v) for k, v in manifest.items()]}, open(os.path.join(saida, prefixo + '.json'), 'w'), indent=1)
