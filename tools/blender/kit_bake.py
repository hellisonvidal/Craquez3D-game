# Transfere (bake) a cor do modelo de uniforme para a UV do corpo do jogador.
# Uso: argv = [..., corpo (blend de corpo_prep), uniforme (blend de corpo_prep), textura_uniforme.png, saida.png, tamanho, distancia]
import bpy, sys, numpy as np
tgt, kitblend, kittex, out, size, ext = sys.argv[-6], sys.argv[-5], sys.argv[-4], sys.argv[-3], int(sys.argv[-2]), float(sys.argv[-1])
bpy.ops.wm.open_mainfile(filepath=tgt+'.blend')
body=bpy.data.objects['Body']
with bpy.data.libraries.load(kitblend+'.blend') as (src, dst): dst.objects=['Body']
kit=dst.objects[0]; kit.name='Kit'; bpy.context.scene.collection.objects.link(kit)
def texmat(name, path):
    m=bpy.data.materials.new(name); m.use_nodes=True; nt=m.node_tree
    b=nt.nodes['Principled BSDF']; im=nt.nodes.new('ShaderNodeTexImage')
    if path: im.image=bpy.data.images.load(path); nt.links.new(im.outputs['Color'], b.inputs['Base Color'])
    return m, nt, im
mk,_,_=texmat('kit', kittex); kit.data.materials.clear(); kit.data.materials.append(mk)
mb,nt,imn=texmat('body', None); body.data.materials.clear(); body.data.materials.append(mb)
img=bpy.data.images.new('bake', size, size, alpha=False, float_buffer=False)
img.generated_color=(1,0,1,1); imn.image=img; nt.nodes.active=imn
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=1
bpy.ops.object.select_all(action='DESELECT'); kit.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active=body
bk=sc.render.bake; bk.use_selected_to_active=True; bk.cage_extrusion=ext; bk.max_ray_distance=ext*2; bk.margin=8
bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, use_selected_to_active=True, cage_extrusion=ext, max_ray_distance=ext*2, margin=8)
img.filepath_raw=out; img.file_format='PNG'; img.save()
print("BAKED", out)
