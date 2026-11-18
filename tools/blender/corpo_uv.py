# Exporta o corpo ("Body") para o gerador de uniformes: triângulos com posição de repouso (m, personagem olhando
# para -Y), UV e o osso dominante de cada vértice.   python -I corpo_uv.py -- MODELO.fbx SAIDA.npz
import bpy, sys, numpy as np
f, out = sys.argv[sys.argv.index('--') + 1:][:2]
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=f, ignore_leaf_bones=False)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
for pb in arm.pose.bones: pb.matrix_basis.identity()
bpy.context.view_layer.update()
b = bpy.data.objects['Body']
me = b.data; me.calc_loop_triangles()
nomes = [g.name.replace('mixamorig:', '') for g in b.vertex_groups]
dom = np.zeros(len(me.vertices), np.int32)
for v in me.vertices:
    if v.groups: dom[v.index] = max(v.groups, key=lambda g: g.weight).group
uvl = me.uv_layers[0].data
P = np.array([(b.matrix_world @ v.co)[:] for v in me.vertices], np.float32)
tris = np.array([t.vertices[:] for t in me.loop_triangles]); loops = np.array([t.loops[:] for t in me.loop_triangles])
UV = np.array([uvl[l].uv[:] for l in range(len(uvl))], np.float32)
J = {pb.name.replace('mixamorig:', ''): (arm.matrix_world @ pb.head)[:] for pb in arm.pose.bones}
np.savez(out, pos=P[tris], uv=UV[loops], bone=dom[tris], nomes=np.array(nomes),
         juntas_nomes=np.array(list(J.keys())), juntas=np.array(list(J.values()), np.float32))
print('ok', len(tris), 'triângulos', nomes)
