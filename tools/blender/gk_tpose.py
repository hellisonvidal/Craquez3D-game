import bpy, math
from mathutils import Vector, Matrix
bpy.ops.wm.open_mainfile(filepath='/home/user/work/gk_rigged_stance.blend')
arm=bpy.data.objects['GK_Rig']; body=bpy.data.objects['GK_Body']
bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='POSE')
P='mixamorig:'
T={'Hips':(0,0,1),'Spine':(0,0,1),'Spine1':(0,0,1),'Spine2':(0,0.05,1),'Neck':(0,-0.05,1),'Head':(0,-0.05,1),
   'LeftShoulder':(1,0,0),'LeftArm':(1,0,0),'LeftForeArm':(1,0,0),'LeftHand':(1,0,0),
   'LeftUpLeg':(0.1,0,-1),'LeftLeg':(0.02,0.03,-1),'LeftFoot':(0,-0.8,-0.6),'LeftToeBase':(0,-1,0)}
for k in list(T):
    if k.startswith('Left'): x,y,z=T[k]; T['Right'+k[4:]]=(-x,y,z)
order=['Hips','Spine','Spine1','Spine2','Neck','Head','LeftShoulder','LeftArm','LeftForeArm','LeftHand','RightShoulder','RightArm','RightForeArm','RightHand',
       'LeftUpLeg','LeftLeg','LeftFoot','LeftToeBase','RightUpLeg','RightLeg','RightFoot','RightToeBase']
for n in order:
    pb=arm.pose.bones[P+n]
    bpy.context.view_layer.update()
    head=pb.head.copy(); cur=(pb.tail-pb.head).normalized(); tgt=Vector(T[n]).normalized()
    q=cur.rotation_difference(tgt)
    M=Matrix.Translation(head)@q.to_matrix().to_4x4()@Matrix.Translation(-head)
    pb.matrix=M@pb.matrix
    bpy.context.view_layer.update()
    print(n, "dir now", tuple(round(c,3) for c in (pb.tail-pb.head).normalized()))
bpy.ops.object.mode_set(mode='OBJECT')
# apply armature deformation to mesh, then pose as rest
bpy.context.view_layer.objects.active=body; bpy.ops.object.select_all(action='DESELECT'); body.select_set(True)
mod=[m for m in body.modifiers if m.type=='ARMATURE'][0]; modname=mod.name
bpy.ops.object.modifier_copy(modifier=modname)
bpy.ops.object.modifier_apply(modifier=modname)
bpy.context.view_layer.objects.active=arm; bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True)
bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.armature_apply(selected=False); bpy.ops.object.mode_set(mode='OBJECT')
# rename copied modifier
for m in body.modifiers:
    if m.type=='ARMATURE': m.name='Armature'; m.object=arm
# put feet on ground
zmin=min((body.matrix_world@v.co).z for v in body.data.vertices)
print("zmin after tpose", zmin)
bpy.ops.wm.save_as_mainfile(filepath='/home/user/work/gk_tpose.blend')
