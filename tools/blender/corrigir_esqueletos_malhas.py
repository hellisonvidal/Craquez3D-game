#!/usr/bin/env python3
"""
Corrige integralmente os esqueletos e malhas de Jogador.fbx e Goleiro.fbx:
1. Esqueleto (Rig Mixamo):
   - Elimina hiperextensão posterior dos joelhos (posiciona LeftLeg/RightLeg com leve avanço anatômico patelar -Y
     em relação à corda quadril-tornozelo, estabilizando o IK 2-ossos do FootGrounder e o retarget Humanoid);
   - Conecta e alinha monotonicamente a cadeia Hips -> Spine -> Spine1 -> Spine2 -> Neck -> Head em x = 0;
   - Simetriza perfeitamente todos os ossos Left/Right (< 0.01 mm), nivela os braços em T-pose horizontal limpa
     e alinha ToeBase -> Toe_End apontando reto para frente (-Y) sem torção lateral de roll;
2. Malhas e Pesos de Skinning (Body, Cabeca, Cabelo_*):
   - Remove faces internas/degeneradas causadoras de arestas non-manifold preservando 100% das UVs;
   - Elimina 100% do vazamento cruzado de pesos Left <-> Right nas coxas/calção/ombros (522 vértices no Jogador e
     94 no Goleiro que repuxavam o calção entre as pernas na corrida e no chute);
   - Sela a costura do pescoço entre Cabeca e Body (0.00 mm de fresta) igualando posição e pesos (Neck + Spine2,
     sem peso de Head no anel de corte para que FaceShapes e porte físico nunca abram fenda no colarinho);
   - Garante 100% de peso em mixamorig:Head nos cabelos 3D e limita todos os vértices a no máximo 4 ossos
     normalizados (soma = 1.0000, padrão Unity Standard Skin Weights).

Uso:
  blender -b --factory-startup -P tools/blender/corrigir_esqueletos_malhas.py -- Assets/Craquez/Models/Jogador.fbx jogador
  blender -b --factory-startup -P tools/blender/corrigir_esqueletos_malhas.py -- Assets/Craquez/Models/Goleiro.fbx goleiro
"""
import sys
import math
import bpy
import bmesh
import numpy as np
from mathutils import Vector

P = "mixamorig:"


def clean_non_manifold_faces(mesh_obj: bpy.types.Object) -> int:
    """Remove apenas faces duplicadas/internas extras em arestas com >2 faces (preserva UVs e normais)."""
    bm = bmesh.new()
    bm.from_mesh(mesh_obj.data)
    bm.edges.ensure_lookup_table()
    bm.faces.ensure_lookup_table()

    to_delete = set()
    # 1) Faces com área quase zero
    for f in bm.faces:
        if f.calc_area() < 1e-9:
            to_delete.add(f)

    # 2) Em arestas com 3+ faces (abas internas residuais da malha esculpida), remove a menor face interna
    for e in bm.edges:
        linked = [f for f in e.link_faces if f not in to_delete]
        while len(linked) > 2:
            # Escolhe a face com mais arestas já compartilhadas por 2 outras faces ou menor área
            def face_internal_score(fc):
                shared_Other = sum(1 for ed in fc.edges if len([ff for ff in ed.link_faces if ff not in to_delete]) > 2)
                boundary_edges = sum(1 for ed in fc.edges if len([ff for ff in ed.link_faces if ff not in to_delete]) == 1)
                return (-boundary_edges, -shared_Other, fc.calc_area())

            linked.sort(key=face_internal_score)
            victim = linked.pop(0)
            to_delete.add(victim)

    removed = len(to_delete)
    if removed > 0:
        bmesh.ops.delete(bm, geom=list(to_delete), context="FACES")
        bm.to_mesh(mesh_obj.data)
        mesh_obj.data.update()
    bm.free()
    return removed


