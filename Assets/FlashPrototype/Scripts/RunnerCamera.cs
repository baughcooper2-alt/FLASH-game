using UnityEngine;

namespace FlashGame
{
    [RequireComponent(typeof(Camera))]
    public sealed class RunnerCamera : MonoBehaviour
    {
        public float Yaw { get; private set; }
        public bool DynamicFov = true;
        float pitch = 14, distance = 6;
        float wallLook;
        Camera view;
        void Awake() { view = GetComponent<Camera>(); view.nearClipPlane = 0.08f; view.farClipPlane = 4000; }
        public void ReadLook(Vector2 delta)
        {
            Yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -25, 65);
        }
        public void Follow(SpeedsterMotor runner, float dt, bool snap = false)
        {
            Vector3 pivot = runner.transform.position + Vector3.up * (runner.WallRunning ? 0.5f : 1.45f);
            wallLook = Mathf.Lerp(wallLook, runner.WallRunning ? 30 : 0, 1 - Mathf.Exp(-5 * dt));
            Quaternion rotation = Quaternion.Euler(pitch - wallLook, Yaw, 0);
            float wantedDistance = Mathf.Lerp(5, 8, Mathf.Clamp01(runner.Speed / 100));
            if (Physics.SphereCast(pivot, 0.2f, rotation * Vector3.back, out RaycastHit hit,
                wantedDistance, ~(1 << 2), QueryTriggerInteraction.Ignore))
                wantedDistance = Mathf.Max(0.25f, hit.distance - 0.12f);
            distance = snap || wantedDistance < distance ? wantedDistance
                : Mathf.Lerp(distance, wantedDistance, 1 - Mathf.Exp(-8 * dt));
            transform.SetPositionAndRotation(pivot + rotation * Vector3.back * distance, rotation);
            view.fieldOfView = Mathf.Lerp(view.fieldOfView,
                DynamicFov ? Mathf.Lerp(65, 88, Mathf.Clamp01(runner.Speed / 130)) : 65, 1 - Mathf.Exp(-5 * dt));
        }
        public void ResetView() { Yaw = 0; pitch = 14; distance = 5; wallLook = 0; }
    }
}
