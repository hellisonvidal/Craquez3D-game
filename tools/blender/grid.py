import sys
from PIL import Image, ImageDraw
# args: img ortho_scale cz  ; front: horizontal=x (left=-S/2), side: horizontal= y? 
p,S,cz,mode=sys.argv[1],float(sys.argv[2]),float(sys.argv[3]),sys.argv[4]
im=Image.open(p).convert('RGB'); W,H=im.size; d=ImageDraw.Draw(im)
def px(u): return (u/S+0.5)*W
def pz(z): return (0.5-(z-cz)/S)*H
k=0
v=-S/2
import math
for i in range(-20,21):
    u=i*0.1
    x=px(u); col=(255,0,0) if i%5==0 else (255,150,150)
    if 0<=x<W: d.line([(x,0),(x,H)],fill=col,width=1); d.text((x+2,2),f"{u:+.1f}",fill=(255,255,0))
for i in range(0,25):
    z=i*0.1; y=pz(z); col=(0,0,255) if i%5==0 else (150,150,255)
    if 0<=y<H: d.line([(0,y),(W,y)],fill=col,width=1); d.text((2,y+2),f"{z:.1f}",fill=(255,255,0))
im.save(p.replace('.png','_g.png'))
