using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    // A speed-made vortex: a tornado on land, a whirlpool on water. It builds while it is fed (the Flash
    // circling, or the Cyclone power), lifts and spins bots, smothers fires and pulls smoke or gas away.
    public sealed class Vortex : MonoBehaviour
    {
        public static readonly List<Vortex> All = new List<Vortex>();
        public Vector3 Centre { get; private set; }
        public float Radius { get; private set; }
        public float Strength { get; private set; }
        public bool OnWater { get; private set; }
        public bool Alive => this != null && !fading;
        float life, fadeTimer;
        bool fading;
        ParticleSystem column, ring;
        Transform whirl;
        readonly List<Material> owned = new List<Material>();

        public static Vortex Create(Transform parent, Vector3 centre, float radius, bool onWater)
        {
            var go = new GameObject(onWater ? "Water vortex" : "Speed Force tornado");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<Vortex>();
            v.Centre = centre; v.Radius = radius; v.OnWater = onWater;
            go.transform.position = centre;
            v.Build();
            All.Add(v);
            return v;
        }

        // Keep the vortex going (and growing) while the Flash keeps circling.
        public void Feed(Vector3 centre, float radius, float dt)
        {
            Centre = Vector3.Lerp(Centre, centre, .2f); Radius = Mathf.Lerp(Radius, radius, .2f);
            transform.position = Centre;
            Strength = Mathf.Min(1, Strength + dt * .8f);
            life = 5;
        }

        void Build()
        {
            var fx = FxSystem.Instance;
            column = Particles("Column", fx.Alpha, OnWater ? new Color(.82f, .92f, 1, .55f) : new Color(.78f, .76f, .72f, .5f), true);
            ring = Particles("Streaks", fx.Additive, OnWater ? new Color(.5f, .8f, 1) : new Color(1, .75f, .35f), false);
            if (OnWater)
            {
                // Spiral of foam on the surface.
                var go = new GameObject("Whirlpool"); go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up * .05f;
                go.AddComponent<MeshFilter>().sharedMesh = Spiral();
                var r = go.AddComponent<MeshRenderer>();
                var m = new Material(fx.Alpha) { name = "Whirlpool" }; m.SetColor("_Tint", new Color(.85f, .95f, 1, .6f)); owned.Add(m);
                r.sharedMaterial = m;
                whirl = go.transform;
            }
        }

        ParticleSystem Particles(string name, Material source, Color color, bool body)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 1500;
            main.startLifetime = body ? 2.6f : 1.2f; main.startSpeed = 0;
            main.startSize = body ? new ParticleSystem.MinMaxCurve(1.2f, 3f) : new ParticleSystem.MinMaxCurve(.08f, .16f);
            main.startColor = color;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var emission = ps.emission; emission.rateOverTime = 0;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = Radius;
            shape.radiusThickness = body ? .25f : .05f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            // Local z is world up (the emitter is turned to face up): rise, orbit and pull inward.
            vel.z = new ParticleSystem.MinMaxCurve(body ? 5 : 8, body ? 10 : 14);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(body ? 3.2f : 4.5f);
            vel.radial = new ParticleSystem.MinMaxCurve(-.35f);
            // Unity requires all three linear curves in the same mode as z (two constants).
            vel.x = new ParticleSystem.MinMaxCurve(0, 0); vel.y = new ParticleSystem.MinMaxCurve(0, 0);
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0); vel.orbitalY = new ParticleSystem.MinMaxCurve(0);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(.8f, .7f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, body ? 2.2f : 1));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = source;
            renderer.renderMode = body ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
            if (!body) { renderer.velocityScale = .08f; renderer.lengthScale = 2; }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        // Driven by SpeedForcePowers with the unscaled frame time.
        public void Tick(float dt, EmergencyDirector emergencies)
        {
            if (fading)
            {
                fadeTimer -= dt;
                SetRate(0);
                if (fadeTimer <= 0) { All.Remove(this); Destroy(gameObject); }
                return;
            }
            life -= dt;
            if (life <= 0) Strength = Mathf.Max(0, Strength - dt * .5f);
            if (Strength <= 0 && life <= 0) { End(); return; }
            SetRate(Strength);
            var shape = column.shape; shape.radius = Radius;
            var ringShape = ring.shape; ringShape.radius = Radius * .9f;
            if (whirl != null) { whirl.localScale = Vector3.one * Radius * 1.3f; whirl.Rotate(0, -200 * Strength * dt, 0); }
            foreach (var bot in BotEnemy.All.ToArray())
            {
                if (bot.Dead) continue;
                Vector3 offset = bot.transform.position - Centre; offset.y = 0;
                if (offset.magnitude < Radius * 1.25f && Strength > .25f) bot.Trap(this);
            }
            if (emergencies != null) emergencies.ApplyVortex(this, dt);
            if (OnWater && Random.value < Strength) FxSystem.Instance.Spray(Centre + Random.insideUnitSphere.Flat() * Radius, Vector3.up, 2, 3);
        }
        void SetRate(float strength)
        {
            var e = column.emission; e.rateOverTime = 160 * strength;
            var r = ring.emission; r.rateOverTime = 220 * strength;
        }

        void End()
        {
            fading = true; fadeTimer = 2.5f;
            // Bots are thrown clear as the vortex collapses.
            foreach (var bot in BotEnemy.All.ToArray())
            {
                if (bot.Dead || !bot.Trapped) continue;
                Vector3 out_ = bot.transform.position - Centre; out_.y = 0;
                bot.Release(out_.normalized * 9);
            }
        }
        public void Collapse() { if (!fading) End(); }

        static Mesh Spiral()
        {
            const int turns = 3, segments = 180;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var c = new List<Color>(); var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float k = (float)i / segments, a = k * turns * Mathf.PI * 2, r = .15f + k * .85f, w = .05f + k * .08f;
                var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                v.Add(d * (r - w)); v.Add(d * (r + w));
                uv.Add(new Vector2(.5f, 0)); uv.Add(new Vector2(.5f, 1));
                c.Add(new Color(1, 1, 1, 1 - k * .6f)); c.Add(new Color(1, 1, 1, 1 - k * .6f));
                if (i == 0) continue;
                int n = i * 2;
                t.AddRange(new[] { n - 2, n, n - 1, n - 1, n, n + 1 });
            }
            var mesh = new Mesh { name = "Whirlpool spiral" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(c); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
            return mesh;
        }

        void OnDestroy()
        {
            All.Remove(this);
            foreach (var m in owned) Destroy(m);
            if (whirl != null) Destroy(whirl.GetComponent<MeshFilter>().sharedMesh);
        }
    }

    static class VectorExtensions
    {
        public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
