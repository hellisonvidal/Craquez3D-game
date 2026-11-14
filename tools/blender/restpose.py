import bpy, os, mathutils
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=os.environ['INP'])
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
arm.data.pose_position='REST'
bpy.context.view_layer.update()
for b in arm.data.bones:
    h=arm.matrix_world@b.head_local; t=arm.matrix_world@b.tail_local
    print(f"BONE {b.name:28s} parent={b.parent.name if b.parent else '-':26s} head=({h.x:.3f},{h.y:.3f},{h.z:.3f}) tail=({t.x:.3f},{t.y:.3f},{t.z:.3f})")
