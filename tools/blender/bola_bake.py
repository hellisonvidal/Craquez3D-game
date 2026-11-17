# Bola: esfera limpa (ico 5) com a textura do modelo do Meshy transferida por bake.
# Uso: python -c "import sys; sys.argv=[..., modelo.fbx, textura.png, saida]; exec(open(f).read())"
import sys, bpy, bmesh, math, numpy as np
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=sys.argv[-3])
src=[o for o in bpy.data.objects if o.type=='MESH'][0]
for o in list(bpy.data.objects):
    if o!=src: bpy.data.objects.remove(o)
src.parent=None; bpy.context.view_layer.objects.active=src; src.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
co=np.array([v.co[:] for v in src.data.vertices]); c=(co.min(0)+co.max(0))/2
print('src bbox', (co.max(0)-co.min(0)).round(3))
for v in src.data.vertices: v.co=(v.co-Vector(c)).normalized()*0.11
m=bpy.data.materials.new('s'); m.use_nodes=True; im=m.node_tree.nodes.new('ShaderNodeTexImage'); im.image=bpy.data.images.load(sys.argv[-2])
m.node_tree.links.new(im.outputs['Color'], m.node_tree.nodes['Principled BSDF'].inputs['Base Color']); src.data.materials.clear(); src.data.materials.append(m)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=5, radius=0.11); t=bpy.context.object; t.name='Bola'
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.smart_project(angle_limit=math.radians(40), island_margin=0.004); bpy.ops.object.mode_set(mode='OBJECT')
for p in t.data.polygons: p.use_smooth=True
mt=bpy.data.materials.new('Bola_Mat'); mt.use_nodes=True; n=mt.node_tree.nodes.new('ShaderNodeTexImage')
img=bpy.data.images.new('b',2048,2048); n.image=img; mt.node_tree.nodes.active=n; t.data.materials.append(mt)
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=1
bpy.ops.object.select_all(action='DESELECT'); src.select_set(True); t.select_set(True); bpy.context.view_layer.objects.active=t
bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, use_selected_to_active=True, cage_extrusion=0.01, max_ray_distance=0.02, margin=8)
img.filepath_raw=sys.argv[-1]+'_Albedo.png'; img.file_format='PNG'; img.save()
print('tris', len(t.data.polygons))
bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active=t
t.data.materials.clear(); t.data.materials.append(bpy.data.materials.new('Bola_Mat'))
bpy.ops.export_scene.fbx(filepath=sys.argv[-1]+'.fbx', use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP')
