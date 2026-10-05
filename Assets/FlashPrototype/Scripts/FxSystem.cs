using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    // Shared visual effects: pooled particle emitters, transient lightning bolts, shockwave rings,
    // Speed Force afterimages and full-screen flashes. One instance per game (FxSystem.Instance).
    public sealed class FxSystem : MonoBehaviour
    {
        public static FxSystem Instance { get; private set; }

        struct ActiveBolt { public Vector3 a, b; public Transform follow; public float life, duration, width; public Color tint; public int seed; public float forks; }
        struct Ring { public Transform t; public float life, duration, radius; public Color color; }
        struct Ghost { public GameObject go; public Mesh mesh; public float life, duration; public Color color; public Vector3 drift; }

        ParticleSystem sparks, flames, smoke, dust, spray, wind, debris;
        readonly List<ActiveBolt> bolts = new List<ActiveBolt>();
        readonly List<Ring> rings = new List<Ring>();
        readonly List<Ghost> ghosts = new List<Ghost>();
        readonly List<Material> owned = new List<Material>();
        readonly LightningBuilder builder = new LightningBuilder();
        Mesh boltMesh, ringMesh;
        Material additive, alpha, ghostMaterial, ringMaterial;
        MaterialPropertyBlock block;
        int frame;

        public Color FlashColor { get; private set; }
        public float FlashAmount { get; private set; }
        public Material Additive => additive;
        public Material Alpha => alpha;

        public static FxSystem Create(Transform parent, Material lightning, Material debrisMaterial)
        {
            var go = new GameObject("Effects");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<FxSystem>();
            fx.Build(lightning, debrisMaterial);
            Instance = fx;
            return fx;
        }

        void Build(Material lightning, Material debrisMaterial)
        {
            block = new MaterialPropertyBlock();
            additive = Owned(Resources.Load<Shader>("FxAdditive"), "FX additive");
            alpha = Owned(Resources.Load<Shader>("FxAlpha"), "FX alpha");
            ghostMaterial = Owned(Resources.Load<Shader>("FxGhost"), "FX ghost");
            ringMaterial = new Material(additive) { name = "FX ring" }; owned.Add(ringMaterial);
            ringMaterial.SetFloat("_Softness", 1.2f); ringMaterial.SetFloat("_Intensity", 3);
            var sparkMaterial = new Material(additive) { name = "FX sparks" }; owned.Add(sparkMaterial);
            sparkMaterial.SetFloat("_Intensity", 5); sparkMaterial.SetFloat("_Softness", 1);
            var flameMaterial = new Material(additive) { name = "FX flames" }; owned.Add(flameMaterial);
            flameMaterial.SetFloat("_Intensity", 2.4f);

            sparks = Emitter("Sparks", sparkMaterial, 1.4f, ParticleSystemRenderMode.Stretch);
            var sr = sparks.GetComponent<ParticleSystemRenderer>(); sr.velocityScale = .035f; sr.lengthScale = 1;
            flames = Emitter("Flames", flameMaterial, -.35f, ParticleSystemRenderMode.Billboard, Gradient(
                new[] { (new Color(1, .95f, .7f), 0f), (new Color(1, .55f, .12f), .25f), (new Color(.9f, .2f, .05f), .7f), (new Color(.2f, .05f, .02f), 1f) },
                new[] { (0f, 0f), (1f, .12f), (.6f, .7f), (0f, 1f) }), Grow(.6f, 1.1f));
            smoke = Emitter("Smoke", alpha, -.08f, ParticleSystemRenderMode.Billboard, Fade(.08f, .6f), Grow(.5f, 2.2f));
            dust = Emitter("Dust", alpha, .15f, ParticleSystemRenderMode.Billboard, Fade(.05f, .5f), Grow(.6f, 2f));
            spray = Emitter("Water spray", alpha, 1.1f, ParticleSystemRenderMode.Billboard, Fade(0, .4f), Grow(.4f, 1.6f));
            wind = Emitter("Wind", additive, 0, ParticleSystemRenderMode.Stretch, Fade(.1f, .5f));
            var wr = wind.GetComponent<ParticleSystemRenderer>(); wr.velocityScale = .06f; wr.lengthScale = 2;
            debris = Emitter("Debris", debrisMaterial, 1.2f, ParticleSystemRenderMode.Mesh);
            var dr = debris.GetComponent<ParticleSystemRenderer>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dr.mesh = cube.GetComponent<MeshFilter>().sharedMesh; Destroy(cube);
            var rot = debris.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-8, 8); rot.y = new ParticleSystem.MinMaxCurve(-8, 8); rot.z = new ParticleSystem.MinMaxCurve(-8, 8);
            var collision = debris.collision; collision.enabled = true; collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D; collision.bounce = .3f; collision.dampen = .4f; collision.collidesWith = ~(1 << 2);
            collision.quality = ParticleSystemCollisionQuality.Low;

            boltMesh = new Mesh { name = "FX bolts" }; boltMesh.MarkDynamic();
            var boltObject = new GameObject("FX bolts");
            boltObject.transform.SetParent(transform, false);
            boltObject.AddComponent<MeshFilter>().sharedMesh = boltMesh;
            var boltRenderer = boltObject.AddComponent<MeshRenderer>();
            var boltMaterial = new Material(lightning) { name = "FX lightning" }; owned.Add(boltMaterial);
            boltMaterial.SetColor("_GlowColor", Color.white); boltMaterial.SetColor("_CoreColor", Color.white);
            boltRenderer.sharedMaterial = boltMaterial;
            boltRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringMesh = RingMesh();
        }

        Material Owned(Shader shader, string name)
        {
            var m = new Material(shader) { name = name };
            owned.Add(m);
            return m;
        }

        // Slows particles along with the world during speed perception.
        public void SetWorldSpeed(float scale)
        {
            foreach (var ps in new[] { sparks, flames, smoke, dust, spray, wind, debris })
            { var main = ps.main; main.simulationSpeed = scale; }
        }

        // ---- Particles -----------------------------------------------------------------------------

        public void Sparks(Vector3 p, Vector3 direction, int count, Color color, float speed = 9)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 v = (direction.normalized * .8f + Random.insideUnitSphere).normalized * speed * Random.Range(.4f, 1.2f);
                Emit(sparks, p, v, Random.Range(.05f, .12f), Random.Range(.25f, .6f), color);
            }
        }
        public void Flames(Vector3 p, float radius, float intensity)
        {
            Vector3 offset = Random.insideUnitSphere * radius; offset.y = Mathf.Abs(offset.y) * .3f;
            Emit(flames, p + offset, new Vector3(Random.Range(-.4f, .4f), Random.Range(1.5f, 3.2f), Random.Range(-.4f, .4f)),
                Random.Range(.8f, 1.6f) * (.5f + intensity * .5f), Random.Range(.5f, 1.1f), Color.white);
        }
        public void Smoke(Vector3 p, Vector3 velocity, float size, Color color, float life = 3)
            => Emit(smoke, p, velocity, size * Random.Range(.8f, 1.2f), life * Random.Range(.8f, 1.2f), color, Random.Range(0, 360));
        public void Dust(Vector3 p, int count, float spread, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 v = Random.insideUnitSphere * spread; v.y = Mathf.Abs(v.y) * .4f + .3f;
                Emit(dust, p + Random.insideUnitSphere * .3f, v, Random.Range(.6f, 1.4f), Random.Range(.8f, 1.6f), color, Random.Range(0, 360));
            }
        }
        public void Spray(Vector3 p, Vector3 direction, int count, float speed = 6)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 v = (direction.normalized + Random.insideUnitSphere * .6f) * speed * Random.Range(.5f, 1.2f) + Vector3.up * Random.Range(2, 5);
                Emit(spray, p, v, Random.Range(.25f, .6f), Random.Range(.5f, 1f), new Color(.85f, .93f, 1, .7f));
            }
        }
        public void Wind(Vector3 p, Vector3 velocity, Color color)
            => Emit(wind, p, velocity, Random.Range(.08f, .16f), Random.Range(.25f, .5f), color);
        public void Debris(Vector3 p, int count, float speed, Color color, float size = .25f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 v = Random.insideUnitSphere * speed; v.y = Mathf.Abs(v.y) + speed * .4f;
                Emit(debris, p + Random.insideUnitSphere * .4f, v, size * Random.Range(.5f, 1.4f), Random.Range(1.5f, 2.5f), color);
            }
        }

        void Emit(ParticleSystem ps, Vector3 p, Vector3 v, float size, float life, Color color, float rotation = 0)
        {
            var e = new ParticleSystem.EmitParams
            {
                position = p, velocity = v, startSize = size, startLifetime = life, startColor = color,
                rotation = rotation, applyShapeToPosition = false,
            };
            ps.Emit(e, 1);
        }

        ParticleSystem Emitter(string name, Material material, float gravity, ParticleSystemRenderMode mode,
            Gradient color = null, ParticleSystem.MinMaxCurve? size = null)
        {
            var go = new GameObject("FX " + name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 6000; main.gravityModifier = gravity; main.startSpeed = 0;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            if (color != null) { var col = ps.colorOverLifetime; col.enabled = true; col.color = color; }
            else { var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(0, .7f); }
            if (size.HasValue) { var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = size.Value; }
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            renderer.shadowCastingMode = mode == ParticleSystemRenderMode.Mesh ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            ps.Play();
            return ps;
        }
        static Gradient Gradient((Color c, float t)[] colors, (float a, float t)[] alphas)
        {
            var g = new Gradient();
            g.SetKeys(System.Array.ConvertAll(colors, k => new GradientColorKey(k.c, k.t)), System.Array.ConvertAll(alphas, k => new GradientAlphaKey(k.a, k.t)));
            return g;
        }
        // Fades in by `inEnd` and out from `outStart` (fractions of the lifetime).
        static Gradient Fade(float inEnd, float outStart) => Gradient(
            new[] { (Color.white, 0f), (Color.white, 1f) },
            inEnd > 0 ? new[] { (0f, 0f), (1f, inEnd), (1f, outStart), (0f, 1f) } : new[] { (1f, 0f), (1f, outStart), (0f, 1f) });
        static ParticleSystem.MinMaxCurve Grow(float from, float to) => new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, from, 1, to));

        // ---- Lightning, rings, afterimages, flashes -----------------------------------------------

        // A bolt that crackles between two points for `duration` seconds (re-struck each frame).
        public void Bolt(Vector3 a, Vector3 b, Color tint, float duration = .15f, float width = 1, float forks = .05f, Transform follow = null)
            => bolts.Add(new ActiveBolt { a = a, b = b, tint = tint, life = duration, duration = duration, width = width, seed = Random.Range(0, 99999), forks = forks, follow = follow });
        public void Burst(Vector3 centre, float radius, int count, Color tint, float duration = .25f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * .6f;
                Bolt(centre, centre + d * radius * Random.Range(.5f, 1f), tint, duration * Random.Range(.6f, 1f), 1, .1f);
            }
        }
        public void Shockwave(Vector3 centre, float radius, Color color, float duration = .45f)
        {
            var go = new GameObject("FX shockwave");
            go.transform.SetParent(transform, false);
            go.transform.position = centre + Vector3.up * .15f;
            go.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = ringMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rings.Add(new Ring { t = go.transform, life = duration, duration = duration, radius = radius, color = color });
        }
        // Freezes the current pose of a skinned character as a glowing afterimage.
        public GameObject Afterimage(SkinnedMeshRenderer source, Color color, float duration, Vector3 drift = default)
        {
            if (source == null) return null;
            var mesh = new Mesh { name = "Afterimage" };
            source.BakeMesh(mesh, true);
            var go = new GameObject("Speed Force afterimage") { layer = 2 };
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = ghostMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ghosts.Add(new Ghost { go = go, mesh = mesh, life = duration, duration = duration, color = color, drift = drift });
            Paint(r, color, 1);
            return go;
        }
        public void ExpireAfterimage(GameObject go)
        {
            for (int i = 0; i < ghosts.Count; i++)
                if (ghosts[i].go == go) { var g = ghosts[i]; g.life = Mathf.Min(g.life, .15f); ghosts[i] = g; }
        }
        public void ScreenFlash(Color color, float amount) { FlashColor = color; FlashAmount = Mathf.Max(FlashAmount, amount); }

        void Paint(Renderer r, Color color, float strength)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_Color", color); block.SetFloat("_Alpha", strength);
            block.SetColor("_Tint", color);
            r.SetPropertyBlock(block);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            FlashAmount = Mathf.MoveTowards(FlashAmount, 0, dt * 2.5f);
            var view = Camera.main;
            Vector3 eye = view != null ? view.transform.position : Vector3.zero;
            builder.Begin(++frame);
            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                var b = bolts[i];
                b.life -= dt;
                if (b.life <= 0) { bolts.RemoveAt(i); continue; }
                bolts[i] = b;
                Vector3 from = b.follow != null ? b.follow.position : b.a;
                float strength = Mathf.Clamp01(b.life / b.duration * 1.5f);
                builder.Bolt(from, b.b, eye, b.tint, b.width, strength, 4, .26f, b.forks);
            }
            builder.Apply(boltMesh);
            for (int i = rings.Count - 1; i >= 0; i--)
            {
                var r = rings[i];
                r.life -= dt;
                if (r.life <= 0 || r.t == null) { if (r.t != null) Destroy(r.t.gameObject); rings.RemoveAt(i); continue; }
                rings[i] = r;
                float k = 1 - r.life / r.duration;
                r.t.localScale = Vector3.one * Mathf.Lerp(.5f, r.radius, 1 - (1 - k) * (1 - k));
                Paint(r.t.GetComponent<Renderer>(), r.color * (1 - k), 1);
            }
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                var g = ghosts[i];
                g.life -= dt;
                if (g.life <= 0 || g.go == null) { if (g.go != null) Destroy(g.go); Destroy(g.mesh); ghosts.RemoveAt(i); continue; }
                ghosts[i] = g;
                g.go.transform.position += g.drift * dt;
                Paint(g.go.GetComponent<Renderer>(), g.color, Mathf.Clamp01(g.life / Mathf.Min(g.duration, .5f)));
            }
        }

        // Flat ring of unit radius; uv.y runs inner (0) to outer (1) so the additive sprite falloff makes a soft band.
        static Mesh RingMesh()
        {
            const int segments = 64;
            var v = new Vector3[segments * 2]; var uv = new Vector2[segments * 2]; var t = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                v[i * 2] = d * .82f; v[i * 2 + 1] = d;
                uv[i * 2] = new Vector2(.5f, 0); uv[i * 2 + 1] = new Vector2(.5f, 1);
                int n = (i + 1) % segments, k = i * 6;
                t[k] = i * 2; t[k + 1] = n * 2; t[k + 2] = i * 2 + 1; t[k + 3] = i * 2 + 1; t[k + 4] = n * 2; t[k + 5] = n * 2 + 1;
            }
            var colors = new Color[v.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            var mesh = new Mesh { name = "FX ring", vertices = v, uv = uv, colors = colors, triangles = t };
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnDestroy()
        {
            foreach (var m in owned) Destroy(m);
            foreach (var g in ghosts) { if (g.go != null) Destroy(g.go); Destroy(g.mesh); }
            Destroy(boltMesh); Destroy(ringMesh);
            if (Instance == this) Instance = null;
        }
    }
}
