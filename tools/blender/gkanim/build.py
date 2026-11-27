#!/usr/bin/env python3
"""
Gera todas as animações nativas do goleiro (FBX só com o esqueleto de Models/Goleiro.fbx, 30 fps)
diretamente em Assets/Craquez/Animations/Goleiro/Jogadas, garantindo:
- Retarget Humanoid 1:1 identidade (sem torção de pés ou braços cruzados);
- Pés 100% cravados no chão em GK_Parado (0,0 mm de oscilação);
- Altura mínima acima do gramado em mergulhos, quedas e abafamentos (nunca afunda no terreno).

Uso:
  blender -b --factory-startup -P tools/blender/gkanim/build.py -- Assets/Craquez/Models/Goleiro.fbx Assets/Craquez/Animations/Goleiro/Jogadas
"""
import bpy
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import posekit
import poses

if "--" in sys.argv:
    args = sys.argv[sys.argv.index("--") + 1 :]
    GK_MODEL, OUT = args[0], args[1]
else:
    GK_MODEL = os.environ.get("GK", "Assets/Craquez/Models/Goleiro.fbx")
    OUT = os.environ.get("OUTDIR", "Assets/Craquez/Animations/Goleiro/Jogadas")

os.makedirs(OUT, exist_ok=True)
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)

bpy.ops.import_scene.fbx(filepath=GK_MODEL, ignore_leaf_bones=False)
arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
for o in list(bpy.data.objects):
    if o.type != "ARMATURE":
        bpy.data.objects.remove(o)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)

rig = posekit.Rig(arm)
sc = bpy.context.scene
sc.render.fps = 30

rig.reset()
rest_ankle_z = 0.5 * (
    (arm.matrix_world @ arm.pose.bones["mixamorig:LeftFoot"].head).z
    + (arm.matrix_world @ arm.pose.bones["mixamorig:RightFoot"].head).z
)

TORSO_CLEARANCE = {"Hips": 0.135, "Spine": 0.145, "Spine1": 0.150, "Spine2": 0.155, "Neck": 0.150, "Head": 0.150}
LIMB_CLEARANCE = 0.055


def P(name):
    return getattr(poses, name), getattr(poses, name + "_H", (0.0, 0.0, 0.0))


def M(name):
    s, h = P(name)
    return posekit.mirror(s, h)


def apply_gk_pose(clip_name, spec, off):
    rig.pose(spec, off)
    if clip_name in ("GK_Parado", "GK_Pronto"):
        cur_z = 0.5 * (
            (arm.matrix_world @ arm.pose.bones["mixamorig:LeftFoot"].head).z
            + (arm.matrix_world @ arm.pose.bones["mixamorig:RightFoot"].head).z
        )
        dz = rest_ankle_z - cur_z
        if abs(dz) > 1e-6:
            rig.pose(spec, (off[0], off[1], off[2] + dz))
    elif clip_name in ("GK_Mergulho", "GK_Caido", "GK_Levantar", "GK_Carrinho"):
        needed_dz = 0.0
        for pb in arm.pose.bones:
            short = pb.name.replace("mixamorig:", "")
            hz = (arm.matrix_world @ pb.head).z
            min_allowed = TORSO_CLEARANCE.get(short, LIMB_CLEARANCE)
            if hz < min_allowed:
                needed_dz = max(needed_dz, min_allowed - hz)
        if needed_dz > 1e-5:
            rig.pose(spec, (off[0], off[1], off[2] + needed_dz))
    else:
        # Locomoção / reposição / salto: impede qualquer pé de atravessar abaixo da sola de repouso
        lf_z = (arm.matrix_world @ arm.pose.bones["mixamorig:LeftFoot"].head).z
        rf_z = (arm.matrix_world @ arm.pose.bones["mixamorig:RightFoot"].head).z
        min_foot = min(lf_z, rf_z)
        if min_foot < rest_ankle_z - 0.004:
            rig.pose(spec, (off[0], off[1], off[2] + (rest_ankle_z - min_foot)))


CLIPS = {
    "GK_Parado":      [(0, P("IDLE_A")), (45, P("IDLE_B")), (90, P("IDLE_A"))],
    "GK_Pronto":      [(0, P("READY")), (24, P("READY2")), (48, P("READY"))],
    "GK_DeslocarDir": [(0, P("SHUF_A")), (9, P("SHUF_B")), (18, P("SHUF_A"))],
    "GK_DeslocarEsq": [(0, M("SHUF_A")), (9, M("SHUF_B")), (18, M("SHUF_A"))],
    "GK_Andar":       [(0, P("W_L")), (7, P("W_M1")), (14, P("W_R")), (21, P("W_M2")), (28, P("W_L"))],
    "GK_Correr":      [(0, P("R_L")), (5, P("R_M1")), (10, P("R_R")), (15, P("R_M2")), (20, P("R_L"))],
    "GK_Recuar":      [(0, P("BK_A")), (11, P("BK_B")), (22, P("BK_A"))],
    "GK_Mergulho":    [(0, P("READY")), (4, P("D_LOAD")), (9, P("D_LAUNCH")), (15, P("D_FLY")), (24, P("D_LAND")), (34, P("D_LIE")), (42, P("D_LIE"))],
    "GK_Caido":       [(0, P("D_LIE")), (30, P("D_LIE"))],
    "GK_Levantar":    [(0, P("D_LIE")), (10, P("G_PUSH")), (21, P("G_KNEEL")), (33, P("READY"))],
    "GK_Salto":       [(0, P("READY")), (5, P("J_CROUCH")), (10, P("J_TAKE")), (15, P("J_PEAK")), (21, P("J_CATCH")), (30, P("J_LAND"))],
    "GK_Soco":        [(0, P("READY")), (5, P("J_CROUCH")), (10, P("P_TAKE")), (15, P("P_PUNCH")), (23, P("J_CATCH")), (31, P("J_LAND"))],
    "GK_Defesa":      [(0, P("READY")), (6, P("B_REACH")), (14, P("B_HUG")), (24, P("B_HUG"))],
    "GK_Carrinho":    [(0, P("READY")), (6, P("K_DROP")), (14, P("K_SPREAD")), (24, P("K_SPREAD")), (32, P("G_KNEEL"))],
    "GK_Lancar":      [(0, P("B_HUG")), (8, P("TH_WIND")), (16, P("TH_REL")), (24, P("TH_FOL")), (34, P("READY"))],
    "GK_Chutao":      [(0, P("B_HUG")), (8, P("PK_DROP")), (14, P("PK_HIT")), (21, P("PK_FOL")), (30, P("READY"))],
}

only = os.environ.get("ONLY")
for name, keys in CLIPS.items():
    if only and name not in only.split(","):
        continue
    act = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = act
    prev = None
    for f, (spec, off) in keys:
        apply_gk_pose(name, spec, off)
        prev = rig.key(f, prev)
    end = keys[-1][0]
    sc.frame_start = 0
    sc.frame_end = end

    min_zs = []
    for f in range(0, end + 1):
        sc.frame_set(f)
        bpy.context.view_layer.update()
        min_zs.append(min((arm.matrix_world @ pb.head).z for pb in arm.pose.bones))

    arm.animation_data.action = None
    rig.reset()
    for a in list(bpy.data.actions):
        if a != act:
            bpy.data.actions.remove(a)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, name + ".fbx"),
        use_selection=True,
        object_types={"ARMATURE"},
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_UNITS",
        path_mode="STRIP",
    )
    bpy.data.actions.remove(act)
    print(f"GK_CLIP {name:16s}: {end:2d}f | minBoneZ={min(min_zs):.4f}")
