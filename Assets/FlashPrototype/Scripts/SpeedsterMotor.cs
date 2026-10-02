using UnityEngine;

namespace FlashGame
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SpeedsterMotor : MonoBehaviour
    {
        public static readonly string[] TierNames = { "NORMAL", "SUPER SPEED", "MACH", "SPEED FORCE" };
        public static readonly float[] TierSpeeds = { 7f, 28f, 65f, 130f };
        public int Tier { get; private set; }
        public float Speed => planarVelocity.magnitude;
        public Vector3 PreviousPosition { get; private set; }
        public bool Grounded => controller.isGrounded;
        public float ImpactTimer { get; private set; }
        public float DistanceTravelled { get; private set; }
        CharacterController controller;
        Vector3 planarVelocity;
        float verticalSpeed, coyote, jumpBuffer;
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
            PreviousPosition = transform.position;
            if (input.Faster.WasPressedThisFrame()) Tier = Mathf.Min(3, Tier + 1);
            if (input.Slower.WasPressedThisFrame()) Tier = Mathf.Max(0, Tier - 1);
            ImpactTimer = Mathf.Max(0, ImpactTimer - dt);
            Vector2 axes = input.Move;
            Vector3 desired = Quaternion.Euler(0, yaw, 0) * new Vector3(axes.x, 0, axes.y);
            bool brake = input.Brake.IsPressed();
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
            jumpBuffer = input.Jump.WasPressedThisFrame() ? 0.12f : jumpBuffer - dt;
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
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
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
        }
    }
}
