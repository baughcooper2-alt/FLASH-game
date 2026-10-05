# Rigs the supplied posed Flash OBJ and unposes it to a Mixamo-named T-pose for Unity Humanoid retargeting.
#   blender -b --python Tools/Blender/rig_flash_realistic.py
# Joint positions (OBJ space: Y up, facing -Z) were placed by hand on front/side reference renders.
# Writes Assets/FlashPrototype/Character/Realistic/FlashRealistic.fbx; then run Flash > Configure realistic character in Unity.
import bpy, bmesh, json, math, os
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
HERE = os.path.dirname(os.path.abspath(__file__))
O = os.path.normpath(os.path.join(HERE, "../../Assets/FlashPrototype")) + "/"
OUT = O + "Character/Realistic/"
J = json.load(open(os.path.join(HERE, "flash_realistic_joints.json")))
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
bpy.ops.wm.obj_import(filepath=O + "ReferenceCharacter/FlashReference.obj")
body = scene.objects[0]; body.name = "FlashRealistic"; body.data.name = "FlashRealistic"
body.rotation_euler = (0, 0, 0)
# OBJ data is Y up, facing -Z, right = +X. Blender rigs face -Y with left = +X and Z up.
M = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
body.data.transform(M)
def P(n): return M @ Vector(J[n])

arm_data = bpy.data.armatures.new("Armature")
rig = bpy.data.objects.new("Armature", arm_data); scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones
def bone(name, head, tail, parent=None):
    b = eb.new("mixamorig:" + name); b.head = head; b.tail = tail
    if parent:
        b.parent = eb["mixamorig:" + parent]
        b.use_connect = (b.parent.tail - b.head).length < 1e-4
    return b
up = Vector((0, 0, .06))
bone("Hips", P("Hips"), P("Spine"))
bone("Spine", P("Spine"), P("Spine1"), "Hips")
bone("Spine1", P("Spine1"), P("Spine2"), "Spine")
bone("Spine2", P("Spine2"), P("Neck"), "Spine1")
bone("Neck", P("Neck"), P("Head"), "Spine2")
bone("Head", P("Head"), P("HeadTop"), "Neck")
bone("HeadTop_End", P("HeadTop"), P("HeadTop") + up, "Head")
for side, s in (("Left", "L"), ("Right", "R")):
    bone(side + "Shoulder", P(s + "Shoulder"), P(s + "Arm"), "Spine2")
    bone(side + "Arm", P(s + "Arm"), P(s + "ForeArm"), side + "Shoulder")
    bone(side + "ForeArm", P(s + "ForeArm"), P(s + "Hand"), side + "Arm")
    bone(side + "Hand", P(s + "Hand"), P(s + "HandEnd"), side + "ForeArm")
    bone(side + "UpLeg", P(s + "UpLeg"), P(s + "Leg"), "Hips")
    bone(side + "Leg", P(s + "Leg"), P(s + "Foot"), side + "UpLeg")
    bone(side + "Foot", P(s + "Foot"), P(s + "ToeBase"), side + "Leg")
    bone(side + "ToeBase", P(s + "ToeBase"), P(s + "ToeEnd"), side + "Foot")
    end = P(s + "ToeEnd"); d = (end - P(s + "ToeBase")).normalized() * .04
    bone(side + "Toe_End", end, end + d, side + "ToeBase")
# Hinge axes of the posed elbows and knees, measured before anything moves.
def hinge(a, b):
    h = (eb["mixamorig:" + a].tail - eb["mixamorig:" + a].head).normalized().cross(
        (eb["mixamorig:" + b].tail - eb["mixamorig:" + b].head).normalized())
    return h.normalized() if h.length > .2 else None
hinges = {}
for side in ("Left", "Right"):
    hinges[side + "Arm"] = hinge(side + "Arm", side + "ForeArm")
    hinges[side + "UpLeg"] = hinge(side + "UpLeg", side + "Leg")
for b in eb:
    if b.name.endswith(("Toe_End", "HeadTop_End")): b.use_deform = False
bpy.ops.object.mode_set(mode='OBJECT')

# Bone heat needs a watertight surface: weight a voxel-remeshed proxy, then transfer onto the real mesh.
proxy = body.copy(); proxy.data = body.data.copy(); proxy.name = "Weight proxy"; scene.collection.objects.link(proxy)
rm = proxy.modifiers.new("Remesh", 'REMESH'); rm.mode = 'VOXEL'; rm.voxel_size = .011; rm.adaptivity = 0
bpy.context.view_layer.objects.active = proxy; bpy.ops.object.modifier_apply(modifier=rm.name)
print("proxy verts", len(proxy.data.vertices))
for o in scene.objects: o.select_set(False)
proxy.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
weighted = sum(1 for v in proxy.data.vertices if any(g.weight > 0 for g in v.groups))
print("proxy weighted", weighted, "of", len(proxy.data.vertices))
for o in scene.objects: o.select_set(False)
body.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_NAME')   # empty groups named after the bones
bpy.context.view_layer.objects.active = body
dt = body.modifiers.new("Weights from proxy", 'DATA_TRANSFER'); dt.object = proxy
dt.use_vert_data = True; dt.data_types_verts = {'VGROUP_WEIGHTS'}; dt.vert_mapping = 'POLYINTERP_NEAREST'
dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
bpy.ops.object.modifier_move_to_index(modifier=dt.name, index=0)
bpy.ops.object.modifier_apply(modifier=dt.name)
bpy.data.objects.remove(proxy)