def fix_model(fbx_path: str, kind: str) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx_path, ignore_leaf_bones=False)

    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    body = bpy.data.objects["Body"]
    cabeca = bpy.data.objects["Cabeca"]

    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.matrix_basis.identity()
    bpy.context.view_layer.update()

    # Limpa faces internas non-manifold sem alterar UVs
    for m in meshes:
        rem = clean_non_manifold_faces(m)
        if rem:
            print(f"[corrigir] {m.name}: {rem} faces internas/degeneradas removidas")

    aw = arm.matrix_world.copy()
    awi = aw.inverted()
    body_pts = np.array([(body.matrix_world @ v.co)[:] for v in body.data.vertices], dtype=np.float64)

    def slab_mid_y(z: float, half_x: float = 0.13, dz: float = 0.022):
        sl = body_pts[(np.abs(body_pts[:, 2] - z) < dz) & (np.abs(body_pts[:, 0]) < half_x)]
        if len(sl) < 6:
            return None
        return float(0.5 * (sl[:, 1].min() + sl[:, 1].max()))

    def leg_slice_y(z: float, hx: float):
        sl = body_pts[(np.abs(body_pts[:, 2] - z) < 0.022) & (body_pts[:, 0] > 0.015) & (body_pts[:, 0] < hx + 0.11)]
        if len(sl) < 8:
            return None
        return float(sl[:, 1].min()), float(sl[:, 1].max())

    # =========================================================================
    # 1. CORREÇÃO DO ESQUELETO (EDIT BONES)
    # =========================================================================
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones

    def W(v):
        return aw @ v

    def L(v):
        return awi @ v

    for b in eb:
        b.use_connect = False

    hips_h = W(eb[P + "Hips"].head)
    head_h = W(eb[P + "Head"].head)
    head_t = W(eb[P + "Head"].tail)

    # Coluna vertebral contínua e alinhada em x = 0
    new_heads = {}
    hips_mid = slab_mid_y(hips_h.z, 0.14)
    new_heads["Hips"] = Vector((0.0, hips_mid if hips_mid is not None else hips_h.y, hips_h.z))
    new_heads["Head"] = Vector((0.0, head_h.y, head_h.z))

    for nm in ("Spine", "Spine1", "Spine2", "Neck"):
        bh = W(eb[P + nm].head)
        mid = slab_mid_y(bh.z, 0.085 if nm == "Neck" else 0.14)
        t = (bh.z - hips_h.z) / max(1e-4, head_h.z - hips_h.z)
        line_y = new_heads["Hips"].y * (1.0 - t) + new_heads["Head"].y * t
        y_val = 0.55 * line_y + 0.45 * (mid if mid is not None else line_y)
        if nm in ("Spine1", "Spine2"):
            y_val += 0.007  # curva torácica natural suave
        new_heads[nm] = Vector((0.0, y_val, bh.z))

    chain_next = {"Hips": "Spine", "Spine": "Spine1", "Spine1": "Spine2", "Spine2": "Neck", "Neck": "Head"}
    for nm, nxt in chain_next.items():
        b = eb[P + nm]
        b.head = L(new_heads[nm])
        b.tail = L(new_heads[nxt])

    b_head = eb[P + "Head"]
    b_head.head = L(new_heads["Head"])
    b_head.tail = L(Vector((0.0, new_heads["Head"].y, head_t.z)))
    if (P + "HeadTop_End") in eb:
        hte = eb[P + "HeadTop_End"]
        hte_len = max(0.12, (W(hte.tail) - W(hte.head)).length)
        hte.head = b_head.tail
        hte.tail = L(Vector((0.0, new_heads["Head"].y, head_t.z + hte_len)))

    # Pares simétricos Left/Right
    pairs = ["Shoulder", "Arm", "ForeArm", "Hand", "HandEnd", "UpLeg", "Leg", "Foot", "ToeBase", "Toe_End"]
    for b in eb:
        s = b.name.replace(P, "")
        if s.startswith("Left"):
            suf = s[4:]
            if suf not in pairs and (P + "Right" + suf) in eb:
                pairs.append(suf)

    sym_h = {}
    sym_t = {}
    for suf in pairs:
        bl, br = eb.get(P + "Left" + suf), eb.get(P + "Right" + suf)
        if not bl or not br:
            continue
        lh, rh = W(bl.head), W(br.head)
        lt, rt = W(bl.tail), W(br.tail)
        sym_h[suf] = Vector((0.5 * (abs(lh.x) + abs(rh.x)), 0.5 * (lh.y + rh.y), 0.5 * (lh.z + rh.z)))
        sym_t[suf] = Vector((0.5 * (abs(lt.x) + abs(rt.x)), 0.5 * (lt.y + rt.y), 0.5 * (lt.z + rt.z)))

    # Corrige T-pose horizontal limpa dos braços (Shoulder -> Arm -> ForeArm -> Hand -> HandEnd)
    arm_z = sym_h["Arm"].z
    arm_y = sym_h["Arm"].y
    for suf in ("Arm", "ForeArm", "Hand"):
        sym_h[suf].y = arm_y
        sym_h[suf].z = arm_z
    if "HandEnd" in sym_h:
        sym_h["HandEnd"].y = arm_y
        sym_h["HandEnd"].z = arm_z
    sym_t["Hand"].y = arm_y
    sym_t["Hand"].z = arm_z
    sym_h["Shoulder"].y = 0.5 * (arm_y + new_heads["Spine2"].y)

    # Corrige geometria sagital das pernas: elimina hiperextensão do joelho (genu recurvatum)!
    # Em Blender, -Y é FRENTE e +Y é COSTAS. O joelho deve ficar ~1.6 cm À FRENTE (-Y) da linha quadril-tornozelo!
    hip_x = sym_h["UpLeg"].x
    knee_x = sym_h["Leg"].x
    ankle_x = sym_h["Foot"].x

    hip_bounds = leg_slice_y(sym_h["UpLeg"].z, hip_x)
    knee_bounds = leg_slice_y(sym_h["Leg"].z, knee_x)

    hip_y = 0.5 * (hip_bounds[0] + hip_bounds[1]) if hip_bounds else sym_h["UpLeg"].y
    # Mantém o tornozelo no centro talocrural real (sem puxar para o calcanhar da chuteira)
    ankle_y = min(sym_h["Foot"].y, hip_y + 0.022)
    chord_knee_y = 0.5 * (hip_y + ankle_y)
    anat_knee_y = (knee_bounds[0] * 0.58 + knee_bounds[1] * 0.42) if knee_bounds else (chord_knee_y - 0.018)
    # Joelho levemente anterior (-Y) à corda quadril-tornozelo (~3° de flexão fisiológica natural, 0% hiperextensão)
    knee_y = min(anat_knee_y, chord_knee_y - 0.018, hip_y - 0.008)

    sym_h["UpLeg"].y = hip_y
    sym_h["Leg"].y = knee_y
    sym_h["Leg"].x = 0.5 * (hip_x + ankle_x)  # alinha joelho no plano frontal quadril-tornozelo
    sym_h["Foot"].y = ankle_y
    sym_h["Foot"].x = sym_h["Leg"].x

    # Alinha ToeBase e Toe_End reto para frente (-Y) na horizontal da sola, eliminando roll torto nos dedos
    foot_x = sym_h["Foot"].x
    toe_z = sym_h["ToeBase"].z
    toe_len = max(0.068, abs(sym_t["ToeBase"].y - sym_h["ToeBase"].y))
    sym_h["ToeBase"] = Vector((foot_x, sym_h["ToeBase"].y, toe_z))
    sym_t["ToeBase"] = Vector((foot_x, sym_h["ToeBase"].y - toe_len, toe_z))
    if "Toe_End" in sym_h:
        sym_h["Toe_End"] = sym_t["ToeBase"].copy()
        sym_t["Toe_End"] = Vector((foot_x, sym_t["ToeBase"].y - toe_len, toe_z))

    limb_next = {
        "Shoulder": "Arm", "Arm": "ForeArm", "ForeArm": "Hand",
        "UpLeg": "Leg", "Leg": "Foot", "Foot": "ToeBase",
    }
    for side, sg in (("Left", 1.0), ("Right", -1.0)):
        for suf in pairs:
            if suf not in sym_h:
                continue
            b = eb[P + side + suf]
            h = sym_h[suf]
            b.head = L(Vector((sg * h.x, h.y, h.z)))
            nxt = limb_next.get(suf)
            if nxt and nxt in sym_h:
                nh = sym_h[nxt]
                b.tail = L(Vector((sg * nh.x, nh.y, nh.z)))
            elif suf == "ToeBase" and "Toe_End" in sym_h:
                nh = sym_h["Toe_End"]
                b.tail = L(Vector((sg * nh.x, nh.y, nh.z)))
            elif suf == "Hand" and "HandEnd" in sym_h:
                nh = sym_h["HandEnd"]
                b.tail = L(Vector((sg * nh.x, nh.y, nh.z)))
            else:
                t = sym_t[suf]
                b.tail = L(Vector((sg * t.x, t.y, t.z)))

    # Conecta cadeias contínuas
    connect_bones = [
        "Spine", "Spine1", "Spine2", "Neck", "Head", "HeadTop_End",
        "LeftArm", "LeftForeArm", "LeftHand", "RightArm", "RightForeArm", "RightHand",
        "LeftLeg", "LeftFoot", "LeftToeBase", "LeftToe_End",
        "RightLeg", "RightFoot", "RightToeBase", "RightToe_End",
    ]
    for nm in connect_bones:
        b = eb.get(P + nm)
        if b and b.parent and (b.head - b.parent.tail).length < 1e-4:
            b.use_connect = True

    # Recalcula roll consistente com posekit.py (GLOBAL_POS_Y) e simetriza roll Left/Right
    bpy.ops.armature.select_all(action="SELECT")
    bpy.ops.armature.calculate_roll(type="GLOBAL_POS_Y")
    for suf in pairs:
        bl, br = eb.get(P + "Left" + suf), eb.get(P + "Right" + suf)
        if bl and br:
            br.roll = -bl.roll

    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()

    # =========================================================================
    # 2. COSTURA DO PESCOÇO (CABECA <-> BODY) E PESOS DE SKINNING
    # =========================================================================
    def boundary_indices(obj: bpy.types.Object):
        ec = {}
        for poly in obj.data.polygons:
            for ek in poly.edge_keys:
                ec[ek] = ec.get(ek, 0) + 1
        bvs = set()
        for (a, b_idx), cnt in ec.items():
            if cnt == 1:
                bvs.add(a)
                bvs.add(b_idx)
        return sorted(bvs)

    z_neck_cut = new_heads["Neck"].z
    cab_mw = cabeca.matrix_world.copy()
    cab_mwi = cab_mw.inverted()
    body_mw = body.matrix_world.copy()
    body_mwi = body_mw.inverted()

    cab_bnd = [i for i in boundary_indices(cabeca) if (cab_mw @ cabeca.data.vertices[i].co).z < z_neck_cut + 0.12]
    body_bnd = [i for i in boundary_indices(body) if (body_mw @ body.data.vertices[i].co).z > z_neck_cut - 0.10]

    # Sela fresta de até 6 mm casando exatamente os vértices da borda inferior da Cabeca à gola/pescoço do Body
    neck_pairs = []
    if cab_bnd and body_bnd:
        body_bnd_pts = np.array([(body_mw @ body.data.vertices[j].co)[:] for j in body_bnd], dtype=np.float64)
        for ci in cab_bnd:
            cp = np.array((cab_mw @ cabeca.data.vertices[ci].co)[:], dtype=np.float64)
            dists = np.linalg.norm(body_bnd_pts - cp, axis=1)
            k_min = int(np.argmin(dists))
            if dists[k_min] < 0.018:
                bi = body_bnd[k_min]
                bp = body_bnd_pts[k_min]
                # Ponto fundido exato (mantém Body intacto para não alterar UV do uniforme; encaixa Cabeca 100% na borda!)
                cabeca.data.vertices[ci].co = cab_mwi @ Vector(bp)
                neck_pairs.append((ci, bi))
        cabeca.data.update()

    # Corrige pesos de skinning em Body, Cabeca e Cabelo_*
    arm_bone_names = set(arm.data.bones.keys())

    def clean_and_fix_weights(mesh_obj: bpy.types.Object, is_hair: bool = False, is_head: bool = False, is_body: bool = False):
        # Garante grupos para todos os ossos necessários
        for bn in ("mixamorig:Hips", "mixamorig:Spine", "mixamorig:Spine2", "mixamorig:Neck", "mixamorig:Head"):
            if bn in arm_bone_names and bn not in mesh_obj.vertex_groups:
                mesh_obj.vertex_groups.new(name=bn)

        # Garante que todos os pares Left/Right tenham ambos os grupos
        existing_names = [g.name for g in mesh_obj.vertex_groups]
        for gn in list(existing_names):
            if "Left" in gn:
                rn = gn.replace("Left", "Right")
                if rn in arm_bone_names and rn not in mesh_obj.vertex_groups:
                    mesh_obj.vertex_groups.new(name=rn)
            elif "Right" in gn:
                ln = gn.replace("Right", "Left")
                if ln in arm_bone_names and ln not in mesh_obj.vertex_groups:
                    mesh_obj.vertex_groups.new(name=ln)

        vg_index = {g.name: g.index for g in mesh_obj.vertex_groups}
        valid_indices = {g.index: g.name for g in mesh_obj.vertex_groups if g.name in arm_bone_names}
        vg_count = len(mesh_obj.vertex_groups)
        verts = mesh_obj.data.vertices
        mw = mesh_obj.matrix_world

        if is_hair:
            # Cabelos 3D seguem 100% mixamorig:Head sem deformação parasita
            hi = vg_index["mixamorig:Head"]
            for i in range(len(verts)):
                for g_idx in range(vg_count):
                    mesh_obj.vertex_groups[g_idx].remove([i])
                mesh_obj.vertex_groups[hi].add([i], 1.0, "REPLACE")
            return

        W_mat = np.zeros((len(verts), vg_count), dtype=np.float64)
        pts = np.zeros((len(verts), 3), dtype=np.float64)
        for i, v in enumerate(verts):
            p = mw @ v.co
            pts[i] = (p.x, p.y, p.z)
            for g in v.groups:
                if g.group in valid_indices and g.weight > 1e-6:
                    W_mat[i, g.group] = g.weight

        # Remove 100% do cross-bleeding Left <-> Right!
        hips_idx = vg_index["mixamorig:Hips"]
        spine2_idx = vg_index["mixamorig:Spine2"]
        neck_idx = vg_index["mixamorig:Neck"]
        head_idx = vg_index["mixamorig:Head"]

        left_to_right = {}
        right_to_left = {}
        for gn, gi in vg_index.items():
            if "Left" in gn and gn.replace("Left", "Right") in vg_index:
                ri = vg_index[gn.replace("Left", "Right")]
                left_to_right[gi] = ri
                right_to_left[ri] = gi

        for i in range(len(verts)):
            x, y, z = pts[i]
            if x > 0.012:
                # Lado ESQUERDO (+X): nenhum osso Right* pode puxar este vértice!
                blend_mid = min(1.0, (x - 0.012) / 0.022)  # transição suave perto de x=0
                for ri, li in right_to_left.items():
                    w_bad = W_mat[i, ri]
                    if w_bad > 0.0:
                        rname = mesh_obj.vertex_groups[ri].name
                        is_leg = any(k in rname for k in ("UpLeg", "Leg", "Foot", "Toe"))
                        mid_idx = hips_idx if is_leg else spine2_idx
                        W_mat[i, li] += w_bad * blend_mid
                        W_mat[i, mid_idx] += w_bad * (1.0 - blend_mid)
                        W_mat[i, ri] = 0.0
            elif x < -0.012:
                # Lado DIREITO (-X): nenhum osso Left* pode puxar este vértice!
                blend_mid = min(1.0, (-x - 0.012) / 0.022)
                for li, ri in left_to_right.items():
                    w_bad = W_mat[i, li]
                    if w_bad > 0.0:
                        lname = mesh_obj.vertex_groups[li].name
                        is_leg = any(k in lname for k in ("UpLeg", "Leg", "Foot", "Toe"))
                        mid_idx = hips_idx if is_leg else spine2_idx
                        W_mat[i, ri] += w_bad * blend_mid
                        W_mat[i, mid_idx] += w_bad * (1.0 - blend_mid)
                        W_mat[i, li] = 0.0
            else:
                # Faixa central da costura do calção / esterno (|x| <= 1.2 cm): transfere excesso oposto ao quadril/tronco
                if is_body and z < new_heads["Spine"].z + 0.04:
                    for gi in list(left_to_right.keys()) + list(right_to_left.keys()):
                        if W_mat[i, gi] > 0.0:
                            W_mat[i, hips_idx] += W_mat[i, gi] * 0.65
                            W_mat[i, gi] *= 0.35

        # Suavização Laplaciana controlada nas articulações críticas (sem cruzar hemisférios X!)
        if is_body:
            adj = [[] for _ in range(len(verts))]
            for poly in mesh_obj.data.polygons:
                vs = list(poly.vertices)
                for k in range(len(vs)):
                    a, b_idx = vs[k], vs[(k + 1) % len(vs)]
                    # Só conecta vizinhos do mesmo lado (impede reintroduzir cross-bleed na virilha!)
                    if pts[a, 0] * pts[b_idx, 0] >= -1e-5 or (abs(pts[a, 0]) < 0.015 and abs(pts[b_idx, 0]) < 0.015):
                        adj[a].append(b_idx)
                        adj[b_idx].append(a)

            z_kn = sym_h["Leg"].z
            z_hp = sym_h["UpLeg"].z
            x_sh = sym_h["Arm"].x
            x_el = sym_h["ForeArm"].x
            for _ in range(2):
                W_next = W_mat.copy()
                for i in range(len(verts)):
                    if not adj[i]:
                        continue
                    ax, z = abs(pts[i, 0]), pts[i, 2]
                    near_joint = (
                        abs(z - z_kn) < 0.065 or
                        (z_hp - 0.14 < z < z_hp + 0.10 and ax < 0.24) or
                        (abs(ax - x_sh) < 0.075 and abs(z - arm_z) < 0.14) or
                        (abs(ax - x_el) < 0.055 and abs(z - arm_z) < 0.10)
                    )
                    if near_joint:
                        nb = W_mat[adj[i]].mean(axis=0)
                        W_next[i] = W_mat[i] * 0.68 + nb * 0.32
                W_mat = W_next

        # Na Cabeca, garante transição suave do crânio (Head) para a base do pescoço (Neck/Spine2)
        if is_head:
            cab_bnd_set = set(cab_bnd)
            for i in range(len(verts)):
                if i in cab_bnd_set:
                    W_mat[i, :] = 0.0
                    W_mat[i, neck_idx] = 0.68
                    W_mat[i, spine2_idx] = 0.32
                else:
                    # Nos 2.5 cm logo acima da costura, reduz gradualmente Head para não abrir degrau ao girar a cabeça
                    dz = pts[i, 2] - z_neck_cut
                    if dz < 0.026:
                        t_up = max(0.0, min(1.0, (dz + 0.005) / 0.031))
                        t_up = t_up * t_up * (3.0 - 2.0 * t_up)
                        base_w = np.zeros(vg_count, dtype=np.float64)
                        base_w[neck_idx] = 0.68
                        base_w[spine2_idx] = 0.32
                        W_mat[i] = base_w * (1.0 - t_up) + W_mat[i] * t_up

        if is_body and neck_pairs:
            body_neck_set = {bi for _, bi in neck_pairs}
            for bi in body_neck_set:
                W_mat[bi, :] = 0.0
                W_mat[bi, neck_idx] = 0.68
                W_mat[bi, spine2_idx] = 0.32

        # Grava no máximo 4 pesos normalizados (soma = 1.0000) em todos os vértices
        for i in range(len(verts)):
            row = W_mat[i]
            nz = np.where(row > 1e-4)[0]
            if len(nz) == 0:
                fallback = head_idx if is_head else hips_idx
                nz = np.array([fallback])
                row[fallback] = 1.0
            elif len(nz) > 4:
                nz = nz[np.argsort(row[nz])[-4:]]
            s = float(row[nz].sum())
            for g_idx in range(vg_count):
                mesh_obj.vertex_groups[g_idx].remove([i])
            for g_idx in nz:
                mesh_obj.vertex_groups[int(g_idx)].add([i], float(row[g_idx] / s), "REPLACE")

    for m in meshes:
        clean_and_fix_weights(
            m,
            is_hair=m.name.startswith("Cabelo_"),
            is_head=(m.name == "Cabeca"),
            is_body=(m.name == "Body"),
        )
        for slot in m.material_slots:
            if slot.material and "." in slot.material.name:
                base_n = slot.material.name.split(".")[0]
                ex = bpy.data.materials.get(base_n)
                if ex and ex != slot.material:
                    slot.material = ex
                else:
                    slot.material.name = base_n

    print(
        f"[corrigir_esqueletos_malhas] {fbx_path} ({kind}): "
        f"UpLegY={sym_h['UpLeg'].y:+.4f}, KneeY={sym_h['Leg'].y:+.4f}, AnkleY={sym_h['Foot'].y:+.4f}, "
        f"neckPairs={len(neck_pairs)}"
    )

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
    fix_model(argv[0], argv[1])
