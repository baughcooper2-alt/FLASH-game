using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    // Speed Force lightning: jagged, forking bolts that trail the runner's actual path, plus arcs that
    // crackle off the limbs. Every bolt is a camera-facing ribbon in one dynamic world-space mesh.
    // Shapes are re-struck ~30 times a second and drawn additively at HDR intensity, so bloom makes them glow.
    public sealed class SpeedLightning : MonoBehaviour
    {
        struct Sample { public Vector3 position; public float time; public Vector3 jitter; }
        const float TrailSeconds = .2f, TrailMaxLength = 20, StrikeInterval = 1 / 30f, Spacing = .55f;
        static readonly int GlowColorId = Shader.PropertyToID("_GlowColor"), CoreColorId = Shader.PropertyToID("_CoreColor");
        readonly List<Sample> history = new List<Sample>();
        readonly List<Vector3> path = new List<Vector3>(), bolt = new List<Vector3>();
        readonly List<float> pathFade = new List<float>(), boltFade = new List<float>();
        readonly LightningBuilder builder = new LightningBuilder();
        Transform[] anchors = new Transform[0];
        GameObject meshObject;
        Mesh mesh;
        Material material;
        float clock, strikeClock, surge;
        int strike;
        Color glow = new Color(1, .42f, .06f), core = new Color(1, .9f, .62f);

        // Infinite Mass charged: hotter, whiter, busier lightning.
        public bool Overcharged;

        public void Build(Material source)
        {
            // World-space mesh on its own object so it never inherits the runner's rotation.
            meshObject = new GameObject("Speed Force lightning") { layer = 2 };
            mesh = new Mesh { name = "Speed Force lightning" };
            mesh.MarkDynamic();
            material = new Material(source) { name = "Speed Force lightning" };
            meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ApplyColors();
        }
        // Points the arcs jump between: hands, forearms, feet, shins, chest and head.
        public void SetAnchors(Transform[] points) { anchors = points ?? new Transform[0]; }
        public Transform[] Anchors => anchors;
        public Color Glow => glow;
        public void SetColors(Color glowColor, Color coreColor)
        {
            glow = glowColor; core = coreColor;
            ApplyColors();
        }
        void ApplyColors()
        {
            if (material == null) return;
            material.SetColor(GlowColorId, Overcharged ? Color.Lerp(glow, Color.white, .6f) : glow);
            material.SetColor(CoreColorId, Overcharged ? Color.white : core);
        }
        // A short burst of extra body arcs (power moves), even when standing still.
        public void Surge(float seconds) { surge = Mathf.Max(surge, seconds); }
        public void Clear()
        {
            history.Clear();
            if (mesh != null) mesh.Clear();
        }
        void OnDestroy()
        {
            Destroy(meshObject);
            Destroy(mesh);
            Destroy(material);
        }

        public void Tick(Transform pose, float speed, float dt, bool enabled)
        {
            if (mesh == null) return;
            surge = Mathf.Max(0, surge - dt);
            ApplyColors();
            if (!enabled || speed < 10 && surge <= 0 && !Overcharged) { Clear(); return; }
            clock += dt;
            Vector3 centre = pose.TransformPoint(new Vector3(0, 1, 0));
            if (history.Count > 0 && Vector3.Distance(centre, history[0].position) > 18) history.Clear();
            history.Insert(0, new Sample { position = centre, time = clock, jitter = Random.insideUnitSphere });
            float length = 0;
            for (int i = 1; i < history.Count; i++)
            {
                length += Vector3.Distance(history[i - 1].position, history[i].position);
                if (clock - history[i].time > TrailSeconds || length > TrailMaxLength)
                {
                    history.RemoveRange(i + 1, history.Count - i - 1);
                    break;
                }
            }
            strikeClock += dt;
            if (strikeClock >= StrikeInterval) { strikeClock = 0; strike++; }
            Strike(pose, Mathf.InverseLerp(10, 70, speed), speed >= 10);
        }

        void Strike(Transform pose, float power, bool trail)
        {
            // Seeded per strike: shapes hold for one strike interval instead of fizzing every frame.
            builder.Begin(strike * 7919 + 13);
            var view = Camera.main;
            Vector3 eye = view != null ? view.transform.position : pose.position - pose.forward * 6 + Vector3.up * 2;
            BuildPath();
            if (trail && path.Count > 1)
            {
                int bolts = 2 + Mathf.RoundToInt(power * 4);
                for (int b = 0; b < bolts; b++)
                    if (builder.Next() > .18f) TrailBolt(b, bolts, eye, .75f + .25f * power);
            }
            int arcs = anchors.Length == 0 ? 0 : 3 + Mathf.RoundToInt(power * 6) + (surge > 0 ? 6 : 0) + (Overcharged ? 6 : 0);
            for (int a = 0; a < arcs; a++)
                if (builder.Next() > .25f) Arc(pose, eye);
            builder.Apply(mesh);
        }

        // Resample the travel history at even spacing so bolt detail doesn't depend on frame rate.
        void BuildPath()
        {
            path.Clear(); pathFade.Clear();
            float total = 0;
            for (int i = 1; i < history.Count; i++) total += Vector3.Distance(history[i - 1].position, history[i].position);
            if (total < .4f) return;
            float travelled = 0, next = 0;
            for (int i = 1; i < history.Count; i++)
            {
                Vector3 a = history[i - 1].position, b = history[i].position;
                float span = Vector3.Distance(a, b);
                for (; span > 0 && next <= travelled + span; next += Spacing)
                {
                    float t = (next - travelled) / span;
                    path.Add(Vector3.Lerp(a, b, t) + Vector3.Lerp(history[i - 1].jitter, history[i].jitter, t) * .12f);
                    pathFade.Add(1 - next / total);
                }
                travelled += span;
            }
        }

        void TrailBolt(int index, int count, Vector3 eye, float strength)
        {
            bolt.Clear(); boltFade.Clear();
            float phase = index * Mathf.PI * 2 / count + strike * .9f, radius = .18f + .32f * builder.Next(), twist = (builder.Next() - .5f) * .8f;
            // Each strike lights a different stretch of the trail, so it crackles instead of reading as one rope.
            float from = builder.Next() < .65f ? 0 : builder.Next() * .3f, to = Mathf.Min(1, from + .35f + builder.Next() * .65f);
            for (int i = 0; i < path.Count; i++)
            {
                float u = 1 - pathFade[i];
                if (u < from || u > to) continue;
                Vector3 along = (i + 1 < path.Count ? path[i + 1] - path[i] : path[i] - path[i - 1]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, along);
                side = side.sqrMagnitude < .01f ? Vector3.right : side.normalized;
                Vector3 up = Vector3.Cross(along, side);
                float angle = phase + i * twist;
                // Taller than wide so the bolts wrap the body rather than a tube around the hips.
                Vector3 offset = (side * Mathf.Cos(angle) + up * Mathf.Sin(angle) * 1.6f) * radius * (.5f + .5f * pathFade[i]);
                bolt.Add(path[i] + offset + builder.Kink(.34f));
                boltFade.Add(pathFade[i] * Mathf.Sqrt(Mathf.Sin(Mathf.PI * Mathf.InverseLerp(from - .05f, to + .05f, u))));
            }
            if (bolt.Count < 2) return;
            builder.Jag(bolt, boltFade, .22f);
            builder.Jag(bolt, boltFade, .26f);
            builder.Ribbon(bolt, boltFade, eye, .024f * strength, .17f * strength, Color.white);
            // Forks peel off the main bolt and die quickly.
            var spine = bolt.ToArray();
            var spineFade = boltFade.ToArray();
            for (int i = 1; i < spine.Length - 1; i++)
            {
                if (builder.Next() > .07f) continue;
                Vector3 dir = (spine[i] - spine[i - 1]).normalized;
                Vector3 kick = Vector3.Cross(dir, builder.Unit()).normalized;
                Vector3 end = spine[i] + (kick * (.6f + builder.Next()) - dir * builder.Next() * .6f) * (.5f + spineFade[i]);
                builder.Bolt(spine[i], end, eye, Color.white, strength * .5f, spineFade[i] * .8f, 3, .3f);
            }
        }

        void Arc(Transform pose, Vector3 eye)
        {
            var from = anchors[(int)(builder.Next() * anchors.Length) % anchors.Length];
            var to = anchors[(int)(builder.Next() * anchors.Length) % anchors.Length];
            if (from == null || to == null) return;
            Vector3 start = from.position;
            Vector3 end = builder.Next() < .45f && to != from ? to.position
                : start + (builder.Unit() * .5f - pose.forward * .25f) * (.5f + builder.Next() * .6f);
            builder.Bolt(start, end, eye, Color.white, .7f, .9f, 3, .3f);
        }
    }
}
