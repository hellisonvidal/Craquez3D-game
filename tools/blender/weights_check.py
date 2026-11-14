import bpy, collections, os
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=os.environ['INP'])
body=[o for o in bpy.data.objects if o.type=='MESH'][0]
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
tot=collections.Counter()
for v in body.data.vertices:
    for g in v.groups: tot[body.vertex_groups[g.group].name]+=g.weight
nonm={k:round(x,1) for k,x in tot.items() if not k.startswith('mixamorig:')}
print("NAOMIXAMO", nonm)
for b in arm.data.bones:
    if not b.name.startswith('mixamorig:'): print("BONE", b.name, "parent", b.parent.name if b.parent else None)
