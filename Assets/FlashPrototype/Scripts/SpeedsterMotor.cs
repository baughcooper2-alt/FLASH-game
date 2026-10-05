using System;
using UnityEngine;

namespace FlashGame
{
    public enum MotorEvent { Boost, AirDash, SpeedJump, InstantStop, DriftStart, WaterRun, Sank, CeilingRun, Collided }

    // Per-frame movement commands. Pressed flags are true only on the frame the button goes down.
    public struct MotorInput
    {
        public Vector2 Move;
        public float Yaw;
        public bool Brake, BrakePressed, Jump;
    }

    [RequireComponent(typeof(CharacterController))]
    public sealed class SpeedsterMotor : MonoBehaviour
    {
        public static readonly string[] TierNames = { "NORMAL", "SUPER SPEED", "MACH", "SPEED FORCE" };
        public static readonly float[] TierSpeeds = { 7f, 28f, 65f, 130f };
        public const float WaterRunSpeed = 24, CeilingRunSpeed = 16;
        public int Tier { get; private set; }
        public float Speed => WallRunning ? climbSpeed : planarVelocity.magnitude;
        public Vector3 Velocity => planarVelocity + Vector3.up * verticalSpeed;
        public bool WallRunning { get; private set; }
        public bool Cresting { get; private set; }
        public bool CeilingRunning { get; private set; }
        public bool OnWater { get; private set; }
        public bool Drifting { get; private set; }
        public bool Boosting => boostTimer > 0;
        public bool AirDashReady => !airDashUsed;
        public Vector3 WallNormal { get; private set; }
        public float VerticalSpeed => verticalSpeed;
        public Vector3 PreviousPosition { get; private set; }
        public bool Grounded => controller.isGrounded || OnWater || CeilingRunning;
        public float ImpactTimer { get; private set; }
        public float DistanceTravelled { get; private set; }
        // Extra top speed granted by powers (Speed Steal); 1 = none.
        public float SpeedScale = 1;
        // Powers that hold the runner in place (rapid punches) set this.
        public bool Locked;
        public event Action<MotorEvent, Vector3> Events;
        public event Action<ControllerColliderHit, float> Hit;
        public CharacterController Controller => controller;

