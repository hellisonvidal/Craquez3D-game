import bpy
from mathutils import Vector, Matrix
bpy.ops.wm.open_mainfile(filepath='/home/user/work/gk_tpose.blend')
arm=bpy.data.objects['GK_Rig']; body=bpy.data.objects['GK_Body']
zs=[(body.matrix_world@v.co).z for v in body.data.vertices]
zmin,zmax=min(zs),max(zs); s=1.88/(zmax-zmin)
print("height before", zmax-zmin)
hb=arm.matrix_world@arm.data.bones['mixamorig:Hips'].head_local
arm.matrix_world = Matrix.Translation((-hb.x*s,-hb.y*s,-zmin*s)) @ Matrix.Scale(s,4) @ arm.matrix_world
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.context.view_layer.update()
zs=[(body.matrix_world@v.co).z for v in body.data.vertices]; print("height after", max(zs)-min(zs), "zmin", min(zs))
for p in body.data.polygons: p.use_smooth=True
body.data.materials.clear(); m=bpy.data.materials.new('GK_Mat'); body.data.materials.append(m)
for b in arm.data.bones: print("B", b.name, tuple(round(c,3) for c in b.head_local)) if b.name.endswith(('Hips','Head','LeftHand','LeftFoot')) else None
bpy.ops.export_scene.fbx(filepath='/home/user/work/out/Goleiro.fbx', use_selection=True, object_types={'ARMATURE','MESH'},
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP', primary_bone_axis='Y', secondary_bone_axis='X')
bpy.ops.wm.save_as_mainfile(filepath='/home/user/work/gk_final.blend')
