# Corpo novo (Meshy, A-pose, sem rig): importa, reduz polígonos e normaliza a altura.
# Uso: python -c "import sys; sys.argv=['x', modelo.fbx, saida, altura, triangulos]; exec(open(f).read())"
import bpy, sys, numpy as np
from mathutils import Vector
fbx, out, H, ntri = sys.argv[-4], sys.argv[-3], float(sys.argv[-2]), int(sys.argv[-1])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
o=[x for x in bpy.data.objects if x.type=='MESH'][0]
for x in list(bpy.data.objects):
    if x!=o: bpy.data.objects.remove(x)
o.parent=None
bpy.context.view_layer.objects.active=o; o.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.remove_doubles(threshold=0.0001); bpy.ops.object.mode_set(mode='OBJECT')
n0=sum(len(p.vertices)-2 for p in o.data.polygons)
d=o.modifiers.new('dec','DECIMATE'); d.ratio=ntri/n0; bpy.ops.object.modifier_apply(modifier='dec')
co=np.array([v.co[:] for v in o.data.vertices]); mn,mx=co.min(0),co.max(0)
s=H/(mx[2]-mn[2]); c=(mn+mx)/2
for v in o.data.vertices: v.co=Vector(((v.co.x-c[0])*s,(v.co.y-c[1])*s,(v.co.z-mn[2])*s))
o.data.update(); o.name='Body'
co=np.array([v.co[:] for v in o.data.vertices])
print("TRIS",sum(len(p.vertices)-2 for p in o.data.polygons),"BBOX",co.min(0).round(3),co.max(0).round(3))
np.save(out+'.npy',co)
bpy.ops.wm.save_as_mainfile(filepath=out+'.blend')
