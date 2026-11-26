#!/usr/bin/env python3
"""Gera GK_Parado.fbx no esqueleto do Goleiro.fbx com pés 100% plantados (0,0 mm de oscilação)."""
import bpy, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import posekit, poses

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else ['Assets/Craquez/Models/Goleiro.fbx', 'Assets/Craquez/Animations/Goleiro/Jogadas']
MODEL, OUT = args[0], args[1]
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=MODEL, ignore_leaf_bones=False)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
for o in list(bpy.data.objects):
    if o.type != 'ARMATURE':
        bpy.data.objects.remove(o)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)

rig = posekit.Rig(arm)
sc = bpy.context.scene
sc.render.fps = 30
rig.reset()
rest_ankle_z = 0.5 * ((arm.matrix_world @ arm.pose.bones['mixamorig:LeftFoot'].head).z +
                      (arm.matrix_world @ arm.pose.bones['mixamorig:RightFoot'].head).z)

GK_IDLE_A = poses.merge(poses.legs(26, 46, 18, 9), poses.arms(-52, 42, 62, -4, -8), poses.spine(22, neck=10, head=10))
GK_IDLE_B = poses.merge(poses.legs(26, 46, 18, 9), poses.arms(-50, 44, 65, -3, -8), poses.spine(23.5, neck=11, head=10))

act = bpy.data.actions.new('GK_Parado')
arm.animation_data_create()
arm.animation_data.action = act
prev = None
for f, spec in [(0, GK_IDLE_A), (40, GK_IDLE_B), (80, GK_IDLE_A)]:
    rig.pose(spec, (0, -0.05, 0))
    cur_z = 0.5 * ((arm.matrix_world @ arm.pose.bones['mixamorig:LeftFoot'].head).z +
                   (arm.matrix_world @ arm.pose.bones['mixamorig:RightFoot'].head).z)
    rig.pose(spec, (0, -0.05, rest_ankle_z - cur_z))
    prev = rig.key(f, prev)

sc.frame_start = 0
sc.frame_end = 80
arm.animation_data.action = None
rig.reset()
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=os.path.join(OUT, 'GK_Parado.fbx'),
    use_selection=True,
    object_types={'ARMATURE'},
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0,
    axis_forward='-Z',
    axis_up='Y',
    apply_scale_options='FBX_SCALE_UNITS',
    path_mode='STRIP',
)
print('Exported GK_Parado.fbx')
