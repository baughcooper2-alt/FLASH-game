using UnityEngine;

namespace FlashGame
{
    // Health and Speed Force energy. Energy fills while running fast and pays for powers.
    public sealed class RunnerVitals
    {
        public const float MaxHealth = 100, MaxEnergy = 100;
        public float Health = MaxHealth, Energy = 60;
        public float HurtFlash { get; private set; }
        float sinceHurt = 99, invulnerable;
        public bool Down => Health <= 0;

        public void Tick(float dt, float speed)
        {
            sinceHurt += dt;
            invulnerable = Mathf.Max(0, invulnerable - dt);
            HurtFlash = Mathf.Max(0, HurtFlash - dt * 2);
            // Speed heals: regeneration starts quickly and is faster while running.
            if (sinceHurt > 2.5f && !Down) Health = Mathf.Min(MaxHealth, Health + (6 + speed * .05f) * dt);
            Energy = Mathf.Min(MaxEnergy, Energy + (2.5f + Mathf.Clamp(speed, 0, 130) * .09f) * dt);
        }
        public bool Spend(float amount)
        {
            if (Energy < amount) return false;
            Energy -= amount;
            return true;
        }
        public bool Damage(float amount)
        {
            if (invulnerable > 0 || Down) return false;
            Health = Mathf.Max(0, Health - amount);
            sinceHurt = 0; invulnerable = .35f; HurtFlash = 1;
            return true;
        }
        public void Restore() { Health = MaxHealth; Energy = Mathf.Max(Energy, 60); sinceHurt = 99; invulnerable = 1; }
    }

    public static class Limbs
    {
        // Swings a two-bone limb so it points along `direction` (world space), blended by `weight`.
        public static void Aim(Transform upper, Transform lower, Transform end, Vector3 direction, float weight)
        {
            if (weight <= 0 || upper == null || lower == null || end == null || direction.sqrMagnitude < 1e-6f) return;
            direction.Normalize();
            Vector3 axis = (lower.position - upper.position).normalized;
            upper.rotation = Quaternion.Slerp(upper.rotation, Quaternion.FromToRotation(axis, direction) * upper.rotation, weight);
            Vector3 axis2 = (end.position - lower.position).normalized;
            lower.rotation = Quaternion.Slerp(lower.rotation, Quaternion.FromToRotation(axis2, direction) * lower.rotation, weight);
        }
    }
}
