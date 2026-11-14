import bpy, mathutils, math, os
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath='/home/user/work/gk_prep.blend')
body=bpy.data.objects['GK_Body']
V=[body.matrix_world@v.co for v in body.data.vertices]
J={ 'Hips':(0,0.10,0.93),'Spine':(0,0.08,1.05),'Spine1':(0,0.05,1.18),'Spine2':(0,0.0,1.31),'Neck':(0,-0.07,1.52),
    'Head':(0,-0.14,1.62),'HeadTop_End':(0,-0.16,1.88),
    'LeftShoulder':(0.05,-0.01,1.45),'LeftArm':(0.24,0.0,1.40),'LeftForeArm':(0.37,0.06,1.10),'LeftHand':(0.40,-0.12,0.97),'LeftHandEnd':(0.45,-0.22,0.82),
    'LeftUpLeg':(0.11,0.08,0.87),'LeftLeg':(0.30,-0.04,0.57),'LeftFoot':(0.39,0.10,0.11),'LeftToeBase':(0.42,-0.04,0.03),'LeftToe_End':(0.44,-0.13,0.03)}
for k in list(J):
    if k.startswith('Left'):
        x,y,z=J[k]; J['Right'+k[4:]]=(-x,y,z)
J={k:Vector(v) for k,v in J.items()}
def slab_centroid(p, r, axes, dz=0.025):
    pts=[v for v in V if abs(v.z-p.z)<dz and (Vector((v.x-p.x,v.y-p.y)).length<r)]
    if len(pts)<8: return p, len(pts)
    c=sum(pts,Vector())/len(pts)
    q=p.copy()
    if 'x' in axes: q.x=c.x
    if 'y' in axes: q.y=c.y
    return q,len(pts)
refine={'Hips':('y',0.25),'Spine':('y',0.25),'Spine1':('y',0.25),'Spine2':('y',0.25),'Neck':('xy',0.09),'Head':('xy',0.12),
        'LeftUpLeg':('y',0.10),'RightUpLeg':('y',0.10),'LeftLeg':('xy',0.11),'RightLeg':('xy',0.11),'LeftFoot':('xy',0.09),'RightFoot':('xy',0.09),
        'LeftForeArm':('xy',0.08),'RightForeArm':('xy',0.08),'LeftHand':('xy',0.08),'RightHand':('xy',0.08),'LeftArm':('y',0.09),'RightArm':('y',0.09)}
for k,(ax,r) in refine.items():
    q,n=slab_centroid(J[k],r,ax); print(f"REF {k:14s} {tuple(round(c,3) for c in J[k])} -> {tuple(round(c,3) for c in q)} n={n}"); J[k]=q
J['Neck']=Vector((0,-0.07,1.52)); J['Head']=Vector((0,-0.12,1.63)); J['LeftUpLeg']=Vector((0.11,0.07,0.87)); J['RightUpLeg']=Vector((-0.11,0.07,0.87))
# symmetrize x for spine
for k in ['Hips','Spine','Spine1','Spine2','Neck','Head','HeadTop_End']: J[k].x=0.0
P='mixamorig:'
chain=[('Hips',None,'Spine'),('Spine','Hips','Spine1'),('Spine1','Spine','Spine2'),('Spine2','Spine1','Neck'),('Neck','Spine2','Head'),('Head','Neck','HeadTop_End'),('HeadTop_End','Head',None)]
for s in ['Left','Right']:
    chain+= [(s+'Shoulder','Spine2',s+'Arm'),(s+'Arm',s+'Shoulder',s+'ForeArm'),(s+'ForeArm',s+'Arm',s+'Hand'),(s+'Hand',s+'ForeArm',s+'HandEnd'),
             (s+'UpLeg','Hips',s+'Leg'),(s+'Leg',s+'UpLeg',s+'Foot'),(s+'Foot',s+'Leg',s+'ToeBase'),(s+'ToeBase',s+'Foot',s+'Toe_End'),(s+'Toe_End',s+'ToeBase',None)]
ad=bpy.data.armatures.new('GK_Armature'); arm=bpy.data.objects.new('GK_Rig',ad); bpy.context.scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
eb={}
for name,parent,tailj in chain:
    b=ad.edit_bones.new(P+name); b.head=J[name]
    if tailj: b.tail=J[tailj]
    else:
        pd=(J[name]-J[parent]).normalized(); b.tail=J[name]+pd*0.07
    if name=='Hips': b.tail=J['Hips']+Vector((0,0,0.11))
    eb[name]=b
for name,parent,tailj in chain:
    if parent: 
        eb[name].parent=eb[parent]
        eb[name].use_connect = (name not in ('Spine','LeftShoulder','RightShoulder','LeftUpLeg','RightUpLeg','Neck')) and (eb[parent].tail-eb[name].head).length<1e-4
bpy.ops.armature.select_all(action='SELECT'); bpy.ops.armature.calculate_roll(type='GLOBAL_POS_Y')
bpy.ops.object.mode_set(mode='OBJECT')
# skin
bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
empty=[vg.name for vg in body.vertex_groups if not any(vg.index in [g.group for g in v.groups] for v in body.data.vertices[:])]
cnt={vg.name:0 for vg in body.vertex_groups}
zero=0
for v in body.data.vertices:
    w=[g for g in v.groups if g.weight>0.001]
    if not w: zero+=1
    for g in w: cnt[body.vertex_groups[g.group].name]+=1
print("VG counts", {k.replace(P,''):c for k,c in cnt.items()}); print("unweighted verts", zero)
bpy.ops.wm.save_as_mainfile(filepath='/home/user/work/gk_rigged_stance.blend')