# Small loose parts (emblem, belt, ears, eyes, wrist decals) copy weights from the nearest skin surface.
me = body.data
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
island = [-1] * len(bm.verts); islands = []
for v in bm.verts:
    if island[v.index] >= 0: continue
    stack = [v]; island[v.index] = len(islands); members = []
    while stack:
        x = stack.pop(); members.append(x.index)
        for e in x.link_edges:
            o = e.other_vert(x)
            if island[o.index] < 0: island[o.index] = len(islands); stack.append(o)
    islands.append(members)
bm.free()
big = {i for i, m in enumerate(islands) if len(m) >= 1100 and len(m) != 1903}   # body, mask, face, pants (not the belt)
skin = [i for i in range(len(me.vertices)) if island[i] in big]
tree = KDTree(len(skin))
for n, i in enumerate(skin): tree.insert(me.vertices[i].co, i)
tree.balance()
groups = body.vertex_groups
def weights(i): return {g.group: g.weight for g in me.vertices[i].groups if g.weight > 0}
copied = 0
for i, v in enumerate(me.vertices):
    if island[i] in big: continue
    _, j, _ = tree.find(v.co)
    for g in list(v.groups): groups[g.group].remove([i])
    for gi, w in weights(j).items(): groups[gi].add([i], w, 'REPLACE')
    copied += 1
empty = [i for i, v in enumerate(me.vertices) if not any(g.weight > 0 for g in v.groups)]
for i in empty:
    _, j, _ = tree.find(me.vertices[i].co)
    for gi, w in weights(j).items(): groups[gi].add([i], w, 'REPLACE')
print("islands", sorted((len(m) for m in islands), reverse=True)[:8], "copied", copied, "unweighted fixed", len(empty))

# Joint regions get corrective smoothing so straightened knees and elbows don't pinch.
joints = groups.new(name="joints")
for i, v in enumerate(me.vertices):
    ws = sorted((g.weight for g in v.groups if groups[g.group].name.startswith("mixamorig:")), reverse=True)
    if ws and ws[0] < .9: joints.add([i], min(1, (.9 - ws[0]) * 4), 'REPLACE')
cs = body.modifiers.new("Joint smoothing", 'CORRECTIVE_SMOOTH')
cs.rest_source = 'ORCO'; cs.smooth_type = 'LENGTH_WEIGHTED'; cs.iterations = 10; cs.factor = .6; cs.vertex_group = "joints"

# Unpose into a T-pose: bone directions to targets, elbows/knees twisted so they hinge the right way.
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
targets = {"Hips": Z, "Spine": Z, "Spine1": Z, "Spine2": Z, "Neck": Z, "Head": Z}
hinge_targets = {}
for side, sx in (("Left", X), ("Right", -X)):
    for b in ("Shoulder", "Arm", "ForeArm", "Hand"): targets[side + b] = sx
    targets[side + "UpLeg"] = -Z; targets[side + "Leg"] = -Z
    targets[side + "Foot"] = Vector((0, -.75, -.66)).normalized(); targets[side + "ToeBase"] = -Y
    hinge_targets[side + "Arm"] = sx.cross(-Y)      # elbow flexes forward
    hinge_targets[side + "UpLeg"] = (-Z).cross(Y)   # knee flexes backward
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='POSE')
order = ["Hips", "Spine", "Spine1", "Spine2", "Neck", "Head"]
for side in ("Left", "Right"):
    order += [side + b for b in ("Shoulder", "Arm", "ForeArm", "Hand", "UpLeg", "Leg", "Foot", "ToeBase")]
rest_rot = {pb.name: pb.bone.matrix_local.to_3x3() for pb in rig.pose.bones}
for name in order:
    bpy.context.view_layer.update()
    pb = rig.pose.bones["mixamorig:" + name]
    m = pb.matrix.copy(); head = m.translation.copy()
    rot = m.to_3x3().normalized()
    d = (rot @ Y).normalized()
    q = d.rotation_difference(targets[name])
    new = q.to_matrix() @ rot
    if hinges.get(name) is not None:
        # The hinge axis moved with the bone; spin about the bone so it matches the target hinge.
        posed_hinge = (new @ rest_rot[pb.name].inverted() @ hinges[name]).normalized()
        axis = targets[name].normalized()
        a = (posed_hinge - axis * posed_hinge.dot(axis)).normalized()
        b = (hinge_targets[name] - axis * hinge_targets[name].dot(axis)).normalized()
        angle = math.atan2(a.cross(b).dot(axis), a.dot(b))
        new = Matrix.Rotation(angle, 3, axis) @ new
    pb.matrix = Matrix.Translation(head) @ new.to_4x4()
