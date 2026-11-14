"""Gera o ícone do app a partir do último quadro do vídeo de abertura (logo Craquez).
Uso: python make_icon.py quadro.png pasta_saida
Saídas: Icone_App.png (ícone completo), Icone_Adaptativo_Fundo.png / Icone_Adaptativo_Frente.png (Android 8+)."""
import sys
from PIL import Image, ImageFilter, ImageEnhance, ImageDraw

src, out = sys.argv[1], sys.argv[2]
fr = Image.open(src).convert("RGB")           # 1280x720
S = 1024

def fundo():
    # noite de estádio: degradê azul com brilho central (sem repetir o logo)
    import math
    bg = Image.new("RGB", (S, S))
    px = bg.load()
    for y in range(S):
        for x in range(S):
            dx, dy = (x - S / 2) / S, (y - S * 0.45) / S
            t = max(0.0, 1.0 - math.sqrt(dx * dx + dy * dy) * 1.6)
            px[x, y] = (int(4 + 30 * t), int(10 + 60 * t), int(34 + 140 * t))
    return bg

# logo com o jogador (silhueta) e as manchas de tinta
LX0, LY0, LX1, LY1 = 170, 110, 1110, 610
logo = fr.crop((LX0, LY0, LX1, LY1))
lw, lh = logo.size
mask = Image.new("L", (lw, lh), 0)
ImageDraw.Draw(mask).ellipse((lw * 0.06, lh * 0.10, lw * 0.94, lh * 0.90), fill=255)
mask = mask.filter(ImageFilter.GaussianBlur(lh * 0.07))

def compor(base, largura):
    h = int(lh * largura / lw)
    l = logo.resize((largura, h), Image.LANCZOS)
    m = mask.resize((largura, h), Image.LANCZOS)
    img = base.copy()
    img.paste(l, ((S - largura) // 2, (S - h) // 2 + 10), m)
    return img

compor(fundo(), 980).save(f"{out}/Icone_App.png")
compor(fundo(), 640).save(f"{out}/Icone_Adaptativo_Fundo.png")
Image.new("RGBA", (S, S), (0, 0, 0, 0)).save(f"{out}/Icone_Adaptativo_Frente.png")

# prévia: como fica no celular (quadrado arredondado e círculo do adaptativo)
pv = Image.new("RGB", (S * 2 + 60, S + 40), (235, 235, 235))
a = Image.open(f"{out}/Icone_App.png"); ma = Image.new("L", (S, S), 0)
ImageDraw.Draw(ma).rounded_rectangle((0, 0, S, S), radius=180, fill=255); pv.paste(a, (20, 20), ma)
b = Image.open(f"{out}/Icone_Adaptativo_Fundo.png"); mb = Image.new("L", (S, S), 0)
r = S * 72 / 108 / 2
ImageDraw.Draw(mb).ellipse((S/2 - r, S/2 - r, S/2 + r, S/2 + r), fill=255)
pv.paste(b, (S + 40, 20), mb)
pv.save(sys.argv[3] if len(sys.argv) > 3 else f"{out}/preview.png")
