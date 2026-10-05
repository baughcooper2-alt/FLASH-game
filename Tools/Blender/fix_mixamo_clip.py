# Re-exports an animation-only Mixamo FBX with its T-pose as frame 0 and as the file's default pose.
#   blender -b --python Tools/Blender/fix_mixamo_clip.py -- <input.fbx> <output.fbx>
# Mixamo "without skin" downloads keep the T-pose only as the skeleton's rest pose; Unity builds the Humanoid
# avatar from the pose the file opens in, which is the first frame of the clip (for the supplied Running clip:
# left knee bent 31 degrees, spine leaning 13). Every retargeted frame then comes out bent the opposite way,
# with limbs twisted. Blender reads the true rest pose, so this keys it one frame before the clip and exports
# from there. Unity's import (FlashRealisticSetup.ConfigureRun) trims that frame off the clip again.
import bpy, sys
src, dst = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src, automatic_bone_orientation=False)
scene = bpy.context.scene
start, end = (int(round(f)) for f in bpy.data.actions[0].frame_range)
arm = next(o for o in scene.objects if o.type == 'ARMATURE')
for pb in arm.pose.bones:
    pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
    pb.keyframe_insert("location", frame=start - 1)
    pb.keyframe_insert("rotation_quaternion" if pb.rotation_mode == 'QUATERNION' else "rotation_euler", frame=start - 1)
    pb.keyframe_insert("scale", frame=start - 1)
# Export the T-pose frame plus the clip (the default timeline is 250 frames), opening on the T-pose.
scene.frame_start, scene.frame_end = start - 1, end
scene.frame_set(start - 1)
bpy.ops.export_scene.fbx(filepath=dst, object_types={'ARMATURE'}, use_selection=False, add_leaf_bones=False,
    bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0,
    apply_unit_scale=True, axis_forward='-Z', axis_up='Y', armature_nodetype='NULL')
print("exported", dst)
