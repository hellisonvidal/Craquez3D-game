# Gera texturas procedurais do Craquez 3D (grama, chuva, respingo, poça, rede, nuvens).
import numpy as np, sys
from PIL import Image
OUT=sys.argv[1]
rng=np.random.default_rng(7)
def fbm(n, octaves=5, base=4, seed=0):
    r=np.random.default_rng(seed); acc=np.zeros((n,n)); amp=1; tot=0
    for o in range(octaves):
        f=base*2**o; g=r.random((f+1,f+1)); g[-1,:]=g[0,:]; g[:,-1]=g[:,0]
        x=np.linspace(0,f,n,endpoint=False); i=x.astype(int); t=x-i; t=t*t*(3-2*t)
        a=g[i][:,i]; b=g[i][:,i+1]; c=g[i+1][:,i]; d=g[i+1][:,i+1]
        tx=t[None,:]; ty=t[:,None]
        acc+=amp*((a*(1-tx)+b*tx)*(1-ty)+(c*(1-tx)+d*tx)*ty); tot+=amp; amp*=0.5
    return acc/tot
def save(a,name): Image.fromarray(np.clip(a,0,255).astype(np.uint8)).save(OUT+'/'+name)
def normal_from_height(h,strength):
    gx=(np.roll(h,-1,1)-np.roll(h,1,1))*strength; gy=(np.roll(h,-1,0)-np.roll(h,1,0))*strength
    n=np.dstack([-gx,-gy,np.ones_like(h)]); n/=np.linalg.norm(n,axis=2,keepdims=True)
    return (n*0.5+0.5)*255
N=1024
# grama: blades (ruido de alta frequencia alongado) + manchas
blades=rng.random((N,N)); blades=(blades+np.roll(blades,1,0)+np.roll(blades,2,0)+np.roll(blades,3,0))/4
patch=fbm(N,5,4,1); fine=fbm(N,4,64,2)
v=0.55*blades+0.25*fine+0.20*patch
col=np.dstack([40+45*v, 78+70*v, 22+28*v])
save(col,'Grama_Albedo.png')
save(normal_from_height(blades*0.6+fine*0.4,3.0),'Grama_Normal.png')
# ondulacao da poça (normal tileavel)
h=np.zeros((512,512)); yy,xx=np.mgrid[0:512,0:512]
for i in range(40):
    cx,cy=rng.random(2)*512; r0=rng.random()*60+10
    for ox in (-512,0,512):
        for oy in (-512,0,512):
            d=np.hypot(xx-cx-ox,yy-cy-oy); h+=np.sin(d*0.35)*np.exp(-((d-r0)/14)**2)
h+=fbm(512,4,8,3)*2
save(normal_from_height(h,0.6),'Poca_Normal.png')
# gota de chuva (streak) RGBA
W,H=32,256; a=np.zeros((H,W)); x=np.abs(np.linspace(-1,1,W))[None,:]; y=np.linspace(0,1,H)[:,None]
a=np.exp(-(x/0.18)**2)*np.sin(np.pi*y)**0.6
save(np.dstack([np.full((H,W),220),np.full((H,W),230),np.full((H,W),255),a*255]),'Chuva_Gota.png')
# respingo: anel + pontos
S=128; yy,xx=np.mgrid[0:S,0:S]; d=np.hypot(xx-S/2,yy-S/2)/(S/2)
a=np.exp(-((d-0.55)/0.12)**2)*0.9
for i in range(14):
    ang=rng.random()*6.283; rr=0.6+rng.random()*0.3; px=S/2+np.cos(ang)*rr*S/2; py=S/2+np.sin(ang)*rr*S/2
    a+=np.exp(-((xx-px)**2+(yy-py)**2)/(2*2.2**2))
a=np.clip(a,0,1)*(d<1)
save(np.dstack([np.full((S,S),235),np.full((S,S),240),np.full((S,S),255),a*255]),'Respingo.png')
# rede: grade com alpha (cutout)
S=256; yy,xx=np.mgrid[0:S,0:S]; cell=32; t=3
m=((xx%cell)<t)|((yy%cell)<t)
save(np.dstack([np.full((S,S),245)]*3+[m*255]),'Rede.png')
# nuvens: alpha fbm
S=1024; c=fbm(S,6,3,9); c=np.clip((c-0.35)*2.2,0,1)
save(np.dstack([np.full((S,S),255)]*3+[c*255]),'Nuvens.png')
# vinheta de gota na lente / gradiente de UI
S=64; yy,xx=np.mgrid[0:S,0:S]; d=np.hypot(xx-S/2+0.5,yy-S/2+0.5)/(S/2)
save(np.dstack([np.full((S,S),255)]*3+[np.clip(1-d,0,1)**0.5*255]),'UI_Circulo.png')
print('ok')
# anel de pingo de chuva (marca na poça)
S=128; yy,xx=np.mgrid[0:S,0:S]; d=np.hypot(xx-S/2+0.5,yy-S/2+0.5)/(S/2)
a=np.exp(-((d-0.78)/0.07)**2)*0.9+np.exp(-((d-0.5)/0.06)**2)*0.35
a=np.clip(a,0,1)*(d<1)
save(np.dstack([np.full((S,S),235),np.full((S,S),242),np.full((S,S),255),a*255]),'Anel.png')
print('anel ok')
