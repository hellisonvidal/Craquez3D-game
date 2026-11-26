#!/usr/bin/env python3
"""
Ajusta a modelagem 3D das malhas dos uniformes (camisa, mangas, calção e meiões) em Jogador.fbx e Goleiro.fbx
para vestirem de forma atlética e natural sobre o corpo dos atletas:
- Reduz o efeito "saia/balão" na barra do calção, aproximando o caimento ao redor da coxa;
- Afina a cintura/barra da camisa (corte atlético em V) e suaviza o degrau rígido da bainha;
- No goleiro, corrige o volume peitoral quadrado ("peitoral de armadura") e simetriza a dobra do meião;
- Ajusta a manga curta do jogador ao redor do bíceps sem sobra quadrada;
- Suaviza os pesos de skinning nas axilas e na virilha/calção para não amassar na corrida/braços abaixados.

Uso:
  blender -b --factory-startup -P tools/blender/ajustar_uniformes.py -- Assets/Craquez/Models/Jogador.fbx jogador
  blender -b --factory-startup -P tools/blender/ajustar_uniformes.py -- Assets/Craquez/Models/Goleiro.fbx goleiro
"""
import sys
import math
import bpy
import numpy as np
from mathutils import Vector


def smoothstep(e0: float, e1: float, x: float) -> float:
    if abs(e1 - e0) < 1e-8:
        return 1.0 if x >= e1 else 0.0
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3.0 - 2.0 * t)


