import bpy, sys, mathutils
import os; path = os.environ['INP']
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=path)
print("FILE", path)
for o in bpy.data.objects:
    info = f"  OBJ {o.name} type={o.type} parent={o.parent.name if o.parent else None} scale={tuple(round(s,3) for s in o.scale)} rot={tuple(round(r,3) for r in o.rotation_euler)}"
    if o.type=='MESH':
        me=o.data
        bb=[o.matrix_world @ mathutils.Vector(c) for c in o.bound_box]
        mn=[min(v[i] for v in bb) for i in range(3)]; mx=[max(v[i] for v in bb) for i in range(3)]
        info += f" verts={len(me.vertices)} tris~={sum(len(p.vertices)-2 for p in me.polygons)} vgroups={len(o.vertex_groups)} mats={[m.name for m in me.materials if m]} bbox_min={[round(x,3) for x in mn]} bbox_max={[round(x,3) for x in mx]} uvs={len(me.uv_layers)}"
    if o.type=='ARMATURE':
        bones=[b.name for b in o.data.bones]
        info += f" bones={len(bones)} first={bones[:70]}"
    print(info)
for a in bpy.data.actions:
    print("  ACTION", a.name, "frames", tuple(a.frame_range))
for img in bpy.data.images:
    print("  IMG", img.name, img.size[:], img.filepath)