bpy.context.view_layer.update()
# Level each sole: rotate the foot about the ankle until heel and toe bottoms sit at the same height.
for side in ("Left", "Right"):
    foot = rig.pose.bones["mixamorig:" + side + "Foot"]
    names = {"mixamorig:" + side + "Foot", "mixamorig:" + side + "ToeBase"}
    ids = {g.index for g in body.vertex_groups if g.name in names}
    for attempt in range(3):
        bpy.context.view_layer.update()
        ev = body.evaluated_get(bpy.context.evaluated_depsgraph_get()).data
        # The shoe: everything near the ankle-to-toe segment and below the ankle.
        toes_pb = rig.pose.bones["mixamorig:" + side + "ToeBase"]
        a0, a1 = foot.head.copy(), toes_pb.tail.copy()
        seg = a1 - a0
        def near(p):
            t = max(0, min(1, (p - a0).dot(seg) / seg.length_squared))
            return (p - (a0 + seg * t)).length < .1 and p.z < a0.z + .03
        pts = [v.co.copy() for v in ev.vertices if near(v.co)]
        ys = sorted(p.y for p in pts); back, front = ys[int(len(ys) * .85)], ys[int(len(ys) * .15)]
        heel = min((p for p in pts if p.y >= back), key=lambda p: p.z)
        toe = min((p for p in pts if p.y <= front), key=lambda p: p.z)
        drop = math.atan2(heel.z - toe.z, heel.y - toe.y)
        head = foot.matrix.translation.copy()
        foot.matrix = Matrix.Translation(head) @ Matrix.Rotation(-drop, 4, 'X') @ Matrix.Translation(-head) @ foot.matrix
        print(side, "sole pitch", round(math.degrees(drop), 1), "points", len(pts))
    bpy.context.view_layer.update()
    toes = rig.pose.bones["mixamorig:" + side + "ToeBase"]
    m = toes.matrix.copy(); r = m.to_3x3().normalized()
    q = (r @ Y).normalized().rotation_difference(-Y)
    toes.matrix = Matrix.Translation(m.translation) @ (q.to_matrix() @ r).to_4x4()
bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode='OBJECT')

# Bake the T-pose into the mesh and make it the rest pose.
bpy.context.view_layer.objects.active = body
for mod in list(body.modifiers):
    bpy.ops.object.modifier_apply(modifier=mod.name)
body.vertex_groups.remove(body.vertex_groups["joints"])
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.armature_apply(selected=False); bpy.ops.object.mode_set(mode='OBJECT')
mod = body.modifiers.new("Armature", 'ARMATURE'); mod.object = rig
# Stand the rest pose on the ground: soles at z = 0.
dz = -min(v.co.z for v in body.data.vertices)
body.data.transform(Matrix.Translation((0, 0, dz)))
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
for b in rig.data.edit_bones: b.head.z += dz; b.tail.z += dz
bpy.ops.object.mode_set(mode='OBJECT')
print("raised by", round(dz, 3), "hips at", round(rig.data.bones["mixamorig:Hips"].head_local.z, 3))
# Boots get their own material slot (a copy of the legs material) so suits can colour them separately.
# The boot top is the top of each leg's gold lightning cuff.
bm = bmesh.new(); bm.from_mesh(body.data); bm.faces.ensure_lookup_table()
names = [m.name for m in body.data.materials]
gold, legs = names.index("Mat.3"), names.index("Mat.8")
cuffs, seen = {}, set()
for f in bm.faces:
    if f.material_index != gold or f.index in seen: continue
    stack, part = [f], []; seen.add(f.index)
    while stack:
        x = stack.pop(); part.append(x)
        for e in x.edges:
            for o in e.link_faces:
                if o.index not in seen and o.material_index == gold: seen.add(o.index); stack.append(o)
    zs = [v.co.z for p in part for v in p.verts]
    if max(zs) < .6:
        side = 1 if sum(v.co.x for p in part for v in p.verts) > 0 else -1
        cuffs[side] = max(cuffs.get(side, 0), max(zs))
boots_material = body.data.materials["Mat.8"].copy(); boots_material.name = "Mat.10"
body.data.materials.append(boots_material)
boots = len(body.data.materials) - 1
count = 0
for f in bm.faces:
    if f.material_index != legs: continue
    c = f.calc_center_median()
    if c.z < cuffs[1 if c.x > 0 else -1] + .005: f.material_index = boots; count += 1
bm.to_mesh(body.data); bm.free()
print("boots: cuff tops", {k: round(v, 3) for k, v in cuffs.items()}, "faces", count)
bpy.ops.export_scene.fbx(filepath=OUT + "FlashRealistic.fbx", object_types={'ARMATURE', 'MESH'}, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True, add_leaf_bones=False, bake_anim=False,
    use_armature_deform_only=False, mesh_smooth_type='FACE', path_mode='STRIP')
lo = [min(v.co[k] for v in body.data.vertices) for k in range(3)]; hi = [max(v.co[k] for v in body.data.vertices) for k in range(3)]
print("T-pose bounds", [round(x, 3) for x in lo], [round(x, 3) for x in hi])
