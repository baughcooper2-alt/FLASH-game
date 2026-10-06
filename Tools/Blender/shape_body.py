# Reshapes the Barry base mesh (a T-posed OBJ) toward an athletic adult build before it is rigged: fuller thighs,
# calves and glutes, thicker arms with rounded deltoids, and a sturdier neck. The supplied skinny base mesh is within
# normal measurements but at the thin end everywhere, which read as stick legs and pinched shoulders in game.
# Each region is scaled radially about its own limb axis (found by slicing the mesh), with smooth falloffs so
# nothing creases where regions meet.
#   blender -b --python Tools/Blender/shape_body.py -- <input.obj> <output.obj> [height_m]
import bpy, math, sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
src, dst = args[0], args[1]
H = float(args[2]) if len(args) > 2 else 1.83
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=src)
body = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
for o in bpy.context.scene.objects: o.select_set(o == body)
bpy.context.view_layer.objects.active = body
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = body.data
# Work in metres, feet on the floor, centred, facing -Y (the same normalisation rig_tpose_mesh.py applies).
lo = Vector((min(v.co.x for v in me.vertices), min(v.co.y for v in me.vertices), min(v.co.z for v in me.vertices)))
hi = Vector((max(v.co.x for v in me.vertices), max(v.co.y for v in me.vertices), max(v.co.z for v in me.vertices)))
s = H / (hi.z - lo.z)
centre = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
to_m = lambda c: (c - centre) * s
from_m = lambda p: p / s + centre
P = [to_m(v.co) for v in me.vertices]
flip = 1
feet = [p.y for p in P if p.z < .03 * H]; shins = [p.y for p in P if abs(p.z - .08 * H) < .01 * H]
if sum(feet) / len(feet) > sum(shins) / len(shins): flip = -1          # facing +Y: mirror y for the rules below
edges = [(e.vertices[0], e.vertices[1]) for e in me.edges]

def smooth(t): t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)
def bump(x, a, b, ramp):
    """1 inside [a, b], easing to 0 over `ramp` outside it."""
    return smooth((x - a + ramp) / ramp) * smooth((b + ramp - x) / ramp)
def cut(axis, value, keep):
    pts = []
    for i, j in edges:
        a, b = P[i], P[j]; fa, fb = a[axis] - value, b[axis] - value
        if (fa > 0) != (fb > 0):
            q = a + (b - a) * (fa / (fa - fb))
            if keep(q): pts.append(q)
    return pts
def mid(pts, i): return (min(p[i] for p in pts) + max(p[i] for p in pts)) / 2

# Landmarks (same rules as rig_tpose_mesh.py).
crotch = .40 * H
for k in range(400, 560):
    z = k / 1000 * H
    if cut(2, z, lambda q: abs(q.x) < .012 * H): break
    crotch = z
knee = .285 * H
shoulder_z = sum(p.z for p in P if abs(p.x) > .2 * H) / max(1, sum(1 for p in P if abs(p.x) > .2 * H))
chest_wall = max(abs(q.x) for q in cut(2, .70 * H, lambda q: abs(q.x) < .25 * H))
shoulder_x = chest_wall + .017 * H
tip_x = max(abs(p.x) for p in P)
wrist_x = tip_x - .108 * H
elbow_x = shoulder_x + (wrist_x - shoulder_x) * .56
neck_z = .845 * H
print("landmarks: crotch %.3f knee %.3f shoulder x %.3f z %.3f elbow x %.3f wrist x %.3f" % (crotch, knee, shoulder_x, shoulder_z, elbow_x, wrist_x))

# Limb axes: slice centres, sampled every 2 cm and interpolated.
def table(axis, values, keep, coords):
    rows = []
    for v in values:
        pts = cut(axis, v, keep)
        if len(pts) >= 6: rows.append((v, tuple(mid(pts, c) for c in coords)))
    return rows
