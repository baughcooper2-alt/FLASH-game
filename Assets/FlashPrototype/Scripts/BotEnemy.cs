using System.Collections.Generic;
using UnityEngine;

namespace FlashGame
{
    public enum BotKind { Striker, Gunner, Heavy }

    // An enemy robot (the supplied enemy-bot model). Strikers and Heavies brawl; Gunners keep their distance
    // and fire energy bolts. Bots can be stunned, slowed, launched, knocked back and caught in vortices.
    public sealed class BotEnemy : MonoBehaviour
    {
        public static readonly List<BotEnemy> All = new List<BotEnemy>();
        public BotKind Kind { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public bool Dead { get; private set; }
        public bool Trapped => vortex != null;
        public bool Stunned => stun > 0;
        public bool Aggro;
        public Vector3 Chest => transform.position + Vector3.up * (1.1f * scale);
        public string StateLabel => Dead ? "" : Trapped ? "CAUGHT" : Stunned ? "STUNNED" : slowTimer > 0 ? "SLOWED" : "";

        BotDirector director;
        CharacterController controller;
        Animator animator;
        SkinnedMeshRenderer skin;
        Transform rightUpper, rightLower, rightHand, leftUpper, leftLower, leftHand, head;
        Renderer visor;
        MaterialPropertyBlock block;
        Vector3 knock, moveVelocity, home, lastPosition;
        float vertical, stun, slowTimer, slow = 1, attackTimer, attackLength, cooldown, hitFlash, stuck, strafe = 1, scale = 1, airborne;
        bool striking;
        Vortex vortex;
        float orbitAngle, orbitHeight, orbitRadius;
        Transform target;

        public void Init(BotDirector owner, BotKind kind, Vector3 position, Vector3 homePosition)
        {
            director = owner; Kind = kind; home = homePosition;
            scale = kind == BotKind.Heavy ? 1.3f : 1;
            transform.localScale = Vector3.one * scale;
            transform.position = position;
            MaxHealth = Health = kind switch { BotKind.Heavy => 260, BotKind.Gunner => 80, _ => 110 };
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = 1.9f; controller.radius = .38f; controller.center = new Vector3(0, .95f, 0);
            controller.stepOffset = .35f; controller.skinWidth = .04f; controller.minMoveDistance = 0;
            animator = GetComponentInChildren<Animator>();
            skin = GetComponentInChildren<SkinnedMeshRenderer>();
            if (animator != null && animator.isHuman)
            {
                rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
            }
            block = new MaterialPropertyBlock();
            // Glowing visor so bots read at a distance and against the white city.
            var v = GameObject.CreatePrimitive(PrimitiveType.Cube);
            v.name = "Visor"; DestroyImmediate(v.GetComponent<Collider>());
            v.transform.SetParent(transform, false);
            v.transform.localScale = new Vector3(.2f, .045f, .03f);
            visor = v.GetComponent<Renderer>();
            visor.sharedMaterial = director.VisorMaterial(kind);
            visor.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lastPosition = position;
            strafe = Random.value < .5f ? -1 : 1;
            All.Add(this);
        }
        void OnDestroy() => All.Remove(this);

        public void TakeHit(float damage, Vector3 knockback, float upward, float stunSeconds)
        {
            if (Dead) return;
            Health -= damage;
            Aggro = true;
            hitFlash = .18f;
            stun = Mathf.Max(stun, stunSeconds);
            attackTimer = 0; striking = false;
            float mass = Kind == BotKind.Heavy ? .55f : 1;
            if (!Trapped)
            {
                knock += knockback * mass;
                if (upward > 0) { vertical = Mathf.Max(vertical, upward * mass); airborne = .2f; }
            }
            if (Health <= 0) Die();
        }
        public void Freeze() { if (animator != null) animator.speed = 0; }
        public void Slow(float factor, float seconds) { slow = factor; slowTimer = seconds; Aggro = true; }
        public void Trap(Vortex v)
        {
            if (Dead || vortex == v) return;
            if (vortex != null && vortex.Strength > v.Strength) return;
            vortex = v; Aggro = true; striking = false; attackTimer = 0;
            Vector3 offset = transform.position - v.Centre;
            orbitAngle = Mathf.Atan2(offset.x, offset.z);
            orbitRadius = Mathf.Max(1, new Vector2(offset.x, offset.z).magnitude);
            orbitHeight = transform.position.y - v.Centre.y;
            controller.enabled = false;
        }
        public void Release(Vector3 fling)
        {
            if (vortex == null) return;
            vortex = null;
            controller.enabled = true;
            knock = fling; vertical = 2; airborne = .2f; stun = Mathf.Max(stun, 1.2f);
            TakeHit(12, Vector3.zero, 0, 1.2f);
        }

        // dt is already scaled by the world clock (timeScale) for speed perception.
        public void Tick(float dt, float timeScale)
        {
            if (Dead) return;
            float local = dt * (slowTimer > 0 ? slow : 1);
            slowTimer = Mathf.Max(0, slowTimer - dt);
            stun = Mathf.Max(0, stun - local);
            hitFlash = Mathf.Max(0, hitFlash - dt);
            cooldown = Mathf.Max(0, cooldown - local);
            airborne = Mathf.Max(0, airborne - local);
            if (animator != null) animator.speed = timeScale * (slowTimer > 0 ? slow : 1) * (Stunned ? .35f : 1);

            if (Trapped)
            {
                if (vortex == null || !vortex.Alive) { Release(Vector3.zero); return; }
                // Spin up and around inside the vortex.
                orbitAngle += (2.4f + vortex.Strength * 3) * dt;
                orbitRadius = Mathf.MoveTowards(orbitRadius, vortex.Radius * .55f, 3 * dt);
                orbitHeight = Mathf.MoveTowards(orbitHeight, 2 + vortex.Strength * 7 + (GetInstanceID() % 5), 5 * dt);
                transform.position = vortex.Centre + new Vector3(Mathf.Sin(orbitAngle) * orbitRadius, orbitHeight, Mathf.Cos(orbitAngle) * orbitRadius);
                transform.rotation = Quaternion.Euler(0, orbitAngle * Mathf.Rad2Deg * 2, 25);
                Health -= 4 * dt;
                if (Health <= 0) Die();
                SetAnimation(0);
                return;
            }

            Vector3 desired = Vector3.zero;
            bool grounded = controller.isGrounded && airborne <= 0;
            if (!Stunned && grounded) desired = Think(local);
            // Knockback decays; desired motion steers.
            knock = Vector3.MoveTowards(knock, Vector3.zero, (grounded ? 18 : 3) * local);
            moveVelocity = Vector3.MoveTowards(moveVelocity, desired, 20 * local);
            if (grounded && vertical < 0) vertical = -2;
            vertical -= 26 * local;
            Vector3 step = (moveVelocity + knock + Vector3.up * vertical) * local;
            int steps = SpeedMath.MovementSteps(step.magnitude);
            for (int i = 0; i < steps; i++) controller.Move(step / steps);
            if (transform.position.y < -30) { Die(); return; }
            Vector3 face = desired.sqrMagnitude > .1f ? desired : (target != null && !Stunned ? target.position - transform.position : Vector3.zero);
            face.y = 0;
            if (face.sqrMagnitude > .01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 1 - Mathf.Exp(-10 * local));
            SetAnimation(new Vector2(moveVelocity.x, moveVelocity.z).magnitude);
            // Stuck against something: try the other way around.
            if (local > 0)
            {
                stuck = desired.sqrMagnitude > 1 && (transform.position - lastPosition).sqrMagnitude < (desired.sqrMagnitude * local * local * .05f) ? stuck + local : 0;
                if (stuck > .8f) { strafe = -strafe; stuck = 0; }
                lastPosition = transform.position;
            }
        }

        Vector3 Think(float dt)
        {
            target = director.ChooseTarget(this, out bool isDecoy);
            if (target == null)
            {
                // Patrol around the incursion site.
                Vector3 toHome = home - transform.position; toHome.y = 0;
                return toHome.magnitude > 10 ? Steer(toHome.normalized) * 1.6f : Vector3.zero;
            }
            Vector3 to = target.position - transform.position; to.y = 0;
            float distance = to.magnitude;
            Vector3 dir = distance > .01f ? to / distance : transform.forward;
            if (attackTimer > 0) return AttackStep(dt, distance, isDecoy);
            if (Kind == BotKind.Gunner)
            {
                if (cooldown <= 0 && distance < 40) { attackTimer = attackLength = .7f; striking = false; return Vector3.zero; }
                if (distance > 26) return Steer(dir) * 5;
                if (distance < 12) return Steer(-dir) * 4;
                return Steer(Vector3.Cross(Vector3.up, dir) * strafe) * 2.5f;
            }
            float reach = Kind == BotKind.Heavy ? 2.6f : 2.1f;
            if (distance <= reach && cooldown <= 0) { attackTimer = attackLength = Kind == BotKind.Heavy ? .75f : .5f; striking = false; return Vector3.zero; }
            return Steer(dir) * (Kind == BotKind.Heavy ? 4.5f : 6.5f) * (distance < reach ? 0 : 1);
        }

        Vector3 AttackStep(float dt, float distance, bool decoy)
        {
            attackTimer -= dt;
            // Wind-up, then the blow lands at 60% of the swing.
            if (!striking && attackTimer < attackLength * .4f)
            {
                striking = true;
                if (Kind == BotKind.Gunner) director.FireBolt(this, rightHand != null ? rightHand.position : Chest, target);
                else if (distance < (Kind == BotKind.Heavy ? 3.2f : 2.7f)) director.MeleeHit(this, target, Kind == BotKind.Heavy ? 22 : 12);
            }
            if (attackTimer <= 0) { cooldown = Kind == BotKind.Gunner ? 1.6f : 1.1f; attackTimer = 0; }
            return Vector3.zero;
        }

        // Steering with whisker avoidance so bots round buildings instead of grinding into them.
        Vector3 Steer(Vector3 dir)
        {
            Vector3 origin = transform.position + Vector3.up * 1.1f * scale;
            if (Physics.SphereCast(origin, .35f, dir, out RaycastHit hit, 3.5f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<BotEnemy>() != this)
            {
                Vector3 around = Vector3.Cross(Vector3.up, hit.normal) * strafe;
                around.y = 0;
                return Vector3.Lerp(dir, around.normalized, .75f).normalized;
            }
            return dir;
        }

        void SetAnimation(float speed)
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            animator.SetFloat("Speed", speed, .1f, Time.deltaTime);
            animator.SetFloat("RunRate", speed > 5 ? .55f : 1);
        }

        void LateUpdate()
        {
            if (Dead) return;
            // Visor rides the head; strikes and gun shots are posed procedurally over the walk cycle.
            if (head != null)
            {
                visor.transform.position = head.position + transform.forward * .12f * scale + transform.up * .04f * scale;
                visor.transform.rotation = transform.rotation;
            }
            if (attackTimer > 0 && target != null)
            {
                float k = 1 - attackTimer / attackLength;
                Vector3 toTarget = (target.position + Vector3.up - (rightUpper != null ? rightUpper.position : Chest)).normalized;
                Vector3 windup = (-transform.forward * .4f + Vector3.up).normalized;
                Vector3 dir = Kind == BotKind.Gunner ? toTarget : k < .6f ? Vector3.Slerp(windup, toTarget, k * k) : toTarget;
                float weight = Kind == BotKind.Gunner ? Mathf.Clamp01(k * 4) : Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 1.15f));
                Limbs.Aim(rightUpper, rightLower, rightHand, dir, weight);
                if (Kind == BotKind.Heavy) Limbs.Aim(leftUpper, leftLower, leftHand, dir, weight);
            }
            if (Stunned && head != null)
                head.rotation *= Quaternion.Euler(Mathf.Sin(Time.time * 37) * 6, Mathf.Sin(Time.time * 23) * 10, 0);
            if (skin != null)
            {
                skin.GetPropertyBlock(block);
                block.SetColor("_BaseColor", hitFlash > 0 ? new Color(1.6f, .8f, .7f) : Color.white);
                skin.SetPropertyBlock(block);
            }
            bool blink = Stunned && Mathf.Repeat(Time.time * 8, 1) > .5f;
            visor.enabled = !blink;
        }

        void Die()
        {
            if (Dead) return;
            Dead = true;
            if (vortex != null) vortex = null;
            director.OnBotDestroyed(this);
            Destroy(gameObject);
        }
    }
}