        CharacterController controller;
        Vector3 planarVelocity;
        float verticalSpeed, coyote, jumpBuffer;
        float climbSpeed, wallCooldown, boostTimer, stopTimer, dashGlide;
        bool airDashUsed;
        Vector3 crestTarget;
        const int SurfaceMask = ~(1 << 2);

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = 1.85f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0, 0.94f, 0);
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0;
            controller.slopeLimit = 50;
        }
        public void Tick(RunnerInput input, float yaw, float dt)
        {
            if (input.Faster.WasPressedThisFrame()) Tier = Mathf.Min(3, Tier + 1);
            if (input.Slower.WasPressedThisFrame()) Tier = Mathf.Max(0, Tier - 1);
            Step(new MotorInput
            {
                Move = input.Move, Yaw = yaw, Brake = input.Brake.IsPressed(),
                BrakePressed = input.Brake.WasPressedThisFrame(), Jump = input.Jump.WasPressedThisFrame(),
            }, dt);
        }
        // Shared by actual input and deterministic physics checks.
        public void TickControls(Vector2 axes, bool brake, bool jump, float yaw, float dt)
            => Step(new MotorInput { Move = axes, Brake = brake, Jump = jump, Yaw = yaw }, dt);
        public void SetTier(int tier) => Tier = Mathf.Clamp(tier, 0, 3);

        // Movement powers, paid for by SpeedForcePowers.
        public bool StartBoost()
        {
            if (!Grounded && !WallRunning || Locked) return false;
            boostTimer = 1.4f;
            Raise(MotorEvent.Boost);
            return true;
        }
        public bool AirDash(Vector3 direction)
        {
            if (Grounded || WallRunning || airDashUsed || Locked) return false;
            direction.y = 0;
            if (direction.sqrMagnitude < .01f) direction = transform.forward;
            planarVelocity = direction.normalized * Mathf.Max(Speed, 45);
            verticalSpeed = Mathf.Max(verticalSpeed, 3);
            airDashUsed = true; dashGlide = .3f;
            Raise(MotorEvent.AirDash);
            return true;
        }
        // Moves the runner with collision (power dashes); returns how far it got.
        public float DashTowards(Vector3 target, float maxDistance)
        {
            Vector3 delta = target - transform.position;
            delta = Vector3.ClampMagnitude(delta, maxDistance);
            Vector3 before = transform.position;
            Sweep(delta, false);
            if (delta.sqrMagnitude > .01f)
            {
                Vector3 flat = new Vector3(delta.x, 0, delta.z);
                if (flat.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(flat);
            }
            return Vector3.Distance(before, transform.position);
        }
        public void FaceTowards(Vector3 point)
        {
            Vector3 flat = point - transform.position; flat.y = 0;
            if (flat.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(flat);
        }
        public void SetPlanarVelocity(Vector3 velocity) { velocity.y = 0; planarVelocity = velocity; }
        public void Launch(float upward) { verticalSpeed = upward; coyote = 0; }

        public void Step(MotorInput input, float dt)
        {
            dt = Mathf.Clamp(dt, 0, 0.05f);
            Vector2 axes = Vector2.ClampMagnitude(input.Move, 1);
            PreviousPosition = transform.position;
            wallCooldown = Mathf.Max(0, wallCooldown - dt);
            ImpactTimer = Mathf.Max(0, ImpactTimer - dt);
            boostTimer = Mathf.Max(0, boostTimer - dt);
            dashGlide = Mathf.Max(0, dashGlide - dt);
            Vector3 desired = Quaternion.Euler(0, input.Yaw, 0) * new Vector3(axes.x, 0, axes.y);
            if (Locked)
            {
                planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, 400 * dt);
                desired = Vector3.zero; axes = Vector2.zero; input.Jump = false;
            }
            if (WallRunning)
            {
                TickWall(desired, axes.magnitude, input.Brake, input.Jump, dt);
                DistanceTravelled += Vector3.Distance(PreviousPosition, transform.position);
                return;
            }
            if (CeilingRunning)
            {
                TickCeiling(desired, axes.magnitude, input, dt);
                DistanceTravelled += Vector3.Distance(PreviousPosition, transform.position);
                return;
            }
            if (!Locked && wallCooldown == 0 && Tier > 0 && Speed >= 12 && !input.Brake && desired.sqrMagnitude > 0.1f
                && Physics.Raycast(transform.position + Vector3.up * 0.8f, desired.normalized, out RaycastHit wall,
                    2 + Speed * dt, SurfaceMask, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(wall.normal.y) < 0.15f && wall.collider.bounds.size.y >= 4
                && Vector3.Dot(desired.normalized, -wall.normal) > 0.75f)
            {
                climbSpeed = Mathf.Clamp(Speed, 12, 32);
                WallRunning = true; WallNormal = wall.normal; OnWater = false; Drifting = false;
                // Preserve collision checks even when one fast frame reaches the facade.
                MoveWall(-WallNormal * Mathf.Max(0, wall.distance - 1.8f));
                planarVelocity = Vector3.zero; verticalSpeed = 0; coyote = 0; jumpBuffer = 0; airDashUsed = false;
                TickWall(desired, axes.magnitude, false, input.Jump, dt);
                DistanceTravelled += Vector3.Distance(PreviousPosition, transform.position);
                return;
            }

            bool grounded = controller.isGrounded || OnWater;
            // Brake tap = instant stop; brake held while steering hard at speed = drift.
            float angle = Speed > 1 && desired.sqrMagnitude > .09f ? Vector3.Angle(planarVelocity, desired) : 0;
            if (input.BrakePressed && grounded && Speed > 12)
            {
                if (angle > 25 && Speed > 25) { Drifting = true; Raise(MotorEvent.DriftStart); }
                else { stopTimer = .12f; Raise(MotorEvent.InstantStop); }
            }
            if (!input.Brake || !grounded || Speed < 10) Drifting = false;
            stopTimer = Mathf.Max(0, stopTimer - dt);

            float topSpeed = TierSpeeds[Tier] * SpeedScale;
            if (Boosting) topSpeed = Mathf.Max(topSpeed * 1.5f, 42);
            float targetSpeed = input.Brake && !Drifting ? 0 : topSpeed * axes.magnitude;
            float acceleration = targetSpeed < Speed ? (input.Brake ? 210f : 85f) : (Boosting ? 320f : 35f + Tier * 32f);
            if (stopTimer > 0) { targetSpeed = 0; acceleration = Mathf.Max(acceleration, Speed / Mathf.Max(stopTimer, dt)); }
            if (Drifting) { targetSpeed = Speed * .85f; acceleration = Speed * .15f; }
            Vector3 direction = Speed > 0.15f ? planarVelocity.normalized : desired.normalized;
            if (desired.sqrMagnitude > 0.001f)
            {
                float turnRate = Drifting ? 300f : Mathf.Lerp(700f, 100f, Mathf.Clamp01(Speed / 130f));
                if (!grounded) turnRate *= .5f;
                direction = Vector3.RotateTowards(direction, desired.normalized, turnRate * Mathf.Deg2Rad * dt, 0);
            }
            float newSpeed = grounded || targetSpeed > Speed ? Mathf.MoveTowards(Speed, targetSpeed, acceleration * dt) : Speed;
            if (!grounded && dashGlide <= 0) newSpeed = Mathf.MoveTowards(Speed, Mathf.Min(Speed, targetSpeed + 20), 8 * dt);
            planarVelocity = direction * newSpeed;

            if (grounded) { coyote = 0.12f; airDashUsed = false; if (verticalSpeed < 0) verticalSpeed = -3; }
            else coyote -= dt;
            jumpBuffer = input.Jump ? 0.12f : jumpBuffer - dt;
            if (jumpBuffer > 0 && coyote > 0)
            {
                // Speed Jump: momentum becomes height.
                verticalSpeed = Mathf.Min(34, 12 + Mathf.Max(0, Speed - 12) * .16f);
                jumpBuffer = 0; coyote = 0; OnWater = false;
                if (Speed > 28) Raise(MotorEvent.SpeedJump);
            }
            float gravity = dashGlide > 0 ? 8 : 32;
            verticalSpeed = Mathf.Max(-65, verticalSpeed - gravity * dt);
            Vector3 displacement = (planarVelocity + Vector3.up * verticalSpeed) * dt;
            Vector3 ceilingNormal = Sweep(displacement, true);

            // Water running: the harbour holds a runner up only above WaterRunSpeed.
            Vector3 p = transform.position;
            if (WaterZones.Surface(p, out float surface))
            {
                bool wasOnWater = OnWater;
                OnWater = p.y <= surface + .15f && verticalSpeed <= 0 && Speed >= (wasOnWater ? WaterRunSpeed - 2 : WaterRunSpeed);
                if (OnWater)
                {
                    if (p.y < surface) controller.Move(Vector3.up * (surface - p.y));
                    verticalSpeed = 0;
                    if (!wasOnWater) Raise(MotorEvent.WaterRun);
                }
                else if (p.y < surface - 1.2f) { Raise(MotorEvent.Sank); }
            }
            else OnWater = false;

            // Jumping into a ceiling at speed sticks to it.
            if (ceilingNormal != Vector3.zero && Speed >= 20 && Tier > 0)
                StartCeiling(planarVelocity);

            Vector3 traveled = transform.position - PreviousPosition;
            traveled.y = 0;
            DistanceTravelled += traveled.magnitude;
            if (planarVelocity.sqrMagnitude > 0.1f && !Locked)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(planarVelocity), 1 - Mathf.Exp(-16 * dt));
        }

        // Collision-stepped move. Returns the normal of a ceiling hit while rising, or zero.
        Vector3 Sweep(Vector3 displacement, bool reactToCeiling)
        {
            Vector3 ceiling = Vector3.zero;
            int steps = SpeedMath.MovementSteps(displacement.magnitude);
            Vector3 step = displacement / steps;
            for (int i = 0; i < steps; i++)
            {
                CollisionFlags flags = controller.Move(step);
                if ((flags & CollisionFlags.Above) != 0)
                {
                    if (reactToCeiling && verticalSpeed > 2) ceiling = Vector3.down;
                    verticalSpeed = Mathf.Min(0, verticalSpeed); step.y = Mathf.Min(0, step.y);
                }
                if ((flags & CollisionFlags.Sides) != 0) break;
            }
            return ceiling;
        }

        // Ceiling running: hang head-down with the capsule top on the ceiling and keep momentum.
        void StartCeiling(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude < .01f || !CeilingAbove(out _)) return;
            CeilingRunning = true; WallRunning = false; Cresting = false; OnWater = false; Drifting = false;
            planarVelocity = direction.normalized * Mathf.Max(Speed, climbSpeed, 20);
            verticalSpeed = 0; airDashUsed = false;
            Raise(MotorEvent.CeilingRun);
        }
        // Space between the capsule top and the ceiling found by CeilingAbove (cast from the capsule centre).
        static float CeilingGap(RaycastHit hit) => hit.distance - .675f;
        bool CeilingAbove(out RaycastHit hit) =>
            Physics.SphereCast(transform.position + Vector3.up * .94f, .25f, Vector3.up, out hit, 1.6f, SurfaceMask, QueryTriggerInteraction.Ignore)
            && hit.normal.y < -.7f;
        void TickCeiling(Vector3 desired, float amount, MotorInput input, float dt)
        {
            if (input.Jump || input.Brake) { DropFromCeiling(); return; }
            float target = Mathf.Max(TierSpeeds[Tier] * SpeedScale, 20) * (Boosting ? 1.5f : 1) * Mathf.Max(amount, .0f);
            Vector3 direction = planarVelocity.sqrMagnitude > .01f ? planarVelocity.normalized : transform.forward;
            if (desired.sqrMagnitude > .001f)
                direction = Vector3.RotateTowards(direction, desired.normalized, Mathf.Lerp(400, 90, Speed / 130) * Mathf.Deg2Rad * dt, 0);
            planarVelocity = direction * Mathf.MoveTowards(Speed, target, (target < Speed ? 30 : 60) * dt);
            if (Speed < CeilingRunSpeed || !CeilingAbove(out RaycastHit ceiling)) { DropFromCeiling(); return; }
            // Hold the capsule just under the ceiling, then travel along it.
            float gap = CeilingGap(ceiling) - .02f;
            Vector3 before = transform.position;
            Sweep(planarVelocity * dt + Vector3.up * Mathf.Clamp(gap, -.5f, .5f), false);
            if (Vector3.Distance(before, transform.position) < Speed * dt * .3f && dt > 0) { ImpactTimer = .6f; DropFromCeiling(); return; }
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(planarVelocity), 1 - Mathf.Exp(-16 * dt));
        }
        void DropFromCeiling() { CeilingRunning = false; verticalSpeed = -4; wallCooldown = .3f; }

        void TickWall(Vector3 desired, float amount, bool brake, bool jump, float dt)
        {
            if (jump) { LeaveWall(12, 12); return; }
            if (brake || amount < 0.2f || Tier == 0 || Vector3.Dot(desired.normalized, -WallNormal) < 0.25f)
            { LeaveWall(4, 0); return; }
            if (Cresting)
            {
                Vector3 target = transform.position.y < crestTarget.y - 0.04f
                    ? new Vector3(transform.position.x, crestTarget.y, transform.position.z) : crestTarget;
                Vector3 before = transform.position;
                MoveWall(Vector3.ClampMagnitude(target - before, 14 * dt));
                if (Vector3.Distance(transform.position, crestTarget) < 0.08f)
                { Vector3 forward = -WallNormal; LeaveWall(0, 0); planarVelocity = forward * 14; }
                else if (Vector3.Distance(before, transform.position) < 0.001f && dt > 0)
                    LeaveWall(4, 0);
                return;
            }
            Vector3 p = transform.position;
            // Stand clear of facade trim; find a real walkable roof and rise above it before moving inward.
            if (Physics.Raycast(p - WallNormal * 3 + Vector3.up * 6, Vector3.down,
                    out RaycastHit roof, 6.5f, SurfaceMask, QueryTriggerInteraction.Ignore)
                && roof.normal.y > 0.7f && roof.point.y > p.y + 0.25f && roof.point.y <= p.y + 2.5f)
            {
                crestTarget = roof.point + Vector3.up * 0.08f;
                Cresting = true;
                return;
            }
            // An overhang or ceiling above turns the climb into a ceiling run, carrying on away from the wall.
            if (CeilingAbove(out RaycastHit overhead) && CeilingGap(overhead) < .3f)
            {
                WallRunning = false;
                StartCeiling(WallNormal);
                if (CeilingRunning) return;
                WallRunning = true;
            }
            if (!Physics.Raycast(p + Vector3.up * 0.8f, -WallNormal, out RaycastHit face,
                    4, SurfaceMask, QueryTriggerInteraction.Ignore) || Mathf.Abs(face.normal.y) > 0.15f
                || Vector3.Dot(face.normal, WallNormal) < 0.9f)
            { LeaveWall(4, 6); return; }
            // Speed Climb: boosting drives the climb far faster.
            climbSpeed = Mathf.MoveTowards(climbSpeed, Boosting ? 60 : Mathf.Min(TierSpeeds[Tier], 32), (Boosting ? 160 : 45) * dt);
            float correction = Mathf.Clamp(face.distance - 1.8f, -8 * dt, 8 * dt);
            Vector3 beforeMove = transform.position;
            bool blocked = MoveWall(Vector3.up * climbSpeed * dt - WallNormal * correction);
            if (blocked && CeilingAbove(out _))
            {
                WallRunning = false;
                StartCeiling(WallNormal);
                if (CeilingRunning) return;
                WallRunning = true;
            }
            if (transform.position.y - beforeMove.y < climbSpeed * dt * 0.2f && dt > 0)
                LeaveWall(4, 0);
        }
        bool MoveWall(Vector3 displacement)
        {
            int steps = SpeedMath.MovementSteps(displacement.magnitude);
            for (int i = 0; i < steps; i++)
                if ((controller.Move(displacement / steps) & CollisionFlags.Above) != 0) return true;
            return false;
        }
        void LeaveWall(float outward, float upward)
        {
            WallRunning = false; Cresting = false; wallCooldown = 0.45f;
            planarVelocity = WallNormal * outward; verticalSpeed = upward;
            climbSpeed = 0; coyote = 0; jumpBuffer = 0;
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Hit?.Invoke(hit, Speed);
            if (WallRunning || CeilingRunning) return;
            // Bots are handled as tackles by SpeedForcePowers.
            if (hit.collider.GetComponentInParent<BotEnemy>() != null) return;
            // Glancing scrapes slide. Head-on impacts cost momentum, not a health bar.
            if (Mathf.Abs(hit.normal.y) > 0.5f || Speed < 1f) return;
            float approach = Vector3.Dot(planarVelocity.normalized, -hit.normal);
            if (approach < 0.25f) return;
            if (Speed > 22 && approach > 0.6f && stopTimer <= 0) ImpactTimer = 1.2f;
            planarVelocity = Vector3.ProjectOnPlane(planarVelocity, hit.normal) * 0.65f;
        }
        void Raise(MotorEvent e) => Events?.Invoke(e, transform.position);
        public void Respawn(Vector3 position)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            controller.enabled = true;
            planarVelocity = Vector3.zero;
            verticalSpeed = 0; coyote = 0; jumpBuffer = 0; Tier = 0; ImpactTimer = 0;
            PreviousPosition = position;
            WallRunning = false; Cresting = false; climbSpeed = 0; wallCooldown = 0; WallNormal = Vector3.zero;
            CeilingRunning = false; OnWater = false; Drifting = false; boostTimer = 0; stopTimer = 0; dashGlide = 0;
            airDashUsed = false; SpeedScale = 1; Locked = false;
        }
    }

    // Water surfaces a fast runner can cross (registered by the city builder).
    public static class WaterZones
    {
        static readonly System.Collections.Generic.List<(Rect area, float surface)> zones = new System.Collections.Generic.List<(Rect, float)>();
        public static void Clear() => zones.Clear();
        public static void Add(Rect area, float surface) => zones.Add((area, surface));
        public static bool Surface(Vector3 p, out float surface)
        {
            foreach (var (area, y) in zones)
                if (area.Contains(new Vector2(p.x, p.z))) { surface = y; return true; }
            surface = 0;
            return false;
        }
    }
}
