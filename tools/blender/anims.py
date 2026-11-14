import bpy, os, glob, mathutils
files=sorted(glob.glob(os.environ['GLOB']))
for f in files:
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)
    for a in list(bpy.data.actions): bpy.data.actions.remove(a)
    try: bpy.ops.import_scene.fbx(filepath=f)
    except Exception as e: print("FAIL",os.path.basename(f),e); continue
    arm=[o for o in bpy.data.objects if o.type=='ARMATURE']
    if not arm: print("NOARM",f); continue
    arm=arm[0]; act=arm.animation_data.action if arm.animation_data else None
    if not act: print(os.path.basename(f),"no action", len(arm.data.bones)); continue
    s,e=map(int,act.frame_range); sc=bpy.context.scene
    hips=arm.pose.bones.get('mixamorig:Hips'); head=arm.pose.bones.get('mixamorig:Head')
    def wp(pb): return arm.matrix_world @ pb.head
    rows=[]
    for fr in range(s,e+1,max(1,(e-s)//6)):
        sc.frame_set(fr); bpy.context.view_layer.update()
        h=wp(hips); hd=wp(head); rows.append(f"{fr}:hip({h.x:.2f},{h.y:.2f},{h.z:.2f}) head_z={hd.z:.2f}")
    print(f"{os.path.basename(f)} bones={len(arm.data.bones)} frames={s}-{e} fps={sc.render.fps} | "+" ; ".join(rows))
