using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    // Builds jagged, camera-facing lightning ribbons into one mesh (drawn with FlashGame/Lightning).
    // uv: x along the bolt, y across (-1..1), z strength, w layer (0 glow, 1 core). Vertex colour tints the bolt.
    public sealed class LightningBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector4> uvs = new List<Vector4>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();
        readonly List<Vector3> points = new List<Vector3>();
        readonly List<float> weights = new List<float>();
        System.Random random = new System.Random(1);

        public void Begin(int seed)
        {
            random = new System.Random(seed);
            vertices.Clear(); uvs.Clear(); colors.Clear(); triangles.Clear();
        }
        public void Apply(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }
        public float Next() => (float)random.NextDouble();
        public Vector3 Kink(float size) => new Vector3(Next() - .5f, Next() - .5f, Next() - .5f) * size;
        public Vector3 Unit()
        {
            Vector3 v = Kink(2);
            return v.sqrMagnitude < 1e-4f ? Vector3.up : v.normalized;
        }

        // One level of midpoint displacement: every segment gains a kinked midpoint.
        public void Jag(List<Vector3> pts, List<float> w, float roughness)
        {
            for (int i = pts.Count - 1; i > 0; i--)
            {
                float span = Vector3.Distance(pts[i - 1], pts[i]);
                pts.Insert(i, (pts[i - 1] + pts[i]) / 2 + Kink(span * roughness * 2));
                w.Insert(i, (w[i - 1] + w[i]) / 2);
            }
        }

        // Jagged bolt from a to b, optionally forking.
        public void Bolt(Vector3 a, Vector3 b, Vector3 eye, Color tint, float width = 1, float strength = 1,
            int levels = 4, float roughness = .28f, float forkChance = 0)
        {
            points.Clear(); weights.Clear();
            points.Add(a); points.Add(b); weights.Add(strength); weights.Add(strength * .6f);
            for (int i = 0; i < levels; i++) Jag(points, weights, roughness * (i == 0 ? 1 : .9f));
            Ribbon(points, weights, eye, .022f * width, .15f * width, tint);
            if (forkChance <= 0) return;
            // Forks reuse the scratch lists, so walk a copy of this bolt's spine.
            var spine = points.ToArray();
            var spineWeights = weights.ToArray();
            for (int i = 1; i < spine.Length - 1; i++)
            {
                if (Next() > forkChance) continue;
                Vector3 dir = (spine[i] - spine[i - 1]).normalized;
                Vector3 end = spine[i] + (Vector3.Cross(dir, Unit()).normalized * (.5f + Next()) + dir * Next() * .5f) * Vector3.Distance(a, b) * .25f;
                Bolt(spine[i], end, eye, tint, width * .6f, spineWeights[i] * .7f, 2, .3f);
            }
        }

        // Camera-facing strips: a wide soft glow and a thin hot core share the same spine.
        public void Ribbon(List<Vector3> pts, List<float> w, Vector3 eye, float core, float glow, Color tint)
        {
            if (pts.Count < 2) return;
            for (int layer = 0; layer < 2; layer++)
            {
                float width = layer == 0 ? glow : core;
                int start = vertices.Count;
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector3 tangent = pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)];
                    Vector3 side = Vector3.Cross(tangent, eye - pts[i]);
                    side = side.sqrMagnitude < 1e-8f ? Vector3.up : side.normalized;
                    // Bolts streaming past the camera would fill the screen; fade them out up close.
                    float strength = w[i] * Mathf.Clamp01((Vector3.Distance(pts[i], eye) - 1.2f) / 2.5f);
                    float half = width * (.35f + .65f * strength);
                    vertices.Add(pts[i] - side * half);
                    vertices.Add(pts[i] + side * half);
                    uvs.Add(new Vector4(i, -1, strength, layer));
                    uvs.Add(new Vector4(i, 1, strength, layer));
                    colors.Add(tint); colors.Add(tint);
                    if (i == 0) continue;
                    int v = start + i * 2;
                    triangles.Add(v - 2); triangles.Add(v - 1); triangles.Add(v);
                    triangles.Add(v - 1); triangles.Add(v + 1); triangles.Add(v);
                }
            }
        }
    }
}
