# Builds "CSI Barry" (plaid overshirt, white tee, jeans, sneakers, watch, dark swept-up hair) from the rigged
# Barry Allen base body, after the user's Meshy design and its reference renders.
#   geometry: blender -b --python Tools/Blender/build_csi_barry.py -- geometry <BarryBase.fbx> <work folder>
#   assemble: blender -b --python Tools/Blender/build_csi_barry.py -- assemble <work folder> <output.fbx>
# "geometry" grows the clothing layers from the body (clean cuts along smooth boundary functions), skins them,
# gives each its own UVs and writes each layer's rest-pose triangles (UV, position, normal) for
# Tools/csi_textures.py, which paints the textures. "assemble" applies those textures and exports the FBX.
import bpy, bmesh, math, os, sys, json
import numpy as np
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
MODE = args[0]
LAYERS = ("Body", "Shirt", "Tee", "Jeans", "Jacket", "Hair", "Shoes", "Watch")

def smooth(t): t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)

def cut(bm, field):
    """Removes the part of the mesh where field(position) > 0, cutting faces exactly along field == 0."""
    vals = {v: field(v.co) for v in bm.verts}
    crossing = {}
    for e in bm.edges:
        a, b = e.verts
        fa, fb = vals[a], vals[b]
        if (fa > 0) != (fb > 0):
            t = fa / (fa - fb)
            if .02 < t < .98: crossing[e] = t
    on_cut = {v for v, f in vals.items() if abs(f) < 1e-9}
    if crossing:
        ret = bmesh.ops.bisect_edges(bm, edges=list(crossing), cuts=1, edge_percents=crossing)
        for g in ret["geom_split"]:
            if isinstance(g, bmesh.types.BMVert):
                on_cut.add(g); vals[g] = 0.0
    for f in list(bm.faces):
        pts = [v for v in f.verts if v in on_cut]
        if len(pts) == 2 and not any(e.other_vert(pts[0]) == pts[1] for e in pts[0].link_edges):
            bmesh.ops.connect_verts(bm, verts=pts)
    # Snap: crossings near a vertex leave it slightly outside; classify faces by their non-cut vertices.
    gone = []
    for f in bm.faces:
        rest = [vals.get(v, field(v.co)) for v in f.verts if v not in on_cut]
        side = max(rest) if rest else field(f.calc_center_median())
        if side > 0: gone.append(f)
    bmesh.ops.delete(bm, geom=gone, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.normal_update()

def double_sided(bm, inset=.002, near=None):
    """Adds a flipped copy just inside the surface: game engines cull back faces, so open garments (fronts, collars,
    cuffs, hems) would otherwise show through to the far side. With `near`, only faces within that distance of an
    opening get an inside (the rest of a closed garment's inside can never be seen)."""
    bm.normal_update()
    faces = bm.faces[:]
    if near is not None:
        from mathutils.kdtree import KDTree
        edge = [v.co.copy() for v in bm.verts if v.is_boundary]
        tree = KDTree(len(edge))
        for i, c in enumerate(edge): tree.insert(c, i)
        tree.balance()
        faces = [f for f in faces if tree.find(f.calc_center_median())[2] < near]
    verts = {v for f in faces for v in f.verts}
    normals = {v: v.normal.copy() for v in verts}
    dup = bmesh.ops.duplicate(bm, geom=list(verts) + list({e for f in faces for e in f.edges}) + faces)
    for old, new in dup["vert_map"].items():
        if isinstance(old, bmesh.types.BMVert) and old in normals: new.co -= normals[old] * inset
    bmesh.ops.reverse_faces(bm, faces=[g for g in dup["geom"] if isinstance(g, bmesh.types.BMFace)])
    bm.normal_update()

if MODE == "geometry":
    BASE, WORK = args[1], args[2]
    os.makedirs(WORK, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    bpy.ops.import_scene.fbx(filepath=BASE)
    rig = next(o for o in scene.objects if o.type == 'ARMATURE')
    body = next(o for o in scene.objects if o.type == 'MESH')
    body.name = body.data.name = "Body"
    # Work in world space: bake the FBX import rotation and scale into the rig and the mesh.
    for o in scene.objects: o.select_set(o in (rig, body))
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    def bone_head(name): return rig.data.bones["mixamorig:" + name].head_local.copy()

    # Main body island (not the eyes, teeth and lashes inside the head).
    def islands():
        bm = bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
        island = [-1] * len(bm.verts); sizes = []
        for v in bm.verts:
            if island[v.index] >= 0: continue
            st = [v]; island[v.index] = len(sizes); n = 0
            while st:
                x = st.pop(); n += 1
                for e in x.link_edges:
                    o = e.other_vert(x)
                    if island[o.index] < 0: island[o.index] = len(sizes); st.append(o)
            sizes.append(n)
        bm.free()
        centres = [Vector() for _ in sizes]
        for v in body.data.vertices: centres[island[v.index]] += v.co
        return island, sizes, [c / n for c, n in zip(centres, sizes)]
    island, sizes, centres = islands()
    # The base mesh's eyelash shells stick out past the eye corners like wings: remove them (small islands at
    # eye height; the teeth are the small islands lower down).
    lash = {i for i, n in enumerate(sizes) if n < 100 and abs(centres[i].z - 1.712) < .015}
    if lash:
        bm = bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if island[v.index] in lash], context='VERTS')
        bm.to_mesh(body.data); bm.free()
        island, sizes, centres = islands()
    print("removed", len(lash), "eyelash islands")
    main = max(range(len(sizes)), key=lambda i: sizes[i])
    eyeballs = {i for i, n in enumerate(sizes) if 300 < n < 500 and centres[i].z > 1.68}

    def layer(name, field, displace, subdivide=0, near=None):
        obj = body.copy(); obj.data = body.data.copy(); obj.name = obj.data.name = name
        scene.collection.objects.link(obj)
        bm = bmesh.new(); bm.from_mesh(obj.data); bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if island[v.index] != main], context='VERTS')
        if subdivide:
            inside = (lambda f: abs(f) < near) if near else (lambda f: f < .03)
            bmesh.ops.subdivide_edges(bm, edges=[e for e in bm.edges if inside(field(e.verts[0].co)) or inside(field(e.verts[1].co))],
                                      cuts=subdivide, use_grid_fill=True, smooth=0.6)
        cut(bm, field)
        moved = {v: displace(v.co.copy(), v.normal.copy()) for v in bm.verts}
        for v, p in moved.items(): v.co = p
        bm.normal_update(); bm.to_mesh(obj.data); bm.free()
        return obj

    # --- Shirt: open-front overshirt, sleeves rolled past the elbow ------------------------------
    ELBOW_X = abs(bone_head("LeftForeArm").x)
    SLEEVE_END, HEM = ELBOW_X + .035, .905
    neck_y = bone_head("Neck").y
    def gap(z): return .036 + .022 * smooth((z - 1.33) / .16)        # half-width of the open front
    def neckline(p, lift=0.0):
        # Base of the neck: low at the front (the notch above the breastbone), higher at the back (C7). Above this
        # nothing is worn, so garments can never ride up over the jaw.
        # A rounded front: lowest at the centre, rising toward the sides of the neck.
        return p.z - (1.48 + lift + .022 * smooth(abs(p.x) / .075) + .065 * smooth((p.y + .06) / .12))
    def shirt_field(p):
        neck_hole = min(.072 - math.hypot(p.x, (p.y - neck_y + .012) / 1.15), p.z - 1.45)
        front_gap = min(gap(p.z) - abs(p.x), -(p.y + .025))          # positive inside the opening (front only)
        return max(HEM - p.z, abs(p.x) - SLEEVE_END, p.z - 1.62, neck_hole, front_gap, neckline(p, .03))
    def shirt_displace(p, n):
        t = .016 if abs(p.x) < .19 else .022                          # clear of the tee underneath
        q = p + n * t
        hang = smooth((1.03 - p.z) / .1)                              # loose over the hips
        r = Vector((p.x, p.y + .005, 0))
        if r.length > 1e-4: q += r.normalized() * hang * .02
        if p.y < -.03 and abs(p.x) < .14:                             # open fronts stand off the tee
            q.y -= .008 * smooth((1.42 - p.z) / .2)
        return q
    shirt = layer("Shirt", shirt_field, shirt_displace, subdivide=1)
    bm = bmesh.new(); bm.from_mesh(shirt.data); bm.normal_update()
    boundary = [e for e in bm.edges if e.is_boundary]
    # Collar: the neckline and the top of the opening stand up and roll outward.
    collar = [e for e in boundary if all(v.co.z > 1.36 for v in e.verts)]
    ret = bmesh.ops.extrude_edge_only(bm, edges=collar)
    for v in {g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)}:
        out = Vector((v.co.x, v.co.y - neck_y, 0)); out = out.normalized() if out.length > 1e-4 else Vector((0, -1, 0))
        stand = smooth((v.co.z - 1.47) / .06)
        v.co += Vector((0, 0, .03 * stand)) + out * (.012 + .03 * (1 - stand))
    # Rolled cuffs: each sleeve end folds back toward the shoulder.
    for side in (1, -1):
        ring = [e for e in boundary if all(v.co.x * side > SLEEVE_END - .02 for v in e.verts)]
        if not ring: continue
        ret = bmesh.ops.extrude_edge_only(bm, edges=ring)
        new = {g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)}
        c = sum((v.co for v in new), Vector()) / len(new)
        for v in new:
            out = Vector((0, v.co.y - c.y, v.co.z - c.z)).normalized()
            v.co += Vector((-side * .05, 0, 0)) + out * .01
    double_sided(bm); bm.to_mesh(shirt.data); bm.free()

    # --- Tee, jeans and jacket ------------------------------------------------------------------
    # Real garments over the body instead of colour painted on the skin, so the silhouette has cloth volume:
    # a tee that hangs over the waistband, straight-leg jeans and (for townspeople) a long-sleeved jacket.
    SHOULDER_X, WRIST_X = abs(bone_head("LeftArm").x), abs(bone_head("LeftHand").x)
    KNEE_Z = bone_head("LeftLeg").z
    def neck_hole(p, r): return min(r - math.hypot(p.x, (p.y - neck_y + .012) / 1.15), p.z - 1.45)
    TEE_HEM, TEE_SLEEVE = .895, SHOULDER_X + .16
    def tee_field(p): return max(TEE_HEM - p.z, abs(p.x) - TEE_SLEEVE, p.z - 1.62, neck_hole(p, .068), neckline(p))
    def tee_displace(p, n):
        loose = smooth((1.0 - p.z) / .06)                              # hangs clear of the jeans waistband
        sleeve = smooth((abs(p.x) - SHOULDER_X) / .04)
        return p + n * (.005 + .019 * loose + .003 * sleeve)
    tee = layer("Tee", tee_field, tee_displace, subdivide=1, near=.04)
    JEANS_WAIST, JEANS_HEM = .945, .085
    # Each leg's axis and half-width by height, for a straight leg below the knee.
    legs = {}
    for sx in (1, -1):
        rows = []
        for k in range(6, 82, 2):
            z = k / 100
            pts = [v.co for v in body.data.vertices if island[v.index] == main and abs(v.co.z - z) < .012 and v.co.x * sx > .004 and abs(v.co.x) < .25]
            if len(pts) > 4:
                rows.append((z, (min(p.x for p in pts) + max(p.x for p in pts)) / 2, (min(p.y for p in pts) + max(p.y for p in pts)) / 2))
        legs[sx] = rows
    def leg_at(sx, z):
        rows = legs[sx]
        if z <= rows[0][0]: return rows[0][1:]
        for a, b in zip(rows, rows[1:]):
            if z <= b[0]:
                t = (z - a[0]) / (b[0] - a[0]); return (a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)
        return rows[-1][1:]
    knee_r = {sx: max(Vector((v.co.x - leg_at(sx, KNEE_Z)[0], v.co.y - leg_at(sx, KNEE_Z)[1])).length
                      for v in body.data.vertices if island[v.index] == main and abs(v.co.z - KNEE_Z) < .01 and v.co.x * sx > .004 and abs(v.co.x) < .25)
              for sx in (1, -1)}
    def jeans_field(p): return max(JEANS_HEM - p.z, p.z - JEANS_WAIST)
    def jeans_displace(p, n):
        q = p + n * (.009 + .002 * smooth((p.z - .87) / .05))         # the waistband sits a little proud
        if p.z < KNEE_Z + .12 and abs(p.x) > .004:
            sx = 1 if p.x > 0 else -1
            cx, cy = leg_at(sx, p.z)
            d = Vector((q.x - cx, q.y - cy)); r = d.length
            target = knee_r[sx] + .011                                   # as wide as at the knee, down to the hem
            w = smooth((KNEE_Z + .12 - p.z) / .14)
            if r > 1e-4 and r < target: d *= (r + (target - r) * w * .85) / r
            q.x, q.y = cx + d.x, cy + d.y
        return q
    jeans = layer("Jeans", jeans_field, jeans_displace, subdivide=1, near=.04)
    JACKET_HEM, JACKET_CUFF = .835, WRIST_X - .025
    def jacket_field(p): return max(JACKET_HEM - p.z, abs(p.x) - JACKET_CUFF, p.z - 1.63, neck_hole(p, .074), neckline(p, .01))
    def jacket_displace(p, n): return p + n * (.017 + .012 * smooth((1.0 - p.z) / .1) + .004 * smooth((abs(p.x) - SHOULDER_X) / .05))
    jacket = layer("Jacket", jacket_field, jacket_displace, subdivide=1, near=.04)
    bm = bmesh.new(); bm.from_mesh(jacket.data); bm.normal_update()
    ret = bmesh.ops.extrude_edge_only(bm, edges=[e for e in bm.edges if e.is_boundary and all(v.co.z > 1.44 for v in e.verts)])
    for v in {g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)}:   # a short stand-up collar, lower in front
        v.co += Vector((0, 0, .022 + .016 * smooth((v.co.y + .04) / .1))) + Vector((v.co.x, v.co.y - neck_y, 0)).normalized() * .004
    double_sided(bm, near=.12); bm.to_mesh(jacket.data); bm.free()
    for obj in (tee, jeans):
        bm = bmesh.new(); bm.from_mesh(obj.data); double_sided(bm, near=.12); bm.to_mesh(obj.data); bm.free()
    # Skin wholly under the tee and jeans is removed after skinning (below) so it can never poke through.
    def hidden(p, margin=.012): return tee_field(p) < -margin or jeans_field(p) < -margin

    # --- Hair: short sides, volume on top swept up and back from the forehead --------------------
    head = [v.co for v in body.data.vertices if v.co.z > 1.66 and island[v.index] == main]
    HF, HB = min(c.y for c in head), max(c.y for c in head)
    HC = Vector((0, (HF + HB) / 2, 1.70))                               # head centre for angles
    EAR_Y = sum(c.y for c in head if abs(c.x) > .075) / max(1, sum(1 for c in head if abs(c.x) > .075))
    EAR_TOP = max(c.z for c in head if abs(c.x) > .078 and abs(c.y - EAR_Y) < .03)
    def hairline(p):
        # A rounded forehead hairline; at the temples it drops to sideburns and runs just above the ears, then
        # down the back of the head to the nape.
        t = (p.y - HF) / (HB - HF)                                     # 0 at the brow, 1 at the back
        side = smooth((abs(p.x) - .04) / .035)
        front = 1.772 + .006 * (abs(p.x) / .04) ** 2
        sides = EAR_TOP + .004 - .03 * smooth((.2 - t) / .12) - .105 * smooth((t - .55) / .4)
        ear = smooth((abs(p.x) - .06) / .015) * math.exp(-((p.y - EAR_Y) / .03) ** 4)
        return front * (1 - side) + sides * side + .006 * ear
    def hair_field(p): return hairline(p) - p.z if abs(p.x) < .11 and p.z > 1.55 else 1.0
    def hair_displace(p, n):
        depth = smooth((p.z - hairline(p)) / .06)                       # tapers to nothing at the hairline
        side = smooth((abs(p.x) - .045) / .035)
        top = smooth((p.z - 1.785) / .04)
        quiff = smooth((.06 - (p.y - HF)) / .06) * smooth((p.z - 1.77) / .03)
        thick = depth * (.004 + (.014 + .02 * quiff) * (1 - side) * (.5 + .5 * top) + .004 * top)
        clump = .003 * depth * math.sin(p.x * 260 + math.sin(p.y * 90) * 2.5) * math.sin(p.y * 70 + p.x * 40)
        q = p + n * (.002 + max(0.0, thick + clump) * (1 - .55 * quiff))
        # The quiff rises and sweeps back instead of overhanging the forehead.
        return q + Vector((0, .008, .024)) * quiff * depth
    hair = layer("Hair", hair_field, hair_displace, subdivide=2)
    hb = bmesh.new(); hb.from_mesh(hair.data)
    inner = [v for v in hb.verts if not v.is_boundary]
    for _ in range(6): bmesh.ops.smooth_vert(hb, verts=inner, factor=.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    hb.normal_update(); hb.to_mesh(hair.data); hb.free()

    # --- Shoes: a smoothed shell over each foot (low-top sneakers), with thick soles ---------------
    shoes_bm = bmesh.new()
    for side in (1, -1):
        pts = [v.co.copy() for v in body.data.vertices if island[v.index] == main and v.co.z < .13 and v.co.x * side > 0]
        part = bmesh.new()
        for p in pts: part.verts.new(p)
        bmesh.ops.convex_hull(part, input=part.verts[:])
        bmesh.ops.delete(part, geom=[v for v in part.verts if not v.link_faces], context='VERTS')
        bmesh.ops.subdivide_edges(part, edges=part.edges[:], cuts=2, use_grid_fill=True)
        bmesh.ops.triangulate(part, faces=part.faces[:])
        c = sum((v.co for v in part.verts), Vector()) / len(part.verts)
        for v in part.verts:                                             # round it into a shoe
            d = v.co - c
            v.co = c + Vector((d.x * 1.12, d.y * 1.06, d.z))
            v.co.z = v.co.z - .022 if v.co.z < .02 else v.co.z
        tmp = bpy.data.meshes.new("tmp"); part.to_mesh(tmp); part.free()
        shoes_bm.from_mesh(tmp); bpy.data.meshes.remove(tmp)
    cut(shoes_bm, lambda p: p.z - (.105 - .03 * smooth((-p.y - .02) / .1)))   # low top, open over the ankle
    for _ in range(4): bmesh.ops.smooth_vert(shoes_bm, verts=shoes_bm.verts[:], factor=.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    sm = bpy.data.meshes.new("Shoes"); shoes_bm.normal_update(); shoes_bm.to_mesh(sm); shoes_bm.free()
    shoes = bpy.data.objects.new("Shoes", sm); scene.collection.objects.link(shoes)

    # --- Watch on the right wrist: a strap ring and a round face -----------------------------------
    wrist, elbow = bone_head("RightHand"), bone_head("RightForeArm")
    axis = (wrist - elbow).normalized()
    at = wrist - axis * .03
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=.031, depth=.018, location=at)
    strap = bpy.context.object
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=.018, depth=.008, location=at - Vector((0, .028, 0)))
    face = bpy.context.object; face.rotation_euler = (math.pi / 2, 0, 0)
    strap.rotation_euler = axis.to_track_quat('Z', 'Y').to_euler()
    for o in scene.objects: o.select_set(o in (strap, face))
    bpy.context.view_layer.objects.active = strap
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.join()
    watch = bpy.context.object; watch.name = watch.data.name = "Watch"

    # Skin weights from the body for everything new; fresh UVs for every layer but the body.
    for obj in (shirt, tee, jeans, jacket, hair, shoes, watch):
        obj.vertex_groups.clear()
        for o in scene.objects: o.select_set(o in (obj, rig))
        bpy.context.view_layer.objects.active = rig
        obj.parent = None
        bpy.ops.object.parent_set(type='ARMATURE_NAME')
        for m in list(obj.modifiers):
            if m.type == 'ARMATURE': m.object = rig
        bpy.context.view_layer.objects.active = obj
        dt = obj.modifiers.new("Weights", 'DATA_TRANSFER'); dt.object = body
        dt.use_vert_data = True; dt.data_types_verts = {'VGROUP_WEIGHTS'}; dt.vert_mapping = 'POLYINTERP_NEAREST'
        dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
        bpy.ops.object.modifier_move_to_index(modifier=dt.name, index=0)
        bpy.ops.object.modifier_apply(modifier=dt.name)
        while obj.data.uv_layers: obj.data.uv_layers.remove(obj.data.uv_layers[0])
        obj.data.uv_layers.new(name="UVMap")
        for o in scene.objects: o.select_set(o == obj)
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(55), island_margin=.006)
        bpy.ops.object.mode_set(mode='OBJECT')

    isl = body.data.attributes.new("island", 'INT', 'POINT')
    for i, v in enumerate(island): isl.data[i].value = v
    bm = bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
    gone = [f for f in bm.faces if all(island[v.index] == main and hidden(v.co) for v in f.verts)]
    bmesh.ops.delete(bm, geom=gone, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(body.data); bm.free()
    island = [d.value for d in body.data.attributes["island"].data]
    body.data.attributes.remove(body.data.attributes["island"])
    print("hidden skin faces removed:", len(gone))

    # Texture data: every layer's triangles in UV space with rest-pose positions and normals.
    info = {}
    for obj in (body, shirt, tee, jeans, jacket, hair, shoes, watch):
        me = obj.data; me.calc_loop_triangles()
        uv = me.uv_layers.active.data
        tri = np.array([[l for l in t.loops] for t in me.loop_triangles])
        loops_vert = np.array([l.vertex_index for l in me.loops])
        co = np.array([v.co[:] for v in me.vertices]); nrm = np.array([v.normal[:] for v in me.vertices])
        uvs = np.array([d.uv[:] for d in uv])
        if obj is body:
            isl = np.array(island)[loops_vert[tri][:, 0]]
            part = np.where(isl == main, 0, np.where(np.isin(isl, list(eyeballs)), 1, 2))
        else:
            part = np.zeros(len(tri), int)
        np.savez_compressed(os.path.join(WORK, obj.name + ".npz"), uv=uvs[tri], pos=co[loops_vert[tri]], nrm=nrm[loops_vert[tri]], part=part)
        info[obj.name] = {"verts": len(me.vertices), "tris": len(tri)}
    # Body landmarks for the face warp: eye centres (eyeball islands) and the scalp/face extents.
    eyes = {}
    for i in eyeballs: eyes["right" if centres[i].x < 0 else "left"] = centres[i][:]
    info["eyes"] = eyes
    info["hair"] = {"front": HF, "back": HB, "ear_y": EAR_Y, "ear_top": EAR_TOP}
    info["garments"] = {"neck_y": neck_y, "tee_hem": TEE_HEM, "tee_sleeve": TEE_SLEEVE, "jeans_waist": JEANS_WAIST, "jeans_hem": JEANS_HEM,
                        "jacket_hem": JACKET_HEM, "jacket_cuff": JACKET_CUFF, "knee_z": KNEE_Z, "shoulder_x": SHOULDER_X}
    json.dump(info, open(os.path.join(WORK, "layers.json"), "w"), indent=1)
    print("layers", json.dumps({k: v for k, v in info.items() if k != "eyes"}), "eyes", eyes)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(WORK, "csi_layers.blend"))

