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
        // World-clock tick (slowed by speed perception, stopped while paused) for animated set pieces.
        public event System.Action<float> Ticked;
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
            if(solid && type==PrimitiveType.Cylinder)
            {
                var original=obj.GetComponent<Collider>();original.enabled=false;Object.Destroy(original);
                obj.AddComponent<MeshCollider>().sharedMesh=obj.GetComponent<MeshFilter>().sharedMesh;
            }
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
            CityDistrict.Build(this, root);
            var glass = Material("Vehicle glass", new Color(.12f,.2f,.25f));
            for (int i = 0; i < 10; i++)
            {
                var car = new GameObject("Traffic " + i).transform;
                car.SetParent(root, false);
                var paint = Material("Car paint " + i, i % 2 == 0 ? new Color(.2f,.38f,.52f) : new Color(.7f,.58f,.25f));
                Shape("Body", PrimitiveType.Cube, car, new Vector3(0,.7f,0), new Vector3(2,1,4.4f), paint);
                Shape("Cabin", PrimitiveType.Cube, car, new Vector3(0,1.4f,-.2f), new Vector3(1.7f,.7f,2.2f), glass, false);
                cars.Add(car);
            }
            TickTraffic(0);
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
            Ticked?.Invoke(dt);
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
