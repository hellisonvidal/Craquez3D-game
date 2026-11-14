import sys, glob
from PIL import Image
Image.MAX_IMAGE_PIXELS=None
def conv(prefix, name, base_sz, nrm_sz, ms_sz):
    b=Image.open(prefix+'.png').convert('RGB').resize((base_sz,base_sz),Image.LANCZOS); b.save(f'/home/user/work/out/tex/{name}_Albedo.png',optimize=True)
    n=Image.open(prefix+'_normal.png').convert('RGB').resize((nrm_sz,nrm_sz),Image.LANCZOS); n.save(f'/home/user/work/out/tex/{name}_Normal.png',optimize=True)
    m=Image.open(prefix+'_metallic.png').convert('L').resize((ms_sz,ms_sz),Image.LANCZOS)
    r=Image.open(prefix+'_roughness.png').convert('L').resize((ms_sz,ms_sz),Image.LANCZOS)
    sm=r.point(lambda v:255-v)
    Image.merge('RGBA',(m,m,m,sm)).save(f'/home/user/work/out/tex/{name}_MetallicSmoothness.png',optimize=True)
    print(name, 'mean albedo', b.resize((1,1)).getpixel((0,0)), 'mean normal', n.resize((1,1)).getpixel((0,0)), 'metal', m.resize((1,1)).getpixel((0,0)), 'rough', r.resize((1,1)).getpixel((0,0)))
X='/home/user/drafts/x/'
conv(glob.glob(X+'SoccerATA/*/*_All_animation')[0] if False else glob.glob(X+'SoccerATA/*/*.fbx')[0][:-4],'Jogador',2048,2048,1024)
conv(glob.glob(X+'GK2/*/*_All_animation.fbx')[0][:-4],'Goleiro',2048,2048,1024)
conv(glob.glob(X+'ball/*/*.fbx')[0][:-4],'Bola',2048,1024,512)
conv(glob.glob(X+'arena/*/*.fbx')[0][:-4],'Estadio',4096,2048,1024)
