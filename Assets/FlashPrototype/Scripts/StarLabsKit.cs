using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    // Construction helpers for StarLabs. Positions are lab-local metres: origin at the plaza centre,
    // +z north, y measured from the street. Angles are degrees clockwise from north.
    public sealed partial class StarLabs
    {
        readonly PrototypeWorld w;
        readonly Transform site, animated;
        readonly Vector3 origin;
        readonly Font font;
        readonly Shader glassShader;
        readonly StarLabsDynamic runtime;
        readonly Material textMaterial;
        readonly List<Vector4> lamps = new List<Vector4>(), lampColors = new List<Vector4>();

        static Vector3 Dir(float degrees) => new Vector3(Mathf.Sin(degrees * Mathf.Deg2Rad), 0, Mathf.Cos(degrees * Mathf.Deg2Rad));
        static Vector3 Polar(float degrees, float radius, float y) => Dir(degrees) * radius + Vector3.up * y;

        Material M(string name, float r, float g, float b, float glow = 0, float surface = 0, float detail = 0, bool interior = true)
        {
            var material = w.Material(name, new Color(r, g, b), glow);
            material.SetFloat("_Surface", surface);
            material.SetFloat("_WindowStyle", detail);
            // Interior rooms draw after the shell and city, so rooms hidden behind walls are depth-rejected.
            if (interior) material.renderQueue = 2010;
            return material;
        }
        Material Glass(string name, Color tint)
        {
            if (glassShader == null) return M(name, tint.r * .35f, tint.g * .35f, tint.b * .35f);
            var material = new Material(glassShader) { name = name };
            material.SetColor("_BaseColor", tint);
            runtime.Materials.Add(material);
            return material;
        }

        Transform Frame(string name, Vector3 p, float yaw, Transform parent = null)
        {
            var frame = new GameObject(name).transform;
            frame.SetParent(parent != null ? parent : site, false);
            frame.localPosition = p;
            frame.localRotation = Quaternion.Euler(0, yaw, 0);
            return frame;
        }
        GameObject Box(Transform t, string name, Vector3 p, Vector3 size, Material m, Vector3 euler = default, bool solid = false)
        {
            var go = w.Shape(name, PrimitiveType.Cube, t, p, size, m, solid);
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }
        // Unity's cylinder is 2 m tall, so length is halved; euler (90,0,0) lays it along z.
        GameObject Cyl(Transform t, string name, Vector3 p, float radius, float length, Material m, Vector3 euler = default, bool solid = false)
        {
            var go = w.Shape(name, PrimitiveType.Cylinder, t, p, new Vector3(radius * 2, length / 2, radius * 2), m, solid);
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }
        GameObject Ball(Transform t, string name, Vector3 p, Vector3 size, Material m)
            => w.Shape(name, PrimitiveType.Sphere, t, p, size, m, false);
        GameObject Strut(Transform t, string name, Vector3 a, Vector3 b, float thickness, Material m)
        {
            var go = w.Shape(name, PrimitiveType.Cube, t, (a + b) / 2, new Vector3(thickness, Vector3.Distance(a, b), thickness), m, false);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            return go;
        }
        // Axis-aligned box between two corners: the workhorse for floors, walls and ceilings.
        GameObject Block(string name, float x0, float y0, float z0, float x1, float y1, float z1, Material m, bool solid = true)
        {
            var min = new Vector3(Mathf.Min(x0, x1), Mathf.Min(y0, y1), Mathf.Min(z0, z1));
            var max = new Vector3(Mathf.Max(x0, x1), Mathf.Max(y0, y1), Mathf.Max(z0, z1));
            return w.Shape(name, PrimitiveType.Cube, site, (min + max) / 2, max - min, m, solid);
        }
        void Blocker(string name, Vector3 min, Vector3 max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(site, false);
            go.transform.localPosition = (min + max) / 2;
            go.AddComponent<BoxCollider>().size = max - min;
        }
        GameObject Shape(Transform t, string name, Mesh mesh, Material m, Vector3 p, bool solid = false, bool convex = false, Vector3 euler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(t, false);
            go.transform.localPosition = p;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (solid) { var collider = go.AddComponent<MeshCollider>(); collider.convex = convex; collider.sharedMesh = mesh; }
            go.AddComponent<RuntimeMeshOwner>().Mesh = mesh;
            return go;
        }
        // Walls along x (or z) with openings. Each opening is (from, to, bottom, top).
        void WallX(string name, float z0, float z1, float x0, float x1, float y0, float y1, Material m, params Vector4[] openings)
        {
            float x = x0;
            foreach (var o in openings)
            {
                if (o.x > x) Block(name, x, y0, z0, o.x, y1, z1, m);
                if (o.z > y0) Block(name, o.x, y0, z0, o.y, o.z, z1, m);
                if (o.w < y1) Block(name, o.x, o.w, z0, o.y, y1, z1, m);
                x = o.y;
            }
            if (x < x1) Block(name, x, y0, z0, x1, y1, z1, m);
        }
        void WallZ(string name, float x0, float x1, float z0, float z1, float y0, float y1, Material m, params Vector4[] openings)
        {
            float z = z0;
            foreach (var o in openings)
            {
                if (o.x > z) Block(name, x0, y0, z, x1, y1, o.x, m);
                if (o.z > y0) Block(name, x0, y0, o.x, x1, o.z, o.y, m);
                if (o.w < y1) Block(name, x0, o.w, o.x, x1, y1, o.y, m);
                z = o.y;
            }
            if (z < z1) Block(name, x0, y0, z, x1, y1, z1, m);
        }
        static Vector4 Door(float from, float to, float top, float bottom = -1000) => new Vector4(from, to, bottom, top);

        // Readable from the direction (sin yaw, 0, cos yaw) in the parent's space.
        TextMesh Text(Transform t, string text, Vector3 p, float height, Color color, float yaw = 0)
        {
            var go = new GameObject("Sign: " + text.Replace('\n', ' '));
            go.transform.SetParent(t, false);
            go.transform.localPosition = p;
            go.transform.localRotation = Quaternion.Euler(0, yaw + 180, 0);
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font; mesh.fontSize = 96; mesh.characterSize = height / 9.6f;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center;
            mesh.fontStyle = FontStyle.Bold; mesh.color = color; mesh.text = text;
            go.GetComponent<MeshRenderer>().sharedMaterial = textMaterial;
            return mesh;
        }

        // A lamp lights the custom-shaded lab and, as a real point light, the animated character.
        void Lamp(Vector3 p, Color color, float range, bool real = true)
        {
            if (lamps.Count < LampLimit)
            {
                lamps.Add(new Vector4(origin.x + p.x, origin.y + p.y, origin.z + p.z, range));
                lampColors.Add(color);
            }
            else Debug.LogWarning("S.T.A.R. Labs lamp limit reached; " + p + " only lights characters.");
            if (real) RealLight(p, color, range);
        }
        void RealLight(Vector3 p, Color color, float range, float boost = 1.4f)
        {
            var go = new GameObject("Lab light");
            go.transform.SetParent(site, false);
            go.transform.localPosition = p;
            var light = go.AddComponent<Light>();
            float intensity = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            light.type = LightType.Point; light.range = range; light.shadows = LightShadows.None;
            light.color = color / intensity; light.intensity = intensity * boost;
        }

        // Shared props ------------------------------------------------------------------

        void Monitor(Transform t, Vector3 p, float yaw, float width = .95f, bool stand = true)
        {
            var m = Frame("Monitor", p, yaw, t);
            float h = width * .6f, y = h / 2 + (stand ? .2f : 0);
            Box(m, "Bezel", new Vector3(0, y, 0), new Vector3(width, h, .05f), black);
            Box(m, "Screen", new Vector3(0, y, .028f), new Vector3(width - .05f, h - .05f, .006f), screen);
            Text(m, "S.T.A.R.\n<size=26>LABORATORIES</size>", new Vector3(0, y, .034f), h * .26f, Color.white);
            if (!stand) return;
            Box(m, "Neck", new Vector3(0, .1f, -.06f), new Vector3(.07f, .2f, .04f), black);
            Box(m, "Foot", new Vector3(0, .012f, -.06f), new Vector3(.32f, .024f, .22f), black);
        }
        void StarSign(Vector3 p, float yaw)
        {
            var s = Frame("S.T.A.R. Labs sign stand", p, yaw);
            Box(s, "Sign frame", new Vector3(0, 2.4f, 0), new Vector3(1.8f, 1.2f, .08f), metal);
            Box(s, "Sign screen", new Vector3(0, 2.4f, .045f), new Vector3(1.66f, 1.06f, .01f), screen);
            Text(s, "S.T.A.R.\n<size=26>LABORATORIES</size>", new Vector3(0, 2.4f, .055f), .3f, Color.white);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(s, "Sign leg", new Vector3(i * .55f, .9f, 0), new Vector3(.05f, 1.8f, .05f), metal);
                Box(s, "Sign foot", new Vector3(i * .55f, .02f, 0), new Vector3(.08f, .04f, .9f), metal);
            }
        }
        void Chair(Vector3 p, float yaw, Material seat)
        {
            var c = Frame("Office chair", p, yaw);
            Box(c, "Seat", new Vector3(0, .5f, 0), new Vector3(.52f, .09f, .5f), seat);
            Box(c, "Back", new Vector3(0, .9f, -.27f), new Vector3(.5f, .6f, .07f), seat, new Vector3(-8, 0, 0));
            Cyl(c, "Column", new Vector3(0, .27f, 0), .035f, .45f, metal);
            Box(c, "Base", new Vector3(0, .05f, 0), new Vector3(.62f, .05f, .07f), metal, new Vector3(0, 30, 0));
            Box(c, "Base", new Vector3(0, .05f, 0), new Vector3(.62f, .05f, .07f), metal, new Vector3(0, -30, 0));
            for (int i = -1; i <= 1; i += 2) Box(c, "Armrest", new Vector3(i * .29f, .68f, -.03f), new Vector3(.05f, .05f, .38f), metal);
        }
        void Stool(Vector3 p)
        {
            var s = Frame("Stool", p, 0);
            Cyl(s, "Stool seat", new Vector3(0, .72f, 0), .22f, .06f, metal);
            Cyl(s, "Stool column", new Vector3(0, .4f, 0), .03f, .6f, metal);
            for (int i = 0; i < 4; i++)
                Strut(s, "Stool leg", new Vector3(0, .45f, 0), Dir(45 + i * 90) * .26f, .03f, metal);
        }
        void Railing(Vector3 a, Vector3 b, Material m, float height = 1.1f, bool solid = false)
        {
            Strut(site, "Railing top", a + Vector3.up * height, b + Vector3.up * height, .07f, m);
            Strut(site, "Railing mid", a + Vector3.up * height * .5f, b + Vector3.up * height * .5f, .05f, m);
            int posts = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 1.6f));
            for (int i = 0; i <= posts; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)i / posts);
                Box(site, "Railing post", p + Vector3.up * height / 2, new Vector3(.07f, height, .07f), m);
            }
            // Guards are axis-aligned; they stop runners but can still be jumped.
            if (solid) Blocker("Railing guard", Vector3.Min(a, b) - new Vector3(.05f, 0, .05f), Vector3.Max(a, b) + new Vector3(.05f, height, .05f));
        }
        void Ladder(Vector3 p, float height, float yaw, Material m)
        {
            var l = Frame("Service ladder", p, yaw);
            for (int i = -1; i <= 1; i += 2) Box(l, "Ladder rail", new Vector3(i * .32f, height / 2, 0), new Vector3(.09f, height, .09f), m);
            for (float y = .4f; y < height; y += .5f) Box(l, "Ladder rung", new Vector3(0, y, 0), new Vector3(.64f, .05f, .05f), m);
        }
        void Shelf(Vector3 p, float yaw, float width, float height, int levels, Material frame)
        {
            var s = Frame("Shelving", p, yaw);
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                Box(s, "Shelf post", new Vector3(x * width / 2, height / 2, z * .28f), new Vector3(.05f, height, .05f), frame);
            for (int i = 0; i < levels; i++)
                Box(s, "Shelf", new Vector3(0, .12f + i * (height - .2f) / (levels - 1), 0), new Vector3(width, .04f, .62f), frame);
        }
        void Bottles(Transform t, Vector3 start, Vector3 step, int count, int seed)
        {
            var random = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float h = .14f + (float)random.NextDouble() * .2f, r = .035f + (float)random.NextDouble() * .05f;
                Cyl(t, "Bottle", start + step * i + Vector3.up * h / 2, r, h, bottleColors[random.Next(bottleColors.Length)]);
            }
        }
        // Housing stays put; blades spin on the animated root.
        void Fan(Vector3 p, float yaw, float radius, int blades, float speed, Material housing, Material blade)
        {
            var frame = Frame("Fan housing", p, yaw);
            Shape(frame, "Fan ring", LabMesh.Tube(radius, radius * .07f), housing, Vector3.zero, euler: new Vector3(90, 0, 0));
            var pivot = Frame("Fan rotor", p, yaw, animated);
            Cyl(pivot, "Fan hub", Vector3.zero, radius * .16f, .12f, housing, new Vector3(90, 0, 0));
            for (int i = 0; i < blades; i++)
            {
                float a = i * 360f / blades + 45;
                Box(pivot, "Fan blade", Quaternion.Euler(0, 0, a) * Vector3.right * radius * .5f,
                    new Vector3(radius * .82f, radius * (blades > 4 ? .09f : .26f), .03f), blade, new Vector3(0, 0, a));
            }
            runtime.Spinners.Add((pivot, speed));
        }
    }

    // Procedural meshes. Faces are wound so their front side matches the normals (raycasts rely on it).
    static class LabMesh
    {
        sealed class Builder
        {
            readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            readonly List<int> t = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                n.Add(na); n.Add(nb); n.Add(nc); n.Add(nd);
                // Unity front faces are clockwise as seen from the normal side.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a) + Vector3.Cross(c - a, d - a), na + nb + nc + nd) >= 0)
                    t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                else
                    t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal) => Quad(a, b, c, d, normal, normal, normal, normal);
            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
        static Vector3 Dir(float degrees) => new Vector3(Mathf.Sin(degrees * Mathf.Deg2Rad), 0, Mathf.Cos(degrees * Mathf.Deg2Rad));
        static int Segments(float a0, float a1, float step) => Mathf.Max(1, Mathf.CeilToInt((a1 - a0) / step));

        // Closed curved slab between radii r0..r1 and heights y0..y1. r0 = 0 gives a disc or polygon.
        public static Mesh Arc(float r0, float r1, float y0, float y1, float a0 = 0, float a1 = 360, int segments = 0)
        {
            if (segments <= 0) segments = Segments(a0, a1, 2.5f);
            var b = new Builder();
            Vector3 up = Vector3.up, lo = up * y0, hi = up * y1;
            for (int s = 0; s < segments; s++)
            {
                Vector3 d0 = Dir(Mathf.Lerp(a0, a1, (float)s / segments)), d1 = Dir(Mathf.Lerp(a0, a1, (s + 1f) / segments));
                b.Quad(d0 * r1 + lo, d0 * r1 + hi, d1 * r1 + hi, d1 * r1 + lo, d0, d0, d1, d1);
                if (r0 > 0) b.Quad(d0 * r0 + lo, d0 * r0 + hi, d1 * r0 + hi, d1 * r0 + lo, -d0, -d0, -d1, -d1);
                b.Quad(d0 * r0 + hi, d0 * r1 + hi, d1 * r1 + hi, d1 * r0 + hi, up);
                b.Quad(d0 * r0 + lo, d0 * r1 + lo, d1 * r1 + lo, d1 * r0 + lo, -up);
            }
            if (a1 - a0 < 359.99f)
                foreach (var (a, side) in new[] { (a0, -1f), (a1, 1f) })
                {
                    Vector3 d = Dir(a), tangent = new Vector3(d.z, 0, -d.x) * side;
                    b.Quad(d * r0 + lo, d * r1 + lo, d * r1 + hi, d * r0 + hi, tangent);
                }
            return b.Build("Lab arc");
        }
        // Open conical surface from ring A to ring B, facing up/outward.
        public static Mesh Band(float rA, float yA, float rB, float yB, float a0 = 0, float a1 = 360)
        {
            var b = new Builder();
            Vector2 normal = new Vector2(yB - yA, rA - rB).normalized;
            if (normal.x + normal.y < 0) normal = -normal;
            int segments = Segments(a0, a1, 2.5f);
            for (int s = 0; s < segments; s++)
            {
                Vector3 d0 = Dir(Mathf.Lerp(a0, a1, (float)s / segments)), d1 = Dir(Mathf.Lerp(a0, a1, (s + 1f) / segments));
                Vector3 n0 = d0 * normal.x + Vector3.up * normal.y, n1 = d1 * normal.x + Vector3.up * normal.y;
                b.Quad(d0 * rA + Vector3.up * yA, d0 * rB + Vector3.up * yB, d1 * rB + Vector3.up * yB, d1 * rA + Vector3.up * yA, n0, n0, n1, n1);
            }
            return b.Build("Lab band");
        }
        // Six-sided solid from a bottom loop c[0..3] and a matching top loop c[4..7].
        public static Mesh Hull(Vector3[] c)
        {
            var b = new Builder();
            Vector3 centre = Vector3.zero;
            foreach (var p in c) centre += p / c.Length;
            void Face(int i, int j, int k, int l)
            {
                Vector3 normal = Vector3.Cross(c[j] - c[i], c[k] - c[i]).normalized;
                if (Vector3.Dot(normal, (c[i] + c[j] + c[k] + c[l]) / 4 - centre) < 0) normal = -normal;
                b.Quad(c[i], c[j], c[k], c[l], normal);
            }
            Face(0, 1, 2, 3); Face(4, 5, 6, 7); Face(0, 1, 5, 4); Face(1, 2, 6, 5); Face(2, 3, 7, 6); Face(3, 0, 4, 7);
            return b.Build("Lab hull");
        }
        // Torus section in the horizontal plane; rotate (90,0,0) for a ring facing z.
        public static Mesh Tube(float radius, float thickness, float a0 = 0, float a1 = 360, int sides = 8)
        {
            var b = new Builder();
            int segments = Segments(a0, a1, Mathf.Clamp(120 / radius, 1.5f, 15));
            for (int s = 0; s < segments; s++)
            {
                Vector3 d0 = Dir(Mathf.Lerp(a0, a1, (float)s / segments)), d1 = Dir(Mathf.Lerp(a0, a1, (s + 1f) / segments));
                for (int k = 0; k < sides; k++)
                {
                    float p0 = k * Mathf.PI * 2 / sides, p1 = (k + 1) * Mathf.PI * 2 / sides;
                    Vector3 n00 = d0 * Mathf.Cos(p0) + Vector3.up * Mathf.Sin(p0), n01 = d0 * Mathf.Cos(p1) + Vector3.up * Mathf.Sin(p1);
                    Vector3 n10 = d1 * Mathf.Cos(p0) + Vector3.up * Mathf.Sin(p0), n11 = d1 * Mathf.Cos(p1) + Vector3.up * Mathf.Sin(p1);
                    b.Quad(d0 * radius + n00 * thickness, d0 * radius + n01 * thickness, d1 * radius + n11 * thickness, d1 * radius + n10 * thickness, n00, n01, n11, n10);
                }
            }
            return b.Build("Lab tube");
        }
    }

    // Marks lab objects that must stay separate (CityDistrict skips them when merging meshes) and owns runtime state.
    public sealed class StarLabsDynamic : MonoBehaviour
    {
        public readonly List<Material> Materials = new List<Material>();
        public readonly List<(Transform pivot, float speed)> Spinners = new List<(Transform, float)>();
        Font font;
        Material text;
        // Dynamic fonts can rebuild their atlas when new characters appear; keep the sign material pointing at it.
        public void TrackFont(Font signFont, Material signText)
        {
            font = signFont; text = signText;
            Font.textureRebuilt += FontRebuilt;
        }
        void FontRebuilt(Font rebuilt) { if (rebuilt == font) text.mainTexture = font.material.mainTexture; }
        void OnDestroy()
        {
            if (font != null) Font.textureRebuilt -= FontRebuilt;
            foreach (var material in Materials) Destroy(material);
            Shader.SetGlobalVector("_LabZone", Vector4.zero);
        }
    }
}
