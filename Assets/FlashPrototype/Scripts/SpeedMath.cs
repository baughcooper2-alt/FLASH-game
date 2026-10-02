using UnityEngine;

namespace FlashGame
{
    public static class SpeedMath
    {
        // Swept checkpoint test: fast runners can cross a gate between frames.
        public static bool SegmentHitsSphere(Vector3 from, Vector3 to, Vector3 center, float radius)
        {
            Vector3 segment = to - from;
            float t = segment.sqrMagnitude < 0.000001f ? 0f
                : Mathf.Clamp01(Vector3.Dot(center - from, segment) / segment.sqrMagnitude);
            return (from + segment * t - center).sqrMagnitude <= radius * radius;
        }
        public static int MovementSteps(float distance) => Mathf.Max(1, Mathf.CeilToInt(distance / 0.75f));
    }
}