def lookup(rows, v):
    if v <= rows[0][0]: return rows[0][1]
    for (a, ca), (b, cb) in zip(rows, rows[1:]):
        if v <= b:
            t = (v - a) / (b - a); return tuple(x + (y - x) * t for x, y in zip(ca, cb))
    return rows[-1][1]
zs = [z / 100 for z in range(4, int(crotch * 100))]
legs = {sx: table(2, zs, lambda q, sx=sx: q.x * sx > .004 and abs(q.x) < .2 * H, (0, 1)) for sx in (1, -1)}
xs = [x / 100 for x in range(int(shoulder_x * 100) - 4, int(wrist_x * 100) + 2)]
arms = {sx: table(0, [x * sx for x in xs], lambda q: q.z > .65 * H, (1, 2)) for sx in (1, -1)}
neck_rows = table(2, [z / 100 for z in range(int((neck_z - .05) * 100), int((neck_z + .04) * 100))], lambda q: abs(q.x) < .07 * H, (0, 1))

moved = 0
for idx, p in enumerate(P):
    q = p.copy()
    sx = 1 if p.x >= 0 else -1
    # Legs: scale about the leg's axis at this height. Thigh 14%, knee 7%, calf 11% (more at the back).
    if p.z < crotch + .02 and abs(p.x) < .2 * H and legs[sx]:
        cx, cy = lookup(legs[sx], p.z)
        d = Vector((p.x - cx, p.y - cy))
        thigh = bump(p.z, knee + .09, crotch - .07, .07)
        kneew = bump(p.z, knee - .03, knee + .03, .05)
        calf = bump(p.z, .13 * H, knee - .07, .06)
        back = smooth((d.y * flip) / .03)                                   # behind the shin (+Y faces back)
        f = 1 + .14 * thigh + .07 * kneew + calf * (.08 + .07 * back)
        q.x, q.y = cx + d.x * f, cy + d.y * f
    # Glutes: fuller and a little higher behind the hips.
    glute = bump(p.z, crotch - .02, crotch + .1, .06) * smooth((p.y * flip - .02) / .04) * smooth((abs(p.x) - .01) / .03) * smooth((.17 - abs(p.x)) / .04)
    q.y += flip * .018 * glute
    # Arms: scale about the arm's axis. Upper arm 15%, forearm 12% easing to 4% at the wrist.
    if abs(p.x) > shoulder_x - .06 and p.z > .65 * H and arms[sx]:
        cy, cz = lookup(arms[sx], p.x)
        d = Vector((0, p.y - cy, p.z - cz))
        ax = abs(p.x)
        upper = bump(ax, shoulder_x + .03, elbow_x - .03, .05)
        fore = bump(ax, elbow_x - .02, wrist_x - .03, .03) * (1 - .65 * smooth((ax - elbow_x) / (wrist_x - elbow_x)))
        f = 1 + .15 * upper + .12 * fore
        q.y, q.z = cy + d.y * f, cz + d.z * f
    # Deltoids: round the shoulder caps outward and up.
    dc = (Vector((p.x, p.y, p.z)) - Vector((sx * shoulder_x, 0, shoulder_z))).length
    delt = smooth((.11 - dc) / .07) * smooth((p.z - (shoulder_z - .08)) / .05)
    q += Vector((sx * .014, 0, .006)) * delt
    # Neck: 10% thicker, more at the sides and back where the trapezius meets it.
    if abs(p.z - neck_z) < .07 and abs(p.x) < .09 and neck_rows:
        cx, cy = lookup(neck_rows, p.z)
        w = bump(p.z, neck_z - .03, neck_z + .02, .04)
        q.x = cx + (q.x - cx) * (1 + .1 * w); q.y = cy + (q.y - cy) * (1 + .08 * w)
    if (q - p).length > 1e-6: moved += 1
    me.vertices[idx].co = from_m(q)
me.update()
print("reshaped vertices:", moved, "of", len(P))
bpy.ops.wm.obj_export(filepath=dst, export_selected_objects=True, export_materials=False, export_uv=True, export_normals=False)
print("written", dst)