if MODE == "assemble":
    WORK, OUT = args[1], args[2]
    bpy.ops.wm.open_mainfile(filepath=os.path.join(WORK, "csi_layers.blend"))
    scene = bpy.context.scene
    finish = {"Body": (.0, .42), "Shirt": (.0, .3), "Tee": (.0, .25), "Jeans": (.0, .3), "Jacket": (.0, .35), "Hair": (.0, .45), "Shoes": (.0, .3), "Watch": (.6, .55)}
    for name in LAYERS:
        obj = scene.objects[name]
        mat = bpy.data.materials.new("Barry " + name.lower()); mat.use_nodes = True
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(os.path.join(WORK, name + ".png")); tex.image.name = "Barry_" + name
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Metallic"].default_value, rough = finish[name][0], finish[name][1]
        bsdf.inputs["Roughness"].default_value = 1 - rough
        obj.data.materials.clear(); obj.data.materials.append(mat)
    if OUT.endswith(".blend"):
        bpy.ops.wm.save_as_mainfile(filepath=OUT)
    else:
        for o in scene.objects: o.select_set(o.type in ('MESH', 'ARMATURE'))
        bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={'ARMATURE', 'MESH'}, apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True, add_leaf_bones=False,
            bake_anim=False, use_armature_deform_only=False, mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)
    print("assembled", OUT)
