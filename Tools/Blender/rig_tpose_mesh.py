# Rigs a character mesh that is already in a T-pose (arms out, facing -Y after import, Z up) with a
# Mixamo-named skeleton for Unity Humanoid retargeting. Joints are read from the mesh using standard human
# proportions, the body is bone-heat skinned through a watertight proxy, and an FBX is written.
#   blender -b --python Tools/Blender/rig_tpose_mesh.py -- <input.obj|.fbx|.glb> <output.fbx> [height_m]
# Used for the Barry Allen base body (Fuse male base mesh) and the CSI Barry model.
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree

args = sys.argv[sys.argv.index("--") + 1:]
src, dst = args[0], args[1]
target_height = float(args[2]) if len(args) > 2 else 1.80
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
ext = os.path.splitext(src)[1].lower()
if ext == ".obj": bpy.ops.wm.obj_import(filepath=src)
elif ext == ".fbx": bpy.ops.import_scene.fbx(filepath=src)
else: bpy.ops.import_scene.gltf(filepath=src)
# Drop anything that came with the file except meshes, join them, and apply all transforms.
for o in list(scene.objects):
    if o.type != 'MESH': bpy.data.objects.remove(o)
meshes = [o for o in scene.objects if o.type == 'MESH']
for o in scene.objects: o.select_set(False)
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = body.data.name = "Body"
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = body.data
# Normalise: stand on the floor, centred, at the requested height.
vs = [v.co for v in me.vertices]
lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
s = target_height / (hi.z - lo.z)
me.transform(Matrix.Translation((0, 0, 0)) @ Matrix.Scale(s, 4) @ Matrix.Translation((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z)))
# Face -Y: the feet reach much further forward of the shins than behind them.
H = target_height
feet = [v.co.y for v in me.vertices if v.co.z < .03 * H]
shins = [v.co.y for v in me.vertices if abs(v.co.z - .08 * H) < .01 * H]
if sum(feet) / len(feet) > sum(shins) / len(shins):
    me.transform(Matrix.Rotation(math.pi, 4, 'Z'))
verts = [v.co.copy() for v in me.vertices]

def centre(points):
    lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return (lo + hi) / 2
def slab(z, dz, cond=lambda v: True): return [v for v in verts if abs(v.z - z) < dz and cond(v)]

# --- Landmarks -------------------------------------------------------------------------------
# Crotch: the highest level where the legs are still separate (no surface on the centre line).
crotch = .40 * H
for k in range(400, 560):
    z = k / 1000 * H
    if any(abs(v.x) < .012 * H for v in slab(z, .004 * H)): break
    crotch = z
hip_z = crotch + .045 * H
torso = lambda z: centre([v for v in slab(z, .01 * H) if abs(v.x) < .14 * H])
legs = {}
for side, sx in (("Left", 1), ("Right", -1)):
    leg = lambda z: centre([v for v in slab(z, .008 * H) if v.x * sx > .01 * H and abs(v.x) < .16 * H])
    hip = leg(crotch - .02 * H); hip.z = hip_z
    knee_z = .285 * H; knee = leg(knee_z); knee.z = knee_z
    shin = leg(.08 * H)
    foot = [v for v in verts if v.z < .03 * H and v.x * sx > .005 * H]
    heel_y, tip_y = max(v.y for v in foot), min(v.y for v in foot)
    ankle = Vector((shin.x, heel_y + .25 * (tip_y - heel_y), .045 * H))
    ball_y = heel_y + .72 * (tip_y - heel_y)
    ball = Vector((centre([v for v in foot if abs(v.y - ball_y) < .012 * H]).x, ball_y, .018 * H))
    tip = Vector((ball.x, tip_y + .008 * H, .015 * H))
    legs[side] = (hip, knee, ankle, ball, tip)
pelvis = torso(hip_z); pelvis.x = 0
spine = torso(.60 * H); spine.x = 0
chest = torso(.68 * H); chest.x = 0
upper = torso(.76 * H); upper.x = 0
neck = centre([v for v in slab(.845 * H, .006 * H) if abs(v.x) < .06 * H]); neck.x = 0
headj = centre([v for v in slab(.885 * H, .006 * H) if abs(v.x) < .07 * H]); headj.x = 0
# One vertical line for the spine, neck and head, as in the realistic Flash's T-pose. Slice centres zig-zag
# (the neck sits behind the jaw), and HumanoidTPose.Enforce would then rotate the neck back to Mixamo's
# straight T-pose, leaving the face tipped up 20 degrees in every clip.
line_y = sum(j.y for j in (pelvis, spine, chest, upper, neck, headj)) / 6
for j in (pelvis, spine, chest, upper, neck, headj): j.y = line_y
top = Vector((0, headj.y, H))
arms = {}
for side, sx in (("Left", 1), ("Right", -1)):
    arm = [v for v in verts if v.x * sx > .2 * H]
    tip_x = max(v.x * sx for v in arm)
    # Arm axis: centres of slices along x (arms may hang a little below horizontal).
    def at(x):
        pts = [v for v in verts if abs(v.x * sx - x) < .004 * H and v.z > .6 * H]
        return centre(pts) if pts else None
    a = at(.22 * H); b = at(tip_x - .14 * H)
    axis = (b - a).normalized()
    chest_pts = [v for v in slab(.70 * H, .008 * H) if v.x * sx > 0]
    wall = max(v.x * sx for v in chest_pts)
    sh_x = wall + .017 * H
    shoulder = at(sh_x); shoulder.x = sh_x * sx
    tipp = a + axis * ((tip_x - a.x * sx) / max(axis.x * sx, 1e-3))
    wrist = tipp - axis * .108 * H
    elbow = shoulder + (wrist - shoulder) * .56
    hand_end = wrist + (tipp - wrist) * .6
    clav = Vector((.02 * H * sx, upper.y, .815 * H))
    arms[side] = (clav, shoulder, elbow, wrist, hand_end)
