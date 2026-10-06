using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    public enum SpecialPower { WhirlwindPunch, TornadoArms, SpeedBarrage, LightningPunch, GroundLightning, Cyclone, SpeedSteal, VacuumBlast }

    public static class PowerCatalog
    {
        public static readonly (SpecialPower power, string name, string hint, float cost)[] Specials =
        {
            (SpecialPower.WhirlwindPunch, "Whirlwind Punch", "Spin and punch everything around you", 20),
            (SpecialPower.TornadoArms, "Tornado Arms", "Hold: wind blast that shoves bots and smothers fire", 15),
            (SpecialPower.SpeedBarrage, "Speed Barrage", "Dash between up to six bots", 30),
            (SpecialPower.LightningPunch, "Lightning Punch", "Charge your fists: the next hit chains lightning", 20),
            (SpecialPower.GroundLightning, "Ground Lightning", "Slam the ground; lightning rings outward", 25),
            (SpecialPower.Cyclone, "Cyclone", "Circle a group until a vortex traps them", 35),
            (SpecialPower.SpeedSteal, "Speed Steal", "Slow a bot and take its speed", 15),
            (SpecialPower.VacuumBlast, "Vacuum Blast", "Rip the air out of an area: stuns bots, kills fire", 30),
        };
    }

    // The Flash's powers: input in, motor dashes, damage, effects and environmental changes out.
    public sealed class SpeedForcePowers : MonoBehaviour
    {
        enum Sequence { None, MachDash, RapidPunches, Barrage, Whirlwind, Cyclone, LightningKick, GroundPound, Assembly }
        enum Channel { None, TornadoArms, Dig, Extinguish }
        const float ImpChargeTime = 4;

        public RunnerVitals Vitals { get; } = new RunnerVitals();
        public int Selected { get; private set; }
        public (SpecialPower power, string name, string hint, float cost) SelectedPower => PowerCatalog.Specials[Selected];
        public bool InfiniteMassReady { get; private set; }
        public float InfiniteMassCharge => InfiniteMassReady ? 1 : Mathf.Clamp01(topSpeedTime / ImpChargeTime);
        public string Feedback => feedbackTimer > 0 ? feedback : null;
        public ContextAction Context { get; private set; }
        public bool LightningFists => lightningFists > 0;
        public bool SpeedStealing => speedSteal > 0;
        public string ActiveMove => sequence != Sequence.None ? sequence.ToString() : channel != Channel.None ? channel.ToString() : null;
        public event Action Downed;

        SpeedsterMotor motor;
        RunnerVisual visual;
        RunnerCamera cam;
        FxSystem fx;
        BotDirector bots;
        EmergencyDirector emergencies;
        Transform vortexParent;
        string feedback;
        float feedbackTimer, clock, topSpeedTime, impDecay, lightningFists, speedSteal, comboTimer, punchHeld, lightningHeld;
        float seqTimer, seqTick, ghostTimer, seqSpeed, seqRadius, seqAngle;
        int combo, seqIndex;
        bool rightArm, downRaised, seqImp;
        Sequence sequence;
        Channel channel;
        BotEnemy seqTarget, stealTarget;
        readonly List<BotEnemy> seqTargets = new List<BotEnemy>();
        Vector3 seqCentre, seqDirection;
        object seqObject;
        Vortex playerVortex, seqVortex;
        readonly Dictionary<BotEnemy, float> tackled = new Dictionary<BotEnemy, float>();
        struct Track { public Vector3 p; public float heading, t; }
        readonly List<Track> track = new List<Track>();

        public void Init(SpeedsterMotor runner, RunnerVisual look, RunnerCamera view, FxSystem effects, BotDirector director,
            EmergencyDirector emergency, Transform vortexRoot)
        {
            motor = runner; visual = look; cam = view; fx = effects; bots = director; emergencies = emergency; vortexParent = vortexRoot;
            motor.Hit += OnMotorHit;
            motor.Events += OnMotorEvent;
            bots.PlayerHit += (damage, from) => { visual.Flinch(); Say("HIT  -" + Mathf.RoundToInt(damage)); };
        }

        Vector3 Chest => transform.position + Vector3.up * 1.1f;
        Vector3 Aim => Quaternion.Euler(0, cam.Yaw, 0) * Vector3.forward;
        Color Glow => visual.LightningGlow;
        void Say(string text, float seconds = 1.6f) { feedback = text; feedbackTimer = seconds; }
        bool Pay(float cost)
        {
            if (Vitals.Spend(cost)) return true;
            Say("NOT ENOUGH SPEED FORCE");
            return false;
        }

        public void ResetState()
        {
            EndSequence(); channel = Channel.None;
            motor.Locked = false; motor.SpeedScale = 1;
            visual.Windmill(false);
            track.Clear();
            topSpeedTime = 0; InfiniteMassReady = false; lightningFists = 0; speedSteal = 0;
            if (playerVortex != null) playerVortex.Collapse();
            downRaised = false;
        }

        public void Tick(RunnerInput input, float dt, float worldScale)
        {
            clock += dt;
            feedbackTimer = Mathf.Max(0, feedbackTimer - dt);
            Vitals.Tick(dt, motor.Speed);
            if (Vitals.Down)
            {
                if (!downRaised) { downRaised = true; EndSequence(); channel = Channel.None; motor.Locked = false; Downed?.Invoke(); }
                return;
            }
            lightningFists = Mathf.Max(0, lightningFists - dt);
            speedSteal = Mathf.Max(0, speedSteal - dt);
            motor.SpeedScale = speedSteal > 0 ? 1.35f : 1;
            ChargeInfiniteMass(dt);
            visual.Overcharged = InfiniteMassReady;
            int cycle = (input.NextPower.WasPressedThisFrame() ? 1 : 0) - (input.PreviousPower.WasPressedThisFrame() ? 1 : 0) + input.Scroll;
            if (cycle != 0) Selected = (Selected + cycle + PowerCatalog.Specials.Length * 4) % PowerCatalog.Specials.Length;
            Context = sequence == Sequence.None ? emergencies.FindContext(transform.position, 7) : Context;

            if (sequence != Sequence.None) TickSequence(input, dt);
            else if (channel != Channel.None) TickChannel(input, dt);
            else
            {
                if (input.Boost.WasPressedThisFrame()) Boost();
                if (input.Punch.WasPressedThisFrame()) { punchHeld = 0; Punch(); }
                if (input.Punch.IsPressed())
                {
                    punchHeld += dt;
                    if (punchHeld > .28f && sequence == Sequence.None) StartRapidPunches();
                }
                if (input.Lightning.WasPressedThisFrame()) lightningHeld = 0;
                if (input.Lightning.IsPressed())
                {
                    lightningHeld += dt;
                    if (lightningHeld > .45f) visual.Surge(.1f);
                }
                if (input.Lightning.WasReleasedThisFrame())
                {
                    if (lightningHeld >= .45f && motor.Grounded) { if (Pay(25)) GroundLightning(); }
                    else LightningThrow();
                }
                if (input.Afterimage.WasPressedThisFrame()) Afterimages();
                if (input.Special.WasPressedThisFrame()) Special();
            }
            CircleRunning(dt);
            foreach (var v in Vortex.All.ToArray()) v.Tick(dt, emergencies);
            Trails(dt);
        }

        // ---- Movement powers -------------------------------------------------------------------

        void Boost()
        {
            if (motor.Grounded || motor.WallRunning)
            {
                if (Vitals.Energy >= 15 && motor.StartBoost()) Vitals.Spend(15);
                else if (Vitals.Energy < 15) Say("NOT ENOUGH SPEED FORCE");
            }
            else if (Vitals.Energy >= 10)
            {
                Vector3 dir = motor.Velocity.Flat().sqrMagnitude > 4 ? motor.Velocity.Flat() : Aim;
                if (motor.AirDash(dir)) Vitals.Spend(10);
            }
        }

        void ChargeInfiniteMass(float dt)
        {
            if (motor.Speed >= 120) { topSpeedTime += dt; impDecay = 0; }
            else if (motor.Speed < 60) impDecay += dt;
            if (!InfiniteMassReady && topSpeedTime >= ImpChargeTime) { InfiniteMassReady = true; Say("INFINITE MASS PUNCH READY", 3); }
            if (impDecay > 6) { InfiniteMassReady = false; topSpeedTime = 0; }
        }

        void OnMotorEvent(MotorEvent e, Vector3 at)
        {
            switch (e)
            {
                case MotorEvent.Boost: fx.Shockwave(at, 5, Glow); visual.Surge(.6f); Say("SPEED BOOST", .8f); break;
                case MotorEvent.AirDash: fx.Burst(Chest, 2.5f, 6, Glow); Ghost(.3f); Say("AIR DASH", .8f); break;
                case MotorEvent.SpeedJump: fx.Dust(at, 10, 4, new Color(.6f, .6f, .62f, .55f)); fx.Shockwave(at, 3, Glow, .3f); break;
                case MotorEvent.InstantStop:
                    fx.Dust(at, 14, 6, new Color(.6f, .6f, .62f, .55f)); fx.Shockwave(at, 6, Color.white, .35f);
                    fx.Sparks(at + Vector3.up * .1f, transform.forward, 18, Glow, 10); Say("INSTANT STOP", .8f); break;
                case MotorEvent.DriftStart: Say("SPEED DRIFT", .8f); break;
                case MotorEvent.WaterRun: fx.Spray(at, transform.forward, 20, 8); Say("WATER RUNNING", 1.2f); break;
                case MotorEvent.CeilingRun: fx.Burst(at + Vector3.up * 1.8f, 2, 6, Glow); Say("CEILING RUN", 1.2f); break;
            }
        }

        void Trails(float dt)
        {
            ghostTimer -= dt;
            Vector3 feet = transform.position + Vector3.up * .1f;
            if (motor.OnWater && motor.Speed > 10)
            {
                fx.Spray(feet - transform.forward * .4f, -transform.forward + Vector3.up * .3f, 2, motor.Speed * .12f);
                if (UnityEngine.Random.value < .3f) fx.Spray(feet, transform.right * (UnityEngine.Random.value < .5f ? 1 : -1), 1, 4);
            }
            if (motor.Drifting) fx.Sparks(feet, -motor.Velocity.Flat() + transform.right * .3f, 3, Glow, 7);
            if (motor.Boosting && ghostTimer <= 0) { Ghost(.22f, .6f); ghostTimer = .06f; }
            // Stealing speed: a stream of yellow lightning from the drained bot.
            if (speedSteal > 5.4f && stealTarget != null && !stealTarget.Dead) fx.Bolt(stealTarget.Chest, Chest, new Color(1, .85f, .2f), .05f, 1.1f, .1f);
        }
        void Ghost(float duration, float strength = 1, Vector3 drift = default)
            => fx.Afterimage(visual.SkinnedMesh, Glow * strength, duration, drift);

        // Circle-running makes a vortex at the centre of the circle: a tornado on land, a whirlpool on water.
        void CircleRunning(float dt)
        {
            bool fast = motor.Speed >= 26 && motor.Grounded && !motor.WallRunning && !motor.CeilingRunning && sequence != Sequence.Cyclone;
            if (!fast) { if (track.Count > 0 && clock - track[track.Count - 1].t > .4f) track.Clear(); return; }
            Vector3 v = motor.Velocity.Flat();
            track.Add(new Track { p = transform.position, heading = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg, t = clock });
            while (track.Count > 0 && clock - track[0].t > 5) track.RemoveAt(0);
            float turned = 0; int first = -1;
            for (int i = track.Count - 1; i > 0; i--)
            {
                turned += Mathf.DeltaAngle(track[i - 1].heading, track[i].heading);
                if (Mathf.Abs(turned) >= 420) { first = i - 1; break; }
            }
            if (first < 0) return;
            Vector3 centre = Vector3.zero; int n = track.Count - first;
            for (int i = first; i < track.Count; i++) centre += track[i].p / n;
            float radius = 0, worst = 0;
            for (int i = first; i < track.Count; i++) radius += Vector3.Distance(track[i].p.Flat(), centre.Flat()) / n;
            for (int i = first; i < track.Count; i++) worst = Mathf.Max(worst, Mathf.Abs(Vector3.Distance(track[i].p.Flat(), centre.Flat()) - radius));
            if (radius < 3 || radius > 30 || worst > radius * .5f) return;
            FeedVortex(ref playerVortex, centre, radius, dt, true);
        }
        void FeedVortex(ref Vortex vortex, Vector3 centre, float radius, float dt, bool announce)
        {
            bool water = WaterZones.Surface(centre, out float surface);
            if (water) centre.y = surface;
            if (vortex == null || !vortex.Alive || Vector3.Distance(vortex.Centre.Flat(), centre.Flat()) > radius + 4)
            {
                vortex = Vortex.Create(vortexParent, centre, radius, water);
                if (announce) Say(water ? "WATER VORTEX" : "TORNADO", 1.5f);
            }
            vortex.Feed(centre, radius, dt);
        }

        // ---- Attacks ---------------------------------------------------------------------------

        BotEnemy FindTarget(float range, float cone)
        {
            BotEnemy best = null; float bestScore = float.MaxValue;
            Vector3 aim = Aim;
            foreach (var bot in BotEnemy.All)
            {
                if (bot.Dead) continue;
                Vector3 to = bot.Chest - Chest;
                float d = to.magnitude;
                if (d > range) continue;
                float angle = Vector3.Angle(aim, to.Flat());
                if (angle > cone && d > 3.5f) continue;
                float score = d * (1 + angle / 45);
                if (score < bestScore) { bestScore = score; best = bot; }
            }
            return best;
        }

        // Every landed blow goes through here so Lightning Punch can add its chain.
        void Strike(BotEnemy bot, float damage, Vector3 knock, float upward, float stun)
        {
            if (lightningFists > 0)
            {
                lightningFists = 0;
                damage += 35; stun = Mathf.Max(stun, 2);
                fx.Burst(bot.Chest, 3, 10, Glow, .4f);
                BotEnemy from = bot;
                var hit = new HashSet<BotEnemy> { bot };
                for (int c = 0; c < 2; c++)
                {
                    BotEnemy next = null; float best = 12;
                    foreach (var other in BotEnemy.All)
                        if (!other.Dead && !hit.Contains(other) && Vector3.Distance(other.Chest, from.Chest) < best) { best = Vector3.Distance(other.Chest, from.Chest); next = other; }
                    if (next == null) break;
                    fx.Bolt(from.Chest, next.Chest, Glow, .35f, 1.3f, .15f);
                    next.TakeHit(18, (next.Chest - from.Chest).normalized * 4, 2, 2);
                    hit.Add(next); from = next;
                }
                Say("LIGHTNING PUNCH", 1);
            }
            bot.TakeHit(damage, knock, upward, stun);
            fx.Sparks(bot.Chest, -knock, 10, new Color(1, .75f, .4f), 7);
        }

        void Punch()
        {
            // Airborne means clear of the ground, not just a frame off it (curbs, landings, respawns).
            bool airborne = !motor.Grounded && !motor.WallRunning && !motor.CeilingRunning
                && !Physics.Raycast(transform.position + Vector3.up * .2f, Vector3.down, 1.2f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            if (airborne) { LightningKick(); return; }
            var far = FindTarget(30, 32);
            if (far != null && (motor.Speed >= 26 || InfiniteMassReady) && Vector3.Distance(far.Chest, Chest) > 4) { StartMachDash(far); return; }
            var near = FindTarget(6, 75);
            if (near != null) { ComboPunch(near); return; }
            rightArm = !rightArm;
            visual.Punch(rightArm, Chest + Aim * 2, .14f);
        }

        void ComboPunch(BotEnemy target)
        {
            Vector3 dir = (target.transform.position - transform.position).Flat().normalized;
            if (Vector3.Distance(target.transform.position, transform.position) > 2.2f)
                motor.DashTowards(target.transform.position - dir * 1.4f, 5);
            motor.FaceTowards(target.transform.position);
            combo = clock - comboTimer < .75f ? combo % 3 + 1 : 1;
            comboTimer = clock;
            if (combo == 3)
            {
                // Speed Uppercut: the third hit launches.
                visual.Punch(true, target.Chest + Vector3.up * 1.5f, .22f, true);
                Strike(target, 22, dir * 2, 15, 1.6f);
                fx.Shockwave(target.transform.position, 3, Glow, .3f);
                Say("SPEED UPPERCUT", .8f);
            }
            else
            {
                rightArm = combo == 1;
                visual.Punch(rightArm, target.Chest, .14f);
                Strike(target, 12, dir * 3, 0, .5f);
            }
        }

        void StartMachDash(BotEnemy target)
        {
            sequence = Sequence.MachDash; seqTarget = target; seqTimer = 0; seqImp = InfiniteMassReady;
            seqSpeed = Mathf.Max(motor.Speed, 90);
            seqDirection = (target.transform.position - transform.position).Flat().normalized;
            entrySpeed = motor.Speed;
            motor.Locked = true;
        }
        float entrySpeed;

        void StartRapidPunches()
        {
            var target = FindTarget(3.5f, 120);
            if (target == null) return;
            sequence = Sequence.RapidPunches; seqTarget = target; seqTimer = 0; seqTick = 0;
            motor.Locked = true;
            Say("RAPID PUNCHES", 1);
        }

        void LightningKick()
        {
            var target = FindTarget(18, 70);
            sequence = target != null ? Sequence.LightningKick : Sequence.GroundPound;
            seqTarget = target; seqTimer = 0;
            visual.Kick(target != null ? target.Chest : transform.position + Vector3.down * 3 + Aim * 2, .5f);
        }

        void LightningThrow()
        {
            if (!Pay(20)) return;
            var target = FindTarget(90, 28);
            Vector3 from = visual.HandPosition(true);
            Vector3 dir = target != null ? target.Chest - from : Aim + Vector3.up * .02f;
            bots.ThrowLightning(from, dir.normalized * 150, target, 35, Glow, 1);
            visual.Punch(true, from + dir.normalized * 3, .2f);
            visual.Surge(.3f);
            Say("LIGHTNING THROW", .8f);
        }

        void GroundLightning()
        {
            Vector3 c = transform.position;
            visual.Slam(.35f);
            fx.Shockwave(c, 15, Glow, .5f);
            fx.Shockwave(c, 9, Color.white, .35f);
            fx.Dust(c, 16, 7, new Color(.6f, .6f, .62f, .5f));
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2 / 14 + UnityEngine.Random.value * .3f;
                Vector3 d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                fx.Bolt(c + Vector3.up * .2f, c + d * UnityEngine.Random.Range(10f, 15f) + Vector3.up * .2f, Glow, .45f, 1.5f, .12f);
            }
            foreach (var bot in BotEnemy.All.ToArray())
            {
                if (bot.Dead) continue;
                float d = Vector3.Distance(bot.transform.position.Flat(), c.Flat());
                if (d > 15) continue;
                bot.TakeHit(30 * (1 - d / 15) + 10, (bot.transform.position - c).Flat().normalized * 8, 6, 2);
            }
            Say("GROUND LIGHTNING", 1);
        }

        void Afterimages()
        {
            if (!Pay(25)) return;
            for (int i = 0; i < 3; i++)
            {
                float a = cam.Yaw * Mathf.Deg2Rad + (i - 1) * 1.1f;
                Vector3 offset = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 3.5f;
                var ghost = fx.Afterimage(visual.SkinnedMesh, Glow * .8f, 5);
                if (ghost == null) return;
                ghost.transform.position += offset;
                bots.AddDecoy(ghost, 5);
                fx.Bolt(Chest, ghost.transform.position + Vector3.up, Glow, .2f, 1, 0);
            }
            Say("AFTERIMAGES", 1);
        }

        void OnMotorHit(ControllerColliderHit hit, float speed)
        {
            var bot = hit.collider.GetComponentInParent<BotEnemy>();
            if (bot == null || bot.Dead || speed < 18 || sequence != Sequence.None) return;
            if (tackled.TryGetValue(bot, out float last) && clock - last < .5f) return;
            tackled[bot] = clock;
            Vector3 dir = motor.Velocity.Flat().normalized;
            Strike(bot, speed * .55f + 5, dir * speed * .35f, 5 + speed * .04f, 1.2f);
            fx.Burst(bot.Chest, 2, 6, Glow, .25f);
            motor.SetPlanarVelocity(motor.Velocity.Flat() * .85f);
            Say("SPEED TACKLE", .8f);
        }

        // ---- Specials and context actions -------------------------------------------------------

        void Special()
        {
            if (Context != null) { ContextActionStart(Context); return; }
            var (power, name, _, cost) = SelectedPower;
            switch (power)
            {
                case SpecialPower.TornadoArms:
                    if (Vitals.Energy < 5) { Say("NOT ENOUGH SPEED FORCE"); return; }
                    channel = Channel.TornadoArms; motor.Locked = true; Say(name.ToUpper(), 1); return;
                case SpecialPower.SpeedBarrage:
                    if (!ChainTargets()) { Say("NO TARGETS IN RANGE"); return; }
                    if (!Pay(cost)) return;
                    sequence = Sequence.Barrage; seqIndex = 0; seqTimer = 0; motor.Locked = true; Say(name.ToUpper(), 1); return;
                case SpecialPower.SpeedSteal:
                {
                    var target = FindTarget(25, 50);
                    if (target == null) { Say("NO TARGET"); return; }
                    if (!Pay(cost)) return;
                    target.Slow(.25f, 6); speedSteal = 6; stealTarget = target;
                    Vitals.Energy = Mathf.Min(RunnerVitals.MaxEnergy, Vitals.Energy + 30);
                    fx.Bolt(target.Chest, Chest, new Color(1, .85f, .2f), .6f, 1.4f, .15f, transform);
                    Say("SPEED STOLEN", 1.4f); return;
                }
            }
            if (!Pay(cost)) return;
            switch (power)
            {
                case SpecialPower.WhirlwindPunch:
                    sequence = Sequence.Whirlwind; seqTimer = 0; seqTick = 0; motor.Locked = true; visual.Spin(.7f, 3); break;
                case SpecialPower.LightningPunch:
                    lightningFists = 8; visual.Surge(1); fx.Burst(visual.HandPosition(true), 1.2f, 6, Glow, .3f); break;
                case SpecialPower.GroundLightning:
                    GroundLightning(); break;
                case SpecialPower.Cyclone:
                    StartCyclone(CycloneCentre(out float radius), radius); break;
                case SpecialPower.VacuumBlast:
                    VacuumBlast(); break;
            }
            Say(name.ToUpper(), 1);
        }

        void ContextActionStart(ContextAction context)
        {
            switch (context.Kind)
            {
                case ContextKind.Dig: channel = Channel.Dig; seqObject = context.Target; motor.Locked = true; Say("SPEED DIG", 1); break;
                case ContextKind.Extinguish: channel = Channel.Extinguish; seqObject = context.Target; motor.Locked = true; Say("EXTINGUISH", 1); break;
                case ContextKind.Repair:
                case ContextKind.Build:
                    emergencies.StartAssembly((Assembly)context.Target);
                    sequence = Sequence.Assembly; seqObject = context.Target; seqCentre = context.Position; seqTimer = 0; motor.Locked = true;
                    Say(context.Kind == ContextKind.Repair ? "RAPID REPAIR" : "RAPID CONSTRUCTION", 1.4f); break;
                case ContextKind.GrabBomb: emergencies.GrabBomb(); Say("GOT THE BOMB • get it to deep water", 2); break;
                case ContextKind.DropBomb: emergencies.DropBomb(); Say("BOMB DROPPED", 1.2f); break;
                case ContextKind.ClearAir:
                {
                    if (!Pay(15)) return;
                    var cloud = (Cloud)context.Target;
                    StartCyclone(cloud.Centre, Mathf.Clamp(cloud.Radius * .8f, 4, 12));
                    Say("REVERSE TORNADO", 1.4f); break;
                }
            }
        }

        bool ChainTargets()
        {
            seqTargets.Clear();
            Vector3 from = transform.position;
            for (int i = 0; i < 6; i++)
            {
                BotEnemy next = null; float best = i == 0 ? 30 : 20;
                foreach (var bot in BotEnemy.All)
                    if (!bot.Dead && !seqTargets.Contains(bot) && Vector3.Distance(bot.transform.position, from) < best) { best = Vector3.Distance(bot.transform.position, from); next = bot; }
                if (next == null) break;
                seqTargets.Add(next); from = next.transform.position;
            }
            return seqTargets.Count > 0;
        }

        Vector3 CycloneCentre(out float radius)
        {
            Vector3 centre = Vector3.zero; int n = 0;
            foreach (var bot in BotEnemy.All)
                if (!bot.Dead && Vector3.Distance(bot.transform.position, transform.position) < 22) { centre += bot.transform.position; n++; }
            if (n > 0)
            {
                centre /= n; radius = 4;
                foreach (var bot in BotEnemy.All)
                    if (!bot.Dead && Vector3.Distance(bot.transform.position, transform.position) < 22) radius = Mathf.Max(radius, Vector3.Distance(bot.transform.position.Flat(), centre.Flat()) + 2.5f);
                radius = Mathf.Min(radius, 12);
                return centre;
            }
            radius = 8;
            return emergencies.AnyHazardNear(transform.position + Aim * 10, 25, out Vector3 hazard) ? hazard : transform.position + Aim * 10;
        }
        void StartCyclone(Vector3 centre, float radius)
        {
            sequence = Sequence.Cyclone; seqCentre = centre; seqRadius = radius; seqTimer = 0;
            Vector3 offset = transform.position - centre;
            seqAngle = Mathf.Atan2(offset.x, offset.z);
            seqVortex = null;
            motor.Locked = true;
        }

        void VacuumBlast()
        {
            var target = FindTarget(20, 40);
            Vector3 point = target != null ? target.transform.position
                : emergencies.AnyHazardNear(transform.position + Aim * 10, 15, out Vector3 hazard) ? hazard : transform.position + Aim * 10;
            fx.Shockwave(point, 9, new Color(.7f, .85f, 1), .35f);
            for (int i = 0; i < 30; i++)
            {
                Vector3 r = UnityEngine.Random.onUnitSphere * 8; r.y = Mathf.Abs(r.y) * .5f;
                fx.Smoke(point + r, -r * 1.6f, 1.2f, new Color(.85f, .9f, 1, .35f), .5f);
            }
            fx.Bolt(Chest, point + Vector3.up, Glow, .25f, 1.2f, .1f);
            emergencies.ApplyVacuum(point, 8);
            foreach (var bot in BotEnemy.All.ToArray())
            {
                if (bot.Dead) continue;
                Vector3 to = point - bot.transform.position;
                if (to.Flat().magnitude > 8) continue;
                bot.TakeHit(10, to.Flat().normalized * 9, 1, 3);
            }
        }

        // ---- Sequences (moves that take over the runner for a moment) ------------------------

        void TickSequence(RunnerInput input, float dt)
        {
            seqTimer += dt; seqTick -= dt; ghostTimer -= dt;
            switch (sequence)
            {
                case Sequence.MachDash:
                {
                    if (seqTarget == null || seqTarget.Dead || seqTimer > .7f) { EndSequence(); break; }
                    Vector3 to = (seqTarget.transform.position - transform.position).Flat();
                    Vector3 goal = seqTarget.transform.position - to.normalized * 1.3f;
                    motor.DashTowards(goal, seqSpeed * dt);
                    if (ghostTimer <= 0) { Ghost(.25f, .8f); ghostTimer = .035f; }
                    if (Vector3.Distance(transform.position.Flat(), seqTarget.transform.position.Flat()) < 1.9f) MachHit(to.normalized);
                    break;
                }
                case Sequence.RapidPunches:
                {
                    if (!input.Punch.IsPressed() || seqTarget == null || seqTarget.Dead || Vector3.Distance(seqTarget.transform.position, transform.position) > 4.5f
                        || !Vitals.Spend(5 * dt)) { EndSequence(); break; }
                    motor.FaceTowards(seqTarget.transform.position);
                    if (seqTick <= 0)
                    {
                        seqTick = .05f; rightArm = !rightArm;
                        visual.Punch(rightArm, seqTarget.Chest + UnityEngine.Random.insideUnitSphere * .25f, .05f);
                        Strike(seqTarget, 2.6f, transform.forward * 1.1f, 0, .4f);
                    }
                    if (ghostTimer <= 0) { Ghost(.12f, .45f, UnityEngine.Random.insideUnitSphere * .6f); ghostTimer = .07f; }
                    break;
                }
                case Sequence.Barrage:
                {
                    if (seqTick > 0) break;
                    while (seqIndex < seqTargets.Count && (seqTargets[seqIndex] == null || seqTargets[seqIndex].Dead)) seqIndex++;
                    if (seqIndex >= seqTargets.Count) { EndSequence(); break; }
                    var bot = seqTargets[seqIndex++];
                    Vector3 before = Chest;
                    Ghost(.35f, .8f);
                    Vector3 approach = (bot.transform.position - transform.position).Flat().normalized;
                    motor.DashTowards(bot.transform.position - approach * 1.3f, 80);
                    motor.FaceTowards(bot.transform.position);
                    fx.Bolt(before, Chest, Glow, .25f, 1.2f, .08f);
                    rightArm = !rightArm;
                    visual.Punch(rightArm, bot.Chest, .08f);
                    Strike(bot, 28, approach * 8, 5, 1.5f);
                    seqTick = .09f;
                    break;
                }
                case Sequence.Whirlwind:
                {
                    if (seqTimer > .7f) { EndSequence(); break; }
                    if (seqTick <= 0)
                    {
                        seqTick = .1f;
                        foreach (var bot in BotEnemy.All.ToArray())
                        {
                            if (bot.Dead) continue;
                            Vector3 away = (bot.transform.position - transform.position).Flat();
                            if (away.magnitude < 6.5f) Strike(bot, 9, away.normalized * 10, 3, .8f);
                        }
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        float a = UnityEngine.Random.value * Mathf.PI * 2;
                        Vector3 d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                        fx.Wind(Chest + d * 1.5f + Vector3.up * UnityEngine.Random.Range(-.8f, .6f), Vector3.Cross(Vector3.up, d) * 22 + d * 6, new Color(1, 1, 1, .5f));
                    }
                    break;
                }
                case Sequence.Cyclone:
                {
                    if (seqTimer > 1.9f) { EndSequence(); break; }
                    seqAngle += 80 / seqRadius * dt;
                    Vector3 goal = seqCentre + new Vector3(Mathf.Sin(seqAngle), 0, Mathf.Cos(seqAngle)) * seqRadius;
                    goal.y = transform.position.y;
                    motor.DashTowards(goal, 200 * dt);
                    motor.FaceTowards(transform.position + new Vector3(Mathf.Cos(seqAngle), 0, -Mathf.Sin(seqAngle)));
                    if (ghostTimer <= 0) { Ghost(.3f, .7f); ghostTimer = .04f; }
                    FeedVortex(ref seqVortex, seqCentre, seqRadius, dt * 1.3f, false);
                    break;
                }
                case Sequence.LightningKick:
                {
                    if (seqTarget == null || seqTarget.Dead || seqTimer > .6f) { EndSequence(); break; }
                    Vector3 to = seqTarget.Chest - Chest;
                    motor.DashTowards(transform.position + Vector3.ClampMagnitude(to, 70 * dt), 70 * dt);
                    if (ghostTimer <= 0) { Ghost(.2f, .7f); ghostTimer = .04f; }
                    if (to.magnitude < 1.8f)
                    {
                        Strike(seqTarget, 32, to.Flat().normalized * 12, 4, 1.5f);
                        KickBlast(seqTarget.transform.position, 4, 12);
                        Say("LIGHTNING KICK", .8f);
                        motor.Launch(6);
                        EndSequence();
                    }
                    break;
                }
                case Sequence.GroundPound:
                {
                    if (motor.Grounded || seqTimer > .8f) { KickBlast(transform.position, 5, 15); Say("GROUND POUND", .8f); EndSequence(); break; }
                    motor.DashTowards(transform.position + Vector3.down * 60 * dt + motor.Velocity.Flat() * dt, 70 * dt);
                    break;
                }
                case Sequence.Assembly:
                {
                    var a = (Assembly)seqObject;
                    if (a.Done || seqTimer > 1.6f) { EndSequence(); break; }
                    Zip(seqCentre, 4);
                    break;
                }
            }
        }

        void MachHit(Vector3 dir)
        {
            var target = seqTarget;
            visual.Punch(true, target.Chest, .18f);
            if (seqImp)
            {
                // Infinite Mass Punch: everything the run-up built goes into one blow.
                Strike(target, 650, dir * 40, 12, 3);
                foreach (var bot in BotEnemy.All.ToArray())
                {
                    if (bot.Dead || bot == target) continue;
                    float d = Vector3.Distance(bot.transform.position, target.transform.position);
                    if (d < 22) bot.TakeHit(160 * (1 - d / 22) + 40, (bot.transform.position - target.transform.position).Flat().normalized * 25, 10, 3);
                }
                fx.Shockwave(target.transform.position, 22, Color.white, .7f);
                fx.Shockwave(target.transform.position, 14, Glow, .5f);
                fx.Burst(target.Chest, 8, 18, Color.white, .5f);
                fx.Dust(target.transform.position, 30, 12, new Color(.65f, .65f, .66f, .6f));
                fx.ScreenFlash(Color.white, 1);
                InfiniteMassReady = false; topSpeedTime = 0;
                Say("INFINITE MASS PUNCH", 2.5f);
            }
            else
            {
                Strike(target, 20 + entrySpeed * .6f, dir * (10 + entrySpeed * .25f), 6, 2);
                fx.Shockwave(target.transform.position, 4 + entrySpeed * .04f, Glow, .35f);
                fx.Burst(target.Chest, 2.5f, 8, Glow, .3f);
                Say("MACH PUNCH", 1);
            }
            EndSequence();
            motor.SetPlanarVelocity(dir * entrySpeed * .4f);
        }

        void KickBlast(Vector3 at, float radius, float damage)
        {
            fx.Shockwave(at, radius * 1.4f, Glow, .4f);
            fx.Burst(at + Vector3.up * .5f, radius, 10, Glow, .35f);
            fx.Dust(at, 10, 5, new Color(.6f, .6f, .62f, .5f));
            foreach (var bot in BotEnemy.All.ToArray())
            {
                if (bot.Dead) continue;
                Vector3 away = (bot.transform.position - at).Flat();
                if (away.magnitude < radius) bot.TakeHit(damage, away.normalized * 6, 4, 1.5f);
            }
        }

        // Speed work on an object: afterimages flicker all around it while the Flash zips about.
        void Zip(Vector3 centre, float radius)
        {
            if (ghostTimer > 0) return;
            ghostTimer = .05f;
            float a = UnityEngine.Random.value * Mathf.PI * 2;
            Vector3 spot = centre + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius * UnityEngine.Random.Range(.6f, 1f);
            var ghost = fx.Afterimage(visual.SkinnedMesh, Glow * .7f, .18f);
            if (ghost != null) ghost.transform.position = new Vector3(spot.x, transform.position.y, spot.z);
            fx.Bolt(Chest, spot + Vector3.up, Glow, .08f, .8f, 0);
        }

        // ---- Channels (held powers) ----------------------------------------------------------

        void TickChannel(RunnerInput input, float dt)
        {
            ghostTimer -= dt;
            if (!input.Special.IsPressed()) { channel = Channel.None; motor.Locked = false; visual.Windmill(false); return; }
            switch (channel)
            {
                case Channel.TornadoArms:
                    if (!Vitals.Spend(15 * dt)) { Say("NOT ENOUGH SPEED FORCE"); channel = Channel.None; motor.Locked = false; visual.Windmill(false); return; }
                    WindBlast(Aim, dt);
                    break;
                case Channel.Extinguish:
                {
                    var fire = (Fire)seqObject;
                    Vector3 dir = (fire.Position - transform.position).Flat().normalized;
                    Vitals.Spend(4 * dt);
                    WindBlast(dir, dt);
                    if (fire.Out) { channel = Channel.None; motor.Locked = false; visual.Windmill(false); Say("FIRE OUT", 1.2f); }
                    break;
                }
                case Channel.Dig:
                {
                    var rubble = (Rubble)seqObject;
                    emergencies.Dig(rubble, dt * .9f);
                    Zip(rubble.Position, 2.5f);
                    if (rubble.Done) { channel = Channel.None; motor.Locked = false; Say("CIVILIAN FREED", 1.5f); }
                    break;
                }
            }
        }

        // Tornado Arms: windmilling arms throw a cone of wind.
        void WindBlast(Vector3 dir, float dt)
        {
            dir = dir.Flat().normalized;
            motor.FaceTowards(transform.position + dir);
            visual.Windmill(true);
            emergencies.ApplyWind(Chest, dir, 25, 30, dt);
            foreach (var bot in BotEnemy.All.ToArray())
            {
                if (bot.Dead) continue;
                Vector3 to = (bot.transform.position - transform.position).Flat();
                if (to.magnitude > 25 || Vector3.Angle(dir, to) > 32) continue;
                bot.TakeHit(6 * dt, dir * 45 * dt, UnityEngine.Random.value < dt * 2 ? 3 : 0, .4f);
            }
            for (int i = 0; i < 8; i++)
            {
                Vector3 jitter = UnityEngine.Random.insideUnitSphere * .8f;
                fx.Wind(Chest + transform.right * (i % 2 == 0 ? .5f : -.5f) + jitter, dir * UnityEngine.Random.Range(22, 34) + jitter * 4, new Color(.85f, .9f, 1, .45f));
            }
            if (UnityEngine.Random.value < .5f) fx.Dust(transform.position + dir * UnityEngine.Random.Range(4, 18), 1, 5, new Color(.7f, .7f, .72f, .4f));
        }

        void EndSequence()
        {
            if (sequence == Sequence.None) return;
            sequence = Sequence.None;
            seqTarget = null; seqObject = null;
            motor.Locked = false;
        }
    }
}
