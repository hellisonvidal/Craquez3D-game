# Transfere pesos de ossos extras do Meshy para os ossos Humanoid (Mixamo) e remove os extras.
import bpy
def merge_weights(body, arm, mapping):
    for src, dst in mapping.items():
        gs = body.vertex_groups.get(src)
        if gs is None: continue
        gd = body.vertex_groups.get(dst) or body.vertex_groups.new(name=dst)
        for v in body.data.vertices:
            for g in v.groups:
                if g.group == gs.index and g.weight > 0:
                    gd.add([v.index], g.weight, 'ADD')
        body.vertex_groups.remove(gs)
    bpy.context.view_layer.objects.active = arm
    for o in bpy.context.selected_objects: o.select_set(False)
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for src in mapping:
        eb = arm.data.edit_bones.get(src)
        if eb is not None:
            for ch in eb.children: ch.parent = eb.parent
            arm.data.edit_bones.remove(eb)
    bpy.ops.object.mode_set(mode='OBJECT')
    print("RIGFIX ok", list(mapping))