print("landmarks: crotch", round(crotch, 3), "hips", tuple(round(c, 3) for c in pelvis), "neck", round(neck.z, 3), "head", round(headj.z, 3))

# --- Skeleton --------------------------------------------------------------------------------
arm_data = bpy.data.armatures.new("Armature")
rig = bpy.data.objects.new("Armature", arm_data); scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones
def bone(name, head, tail, parent=None, deform=True):
    b = eb.new("mixamorig:" + name); b.head = head; b.tail = tail; b.use_deform = deform
    if parent:
        b.parent = eb["mixamorig:" + parent]; b.use_connect = (b.parent.tail - b.head).length < 1e-5
    return b
bone("Hips", pelvis, spine); bone("Spine", spine, chest, "Hips"); bone("Spine1", chest, upper, "Spine")
bone("Spine2", upper, neck, "Spine1"); bone("Neck", neck, headj, "Spine2"); bone("Head", headj, top, "Neck")
bone("HeadTop_End", top, top + Vector((0, 0, .03)), "Head", False)
for side in ("Left", "Right"):
    clav, shoulder, elbow, wrist, hand_end = arms[side]
    bone(side + "Shoulder", clav, shoulder, "Spine2"); bone(side + "Arm", shoulder, elbow, side + "Shoulder")
    bone(side + "ForeArm", elbow, wrist, side + "Arm"); bone(side + "Hand", wrist, hand_end, side + "ForeArm")
    hip, knee, ankle, ball, tip = legs[side]
    bone(side + "UpLeg", hip, knee, "Hips"); bone(side + "Leg", knee, ankle, side + "UpLeg")
    bone(side + "Foot", ankle, ball, side + "Leg"); bone(side + "ToeBase", ball, tip, side + "Foot")
    bone(side + "Toe_End", tip, tip + Vector((0, -.03, 0)), side + "ToeBase", False)
bpy.ops.object.mode_set(mode='OBJECT')

# --- Skinning: bone heat on a watertight voxel proxy, transferred to the real mesh ------------
proxy = body.copy(); proxy.data = body.data.copy(); scene.collection.objects.link(proxy)
rm = proxy.modifiers.new("Remesh", 'REMESH'); rm.mode = 'VOXEL'; rm.voxel_size = .006 * H / 1.8; rm.adaptivity = 0
bpy.context.view_layer.objects.active = proxy; bpy.ops.object.modifier_apply(modifier=rm.name)
for o in scene.objects: o.select_set(False)
proxy.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
print("proxy weighted", sum(1 for v in proxy.data.vertices if any(g.weight > 0 for g in v.groups)), "of", len(proxy.data.vertices))
for o in scene.objects: o.select_set(False)
body.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_NAME')
bpy.context.view_layer.objects.active = body
dt = body.modifiers.new("Weights", 'DATA_TRANSFER'); dt.object = proxy
dt.use_vert_data = True; dt.data_types_verts = {'VGROUP_WEIGHTS'}; dt.vert_mapping = 'POLYINTERP_NEAREST'
dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
bpy.ops.object.modifier_move_to_index(modifier=dt.name, index=0)
bpy.ops.object.modifier_apply(modifier=dt.name)
bpy.data.objects.remove(proxy)
# Eyes, teeth and other loose parts inside the head follow the head rigidly.
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
island = [-1] * len(bm.verts); sizes = []
for v in bm.verts:
    if island[v.index] >= 0: continue
    stack = [v]; island[v.index] = len(sizes); n = 0
    while stack:
        x = stack.pop(); n += 1
        for e in x.link_edges:
            o = e.other_vert(x)
            if island[o.index] < 0: island[o.index] = len(sizes); stack.append(o)
    sizes.append(n)
bm.free()
main = max(range(len(sizes)), key=lambda i: sizes[i])
head_group = body.vertex_groups["mixamorig:Head"]
loose = 0
for i, v in enumerate(me.vertices):
    if island[i] != main and v.co.z > .85 * H:
        for g in list(v.groups): body.vertex_groups[g.group].remove([i])
        head_group.add([i], 1, 'REPLACE'); loose += 1
print("islands", sorted(sizes, reverse=True)[:6], "head-rigid verts", loose)

bpy.ops.export_scene.fbx(filepath=dst, object_types={'ARMATURE', 'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', bake_space_transform=True, add_leaf_bones=False, bake_anim=False,
    use_armature_deform_only=False, mesh_smooth_type='FACE', path_mode='COPY', embed_textures=False)
print("exported", dst)
