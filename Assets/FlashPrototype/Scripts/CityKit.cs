using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    // Builds city detail straight into combined meshes (one per material and 520 m cell) instead of thousands of
    // GameObjects, and adds plain box colliders only where something must be solid. Thousands of props, storefronts
    // and pieces of litter cost a few draw calls and no per-object overhead.
    public sealed class CityKit
    {
        sealed class Bucket
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();
            public readonly List<Color> Colors = new List<Color>();
        }
        const float Cell = 520;
        readonly Dictionary<(Material, int, int, bool), Bucket> buckets = new Dictionary<(Material, int, int, bool), Bucket>();
        // Solid boxes become one static mesh collider per cell (thousands of BoxCollider objects were slow to create).
        // Low solids (cars, dumpsters, counters) get their own colliders: SpeedsterMotor only wall-runs surfaces whose
        // collider is at least 4 m tall, and merging them with buildings would make every parked car a wall.
        readonly Dictionary<(int, int, bool), (List<Vector3> v, List<int> t)> solids = new Dictionary<(int, int, bool), (List<Vector3>, List<int>)>();
        readonly Dictionary<Material, (bool plain, Color tint)> plainCache = new Dictionary<Material, (bool, Color)>();
        readonly Transform root, colliders;
        readonly Material painted;
        Color tint = Color.white;
        public int Solids { get; private set; }

        // `painted` (optional) is a material with _VertexTint on: plain single-colour materials are merged into it,
        // their colour and glow written into the vertices, so all plain props in a cell are a single draw call.
        public CityKit(Transform parent, string name, Material painted = null)
        {
            this.painted = painted;
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
            colliders = new GameObject(name + " colliders").transform;
            colliders.SetParent(root, false);
        }

        Bucket For(Material m, Vector3 at, bool shadows)
        {
            tint = Color.white;
            if (painted != null)
            {
                if (!plainCache.TryGetValue(m, out var info))
                {
                    var c = m.GetColor("_BaseColor");
                    plainCache[m] = info = (m.GetFloat("_Surface") < .5f, new Color(c.r, c.g, c.b, m.GetFloat("_Glow")));
                }
                if (info.plain) { tint = info.tint; m = painted; }
            }
            var key = (m, Mathf.FloorToInt(at.x / Cell), Mathf.FloorToInt(at.z / Cell), shadows);
            if (!buckets.TryGetValue(key, out var b)) buckets[key] = b = new Bucket();
            return b;
        }

        void Face(Bucket b, Vector3 a, Vector3 c, Vector3 d, Vector3 e, Vector3 n)
        {
            int i = b.Vertices.Count;
            b.Vertices.Add(a); b.Vertices.Add(c); b.Vertices.Add(d); b.Vertices.Add(e);
            b.Normals.Add(n); b.Normals.Add(n); b.Normals.Add(n); b.Normals.Add(n);
            for (int k = 0; k < 4; k++) b.Colors.Add(tint);
            b.Triangles.Add(i); b.Triangles.Add(i + 1); b.Triangles.Add(i + 2);
            b.Triangles.Add(i); b.Triangles.Add(i + 2); b.Triangles.Add(i + 3);
        }

        // A box (centre, full size, rotation). `solid` adds a matching collider; `shadows` false for small clutter.
        public void Box(Material m, Vector3 centre, Vector3 size, Quaternion rotation, bool solid = false, bool shadows = true)
        {
            var b = For(m, centre, shadows);
            Vector3 hx = rotation * new Vector3(size.x / 2, 0, 0), hy = rotation * new Vector3(0, size.y / 2, 0), hz = rotation * new Vector3(0, 0, size.z / 2);
            Vector3 X = hx.normalized, Y = hy.normalized, Z = hz.normalized;
            Face(b, centre + hx - hy - hz, centre + hx + hy - hz, centre + hx + hy + hz, centre + hx - hy + hz, X);
            Face(b, centre - hx - hy + hz, centre - hx + hy + hz, centre - hx + hy - hz, centre - hx - hy - hz, -X);
            Face(b, centre - hx + hy - hz, centre - hx + hy + hz, centre + hx + hy + hz, centre + hx + hy - hz, Y);
            Face(b, centre - hx - hy + hz, centre - hx - hy - hz, centre + hx - hy - hz, centre + hx - hy + hz, -Y);
            Face(b, centre + hx - hy + hz, centre + hx + hy + hz, centre - hx + hy + hz, centre - hx - hy + hz, Z);
            Face(b, centre - hx - hy - hz, centre - hx + hy - hz, centre + hx + hy - hz, centre + hx - hy - hz, -Z);
            if (solid) Solid(centre, size, rotation);
        }
        public void Box(Material m, Vector3 centre, Vector3 size, bool solid = false, bool shadows = true) => Box(m, centre, size, Quaternion.identity, solid, shadows);
        public void Box(Material m, Vector3 centre, Vector3 size, float yaw, bool solid = false, bool shadows = true) => Box(m, centre, size, Quaternion.Euler(0, yaw, 0), solid, shadows);

        // A box spanning two points along its long axis (posts, rails, struts, ladders).
        public void Beam(Material m, Vector3 a, Vector3 c, float width, float depth, bool shadows = true)
        {
            Vector3 d = c - a;
            if (d.sqrMagnitude < 1e-6f) return;
            var rotation = Quaternion.LookRotation(d.normalized, Mathf.Abs(d.normalized.y) > .99f ? Vector3.forward : Vector3.up);
            Box(m, (a + c) / 2, new Vector3(width, depth, d.magnitude), rotation, false, shadows);
        }

        // An upright cylinder (or a lying one with `axis`), capped.
        public void Cylinder(Material m, Vector3 centre, float radius, float height, int sides = 10, bool solid = false, bool shadows = true, Vector3? axis = null)
        {
            var b = For(m, centre, shadows);
            Vector3 up = (axis ?? Vector3.up).normalized;
            Vector3 side = Vector3.Cross(up, Mathf.Abs(up.y) > .9f ? Vector3.right : Vector3.up).normalized, side2 = Vector3.Cross(up, side);
            Vector3 top = centre + up * height / 2, bottom = centre - up * height / 2;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 r0 = (side * Mathf.Cos(a0) + side2 * Mathf.Sin(a0)), r1 = (side * Mathf.Cos(a1) + side2 * Mathf.Sin(a1));
                int v = b.Vertices.Count;
                b.Vertices.Add(bottom + r0 * radius); b.Vertices.Add(top + r0 * radius); b.Vertices.Add(top + r1 * radius); b.Vertices.Add(bottom + r1 * radius);
                b.Normals.Add(r0); b.Normals.Add(r0); b.Normals.Add(r1); b.Normals.Add(r1);
                for (int k = 0; k < 4; k++) b.Colors.Add(tint);
                b.Triangles.Add(v); b.Triangles.Add(v + 2); b.Triangles.Add(v + 1); b.Triangles.Add(v); b.Triangles.Add(v + 3); b.Triangles.Add(v + 2);
                int c = b.Vertices.Count;
                b.Vertices.Add(top); b.Vertices.Add(top + r1 * radius); b.Vertices.Add(top + r0 * radius);
                b.Normals.Add(up); b.Normals.Add(up); b.Normals.Add(up);
                for (int k = 0; k < 3; k++) b.Colors.Add(tint);
                b.Triangles.Add(c); b.Triangles.Add(c + 1); b.Triangles.Add(c + 2);
                c = b.Vertices.Count;
                b.Vertices.Add(bottom); b.Vertices.Add(bottom + r0 * radius); b.Vertices.Add(bottom + r1 * radius);
                b.Normals.Add(-up); b.Normals.Add(-up); b.Normals.Add(-up);
                for (int k = 0; k < 3; k++) b.Colors.Add(tint);
                b.Triangles.Add(c); b.Triangles.Add(c + 1); b.Triangles.Add(c + 2);
            }
            if (solid) Solid(centre, new Vector3(radius * 1.6f, height, radius * 1.6f), Quaternion.FromToRotation(Vector3.up, up));
        }

        // A lumpy sack (trash bags): a squashed, slightly irregular octahedral blob.
        public void Sack(Material m, Vector3 bottom, float size, float seed)
        {
            var b = For(m, bottom, false);
            Vector3 c = bottom + Vector3.up * size * .42f;
            float h = size * .45f, w = size * (.5f + .1f * Mathf.Sin(seed * 7));
            Vector3[] ring = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3 + seed;
                ring[i] = c + new Vector3(Mathf.Cos(a), (Mathf.Sin(seed * 3 + i) * .15f), Mathf.Sin(a)) * w;
            }
            Vector3 top = c + Vector3.up * h * (1.1f + .2f * Mathf.Sin(seed)), floor = bottom + Vector3.up * .02f;
            for (int i = 0; i < 6; i++)
            {
                Vector3 r0 = ring[i], r1 = ring[(i + 1) % 6];
                Tri(b, top, r1, r0); Tri(b, floor, r0, r1);
            }
        }
        void Tri(Bucket b, Vector3 a, Vector3 c, Vector3 d)
        {
            Vector3 n = Vector3.Cross(c - a, d - a).normalized;
            int i = b.Vertices.Count;
            b.Vertices.Add(a); b.Vertices.Add(c); b.Vertices.Add(d);
            b.Normals.Add(n); b.Normals.Add(n); b.Normals.Add(n);
            for (int k = 0; k < 3; k++) b.Colors.Add(tint);
            b.Triangles.Add(i); b.Triangles.Add(i + 1); b.Triangles.Add(i + 2);
        }

        // A flat decal-like quad just above a surface (litter, stains, puddles, road paint).
        public void Flat(Material m, Vector3 centre, Vector2 size, float yaw, float lift = .012f)
        {
            var b = For(m, centre, false);
            var r = Quaternion.Euler(0, yaw, 0);
            Vector3 x = r * new Vector3(size.x / 2, 0, 0), z = r * new Vector3(0, 0, size.y / 2), p = centre + Vector3.up * lift;
            Face(b, p - x - z, p - x + z, p + x + z, p + x - z, Vector3.up);
        }

        // A vertical quad facing `normal` (posters, graffiti, signs) with the given width and height.
        public void Panel(Material m, Vector3 centre, Vector3 normal, float width, float height, bool shadows = false)
        {
            var b = For(m, centre, shadows);
            Vector3 right = Vector3.Cross(Vector3.up, normal).normalized * width / 2, up = Vector3.up * height / 2;
            Face(b, centre - right - up, centre - right + up, centre + right + up, centre + right - up, normal.normalized);
        }

        public void Solid(Vector3 centre, Vector3 size, Quaternion rotation)
        {
            Vector3 hx = rotation * new Vector3(size.x / 2, 0, 0), hy = rotation * new Vector3(0, size.y / 2, 0), hz = rotation * new Vector3(0, 0, size.z / 2);
            bool low = centre.y + Mathf.Abs(hx.y) + Mathf.Abs(hy.y) + Mathf.Abs(hz.y) < 3.6f;   // tops below 3.6 m: the set stays under 4 m tall
            var key = (Mathf.FloorToInt(centre.x / Cell), Mathf.FloorToInt(centre.z / Cell), low);
            if (!solids.TryGetValue(key, out var data)) solids[key] = data = (new List<Vector3>(), new List<int>());
            int i = data.v.Count;
            for (int k = 0; k < 8; k++)
                data.v.Add(centre + ((k & 1) != 0 ? hx : -hx) + ((k & 2) != 0 ? hy : -hy) + ((k & 4) != 0 ? hz : -hz));
            // Outward-facing triangles for the six faces (corner index bits: x=1, y=2, z=4).
            int[] faces = { 1, 3, 7, 5, 0, 4, 6, 2, 2, 6, 7, 3, 0, 1, 5, 4, 4, 5, 7, 6, 0, 2, 3, 1 };
            for (int f = 0; f < 6; f++)
            {
                int a = i + faces[f * 4], b = i + faces[f * 4 + 1], c = i + faces[f * 4 + 2], d = i + faces[f * 4 + 3];
                data.t.Add(a); data.t.Add(b); data.t.Add(c); data.t.Add(a); data.t.Add(c); data.t.Add(d);
            }
            Solids++;
        }

        // A smooth ellipsoid (tree crowns), `rings` x `segments` facets with per-vertex normals.
        public void Ellipsoid(Material m, Vector3 centre, Vector3 radii, int rings = 6, int segments = 9, bool shadows = true)
        {
            var b = For(m, centre, shadows);
            int start = b.Vertices.Count;
            for (int r = 0; r <= rings; r++)
            {
                float v = Mathf.PI * r / rings;
                for (int sgm = 0; sgm <= segments; sgm++)
                {
                    float u = 2 * Mathf.PI * sgm / segments;
                    var n = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                    b.Vertices.Add(centre + Vector3.Scale(n, radii));
                    b.Normals.Add(new Vector3(n.x / radii.x, n.y / radii.y, n.z / radii.z).normalized);
                    b.Colors.Add(tint);
                }
            }
            for (int r = 0; r < rings; r++)
                for (int sgm = 0; sgm < segments; sgm++)
                {
                    int a = start + r * (segments + 1) + sgm, c = a + segments + 1;
                    b.Triangles.Add(a); b.Triangles.Add(a + 1); b.Triangles.Add(c);
                    b.Triangles.Add(a + 1); b.Triangles.Add(c + 1); b.Triangles.Add(c);
                }
        }

        public Transform Build()
        {
            foreach (var pair in buckets)
            {
                var (material, _, _, shadows) = pair.Key;
                var data = pair.Value;
                if (data.Triangles.Count == 0) continue;
                var mesh = new Mesh { name = "City detail", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(data.Vertices); mesh.SetNormals(data.Normals); mesh.SetColors(data.Colors); mesh.SetTriangles(data.Triangles, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);       // not readable: CityDistrict's merge pass leaves it alone
                var go = new GameObject("City detail: " + material.name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<RuntimeMeshOwner>().Mesh = mesh;
            }
            buckets.Clear();
            foreach (var pair in solids)
            {
                var mesh = new Mesh { name = "City colliders", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(pair.Value.v); mesh.SetTriangles(pair.Value.t, 0); mesh.RecalculateBounds();
                var go = new GameObject(pair.Key.Item3 ? "City low colliders" : "City colliders");
                go.transform.SetParent(colliders, false);
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                go.AddComponent<RuntimeMeshOwner>().Mesh = mesh;
            }
            solids.Clear();
            return root;
        }
    }
}
