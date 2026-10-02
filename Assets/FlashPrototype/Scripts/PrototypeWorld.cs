using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    public sealed class PrototypeWorld
    {
        public readonly List<WorldLabel> Labels = new List<WorldLabel>();
        readonly List<Material> materials = new List<Material>();
        readonly List<Transform> cars = new List<Transform>();
        readonly Transform root;
        readonly Shader shader;
        float trafficClock;
        public PrototypeWorld(Transform parent, Shader shader) { root = parent; this.shader = shader; }
        public Material Material(string name, Color color, float glow = 0)
        {
            var material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Glow", glow);
            materials.Add(material);
            return material;
        }
        public GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid = true)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid)
            {
                var collider = obj.GetComponent<Collider>();
                collider.enabled = false;
                Object.Destroy(collider);
            }
            return obj;
        }
        public GameObject Box(string name, Vector3 position, Vector3 scale, Material material, bool solid = true)
            => Shape(name, PrimitiveType.Cube, root, position, scale, material, solid);

        public void Build()
        {
            var road = Material("Asphalt", new Color(0.09f, 0.12f, 0.15f));
            var pavement = Material("Concrete", new Color(0.36f, 0.41f, 0.43f));
            var white = Material("Road paint", new Color(0.83f, 0.81f, 0.65f));
            var glass = Material("Blue glass", new Color(0.12f, 0.3f, 0.4f));
            var trim = Material("Facade trim", new Color(0.2f, 0.25f, 0.29f));
            var gold = Material("Training gold", new Color(1f, 0.67f, 0.12f), 1);
            var facades = new[] {
                Material("Stone", new Color(0.52f, 0.49f, 0.43f)),
                Material("Brick", new Color(0.39f, 0.22f, 0.19f)),
                Material("Steel", new Color(0.27f, 0.34f, 0.38f)) };
            Box("District ground", new Vector3(0, -1, 0), new Vector3(680, 2, 680), road);
            Box("Acceleration runway", new Vector3(0, -1, -665), new Vector3(40, 2, 650), road);
            for (int roadIndex = -2; roadIndex <= 2; roadIndex++)
            {
                float offset = roadIndex * 130;
                for (int stripe = -31; stripe <= 31; stripe++)
                {
                    float p = stripe * 10;
                    // Markings have no colliders; intersections remain clear.
                    if (Mathf.Abs(p - Mathf.Round(p / 130) * 130) < 14) continue;
                    Box("Lane marking", new Vector3(offset, 0.012f, p), new Vector3(0.18f, 0.02f, 4), white, false);
                    Box("Lane marking", new Vector3(p, 0.012f, offset), new Vector3(4, 0.02f, 0.18f), white, false);
                }
            }
            for (int z = -360; z > -980; z -= 20)
                Box("Runway marking", new Vector3(0, 0.012f, z), new Vector3(0.2f, 0.02f, 8), white, false);
            var random = new System.Random(2026);
            for (int x = -2; x < 2; x++)
            for (int z = -2; z < 2; z++)
            {
                float cx = x * 130 + 65, cz = z * 130 + 65;
                Box("City block", new Vector3(cx, 0.1f, cz), new Vector3(106, 0.2f, 106), pavement);
                // Reserve the southeast part of this block for the open lab and plaza.
                if (x == -1 && z == -1) continue;
                for (int bx = -1; bx <= 1; bx += 2)
                for (int bz = -1; bz <= 1; bz += 2)
                {
                    float height = 15 + random.Next(50);
                    Vector3 p = new Vector3(cx + bx * 25, height / 2 + 0.2f, cz + bz * 25);
                    Box("Building", p, new Vector3(35, height, 35), facades[random.Next(3)]);
                    Box("Roof", new Vector3(p.x, height + 0.5f, p.z), new Vector3(37, 0.6f, 37), trim);
                    // Broad window strips keep renderer count bounded for the M2.
                    Box("Windows", p + Vector3.forward * 17.55f, new Vector3(24, height * 0.75f, 0.1f), glass, false);
                    Box("Windows", p + Vector3.back * 17.55f, new Vector3(24, height * 0.75f, 0.1f), glass, false);
                }
            }
            BuildLab(pavement, glass, gold, trim);
            Box("Runway end marker", new Vector3(0, 0.025f, -960), new Vector3(30, 0.04f, 2), gold, false);
            Labels.Add(new WorldLabel(new Vector3(0, 5, -310), "ACCELERATION STRAIGHT\n650 m training lane"));
            Labels.Add(new WorldLabel(new Vector3(0, 5, 250), "CENTRAL CITY\nTraining district"));
            for (int i = 0; i < 6; i++)
            {
                var car = new GameObject("Traffic " + i).transform;
                car.SetParent(root, false);
                var paint = Material("Car paint " + i, i % 2 == 0 ? new Color(0.2f, 0.38f, 0.52f) : new Color(0.7f, 0.58f, 0.25f));
                Shape("Body", PrimitiveType.Cube, car, new Vector3(0, 0.7f, 0), new Vector3(2, 1, 4.4f), paint);
                Shape("Cabin", PrimitiveType.Cube, car, new Vector3(0, 1.4f, -0.2f), new Vector3(1.7f, 0.7f, 2.2f), glass, false);
                cars.Add(car);
            }
            TickTraffic(0);
        }
        void BuildLab(Material floor, Material glass, Material gold, Material wall)
        {
            Vector3 center = new Vector3(-55, 0.2f, -55);
            Box("Lab floor", center, new Vector3(32, 0.25f, 28), floor);
            Box("Lab back wall", center + new Vector3(0, 3, 14), new Vector3(32, 6, 0.6f), wall);
            Box("Lab left wall", center + new Vector3(-16, 3, 0), new Vector3(0.6f, 6, 28), glass);
            Box("Lab right wall", center + new Vector3(16, 3, 0), new Vector3(0.6f, 6, 28), glass);
            // A real six-metre doorway; there is no invisible wall or teleport.
            Box("Lab front left", center + new Vector3(-9.5f, 3, -14), new Vector3(13, 6, 0.6f), wall);
            Box("Lab front right", center + new Vector3(9.5f, 3, -14), new Vector3(13, 6, 0.6f), wall);
            Box("Lab roof", center + new Vector3(0, 6.3f, 0), new Vector3(33, 0.5f, 29), wall);
            Box("Door stripe", center + new Vector3(0, 5.2f, -14.4f), new Vector3(6, 0.2f, 0.1f), gold, false);
            for (int i = 0; i < 3; i++)
            {
                Box("Training console", center + new Vector3(-10 + i * 10, 1, 9), new Vector3(3, 1.8f, 1.5f), wall);
                Box("Console display", center + new Vector3(-10 + i * 10, 2, 8.7f), new Vector3(2.5f, 0.8f, 0.1f), gold, false);
            }
            Labels.Add(new WorldLabel(center + new Vector3(0, 8, -14), "S.T.A.R. LABS\nEnter through the front doorway"));
        }
        public void TickTraffic(float dt)
        {
            trafficClock += dt;
            for (int i = 0; i < cars.Count; i++)
            {
                float z = Mathf.Repeat(trafficClock * 11 + i * 87, 560) - 280;
                cars[i].position = new Vector3(i % 2 == 0 ? 124 : 136, 0, i % 2 == 0 ? z : -z);
                cars[i].rotation = Quaternion.Euler(0, i % 2 == 0 ? 0 : 180, 0);
            }
        }
        public void Dispose() { foreach (var material in materials) Object.Destroy(material); }
    }
    public readonly struct WorldLabel
    {
        public readonly Vector3 Position;
        public readonly string Text;
        public WorldLabel(Vector3 position, string text) { Position = position; Text = text; }
    }
}
