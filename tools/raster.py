"""Rasterização de triângulos e amostragem bilinear (numpy), usadas pelos geradores de rostos e uniformes."""
import numpy as np


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


