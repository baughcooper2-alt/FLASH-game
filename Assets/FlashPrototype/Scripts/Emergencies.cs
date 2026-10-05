using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    public enum ContextKind { Dig, Repair, Build, Extinguish, ClearAir }

    // Something the Special button does when the Flash is next to it.
    public sealed class ContextAction
    {
        public ContextKind Kind;
        public string Label;
        public Vector3 Position;
        public object Target;
    }

    // A simple procedural person who reacts to the emergency around them.
    public sealed class Civilian
    {
        public enum Mood { Waiting, Distressed, Trapped, Cheering }
        public Mood State;
        public readonly Transform Root;
        readonly Transform body, leftArm, rightArm;
        readonly float phase = Random.value * 10;
        public Civilian(PrototypeWorld w, Transform parent, Vector3 position, Material shirt, Material skin, Material pants)
        {
            Root = new GameObject("Civilian").transform;
            Root.SetParent(parent, false);
            Root.position = position;
            Root.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            body = new GameObject("Body").transform; body.SetParent(Root, false);
            w.Shape("Torso", PrimitiveType.Capsule, body, new Vector3(0, 1.22f, 0), new Vector3(.42f, .36f, .3f), shirt, false);
            w.Shape("Head", PrimitiveType.Sphere, body, new Vector3(0, 1.7f, 0), new Vector3(.22f, .26f, .24f), skin, false);
            for (int s = -1; s <= 1; s += 2)
                w.Shape("Leg", PrimitiveType.Capsule, body, new Vector3(s * .1f, .45f, 0), new Vector3(.15f, .45f, .15f), pants, false);
            leftArm = Arm(w, -1, shirt); rightArm = Arm(w, 1, shirt);
        }
        Transform Arm(PrototypeWorld w, int side, Material m)
        {
            var pivot = new GameObject("Arm").transform; pivot.SetParent(body, false);
            pivot.localPosition = new Vector3(side * .26f, 1.48f, 0);
            w.Shape("Arm", PrimitiveType.Capsule, pivot, new Vector3(0, -.3f, 0), new Vector3(.11f, .3f, .11f), m, false);
            return pivot;
        }
        public void Tick(float time)
        {
            float t = time + phase;
            switch (State)
            {
                case Mood.Trapped:
                    body.localRotation = Quaternion.Euler(80, 0, 0); body.localPosition = new Vector3(0, -.15f, 0);
                    leftArm.localRotation = Quaternion.Euler(0, 0, -20 + Mathf.Sin(t * 6) * 25); break;
                case Mood.Distressed:
                    // Coughing and waving for help.
                    body.localRotation = Quaternion.Euler(Mathf.Max(0, Mathf.Sin(t * 5)) * 18, 0, 0); body.localPosition = Vector3.zero;
                    rightArm.localRotation = Quaternion.Euler(0, 0, 150 + Mathf.Sin(t * 9) * 25);
                    leftArm.localRotation = Quaternion.Euler(-20, 0, -10); break;
                case Mood.Cheering:
                    body.localRotation = Quaternion.identity; body.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(t * 7)) * .25f;
                    leftArm.localRotation = Quaternion.Euler(0, 0, -160 + Mathf.Sin(t * 10) * 15);
                    rightArm.localRotation = Quaternion.Euler(0, 0, 160 - Mathf.Sin(t * 10) * 15); break;
                default:
                    body.localRotation = Quaternion.Euler(0, Mathf.Sin(t * .7f) * 6, 0); body.localPosition = Vector3.zero;
                    leftArm.localRotation = Quaternion.Euler(Mathf.Sin(t) * 4, 0, -6); rightArm.localRotation = Quaternion.Euler(-Mathf.Sin(t) * 4, 0, 6); break;
            }
        }
    }

    public sealed class Fire
    {
        public Vector3 Position; public float Radius, Intensity = 1;
        public bool Out => Intensity <= 0;
        public Light Glow;
        public void Suppress(float amount)
        {
            if (Out) return;
            Intensity = Mathf.Max(0, Intensity - amount);
            if (Out) { FxSystem.Instance.Smoke(Position + Vector3.up, Vector3.up * 2, 4, new Color(.35f, .35f, .36f, .8f), 2); }
        }
    }

    public sealed class Cloud
    {
        public Vector3 Centre; public float Radius, Density = 1; public Color Colour;
        public bool Clear => Density <= .05f;
        public Vector3 Drift;
    }

    public sealed class Rubble
    {
        public Vector3 Position;
        public readonly List<Transform> Chunks = new List<Transform>();
        public Civilian Trapped;
        public float Progress;
        public bool Done => Progress >= 1;
    }

    // A set of pieces that can be put back (repair) or assembled for the first time (construction).
    public sealed class Assembly
    {
        public string Name; public Vector3 Position;
        public readonly List<(Transform piece, Vector3 homePosition, Quaternion homeRotation, Vector3 fromPosition, Quaternion fromRotation)> Pieces
            = new List<(Transform, Vector3, Quaternion, Vector3, Quaternion)>();
        public float Progress = -1;     // -1 untouched, 0..1 animating
        public bool Done => Progress >= 1;
        public Material Finished;       // swapped in when construction completes
        public readonly List<Renderer> Blueprint = new List<Renderer>();
    }

    public sealed class Emergency
    {
        public string Title, Instructions;
        public Vector3 Location;
        public readonly List<Fire> Fires = new List<Fire>();
        public readonly List<Cloud> Clouds = new List<Cloud>();
        public readonly List<Rubble> Rubble = new List<Rubble>();
        public readonly List<Assembly> Repairs = new List<Assembly>();
        public readonly List<Assembly> Builds = new List<Assembly>();
        public readonly List<Civilian> Civilians = new List<Civilian>();
        public Assembly Leak;       // a gas main that keeps feeding its cloud until repaired
        public Transform Root;
        public bool Complete
        {
            get
            {
                foreach (var f in Fires) if (!f.Out) return false;
                foreach (var c in Clouds) if (!c.Clear) return false;
                foreach (var r in Rubble) if (!r.Done) return false;
                foreach (var a in Repairs) if (!a.Done) return false;
                foreach (var a in Builds) if (!a.Done) return false;
                return true;
            }
        }
        public string Progress
        {
            get
            {
                var parts = new List<string>();
                int fires = 0, clouds = 0, rubble = 0, repairs = 0, builds = 0;
                foreach (var f in Fires) if (!f.Out) fires++;
                foreach (var c in Clouds) if (!c.Clear) clouds++;
                foreach (var r in Rubble) if (!r.Done) rubble++;
                foreach (var a in Repairs) if (!a.Done) repairs++;
                foreach (var a in Builds) if (!a.Done) builds++;
                if (fires > 0) parts.Add(fires + (fires == 1 ? " fire" : " fires"));
                if (clouds > 0) parts.Add(clouds == 1 ? "smoke/gas cloud" : clouds + " clouds");
                if (rubble > 0) parts.Add(rubble + " trapped");
                if (repairs > 0) parts.Add(repairs + " to repair");
                if (builds > 0) parts.Add(builds + " to build");
                return string.Join("  •  ", parts);
            }
        }
    }

    // Rotating emergencies around the city that the environmental powers resolve.
    public sealed class EmergencyDirector : MonoBehaviour
    {
        PrototypeWorld w;
        FxSystem fx;
        Material shirtA, shirtB, shirtC, skin, pants, charred, metal, concrete, glass, wood, blueprint, gasPipe;
        public Emergency Active { get; private set; }
        public int Saved { get; private set; }
        public int Resolved { get; private set; }
        public string Banner { get; private set; }
        float bannerTimer, nextAt = 3, clock;
        int next;
        public bool Enabled = true;

        public static EmergencyDirector Create(Transform parent, PrototypeWorld world, FxSystem fx)
        {
            var go = new GameObject("Emergencies");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<EmergencyDirector>();
            d.w = world; d.fx = fx;
            d.shirtA = world.Material("Civilian shirt blue", new Color(.18f, .32f, .62f));
            d.shirtB = world.Material("Civilian shirt green", new Color(.25f, .5f, .28f));
            d.shirtC = world.Material("Civilian shirt orange", new Color(.85f, .45f, .15f));
            d.skin = world.Material("Civilian skin", new Color(.72f, .52f, .4f));
            d.pants = world.Material("Civilian trousers", new Color(.12f, .13f, .17f));
            d.charred = world.Material("Charred wreck", new Color(.08f, .07f, .07f));
            d.metal = world.Material("Shelter metal", new Color(.45f, .48f, .52f));
            d.concrete = world.Material("Rubble concrete", new Color(.48f, .46f, .43f));
            d.glass = world.Material("Shelter glass", new Color(.35f, .5f, .6f));
            d.wood = world.Material("Building timber", new Color(.55f, .4f, .25f));
            d.gasPipe = world.Material("Gas main yellow", new Color(.85f, .7f, .12f));
            d.blueprint = new Material(fx.Additive) { name = "Blueprint" };
            d.blueprint.SetColor("_Tint", new Color(.25f, .6f, 1, .5f)); d.blueprint.SetFloat("_Intensity", .8f); d.blueprint.SetFloat("_Softness", .3f);
            return d;
        }

        static readonly (string title, Vector3 position)[] Sites =
        {
            ("Fire at Lab Road", new Vector3(-260, 0, -390)),
            ("Gas leak on 2nd Street", new Vector3(-130, 0, -130)),
            ("Building collapse, East 4th", new Vector3(260, 0, -390)),
            ("Shelter needed, Market Street", new Vector3(390, 0, 0)),
        };

        public void Tick(float dt, float timeScale)
        {
            clock += dt;
            bannerTimer = Mathf.Max(0, bannerTimer - dt);
            if (bannerTimer <= 0) Banner = null;
            if (Active == null)
            {
                if (Enabled && clock >= nextAt) Begin(next++ % Sites.Length);
                return;
            }
            float world = dt * timeScale;
            var e = Active;
            foreach (var f in e.Fires)
            {
                if (f.Out) { if (f.Glow != null) f.Glow.enabled = false; continue; }
                // Fires creep back unless they are put out completely.
                f.Intensity = Mathf.Min(1, f.Intensity + world * .03f);
                if (Random.value < .9f * timeScale) fx.Flames(f.Position, f.Radius, f.Intensity);
                if (Random.value < .25f * timeScale) fx.Smoke(f.Position + Vector3.up * 2, new Vector3(Random.Range(-.3f, .3f), 2.2f, Random.Range(-.3f, .3f)), 2.5f, new Color(.18f, .17f, .17f, .75f), 4);
                if (f.Glow != null) f.Glow.intensity = (2.5f + Mathf.PerlinNoise(clock * 8, f.Radius) * 1.5f) * f.Intensity;
            }
            foreach (var c in e.Clouds)
            {
                if (e.Leak != null && !e.Leak.Done) c.Density = Mathf.Min(1, c.Density + world * .08f);
                c.Centre += c.Drift * world; c.Drift = Vector3.MoveTowards(c.Drift, Vector3.zero, world * 2);
                if (c.Clear) continue;
                int puffs = Mathf.CeilToInt(3 * c.Density * timeScale);
                for (int i = 0; i < puffs; i++)
                {
                    Vector2 r = Random.insideUnitCircle * c.Radius;
                    fx.Smoke(c.Centre + new Vector3(r.x, Random.Range(.2f, 2.2f), r.y), new Vector3(Random.Range(-.3f, .3f), .3f, Random.Range(-.3f, .3f)),
                        3.2f * Mathf.Lerp(.5f, 1, c.Density), c.Colour * new Color(1, 1, 1, c.Density), 3);
                }
            }
            foreach (var a in e.Repairs) Animate(a, dt, false);
            foreach (var a in e.Builds) Animate(a, dt, true);
            bool calm = true;
            foreach (var f in e.Fires) if (!f.Out) calm = false;
            foreach (var c in e.Clouds) if (!c.Clear) calm = false;
            foreach (var civ in e.Civilians)
            {
                if (civ.State == Civilian.Mood.Trapped) { civ.Tick(clock); continue; }
                civ.State = e.Complete ? Civilian.Mood.Cheering : calm ? Civilian.Mood.Waiting : Civilian.Mood.Distressed;
                civ.Tick(clock);
            }
            if (e.Complete)
            {
                Saved += e.Civilians.Count; Resolved++;
                Banner = "EMERGENCY RESOLVED • " + e.Civilians.Count + " civilians safe";
                bannerTimer = 6;
                var root = e.Root;
                Active = null; nextAt = clock + 18;
                Destroy(root.gameObject, 12);
            }
        }

        // ---- Power hooks -----------------------------------------------------------------------

        public void ApplyVortex(Vortex v, float dt)
        {
            if (Active == null) return;
            foreach (var f in Active.Fires)
                if (!f.Out && Flat(f.Position - v.Centre) < v.Radius * 1.6f) f.Suppress(dt * .9f * v.Strength);
            foreach (var c in Active.Clouds)
            {
                // Reverse tornado: the vortex draws smoke and gas up and away.
                float d = Flat(c.Centre - v.Centre);
                if (c.Clear || d > v.Radius * 3 + c.Radius) continue;
                c.Centre = Vector3.MoveTowards(c.Centre, v.Centre, dt * 4 * v.Strength);
                c.Density = Mathf.Max(0, c.Density - dt * .45f * v.Strength);
                if (Random.value < .5f) fx.Smoke(c.Centre + Vector3.up * 2, (v.Centre - c.Centre).normalized * 4 + Vector3.up * 6, 2, c.Colour, 2);
            }
        }
        public void ApplyWind(Vector3 origin, Vector3 direction, float range, float cone, float dt)
        {
            if (Active == null) return;
            foreach (var f in Active.Fires)
                if (!f.Out && InCone(origin, direction, f.Position, range, cone)) f.Suppress(dt * .75f);
            foreach (var c in Active.Clouds)
                if (!c.Clear && InCone(origin, direction, c.Centre, range + c.Radius, cone + 15))
                { c.Drift += direction.Flat().normalized * dt * 10; c.Density = Mathf.Max(0, c.Density - dt * .3f); }
        }
        public void ApplyVacuum(Vector3 centre, float radius)
        {
            if (Active == null) return;
            foreach (var f in Active.Fires) if (Flat(f.Position - centre) < radius) f.Suppress(1);
            foreach (var c in Active.Clouds) if (Flat(c.Centre - centre) < radius + c.Radius) c.Density = Mathf.Max(0, c.Density - .75f);
        }
        public bool AnyHazardNear(Vector3 p, float range, out Vector3 at)
        {
            at = p;
            if (Active == null) return false;
            float best = range;
            bool found = false;
            foreach (var f in Active.Fires) if (!f.Out && Vector3.Distance(f.Position, p) < best) { best = Vector3.Distance(f.Position, p); at = f.Position; found = true; }
            foreach (var c in Active.Clouds) if (!c.Clear && Vector3.Distance(c.Centre, p) < best) { best = Vector3.Distance(c.Centre, p); at = c.Centre; found = true; }
            return found;
        }

        public ContextAction FindContext(Vector3 p, float range)
        {
            if (Active == null) return null;
            ContextAction best = null; float bestDistance = range;
            void Consider(ContextKind kind, string label, Vector3 at, object target)
            {
                float d = Flat(at - p);
                if (d < bestDistance) { bestDistance = d; best = new ContextAction { Kind = kind, Label = label, Position = at, Target = target }; }
            }
            foreach (var r in Active.Rubble) if (!r.Done) Consider(ContextKind.Dig, "SPEED DIG • hold", r.Position, r);
            foreach (var a in Active.Repairs) if (a.Progress < 0) Consider(ContextKind.Repair, "RAPID REPAIR • " + a.Name, a.Position, a);
            foreach (var a in Active.Builds) if (a.Progress < 0) Consider(ContextKind.Build, "RAPID CONSTRUCTION • " + a.Name, a.Position, a);
            foreach (var f in Active.Fires) if (!f.Out) Consider(ContextKind.Extinguish, "EXTINGUISH • hold to blast with wind", f.Position, f);
            foreach (var c in Active.Clouds) if (!c.Clear) Consider(ContextKind.ClearAir, "REVERSE TORNADO • pull the cloud away", c.Centre, c);
            return best;
        }

        // Speed dig: chunks fly off the pile as progress builds; the last one frees the civilian.
        public void Dig(Rubble r, float amount)
        {
            if (r.Done) return;
            float before = r.Progress;
            r.Progress = Mathf.Min(1, r.Progress + amount);
            int removeTo = Mathf.FloorToInt(r.Progress * r.Chunks.Count);
            for (int i = Mathf.FloorToInt(before * r.Chunks.Count); i < removeTo && i < r.Chunks.Count; i++)
            {
                var chunk = r.Chunks[r.Chunks.Count - 1 - i];
                if (chunk == null || !chunk.gameObject.activeSelf) continue;
                fx.Dust(chunk.position, 3, 3, new Color(.55f, .52f, .48f, .6f));
                fx.Debris(chunk.position, 2, 6, Color.gray, .3f);
                chunk.gameObject.SetActive(false);
            }
            if (r.Done && r.Trapped != null)
            {
                r.Trapped.State = Civilian.Mood.Cheering;
                r.Trapped.Root.position += Vector3.up * .05f;
                fx.Dust(r.Position, 10, 4, new Color(.55f, .52f, .48f, .6f));
            }
        }
        public void StartAssembly(Assembly a)
        {
            if (a.Progress >= 0) return;
            a.Progress = 0;
        }

        void Animate(Assembly a, float dt, bool construction)
        {
            if (a.Progress < 0 || a.Done) return;
            a.Progress = Mathf.Min(1, a.Progress + dt / 1.1f);
            int n = a.Pieces.Count;
            for (int i = 0; i < n; i++)
            {
                var (piece, homeP, homeR, fromP, fromR) = a.Pieces[i];
                // Pieces launch one after another and arc into place.
                float start = (float)i / n * .6f, k = Mathf.Clamp01((a.Progress - start) / .4f);
                float e = k * k * (3 - 2 * k);
                Vector3 p = Vector3.Lerp(fromP, homeP, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 3;
                if (k > 0 && k < 1 && Random.value < .5f) fx.Bolt(piece.position, p, new Color(1, .6f, .15f), .05f, .6f, 0);
                piece.SetPositionAndRotation(p, Quaternion.Slerp(fromR, homeR, e));
            }
            if (!a.Done) return;
            fx.Shockwave(a.Position, 6, new Color(.4f, .8f, 1));
            foreach (var (piece, _, _, _, _) in a.Pieces)
            {
                var col = piece.GetComponent<Collider>(); if (col != null) col.enabled = true;
                if (construction && a.Finished != null) piece.GetComponent<Renderer>().sharedMaterial = a.Finished;
            }
            foreach (var r in a.Blueprint) if (r != null) r.enabled = false;
        }

        // ---- Scenario builders ------------------------------------------------------------------

        void Begin(int index)
        {
            var (title, p) = Sites[index];
            var e = new Emergency { Title = title, Location = p };
            e.Root = new GameObject("Emergency: " + title).transform;
            e.Root.SetParent(transform, false);
            switch (index)
            {
                case 0:
                    e.Instructions = "Smother the flames: Tornado Arms, Vacuum Blast, or run circles to make a tornado. Then clear the smoke.";
                    Wreck(e, p + new Vector3(-4, 0, 3), 25); Wreck(e, p + new Vector3(5, 0, -2), -60);
                    AddFire(e, p + new Vector3(-4, 1.2f, 3), 2); AddFire(e, p + new Vector3(5, 1.2f, -2), 2);
                    AddFire(e, p + new Vector3(0, .3f, 8), 2.5f);
                    e.Clouds.Add(new Cloud { Centre = p + new Vector3(10, 0, 9), Radius = 7, Colour = new Color(.2f, .2f, .21f, .8f) });
                    for (int i = 0; i < 3; i++) Person(e, p + new Vector3(9 + i * 1.6f, .25f, 11 + (i % 2)));
                    break;
                case 1:
                    e.Instructions = "Rapid-repair the gas main, then pull the gas off the civilians: circle it (reverse tornado) or blow it away.";
                    e.Leak = Pipe(e, p + new Vector3(0, 0, 0));
                    e.Repairs.Add(e.Leak);
                    e.Clouds.Add(new Cloud { Centre = p + new Vector3(3, 0, 3), Radius = 9, Colour = new Color(.45f, .75f, .25f, .7f) });
                    for (int i = 0; i < 3; i++) Person(e, p + new Vector3(2 + i * 2.2f, 0, 5 - i));
                    break;
                case 2:
                    e.Instructions = "Speed-dig the trapped civilians out of the rubble and rapid-repair the bus shelter.";
                    e.Rubble.Add(Pile(e, p + new Vector3(-6, 0, 4)));
                    e.Rubble.Add(Pile(e, p + new Vector3(7, 0, 6)));
                    e.Repairs.Add(Shelter(e, p + new Vector3(0, 0, -8), "bus shelter", false));
                    break;
                default:
                    e.Instructions = "Rapid-construct an emergency shelter for the waiting civilians.";
                    e.Builds.Add(Shelter(e, p + new Vector3(0, 0, 4), "emergency shelter", true));
                    for (int i = 0; i < 4; i++) Person(e, p + new Vector3(-6 + i * 1.5f, 0, -3));
                    break;
            }
            Active = e;
            Banner = "EMERGENCY • " + title;
            bannerTimer = 5;
        }

        void Person(Emergency e, Vector3 p)
        {
            var shirts = new[] { shirtA, shirtB, shirtC };
            e.Civilians.Add(new Civilian(w, e.Root, Ground(p), shirts[e.Civilians.Count % 3], skin, pants));
        }
        void AddFire(Emergency e, Vector3 p, float radius)
        {
            var light = new GameObject("Fire light").AddComponent<Light>();
            light.transform.SetParent(e.Root, false); light.transform.position = p + Vector3.up * 1.5f;
            light.type = LightType.Point; light.range = 14; light.color = new Color(1, .55f, .2f); light.shadows = LightShadows.None;
            e.Fires.Add(new Fire { Position = p, Radius = radius, Glow = light });
        }
        void Wreck(Emergency e, Vector3 p, float yaw)
        {
            var car = new GameObject("Burning wreck").transform; car.SetParent(e.Root, false);
            car.position = Ground(p); car.rotation = Quaternion.Euler(0, yaw, 0);
            w.Shape("Wreck body", PrimitiveType.Cube, car, new Vector3(0, .65f, 0), new Vector3(2, .9f, 4.4f), charred);
            w.Shape("Wreck cabin", PrimitiveType.Cube, car, new Vector3(0, 1.3f, -.3f), new Vector3(1.7f, .6f, 2), charred, false);
        }
        Assembly Pipe(Emergency e, Vector3 p)
        {
            var a = new Assembly { Name = "gas main", Position = p };
            var root = new GameObject("Gas main").transform; root.SetParent(e.Root, false); root.position = Ground(p);
            for (int i = 0; i < 4; i++)
                AddPiece(a, w.Shape("Pipe section", PrimitiveType.Cylinder, root, new Vector3(-3 + i * 2, .45f, 0), new Vector3(.6f, 1, .6f), gasPipe),
                    Quaternion.Euler(0, 0, 90));
            AddPiece(a, w.Shape("Valve wheel", PrimitiveType.Cylinder, root, new Vector3(0, 1.1f, 0), new Vector3(.7f, .05f, .7f), metal), Quaternion.identity);
            Scatter(a, 4);
            return a;
        }
        Rubble Pile(Emergency e, Vector3 p)
        {
            var r = new Rubble { Position = Ground(p) };
            var root = new GameObject("Rubble").transform; root.SetParent(e.Root, false); root.position = r.Position;
            var civ = new Civilian(w, root, r.Position, shirtB, skin, pants) { State = Civilian.Mood.Trapped };
            e.Civilians.Add(civ); r.Trapped = civ;
            for (int i = 0; i < 16; i++)
            {
                float a = i * 2.4f, rad = 1.6f * Mathf.Sqrt((i + 1) / 16f);
                var chunk = w.Shape("Rubble chunk", PrimitiveType.Cube, root, new Vector3(Mathf.Sin(a) * rad, .35f + (i / 6) * .45f, Mathf.Cos(a) * rad),
                    new Vector3(Random.Range(.6f, 1.3f), Random.Range(.35f, .7f), Random.Range(.6f, 1.2f)), concrete, false);
                chunk.transform.localRotation = Random.rotation;
                r.Chunks.Add(chunk.transform);
            }
            return r;
        }
        // A shelter frame: four posts, a roof and a back panel. Repairs start scattered; builds start as a
        // stack of materials beside a glowing blueprint.
        Assembly Shelter(Emergency e, Vector3 p, string name, bool construction)
        {
            var a = new Assembly { Name = name, Position = Ground(p) };
            var root = new GameObject(name).transform; root.SetParent(e.Root, false); root.position = a.Position;
            var parts = new List<(Vector3 pos, Vector3 size, Material m)>();
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2) parts.Add((new Vector3(x * 2.2f, 1.4f, z * 1.2f), new Vector3(.18f, 2.8f, .18f), metal));
            parts.Add((new Vector3(0, 2.9f, 0), new Vector3(4.8f, .15f, 2.8f), construction ? wood : metal));
            parts.Add((new Vector3(0, 1.5f, 1.25f), new Vector3(4.4f, 2, .06f), glass));
            parts.Add((new Vector3(0, .5f, .6f), new Vector3(3.5f, .1f, .5f), wood));
            foreach (var (pos, size, m) in parts)
            {
                var piece = w.Shape(name + " part", PrimitiveType.Cube, root, pos, size, m);
                piece.GetComponent<Collider>().enabled = false;
                AddPiece(a, piece, piece.transform.localRotation);
                if (construction)
                {
                    var outline = w.Shape("Blueprint", PrimitiveType.Cube, root, pos, size * 1.02f, blueprint, false);
                    outline.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    a.Blueprint.Add(outline.GetComponent<Renderer>());
                }
            }
            if (construction)
            {
                // Materials stacked neatly nearby.
                for (int i = 0; i < a.Pieces.Count; i++)
                {
                    var (piece, homeP, homeR, _, _) = a.Pieces[i];
                    Vector3 from = a.Position + new Vector3(-7, .1f + i * .22f, -2);
                    Quaternion fromR = Quaternion.Euler(0, 90, 90);
                    a.Pieces[i] = (piece, homeP, homeR, from, fromR);
                    piece.SetPositionAndRotation(from, fromR);
                }
                a.Finished = wood;
            }
            else Scatter(a, 5);
            return a;
        }
        void AddPiece(Assembly a, GameObject piece, Quaternion localRotation)
        {
            piece.transform.localRotation = localRotation;
            a.Pieces.Add((piece.transform, piece.transform.position, piece.transform.rotation, piece.transform.position, piece.transform.rotation));
        }
        void Scatter(Assembly a, float spread)
        {
            for (int i = 0; i < a.Pieces.Count; i++)
            {
                var (piece, homeP, homeR, _, _) = a.Pieces[i];
                Vector2 r = Random.insideUnitCircle * spread;
                Vector3 from = Ground(a.Position + new Vector3(r.x, 0, r.y)) + Vector3.up * .2f;
                Quaternion fromR = Random.rotation;
                a.Pieces[i] = (piece, homeP, homeR, from, fromR);
                piece.SetPositionAndRotation(from, fromR);
                var col = piece.GetComponent<Collider>(); if (col != null) col.enabled = false;
            }
        }

        static Vector3 Ground(Vector3 p) =>
            Physics.Raycast(p + Vector3.up * 20, Vector3.down, out RaycastHit hit, 40, ~(1 << 2), QueryTriggerInteraction.Ignore) ? hit.point : p;
        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
        static bool InCone(Vector3 origin, Vector3 dir, Vector3 p, float range, float cone)
        {
            Vector3 to = p - origin; to.y = 0; dir.y = 0;
            return to.magnitude < range && Vector3.Angle(dir, to) < cone;
        }
    }
}
