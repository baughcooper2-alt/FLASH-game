using UnityEngine;

namespace FlashGame
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SpeedsterMotor : MonoBehaviour
    {
        public static readonly string[] TierNames = { "NORMAL", "SUPER SPEED", "MACH", "SPEED FORCE" };
        public static readonly float[] TierSpeeds = { 7f, 28f, 65f, 130f };
        public int Tier { get; private set; }
        public float Speed => WallRunning ? climbSpeed : planarVelocity.magnitude;
        public bool WallRunning { get; private set; }
        public bool Cresting { get; private set; }
        public Vector3 WallNormal { get; private set; }
        public float VerticalSpeed => verticalSpeed;
        public Vector3 PreviousPosition { get; private set; }
        public bool Grounded => controller.isGrounded;
        public float ImpactTimer { get; private set; }
        public float DistanceTravelled { get; private set; }
        CharacterController controller;
        Vector3 planarVelocity;
        float verticalSpeed, coyote, jumpBuffer;
        float climbSpeed, wallCooldown;
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
            TickControls(input.Move, input.Brake.IsPressed(), input.Jump.WasPressedThisFrame(), yaw, dt);
        }
        // Shared by actual input and deterministic physics checks.
        public void TickControls(Vector2 axes, bool brake, bool jump, float yaw, float dt)
        {
            dt = Mathf.Clamp(dt, 0, 0.05f);
            axes = Vector2.ClampMagnitude(axes, 1);
            PreviousPosition = transform.position;
            wallCooldown = Mathf.Max(0, wallCooldown - dt);
            ImpactTimer = Mathf.Max(0, ImpactTimer - dt);
            Vector3 desired = Quaternion.Euler(0, yaw, 0) * new Vector3(axes.x, 0, axes.y);
            if (WallRunning)
            {
                TickWall(desired, axes.magnitude, brake, jump, dt);
                DistanceTravelled += Vector3.Distance(PreviousPosition, transform.position);
                return;
            }
            if (wallCooldown == 0 && Tier > 0 && Speed >= 12 && !brake && desired.sqrMagnitude > 0.1f
                && Physics.Raycast(transform.position + Vector3.up * 0.8f, desired.normalized, out RaycastHit wall,
                    2 + Speed * dt, SurfaceMask, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(wall.normal.y) < 0.15f && wall.collider.bounds.size.y >= 4
                && Vector3.Dot(desired.normalized, -wall.normal) > 0.75f)
            {
                climbSpeed = Mathf.Clamp(Speed, 12, 32);
                WallRunning = true; WallNormal = wall.normal;
                // Preserve collision checks even when one fast frame reaches the facade.
                MoveWall(-WallNormal * Mathf.Max(0, wall.distance - 1.8f));
                planarVelocity = Vector3.zero; verticalSpeed = 0; coyote = 0; jumpBuffer = 0;
                TickWall(desired, axes.magnitude, false, jump, dt);
                DistanceTravelled += Vector3.Distance(PreviousPosition, transform.position);
                return;
            }
            float targetSpeed = brake ? 0 : TierSpeeds[Tier] * axes.magnitude;
            float acceleration = targetSpeed < Speed ? (brake ? 210f : 85f) : 35f + Tier * 32f;
            Vector3 direction = Speed > 0.15f ? planarVelocity.normalized : desired.normalized;
            if (desired.sqrMagnitude > 0.001f)
            {
                float turnRate = Mathf.Lerp(700f, 100f, Mathf.Clamp01(Speed / 130f));
                direction = Vector3.RotateTowards(direction, desired.normalized, turnRate * Mathf.Deg2Rad * dt, 0);
            }
            planarVelocity = direction * Mathf.MoveTowards(Speed, targetSpeed, acceleration * dt);
            if (controller.isGrounded) { coyote = 0.12f; if (verticalSpeed < 0) verticalSpeed = -3; }
            else coyote -= dt;
            jumpBuffer = jump ? 0.12f : jumpBuffer - dt;
            if (jumpBuffer > 0 && coyote > 0) { verticalSpeed = 12; jumpBuffer = 0; coyote = 0; }
            verticalSpeed = Mathf.Max(-65, verticalSpeed - 32f * dt);
            Vector3 displacement = (planarVelocity + Vector3.up * verticalSpeed) * dt;
            int steps = SpeedMath.MovementSteps(displacement.magnitude);
            Vector3 step = displacement / steps;
            for (int i = 0; i < steps; i++)
            {
                CollisionFlags flags = controller.Move(step);
                if ((flags & CollisionFlags.Above) != 0) { verticalSpeed = Mathf.Min(0, verticalSpeed); step.y = Mathf.Min(0, step.y); }
                if ((flags & CollisionFlags.Sides) != 0) break;
            }
            Vector3 traveled = transform.position - PreviousPosition;
            traveled.y = 0;
            DistanceTravelled += traveled.magnitude;
            if (planarVelocity.sqrMagnitude > 0.1f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(planarVelocity), 1 - Mathf.Exp(-16 * dt));
        }
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
            if (!Physics.Raycast(p + Vector3.up * 0.8f, -WallNormal, out RaycastHit face,
                    4, SurfaceMask, QueryTriggerInteraction.Ignore) || Mathf.Abs(face.normal.y) > 0.15f
                || Vector3.Dot(face.normal, WallNormal) < 0.9f)
            { LeaveWall(4, 6); return; }
            climbSpeed = Mathf.MoveTowards(climbSpeed, Mathf.Min(TierSpeeds[Tier], 32), 45 * dt);
            float correction = Mathf.Clamp(face.distance - 1.8f, -8 * dt, 8 * dt);
            Vector3 beforeMove = transform.position;
            MoveWall(Vector3.up * climbSpeed * dt - WallNormal * correction);
            if (transform.position.y - beforeMove.y < climbSpeed * dt * 0.2f && dt > 0)
                LeaveWall(4, 0);
        }
        void MoveWall(Vector3 displacement)
        {
            int steps = SpeedMath.MovementSteps(displacement.magnitude);
            for (int i = 0; i < steps; i++)
                if ((controller.Move(displacement / steps) & CollisionFlags.Above) != 0) break;
        }
        void LeaveWall(float outward, float upward)
        {
            WallRunning = false; Cresting = false; wallCooldown = 0.45f;
            planarVelocity = WallNormal * outward; verticalSpeed = upward;
            climbSpeed = 0; coyote = 0; jumpBuffer = 0;
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (WallRunning) return;
            // Glancing scrapes slide. Head-on impacts cost momentum, not a health bar.
            if (Mathf.Abs(hit.normal.y) > 0.5f || Speed < 1f) return;
            float approach = Vector3.Dot(planarVelocity.normalized, -hit.normal);
            if (approach < 0.25f) return;
            if (Speed > 22 && approach > 0.6f) ImpactTimer = 1.2f;
            planarVelocity = Vector3.ProjectOnPlane(planarVelocity, hit.normal) * 0.65f;
        }
        public void Respawn(Vector3 position)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            controller.enabled = true;
            planarVelocity = Vector3.zero;
            verticalSpeed = 0; coyote = 0; jumpBuffer = 0; Tier = 0; ImpactTimer = 0;
            PreviousPosition = position;
            WallRunning = false; Cresting = false; climbSpeed = 0; wallCooldown = 0; WallNormal = Vector3.zero;
        }
    }
}
