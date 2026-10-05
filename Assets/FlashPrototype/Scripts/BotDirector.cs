using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    // Runs bot incursions: waves that warp in at city sites, their projectiles, decoy targeting,
    // and the Flash's thrown lightning. Bots and their bolts run on the world clock (speed perception).
    public sealed class BotDirector : MonoBehaviour
    {
        public static readonly (string name, Vector3 position)[] Sites =
        {
            ("Lab Road South", new Vector3(-390, 0, -520)),
            ("Downtown Grid", new Vector3(0, 0, -260)),
            ("Central Square", new Vector3(130, 0, 260)),
            ("Harbour Promenade", new Vector3(762, .5f, -520)),
            ("Midtown", new Vector3(-130, 0, 390)),
            ("Riverside Avenue", new Vector3(650, 0, 130)),
        };
        public const float AggroRange = 70;

        sealed class Projectile
        {
            public Vector3 position, velocity;
            public float life, damage;
            public bool fromPlayer;
            public BotEnemy homing;
            public Color color;
            public int chains;
        }

        SpeedsterMotor runner;
        RunnerVitals vitals;
        FxSystem fx;
        GameObject prefab;
        readonly List<Projectile> projectiles = new List<Projectile>();
        readonly List<(GameObject ghost, float until)> decoys = new List<(GameObject, float)>();
        readonly List<Material> owned = new List<Material>();
        Material[] visors;
        float clock, nextWave = 6;
        int siteIndex;

        public bool Enabled = true;
        public int Wave { get; private set; }
        public int Destroyed { get; private set; }
        public bool WaveActive { get; private set; }
        public string SiteName => Sites[siteIndex].name;
        public Vector3 SiteCentre => Sites[siteIndex].position;
        public float NextWaveIn => WaveActive ? 0 : Mathf.Max(0, nextWave - clock);
        public int Remaining { get { int n = 0; foreach (var b in BotEnemy.All) if (!b.Dead) n++; return n; } }
        public event Action<float, Vector3> PlayerHit;
        public static readonly Color BoltRed = new Color(1, .12f, .08f);

        public static BotDirector Create(Transform parent, SpeedsterMotor runner, RunnerVitals vitals, FxSystem fx)
        {
            var go = new GameObject("Bot incursions");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<BotDirector>();
            d.runner = runner; d.vitals = vitals; d.fx = fx;
            d.prefab = Resources.Load<GameObject>("EnemyBot");
            if (d.prefab == null) Debug.LogWarning("Resources/EnemyBot prefab missing: run Flash > Configure enemy bot.");
            var colours = new[] { new Color(1, .1f, .06f), new Color(1, .55f, .05f), new Color(.9f, .1f, 1) };
            d.visors = Array.ConvertAll(colours, c =>
            {
                var m = new Material(fx.Additive) { name = "Bot visor" };
                m.SetColor("_Tint", c); m.SetFloat("_Intensity", 5); m.SetFloat("_Softness", .5f);
                d.owned.Add(m);
                return m;
            });
            return d;
        }
        public Material VisorMaterial(BotKind kind) => visors[(int)kind];

        public BotEnemy Spawn(BotKind kind, Vector3 position, Vector3 home)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, transform);
            go.name = kind + " bot";
            var bot = go.AddComponent<BotEnemy>();
            bot.Init(this, kind, position, home);
            fx.Burst(position + Vector3.up, 2.5f, 6, BoltRed, .3f);
            fx.Dust(position, 6, 3, new Color(.6f, .6f, .62f, .5f));
            return bot;
        }

        public void Clear()
        {
            foreach (var bot in BotEnemy.All.ToArray()) if (bot != null) Destroy(bot.gameObject);
            BotEnemy.All.Clear();
            projectiles.Clear();
            WaveActive = false; nextWave = clock + 6;
        }
        public void SetPaused(bool paused)
        {
            if (!paused) return;
            foreach (var bot in BotEnemy.All) bot.Freeze();
        }

        public void Tick(float dt, float timeScale)
        {
            float world = dt * timeScale;
            clock += dt;
            foreach (var bot in BotEnemy.All.ToArray()) if (bot != null) bot.Tick(world, timeScale);
            for (int i = decoys.Count - 1; i >= 0; i--)
                if (decoys[i].ghost == null || clock > decoys[i].until) { PopDecoy(i, false); }
            TickProjectiles(dt, timeScale);
            if (!Enabled) return;
            if (WaveActive && Remaining == 0)
            {
                WaveActive = false; nextWave = clock + 20;
                siteIndex = (siteIndex + 1) % Sites.Length;
            }
            if (!WaveActive && clock >= nextWave) StartWave();
        }

        void StartWave()
        {
            Wave++;
            WaveActive = true;
            Vector3 site = SiteCentre;
            int count = Mathf.Min(3 + Wave, 10);
            for (int i = 0; i < count; i++)
            {
                var kind = Wave >= 3 && i == 0 ? BotKind.Heavy : Wave >= 2 && i % 3 == 1 ? BotKind.Gunner : BotKind.Striker;
                Vector2 offset = UnityEngine.Random.insideUnitCircle * 12;
                Vector3 p = site + new Vector3(offset.x, .2f, offset.y);
                if (Physics.Raycast(p + Vector3.up * 30, Vector3.down, out RaycastHit ground, 60, ~(1 << 2), QueryTriggerInteraction.Ignore)
                    && ground.normal.y > .7f && ground.point.y < site.y + 3)
                    p = ground.point + Vector3.up * .05f;
                Spawn(kind, p, site);
            }
        }

        // Bots chase the nearest afterimage decoy first, then the Flash.
        public Transform ChooseTarget(BotEnemy bot, out bool isDecoy)
        {
            isDecoy = false;
            Transform best = null; float bestDistance = 40;
            foreach (var (ghost, _) in decoys)
            {
                if (ghost == null) continue;
                float d = Vector3.Distance(ghost.transform.position, bot.transform.position);
                if (d < bestDistance) { bestDistance = d; best = ghost.transform; }
            }
            if (best != null) { isDecoy = true; bot.Aggro = true; return best; }
            if (vitals.Down) return null;
            float toPlayer = Vector3.Distance(runner.transform.position, bot.transform.position);
            if (toPlayer < AggroRange) bot.Aggro = true;
            return bot.Aggro ? runner.transform : null;
        }

        public void MeleeHit(BotEnemy bot, Transform target, float damage)
        {
            Vector3 contact = bot.Chest + bot.transform.forward * .8f;
            for (int i = 0; i < decoys.Count; i++)
                if (decoys[i].ghost != null && decoys[i].ghost.transform == target) { PopDecoy(i, true); return; }
            if (target != runner.transform) return;
            Vector3 toPlayer = runner.transform.position + Vector3.up - contact;
            if (toPlayer.magnitude > 2.4f) { fx.Sparks(contact, bot.transform.forward, 4, new Color(1, .5f, .3f)); return; }
            DamagePlayer(damage, bot.Chest);
        }

        public void DamagePlayer(float damage, Vector3 from)
        {
            if (!vitals.Damage(damage)) return;
            Vector3 away = runner.transform.position - from; away.y = 0;
            fx.Sparks(runner.transform.position + Vector3.up * 1.1f, away, 10, new Color(1, .35f, .2f));
            if (runner.Grounded && !runner.WallRunning && !runner.CeilingRunning)
                runner.SetPlanarVelocity(runner.Velocity * .3f + away.normalized * 6);
            PlayerHit?.Invoke(damage, from);
        }

        public void FireBolt(BotEnemy bot, Vector3 from, Transform target)
        {
            Vector3 aim = (target.position + Vector3.up * 1.1f) - from;
            projectiles.Add(new Projectile { position = from, velocity = aim.normalized * 42, life = 2.5f, damage = 9, color = BoltRed });
            fx.Sparks(from, aim, 6, BoltRed, 5);
        }

        public void ThrowLightning(Vector3 from, Vector3 velocity, BotEnemy homing, float damage, Color color, int chains)
            => projectiles.Add(new Projectile { position = from, velocity = velocity, homing = homing, damage = damage, color = color, chains = chains, life = 1.5f, fromPlayer = true });

        public void AddDecoy(GameObject ghost, float seconds) => decoys.Add((ghost, clock + seconds));
        public int DecoyCount => decoys.Count;

        void PopDecoy(int index, bool struck)
        {
            var ghost = decoys[index].ghost;
            decoys.RemoveAt(index);
            if (ghost == null) return;
            Vector3 p = ghost.transform.position + Vector3.up;
            if (struck)
            {
                // A struck afterimage bursts into lightning and stuns whatever hit it.
                fx.Burst(p, 3.5f, 10, Color.white, .35f);
                foreach (var bot in BotEnemy.All.ToArray())
                    if (!bot.Dead && Vector3.Distance(bot.Chest, p) < 3.5f) bot.TakeHit(15, (bot.Chest - p).normalized * 6, 3, 1.4f);
            }
            fx.ExpireAfterimage(ghost);
        }

        void TickProjectiles(float dt, float timeScale)
        {
            Vector3 playerChest = runner.transform.position + Vector3.up * 1.1f;
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var p = projectiles[i];
                float step = p.fromPlayer ? dt : dt * timeScale;
                p.life -= step;
                if (p.homing != null && !p.homing.Dead)
                {
                    float speed = p.velocity.magnitude;
                    p.velocity = Vector3.RotateTowards(p.velocity, p.homing.Chest - p.position, 9 * step, 0).normalized * speed;
                }
                Vector3 from = p.position, to = p.position + p.velocity * step;
                bool done = p.life <= 0;
                if (!done && p.fromPlayer)
                {
                    foreach (var bot in BotEnemy.All.ToArray())
                        if (!bot.Dead && SegmentDistance(bot.Chest, from, to) < 1.1f) { HitBot(p, bot, to); done = true; break; }
                }
                else if (!done)
                {
                    if (SegmentDistance(playerChest, from, to) < .7f && !vitals.Down) { DamagePlayer(p.damage, from); done = true; }
                    for (int d = 0; !done && d < decoys.Count; d++)
                        if (decoys[d].ghost != null && SegmentDistance(decoys[d].ghost.transform.position + Vector3.up, from, to) < 1) { PopDecoy(d, true); done = true; }
                }
                if (!done && Physics.Linecast(from, to, out RaycastHit wall, ~(1 << 2), QueryTriggerInteraction.Ignore)
                    && wall.collider.GetComponentInParent<BotEnemy>() == null)
                {
                    fx.Sparks(wall.point, wall.normal, p.fromPlayer ? 14 : 6, p.color, 7);
                    done = true; to = wall.point;
                }
                p.position = to;
                float tail = p.fromPlayer ? 5 : 1.6f;
                fx.Bolt(to - p.velocity.normalized * tail, to, p.color, Mathf.Max(Time.deltaTime, .016f) * 1.3f, p.fromPlayer ? 1.5f : .8f, p.fromPlayer ? .12f : 0);
                if (done) projectiles.RemoveAt(i);
            }
        }

        void HitBot(Projectile p, BotEnemy bot, Vector3 at)
        {
            bot.TakeHit(p.damage, p.velocity.normalized * 7, 3, 1.2f);
            fx.Burst(bot.Chest, 2.5f, 8, p.color, .3f);
            fx.Sparks(bot.Chest, -p.velocity, 16, p.color, 8);
            // Chain lightning jumps to the nearest other bots.
            BotEnemy from = bot;
            var hit = new HashSet<BotEnemy> { bot };
            for (int c = 0; c < p.chains; c++)
            {
                BotEnemy next = null; float best = 12;
                foreach (var other in BotEnemy.All)
                {
                    if (other.Dead || hit.Contains(other)) continue;
                    float d = Vector3.Distance(other.Chest, from.Chest);
                    if (d < best) { best = d; next = other; }
                }
                if (next == null) break;
                fx.Bolt(from.Chest, next.Chest, p.color, .35f, 1.2f, .15f);
                next.TakeHit(p.damage * .5f, (next.Chest - from.Chest).normalized * 4, 2, 1.2f);
                hit.Add(next); from = next;
            }
        }

        public void OnBotDestroyed(BotEnemy bot)
        {
            Destroyed++;
            Vector3 c = bot.Chest;
            fx.Debris(c, 14, 7, Color.white, .22f);
            fx.Sparks(c, Vector3.up, 26, new Color(1, .6f, .25f), 11);
            fx.Burst(c, 3, 8, BoltRed, .35f);
            fx.Shockwave(bot.transform.position, 5, new Color(1, .45f, .15f));
            vitals.Energy = Mathf.Min(RunnerVitals.MaxEnergy, vitals.Energy + 10);
        }

        public static float SegmentDistance(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0 : Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector3.Distance(point, a + ab * t);
        }

        void OnDestroy()
        {
            foreach (var m in owned) Destroy(m);
            BotEnemy.All.Clear();
        }
    }
}
