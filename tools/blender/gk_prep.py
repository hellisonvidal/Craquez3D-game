import bpy, os, mathutils, math
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=os.environ['INP'])
me=[o for o in bpy.data.objects if o.type=='MESH'][0]
me.name='GK_Body'
bpy.context.view_layer.objects.active=me; me.select_set(True)
# merge doubles + decimate
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.remove_doubles(threshold=0.0001); bpy.ops.object.mode_set(mode='OBJECT')
n0=len(me.data.polygons)
mod=me.modifiers.new('dec','DECIMATE'); mod.ratio=34000/n0
bpy.ops.object.modifier_apply(modifier='dec')
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
vs=[v.co for v in me.data.vertices]
mn=mathutils.Vector([min(v[i] for v in vs) for i in range(3)]); mx=mathutils.Vector([max(v[i] for v in vs) for i in range(3)])
H=1.88; s=H/(mx.z-mn.z)
for v in me.data.vertices:
    v.co=mathutils.Vector(((v.co.x-(mn.x+mx.x)/2)*s,(v.co.y-(mn.y+mx.y)/2)*s,(v.co.z-mn.z)*s))
me.data.update()
print("tris", sum(len(p.vertices)-2 for p in me.data.polygons), "verts", len(me.data.vertices))
# components count
import bmesh
bm=bmesh.new(); bm.from_mesh(me.data)
seen=set(); comps=0
for v in bm.verts:
    if v.index in seen: continue
    comps+=1; stack=[v]; seen.add(v.index)
    while stack:
        x=stack.pop()
        for e in x.link_edges:
            o=e.other_vert(x)
            if o.index not in seen: seen.add(o.index); stack.append(o)
print("components", comps, "nonmanifold edges", sum(1 for e in bm.edges if not e.is_manifold))
bm.free()
bpy.ops.wm.save_as_mainfile(filepath='/home/user/work/gk_prep.blend')