def tailor_model(fbx_path: str, kind: str) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx_path, ignore_leaf_bones=False)

    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    body = bpy.data.objects["Body"]
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.matrix_basis.identity()
    bpy.context.view_layer.update()

    mw = body.matrix_world.copy()
    mwi = mw.inverted()
    J = {pb.name.replace("mixamorig:", ""): np.array((arm.matrix_world @ pb.head)[:], dtype=np.float64) for pb in arm.pose.bones}

    verts = body.data.vertices
    P = np.array([(mw @ v.co)[:] for v in verts], dtype=np.float64)
    P_orig = P.copy()

    is_gk = kind == "goleiro"
    z_knee = J["LeftLeg"][2]
    z_hip = J["LeftUpLeg"][2]
    z_spine = J["Spine"][2]
    z_chest = J["Spine2"][2]
    z_neck = J["Neck"][2]
    arm_z = J["LeftArm"][2]
    arm_y = J["LeftArm"][1]
    arm_x0 = abs(J["LeftArm"][0])

    # Limites exatos das peças esculpidas na malha
    shorts_z0 = 0.774 if is_gk else 0.738
    shirt_hem_z = 1.088 if is_gk else 1.038
    sleeve_x_end = 0.635 if is_gk else 0.396

    for i in range(len(P)):
        x, y, z = P[i]
        ax = abs(x)
        sgn = 1.0 if x >= 0.0 else -1.0

        # 1) Calção ajustado às coxas (elimina a aba aberta em forma de sino/saia)
        if shorts_z0 - 0.012 <= z <= shirt_hem_z + 0.02 and ax < 0.28:
            in_shorts = smoothstep(shorts_z0 - 0.006, shorts_z0 + 0.014, z) * (1.0 - smoothstep(shirt_hem_z - 0.01, shirt_hem_z + 0.025, z))
            if in_shorts > 0.001:
                # Eixo central da coxa correspondente na altura z
                t_leg = np.clip((z - z_knee) / max(1e-4, z_hip - z_knee), 0.0, 1.0)
                cx = sgn * (abs(J["LeftLeg"][0]) + (abs(J["LeftUpLeg"][0]) - abs(J["LeftLeg"][0])) * t_leg)
                cy = J["LeftLeg"][1] + (J["LeftUpLeg"][1] - J["LeftLeg"][1]) * t_leg

                # Mais justo perto da barra inferior do calção, suavizando rumo ao quadril
                hem_focus = 1.0 - 0.48 * smoothstep(shorts_z0 + 0.02, shirt_hem_z - 0.02, z)
                shrink_r = (0.23 if not is_gk else 0.22) * in_shorts * hem_focus

                dx = x - cx
                dy = y - cy
                # Preserva a folga interna da virilha (entre as pernas) sem colar uma coxa na outra
                outer_w = smoothstep(0.015, 0.075, ax)
                x = cx + dx * (1.0 - shrink_r * (0.45 + 0.55 * outer_w))
                y = cy + dy * (1.0 - shrink_r * 1.08)

        # 2) Camisa (tronco atlético em V, sem barriga/quadril caixote e sem degrau duro na barra)
        if shirt_hem_z - 0.02 <= z <= z_neck - 0.015 and ax < arm_x0 + 0.06:
            in_shirt = smoothstep(shirt_hem_z - 0.015, shirt_hem_z + 0.018, z) * (1.0 - smoothstep(z_neck - 0.05, z_neck - 0.01, z))
            if in_shirt > 0.001:
                # Cintura e barra mais justas; tórax mantém porte atlético
                waist_band = 1.0 - smoothstep(z_spine + 0.04, z_chest + 0.02, z)
                cy_t = J["Spine"][1] * (1.0 - smoothstep(z_spine, z_chest, z)) + J["Spine2"][1] * smoothstep(z_spine, z_chest, z)
                slim_y = (0.17 if not is_gk else 0.14) * in_shirt * (0.35 + 0.65 * waist_band)
                slim_x = (0.11 if not is_gk else 0.08) * in_shirt * waist_band
                x *= (1.0 - slim_x)
                y = cy_t + (y - cy_t) * (1.0 - slim_y)

                # No goleiro, recolhe o "degrau quadrado" saliente na frente do peito (y < 0, z ~ 1.33..1.48)
                if is_gk and y < cy_t - 0.04:
                    chest_shelf = smoothstep(1.28, 1.36, z) * (1.0 - smoothstep(1.46, 1.53, z)) * (1.0 - smoothstep(0.08, 0.17, ax))
                    if chest_shelf > 0.001:
                        y = cy_t + (y - cy_t) * (1.0 - 0.14 * chest_shelf)

        # 3) Mangas mais anatômicas ao redor do braço
        if ax >= arm_x0 - 0.01 and ax <= sleeve_x_end + 0.015 and abs(z - arm_z) < 0.18:
            in_sleeve = smoothstep(arm_x0 - 0.005, arm_x0 + 0.035, ax) * (1.0 - smoothstep(sleeve_x_end - 0.012, sleeve_x_end + 0.01, ax))
            if in_sleeve > 0.001:
                cuff_focus = 0.55 + 0.45 * smoothstep(arm_x0 + 0.04, sleeve_x_end - 0.02, ax)
                shrink_s = (0.17 if not is_gk else 0.10) * in_sleeve * cuff_focus
                y = arm_y + (y - arm_y) * (1.0 - shrink_s)
                z = arm_z + (z - arm_z) * (1.0 - shrink_s)

        P[i] = (x, y, z)

    # 4) Suavização Laplaciana localizada nas bordas de transição (barra da camisa e barra do calção)
    #    para eliminar quinas serrilhadas mantendo os detalhes musculares e normais limpas
    adj = [[] for _ in range(len(P))]
    for poly in body.data.polygons:
        vs = list(poly.vertices)
        for k in range(len(vs)):
            a, b_idx = vs[k], vs[(k + 1) % len(vs)]
            adj[a].append(b_idx)
            adj[b_idx].append(a)

    for _ in range(2):
        P_next = P.copy()
        for i in range(len(P)):
            if not adj[i]:
                continue
            z = P[i, 2]
            ax = abs(P[i, 0])
            near_hem = (abs(z - shorts_z0) < 0.028 and ax < 0.26) or (abs(z - shirt_hem_z) < 0.030 and ax < 0.25)
            if near_hem:
                nb = P[adj[i]].mean(axis=0)
                P_next[i] = P[i] * 0.65 + nb * 0.35
        P = P_next

    # Mantém a sola das chuteiras cravada exatamente em z = 0.000
    z_min = P[:, 2].min()
    P[:, 2] -= z_min

    for i, v in enumerate(verts):
        v.co = mwi @ Vector(P[i])
    body.data.update()

    # 5) Suavização leve dos pesos de skinning nas axilas e na virilha/calção para não repuxar em movimento
    vg_count = len(body.vertex_groups)
    W = np.zeros((len(verts), vg_count), dtype=np.float64)
    for i, v in enumerate(verts):
        for g in v.groups:
            if 0 <= g.group < vg_count:
                W[i, g.group] = g.weight

    for _ in range(2):
        W_next = W.copy()
        for i in range(len(verts)):
            if not adj[i]:
                continue
            x, y, z = P[i]
            ax = abs(x)
            in_armpit = (arm_x0 - 0.06 < ax < arm_x0 + 0.08) and (arm_z - 0.14 < z < arm_z + 0.06)
            in_groin = (shorts_z0 - 0.02 < z < shirt_hem_z + 0.04) and (ax < 0.22)
            if in_armpit or in_groin:
                avg_w = W[adj[i]].mean(axis=0)
                mixed = W[i] * 0.65 + avg_w * 0.35
                s = mixed.sum()
                if s > 1e-6:
                    W_next[i] = mixed / s
        W = W_next

    for i, v in enumerate(verts):
        # Mantém até os 4 maiores pesos (padrão Unity Standard Skin Weights = 4)
        nz = np.where(W[i] > 1e-4)[0]
        if len(nz) > 4:
            top4 = nz[np.argsort(W[i, nz])[-4:]]
            nz = top4
        s = W[i, nz].sum()
        for g_idx in range(vg_count):
            body.vertex_groups[g_idx].remove([i])
        if s > 1e-6:
            for g_idx in nz:
                body.vertex_groups[int(g_idx)].add([i], float(W[i, g_idx] / s), "REPLACE")

    # Limpa sufixos .001 de materiais antes de exportar
    for o in bpy.data.objects:
        if o.type == "MESH":
            for slot in o.material_slots:
                if slot.material and "." in slot.material.name:
                    slot.material.name = slot.material.name.split(".")[0]

    max_disp = float(np.linalg.norm(P - P_orig, axis=1).max())
    print(f"[ajustar_uniformes] {fbx_path} ({kind}): ajuste máx = {max_disp*100:.2f} cm, z_min = {P[:,2].min():.4f}")

    bpy.ops.object.select_all(action="DESELECT")
    for o in bpy.data.objects:
        if o.type in {"ARMATURE", "MESH"}:
            o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_UNITS",
        mesh_smooth_type="FACE",
        path_mode="STRIP",
        primary_bone_axis="Y",
        secondary_bone_axis="X",
    )


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1 :]
    tailor_model(argv[0], argv[1])
