# Uso: argv = [..., nome (nome.blend de corpo_prep), saida, material]
# Estima articulações (A-pose, frente = -Y), cria esqueleto Mixamo, pesos automáticos, T-pose, exporta.
import bpy, sys, numpy as np
from mathutils import Vector, Matrix
src, out, matname = sys.argv[-3], sys.argv[-2], sys.argv[-1]
bpy.ops.wm.open_mainfile(filepath=src+'.blend')
body=bpy.data.objects['Body']
co=np.array([v.co[:] for v in body.data.vertices]); H=co[:,2].max(); k=H/1.8
def sl(z,dz=0.015,xmin=-9,xmax=9):
    m=(np.abs(co[:,2]-z)<dz)&(co[:,0]>xmin)&(co[:,0]<xmax); return co[m]
# pescoço: primeira altura (de cima p/ baixo) onde a largura passa de 0.13*k
neckZ=None
for z in np.arange(H*0.95,H*0.7,-0.005):
    s=sl(z,0.006)
    if len(s) and (s[:,0].max()-s[:,0].min())>0.30*k: neckZ=z+0.02*k; break
# virilha: subindo do chão, última altura com vão no centro
crotch=None
for z in np.arange(0.4*H,0.62*H,0.005):
    s=sl(z,0.006); s=s[np.abs(s[:,0])<0.06*k]
    if len(s)>0: crotch=z; break
print("neck",neckZ,"crotch",crotch)
J={}
tor=sl(crotch+0.08*k,0.02,-0.2*k,0.2*k)
J['Hips']=Vector((0,tor[:,1].mean(),crotch+0.10*k))
nk=sl(neckZ,0.02,-0.1*k,0.1*k); J['Neck']=Vector((0,nk[:,1].mean(),neckZ))
hd=sl(neckZ+0.08*k,0.02); J['Head']=Vector((0,hd[:,1].mean(),neckZ+0.08*k))
J['HeadTop_End']=Vector((0,hd[:,1].mean(),H))
for i,nm in enumerate(['Spine','Spine1','Spine2']):
    t=(i+1)/4; z=J['Hips'].z+(J['Neck'].z-J['Hips'].z)*t
    s=sl(z,0.02,-0.15*k,0.15*k); J[nm]=Vector((0,s[:,1].mean(),z))
for side,sg in (('Left',1),('Right',-1)):
    # braço: pontos fora do tronco
    arm=co[(sg*co[:,0]>0.26*k)&(co[:,2]>0.38*H)&(co[:,2]<neckZ)]
    top=sl(neckZ-0.06*k,0.02); outer=(sg*top[:,0]).max()
    sh=Vector((sg*(outer-0.07*k),0,neckZ-0.09*k))
    s=co[(np.linalg.norm(co-np.array(sh),axis=1)<0.07*k)]; sh.y=s[:,1].mean()
    tip=arm[np.argmax(np.linalg.norm(arm-np.array(sh),axis=1))]
    T=Vector(tip); L=(T-sh).length; d=(T-sh).normalized()
    def ref(p,r=0.06*k):
        # centróide da fatia perpendicular ao braço
        rel=co-np.array(p); along=rel@np.array(d); perp=np.linalg.norm(rel-np.outer(along,np.array(d)),axis=1)
        m=(np.abs(along)<0.012*k)&(perp<r); 
        return Vector(co[m].mean(0)) if m.sum()>5 else p
    J[side+'Arm']=sh
    J[side+'ForeArm']=ref(sh+d*L*0.43)
    J[side+'Hand']=ref(sh+d*L*0.76,0.05*k)
    J[side+'HandEnd']=sh+d*L*0.97
    J[side+'Shoulder']=Vector((sg*0.05*k,sh.y+0.01*k,sh.z+0.01*k))
    # perna
    def leg(z):
        s=sl(z,0.015); s=s[(sg*s[:,0]>0.0)&(sg*s[:,0]<0.24*k)]
        return Vector(s.mean(0)) if len(s) else Vector((sg*0.1,0,z))
    up=leg(crotch-0.04*k); J[side+'UpLeg']=Vector((up.x,up.y,crotch+0.05*k))
    an=leg(0.10*k); an.z=0.085*k
    kn=leg(J[side+'UpLeg'].z*0.5+an.z*0.5-0.02*k); J[side+'Leg']=kn
    foot=co[(sg*co[:,0]>0)&(co[:,2]<0.06*k)]
    J[side+'Foot']=an
    toeY=foot[:,1].min()
    J[side+'ToeBase']=Vector((an.x+sg*0.01*k, 0.0, 0.0265*H)); J[side+'ToeBase'].y=an.y+(toeY-an.y)*0.62
    J[side+'Toe_End']=Vector((an.x+sg*0.015*k,toeY,0.0265*H))
import json; json.dump({a:list(b) for a,b in J.items()},open(out+'_J.json','w'))
for kk,v in J.items(): print(f"J {kk:14s} {v.x:6.3f} {v.y:6.3f} {v.z:6.3f}")
P='mixamorig:'
chain=[('Hips',None,'Spine'),('Spine','Hips','Spine1'),('Spine1','Spine','Spine2'),('Spine2','Spine1','Neck'),('Neck','Spine2','Head'),('Head','Neck','HeadTop_End'),('HeadTop_End','Head',None)]
for s in ['Left','Right']:
    chain+=[(s+'Shoulder','Spine2',s+'Arm'),(s+'Arm',s+'Shoulder',s+'ForeArm'),(s+'ForeArm',s+'Arm',s+'Hand'),(s+'Hand',s+'ForeArm',s+'HandEnd'),
            (s+'UpLeg','Hips',s+'Leg'),(s+'Leg',s+'UpLeg',s+'Foot'),(s+'Foot',s+'Leg',s+'ToeBase'),(s+'ToeBase',s+'Foot',s+'Toe_End'),(s+'Toe_End',s+'ToeBase',None)]
ad=bpy.data.armatures.new('Rig'); arm=bpy.data.objects.new('Rig',ad); bpy.context.scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); eb={}
for name,parent,tj in chain:
    b=ad.edit_bones.new(P+name); b.head=J[name]
    if tj: b.tail=J[tj]
    else: b.tail=J[name]+(J[name]-J[parent]).normalized()*0.07*k
    if name=='Hips': b.tail=J['Hips']+Vector((0,0,0.1*k))
    eb[name]=b
for name,parent,tj in chain:
    if parent:
        eb[name].parent=eb[parent]
        eb[name].use_connect=(eb[parent].tail-eb[name].head).length<1e-4 and name not in ('Spine',)
bpy.ops.armature.select_all(action='SELECT'); bpy.ops.armature.calculate_roll(type='GLOBAL_POS_Y')
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
zero=sum(1 for v in body.data.vertices if not any(g.weight>0.001 for g in v.groups))
print("unweighted", zero, "of", len(body.data.vertices))
bpy.ops.wm.save_as_mainfile(filepath=out+'_rig.blend')
