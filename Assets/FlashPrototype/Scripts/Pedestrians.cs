using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    // Townspeople walking the sidewalks near the player. Each one walks the loop of pavement around a city block
    // (inside the lamps and trees, outside the shop doors) and ducks when the Flash blasts past. People far behind
    // the player are moved to a block nearby, so the streets around you always have someone on them.
    public sealed class Pedestrians : MonoBehaviour
    {
        const int Count = 18;
        const float Loop = 49.4f;         // half-size of the walking loop around a block centre
        sealed class Walker { public Townsperson Person; public Vector2 Block; public float S, Speed, Flinch; public int Dir; }
        readonly List<Walker> walkers = new List<Walker>();
        readonly System.Random rng = new System.Random(7);
        Transform player;
        bool paused;

        public static Pedestrians Create(Transform parent, Transform player)
        {
            var prefab = Resources.Load<GameObject>("Townsperson");
            var go = new GameObject("Pedestrians");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<Pedestrians>();
            p.player = player;
            if (prefab == null) { Debug.LogWarning("Resources/Townsperson missing: run Flash > Configure Barry Allen."); return p; }
            for (int i = 0; i < Count; i++)
            {
                var person = Instantiate(prefab, go.transform).GetComponent<Townsperson>();
                person.Dress(p.rng);
                var w = new Walker { Person = person, Dir = p.rng.NextDouble() < .5 ? 1 : -1, Speed = 1.25f + (float)p.rng.NextDouble() * .5f };
                p.Place(w, true);
                p.walkers.Add(w);
            }
            return p;
        }

        // A block near the player that has pavement: not the S.T.A.R. Labs grounds, not off the map.
        void Place(Walker w, bool anywhere)
        {
            Vector3 at = player.position;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int dx = rng.Next(-2, 3), dz = rng.Next(-2, 3);
                if (!anywhere && Mathf.Abs(dx) < 1 && Mathf.Abs(dz) < 1) continue;
                float cx = Mathf.Round((at.x - 65) / 130) * 130 + 65 + dx * 130, cz = Mathf.Round((at.z - 65) / 130) * 130 + 65 + dz * 130;
                if (Mathf.Abs(cx) > 715 || Mathf.Abs(cz) > 715) continue;
                if (Mathf.Abs(cx - CityDistrict.LabCenter.x) < 190 && Mathf.Abs(cz - CityDistrict.LabCenter.z) < 160) continue;
                w.Block = new Vector2(cx, cz);
                w.S = (float)rng.NextDouble() * 8 * Loop;
                Move(w, 0);
                return;
            }
        }

        // Position on the loop from a distance along it (four straight sides, counter-clockwise).
        static Vector3 OnLoop(Vector2 block, float s, out Vector3 heading)
        {
            s = Mathf.Repeat(s, 8 * Loop);
            int side = Mathf.FloorToInt(s / (2 * Loop));
            float t = s - side * 2 * Loop - Loop;
            Vector3 p;
            switch (side)
            {
                case 0: p = new Vector3(t, 0, -Loop); heading = Vector3.right; break;
                case 1: p = new Vector3(Loop, 0, t); heading = Vector3.forward; break;
                case 2: p = new Vector3(-t, 0, Loop); heading = Vector3.left; break;
                default: p = new Vector3(-Loop, 0, -t); heading = Vector3.back; break;
            }
            return new Vector3(block.x, .24f, block.y) + p;
        }

        void Move(Walker w, float dt)
        {
            w.S += w.Speed * w.Dir * dt;
            Vector3 p = OnLoop(w.Block, w.S, out Vector3 heading);
            w.Person.transform.position = p;
            var look = Quaternion.LookRotation(heading * w.Dir);
            w.Person.transform.rotation = dt > 0 ? Quaternion.Slerp(w.Person.transform.rotation, look, 1 - Mathf.Exp(-6 * dt)) : look;
        }

        public void SetPaused(bool value)
        {
            paused = value;
            foreach (var w in walkers) if (w.Person.Animator != null) w.Person.Animator.speed = value ? 0 : 1;
        }

        public void Tick(float dt, float timeScale, float playerSpeed)
        {
            if (paused) return;
            float world = dt * timeScale;
            Vector3 me = player.position;
            foreach (var w in walkers)
            {
                if (w.Person.Animator != null) w.Person.Animator.speed = timeScale;
                Vector3 p = w.Person.transform.position;
                float d = Vector3.Distance(p, me);
                if (d > 230) { Place(w, false); continue; }
                // A speedster tearing past makes people duck and turn to look.
                if (playerSpeed > 18 && d < 9) w.Flinch = 1.6f;
                if (w.Flinch > 0)
                {
                    w.Flinch -= world;
                    w.Person.Posing = Townsperson.Pose.Cower;
                    w.Person.SetSpeed(0);
                    Vector3 to = me - p; to.y = 0;
                    if (to.sqrMagnitude > .01f) w.Person.transform.rotation = Quaternion.Slerp(w.Person.transform.rotation, Quaternion.LookRotation(to), 1 - Mathf.Exp(-5 * world));
                    continue;
                }
                w.Person.Posing = Townsperson.Pose.None;
                w.Person.SetSpeed(w.Speed);
                Move(w, world);
            }
        }
    }
}
