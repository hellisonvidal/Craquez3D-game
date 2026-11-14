import bpy, os
from mathutils import Vector, Matrix
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=os.environ['INP'])
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; body=[o for o in bpy.data.objects if o.type=='MESH'][0]
arm.name='GK_Rig'; body.name='GK_Body'
if arm.animation_data: arm.animation_data.action=None
for pb in arm.pose.bones: pb.matrix_basis=Matrix()
bpy.context.view_layer.update()
# apply object transforms on both
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
# decimate body (keeps vertex groups)
bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); bpy.context.view_layer.objects.active=body
d=body.modifiers.new('dec','DECIMATE'); d.ratio=32000/len(body.data.polygons)
bpy.ops.object.modifier_move_to_index(modifier='dec', index=0)
bpy.ops.object.modifier_apply(modifier='dec')
print("tris", sum(len(p.vertices)-2 for p in body.data.polygons))
# T-pose
bpy.context.view_layer.objects.active=arm; bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True)
bpy.ops.object.mode_set(mode='POSE')
P='mixamorig:'
T={'Hips':(0,0,1),'Spine':(0,0,1),'Spine1':(0,0,1),'Spine2':(0,0.03,1),'Neck':(0,-0.05,1),'Head':(0,-0.1,1),
   'LeftShoulder':(1,0,0),'LeftArm':(1,0,0),'LeftForeArm':(1,0,0),'LeftHand':(1,0,0),
   'LeftUpLeg':(0.08,0,-1),'LeftLeg':(0.0,0.03,-1),'LeftFoot':(0,-0.8,-0.6),'LeftToeBase':(0,-1,0)}
for k in list(T):
    if k.startswith('Left'): x,y,z=T[k]; T['Right'+k[4:]]=(-x,y,z)
order=['LeftShoulder','LeftArm','LeftForeArm','LeftHand','RightShoulder','RightArm','RightForeArm','RightHand',
       'LeftUpLeg','LeftLeg','LeftFoot','LeftToeBase','RightUpLeg','RightLeg','RightFoot','RightToeBase']
bpy.context.view_layer.update()
hp=arm.pose.bones[P+'Hips']; nk=arm.pose.bones[P+'Neck']
v=(nk.head-hp.head).normalized(); q=v.rotation_difference(Vector((0,0,1))); h=hp.head.copy()
hp.matrix=Matrix.Translation(h)@q.to_matrix().to_4x4()@Matrix.Translation(-h)@hp.matrix
bpy.context.view_layer.update()
sp=arm.pose.bones[P+'Spine']
v=(nk.head-sp.head).normalized(); q=v.rotation_difference(Vector((0,0,1)))
h=sp.head.copy(); sp.matrix=Matrix.Translation(h)@q.to_matrix().to_4x4()@Matrix.Translation(-h)@sp.matrix
bpy.context.view_layer.update(); print('spine fixed', tuple(round(c,3) for c in (nk.head-sp.head).normalized()))
orig={n:(arm.pose.bones[P+n].tail-arm.pose.bones[P+n].head).normalized() for n in ['LeftFoot','LeftToeBase','RightFoot','RightToeBase']}
for k,v in orig.items(): T[k]=tuple(v)
for n in order:
    pb=arm.pose.bones.get(P+n)
    if pb is None: print("missing",n); continue
    bpy.context.view_layer.update()
    head=pb.head.copy(); cur=(pb.tail-pb.head).normalized(); tgt=Vector(T[n]).normalized()
    q=cur.rotation_difference(tgt)
    pb.matrix=Matrix.Translation(head)@q.to_matrix().to_4x4()@Matrix.Translation(-head)@pb.matrix
bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.objects.active=body; bpy.ops.object.select_all(action='DESELECT'); body.select_set(True)
mod=[m for m in body.modifiers if m.type=='ARMATURE'][0]; mn=mod.name
bpy.ops.object.modifier_copy(modifier=mn); bpy.ops.object.modifier_apply(modifier=mn)
for m in body.modifiers:
    if m.type=='ARMATURE': m.name='Armature'; m.object=arm
bpy.context.view_layer.objects.active=arm; bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True)
bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.armature_apply(selected=False); bpy.ops.object.mode_set(mode='OBJECT')
# normalize height 1.90, feet on 0, hips centered xy
bpy.context.view_layer.update()
zs=[(body.matrix_world@v.co).z for v in body.data.vertices]; zmin,zmax=min(zs),max(zs); s=1.90/(zmax-zmin)
hb=arm.matrix_world@arm.data.bones[P+'Hips'].head_local
arm.matrix_world=Matrix.Translation((-hb.x*s,-hb.y*s,-zmin*s))@Matrix.Scale(s,4)@arm.matrix_world
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
zs=[(body.matrix_world@v.co).z for v in body.data.vertices]; print("height",max(zs)-min(zs),"zmin",min(zs))
for p in body.data.polygons: p.use_smooth=True
body.data.materials.clear(); body.data.materials.append(bpy.data.materials.new('GK_Mat'))
for b in arm.data.bones:
    if b.name in (P+'Hips',P+'Head',P+'LeftHand',P+'LeftFoot'): print("B",b.name,tuple(round(c,3) for c in b.head_local))
bpy.ops.wm.save_as_mainfile(filepath='/home/user/work/gk2_final.blend')
bpy.ops.export_scene.fbx(filepath='/home/user/work/out/Goleiro.fbx', use_selection=True, object_types={'ARMATURE','MESH'},
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP')
