# Copia a orientação de repouso (inclusive o "roll") de cada osso do X Bot do Mixamo para o modelo,
# mantendo posições e malha. A Unity usa a orientação dos ossos para as torções (pernas/braços);
# ossos com roll diferente do Mixamo fazem o pé/joelho girar para o lado nas animações.
# Uso: INP=modelo.fbx OUT=saida.fbx REF="X Bot.fbx"
import bpy, os
from mathutils import Matrix
P='mixamorig:'
def load(path):
    before=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=path)
    new=[o for o in bpy.data.objects if o not in before]
    return [o for o in new if o.type=='ARMATURE'][0], [o for o in new if o.type=='MESH']
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
ref,refm=load(os.environ['REF'])
refrot={b.name:(ref.matrix_world.to_3x3()@b.matrix_local.to_3x3()).normalized() for b in ref.data.bones}
for o in [ref]+refm: bpy.data.objects.remove(o)
arm,meshes=load(os.environ['INP'])
if arm.animation_data: arm.animation_data.action=None
bpy.context.view_layer.objects.active=arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
Mw=arm.matrix_world.to_3x3(); Mwi=Mw.inverted()
n=0
for eb in arm.data.edit_bones:
    if eb.name not in refrot: continue
    R=(Mwi@refrot[eb.name]).normalized()
    L=eb.length
    old=eb.y_axis.copy()
    m=R.to_4x4(); m.translation=eb.head.copy()
    eb.matrix=m
    eb.length=L
    n+=1
    ang=old.angle(eb.y_axis)*57.3
    if ang>5: print("direção mudou", eb.name, round(ang,1))
bpy.ops.object.mode_set(mode='OBJECT')
print("ossos reorientados", n)
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True)
for b in meshes: b.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.environ['OUT'], use_selection=True, object_types={'ARMATURE','MESH'},
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP')
print("OK")
