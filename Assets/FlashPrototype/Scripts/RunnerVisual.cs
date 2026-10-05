using UnityEngine;

namespace FlashGame
{
    // The Flash's look: animated Humanoid character (or the original primitive mannequin as a fallback),
    // suits, Speed Force lightning, and procedural poses for powers layered over the locomotion clips.
    public sealed class RunnerVisual : MonoBehaviour
    {
        enum Move { None, PunchRight, PunchLeft, Uppercut, Kick, Slam }
        Transform leftArm, rightArm, leftLeg, rightLeg, torso;
        Transform rUpper, rLower, rHand, lUpper, lLower, lHand, rThigh, rShin, rFoot, chest, lToes, rToes;
        Quaternion lToesRest, rToesRest;
        SpeedLightning lightning;
        float stride;
        Animator animator;
        SpeedsterMotor motor;
        SuitPainter painter;
        Material mannequinSuit, mannequinTrim;
        Move move;
        Vector3 moveTarget;
        float moveTime, moveLength, spinTime, spinLength, spinTurns, flinch, windmill, bank, lastYaw;
        bool windmilling;
        public int Skin { get; private set; }
        public SkinnedMeshRenderer SkinnedMesh { get; private set; }
        public Color LightningGlow => lightning.Glow;
        public bool Overcharged { get => lightning.Overcharged; set => lightning.Overcharged = value; }

        public void SetPaused(bool paused) { if (animator != null) animator.speed = paused ? 0 : 1; }
        public void Build(PrototypeWorld world, GameObject characterPrefab = null)
        {
            motor = GetComponent<SpeedsterMotor>();
            if (characterPrefab != null)
            {
                var character = Instantiate(characterPrefab, transform, false);
                character.name = "Flash - animated character";
                torso = character.transform;
                animator = character.GetComponentInChildren<Animator>();
                SkinnedMesh = character.GetComponentInChildren<SkinnedMeshRenderer>();
                if (animator != null) animator.applyRootMotion = false;
                BuildTrails(world.Material("Speed lightning", new Color(1, 0.66f, 0.12f), 0.6f));
                if (animator != null && animator.isHuman)
                {
                    var anchors = new System.Collections.Generic.List<Transform>();
                    foreach (var bone in ArcBones)
                        if (animator.GetBoneTransform(bone) is Transform t) anchors.Add(t);
                    lightning.SetAnchors(anchors.ToArray());
                    rUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    rLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                    rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    lUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    lLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    rThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    rShin = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                    rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    lToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
                    rToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
                    lToesRest = BindRotation(SkinnedMesh, lToes); rToesRest = BindRotation(SkinnedMesh, rToes);
                    chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                    if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Spine);
                }
                painter = character.AddComponent<SuitPainter>();
                painter.Collect();
                SetSkin(FlashSkins.Saved);
                foreach (var child in GetComponentsInChildren<Transform>()) child.gameObject.layer = 2;
                return;
            }
            var red = world.Material("Runner burgundy", new Color(0.48f, 0.025f, 0.045f));
            var gold = world.Material("Runner gold", new Color(1f, 0.66f, 0.12f), 0.6f);
            var white = world.Material("Emblem", new Color(0.9f, 0.9f, 0.83f));
            var skin = world.Material("Face", new Color(0.63f, 0.4f, 0.29f));
            torso = new GameObject("Visual rig").transform;
            torso.SetParent(transform, false);
            Part(world, "Torso", torso, new Vector3(0, 1.22f, 0), new Vector3(0.43f, 0.4f, 0.25f), red);
            Part(world, "Cowl", torso, new Vector3(0, 1.75f, 0), new Vector3(0.28f, 0.19f, 0.27f), red);
            world.Shape("Face opening", PrimitiveType.Sphere, torso, new Vector3(0, 1.71f, 0.12f), new Vector3(0.18f, 0.15f, 0.09f), skin, false);
            world.Shape("Chest emblem", PrimitiveType.Sphere, torso, new Vector3(0, 1.36f, 0.14f), new Vector3(0.17f, 0.17f, 0.025f), white, false);
            var bolt = world.Shape("Gold insignia", PrimitiveType.Cube, torso, new Vector3(0, 1.36f, 0.16f), new Vector3(0.035f, 0.15f, 0.02f), gold, false);
            bolt.transform.localRotation = Quaternion.Euler(0, 0, -25);
            world.Shape("Belt", PrimitiveType.Cube, torso, new Vector3(0, 0.99f, 0), new Vector3(0.39f, 0.05f, 0.26f), gold, false);
            leftArm = Limb(world, "Left arm", new Vector3(-0.29f, 1.5f, 0), 0.52f, 0.13f, red);
            rightArm = Limb(world, "Right arm", new Vector3(0.29f, 1.5f, 0), 0.52f, 0.13f, red);
            leftLeg = Limb(world, "Left leg", new Vector3(-0.12f, 0.95f, 0), 0.75f, 0.16f, red);
            rightLeg = Limb(world, "Right leg", new Vector3(0.12f, 0.95f, 0), 0.75f, 0.16f, red);
            world.Shape("Left boot", PrimitiveType.Cube, leftLeg, new Vector3(0, -0.78f, 0.06f), new Vector3(0.17f, 0.19f, 0.3f), gold, false);
            world.Shape("Right boot", PrimitiveType.Cube, rightLeg, new Vector3(0, -0.78f, 0.06f), new Vector3(0.17f, 0.19f, 0.3f), gold, false);
            BuildTrails(gold);
            lightning.SetAnchors(new[] { leftArm, rightArm, leftLeg, rightLeg, torso });
            mannequinSuit = red; mannequinTrim = gold;
            SetSkin(FlashSkins.Saved);
            foreach (var child in GetComponentsInChildren<Transform>()) child.gameObject.layer = 2;
        }
        // The supplied run covers 14.3 m per second of clip on this body, so playing it at speed / 14.3 plants
        // each foot without sliding. Past ~30 m/s the legs would strobe, so cadence grows only slowly from there
        // (the lightning sells the rest). Below a run the walk plays at its own pace.
        public const float RunStrideSpeed = 14.3f;
        public static float RunRate(float speed) => speed < 7
            ? Mathf.Lerp(1, 7 / RunStrideSpeed, Mathf.InverseLerp(2, 7, speed))
            : speed < 30 ? speed / RunStrideSpeed : 30 / RunStrideSpeed + (speed - 30) * .005f;
        public void SetSkin(int index)
        {
            Skin = FlashSkins.Wrap(index);
            var skin = FlashSkins.All[Skin];
            if (painter != null) painter.Apply(Skin);
            if (mannequinSuit != null) { mannequinSuit.SetColor("_BaseColor", skin.Suit); mannequinTrim.SetColor("_BaseColor", skin.Trim); }
            lightning.SetColors(skin.Glow, skin.Core);
        }
        // Where the Speed Force arcs jump from and to.
        static readonly HumanBodyBones[] ArcBones = {
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.Chest, HumanBodyBones.Head };
        void BuildTrails(Material gold)
        {
            lightning=gameObject.AddComponent<SpeedLightning>();
            lightning.Build(Resources.Load<Material>("SpeedLightning") ?? gold);
        }
        void Part(PrototypeWorld world, string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
            => world.Shape(name, PrimitiveType.Capsule, parent, pos, size, mat, false);
        Transform Limb(PrototypeWorld world, string name, Vector3 pivot, float length, float width, Material mat)
        {
            var limb = new GameObject(name).transform;
            limb.SetParent(torso, false); limb.localPosition = pivot;
            Part(world, name + " mesh", limb, new Vector3(0, -length / 2, 0), new Vector3(width, length / 2, width), mat);
            return limb;
        }

        // ---- Power poses ----------------------------------------------------------------------

        public void Punch(bool right, Vector3 target, float duration, bool uppercut = false)
            => Play(uppercut ? Move.Uppercut : right ? Move.PunchRight : Move.PunchLeft, target, duration);
        public void Kick(Vector3 target, float duration) => Play(Move.Kick, target, duration);
        public void Slam(float duration) => Play(Move.Slam, transform.position, duration);
        public void Spin(float duration, float turns) { spinTime = 0; spinLength = duration; spinTurns = turns; }
        public void Windmill(bool on) => windmilling = on;
        public void Flinch() => flinch = .3f;
        public void Surge(float seconds) => lightning.Surge(seconds);
        void Play(Move m, Vector3 target, float duration) { move = m; moveTarget = target; moveTime = 0; moveLength = Mathf.Max(.04f, duration); }
        public Vector3 HandPosition(bool right)
        {
            var hand = right ? rHand : lHand;
            return hand != null ? hand.position : transform.position + Vector3.up * 1.3f + transform.right * (right ? .35f : -.35f) + transform.forward * .3f;
        }

        public void Tick(float speed, bool grounded, float dt, bool effects, float verticalSpeed = 0)
        {
            bool onWall = motor != null && motor.WallRunning && !motor.Cresting;
            bool onCeiling = motor != null && motor.CeilingRunning;
            moveTime += dt; flinch = Mathf.Max(0, flinch - dt);
            windmill = Mathf.MoveTowards(windmill, windmilling ? 1 : 0, dt * 6);
            if (spinLength > 0) { spinTime += dt; if (spinTime >= spinLength) spinLength = 0; }
            if (animator != null)
            {
                Quaternion pose = Quaternion.identity;
                Vector3 offset = Vector3.zero;
                if (onWall)
                {
                    pose = Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(Vector3.up, motor.WallNormal);
                    offset = transform.InverseTransformDirection(-motor.WallNormal * 1.55f);
                }
                else if (onCeiling)
                {
                    // Head-down with the feet on the ceiling (the capsule top).
                    pose = Quaternion.Euler(0, 0, 180);
                    offset = new Vector3(0, 1.85f, 0);
                }
                else if (motor != null && motor.Drifting)
                {
                    // Lean into the slide and turn the body toward the inside of the turn.
                    float side = Mathf.Sign(Vector3.Cross(transform.forward, motor.Velocity).y + 1e-4f);
                    pose = Quaternion.Euler(0, side * 30, -side * 18);
                }
                else if (grounded && speed > 6)
                {
                    // Lean into turns like a sprinter (a fraction of the physical lean, capped at 16 degrees).
                    float yawRate = dt > 0 ? Mathf.DeltaAngle(lastYaw, transform.eulerAngles.y) / dt : 0;
                    float target = Mathf.Clamp(-Mathf.Atan(speed * yawRate * Mathf.Deg2Rad / 9.81f) * Mathf.Rad2Deg * .35f, -16, 16);
                    bank = Mathf.Lerp(bank, target, 1 - Mathf.Exp(-6 * dt));
                    pose = Quaternion.Euler(0, 0, bank);
                }
                if (!(grounded && speed > 6) || onWall || onCeiling) bank = 0;
                lastYaw = transform.eulerAngles.y;
                if (spinLength > 0) pose *= Quaternion.Euler(0, spinTime / spinLength * 360 * spinTurns, 0);
                float blend = spinLength > 0 ? 1 : 1 - Mathf.Exp(-12 * dt);
                torso.localRotation = Quaternion.Slerp(torso.localRotation, pose, blend);
                torso.localPosition = Vector3.Lerp(torso.localPosition, offset, 1 - Mathf.Exp(-12 * dt));
                animator.SetFloat("Speed", speed, 0.12f, dt);
                animator.SetBool("Grounded", grounded || onWall);
                animator.SetFloat("VerticalSpeed", verticalSpeed);
                animator.SetFloat("RunRate", RunRate(speed));
                lightning.Tick(torso, speed, dt, effects);
                return;
            }
            stride += dt * Mathf.Lerp(0, 24, Mathf.Clamp01(speed / 25));
            float swing = Mathf.Sin(stride) * Mathf.Clamp01(speed / 5) * (grounded ? 48 : 18);
            leftArm.localRotation = Quaternion.Euler(-swing - 15, 0, -8);
            rightArm.localRotation = Quaternion.Euler(swing - 15, 0, 8);
            leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            if (move != Move.None && moveTime < moveLength)
                (move == Move.PunchLeft ? leftArm : rightArm).localRotation = Quaternion.Euler(-90 * Mathf.Sin(Mathf.PI * moveTime / moveLength), 0, 0);
            torso.localRotation = Quaternion.Euler(Mathf.Lerp(0, 13, speed / 130), spinLength > 0 ? spinTime / spinLength * 360 * spinTurns : 0, 0);
            lightning.Tick(torso, speed, dt, effects);
        }

        // A bone's local rotation in the mesh's bind pose, where the boots' soles and toes are flat.
        static Quaternion BindRotation(SkinnedMeshRenderer skin, Transform bone)
        {
            if (skin == null || bone == null) return bone != null ? bone.localRotation : Quaternion.identity;
            int i = System.Array.IndexOf(skin.bones, bone), p = System.Array.IndexOf(skin.bones, bone.parent);
            if (i < 0 || p < 0) return bone.localRotation;
            var poses = skin.sharedMesh.bindposes;
            return (poses[p] * poses[i].inverse).rotation;
        }

        // Power poses are layered over the animation after the Animator has written the bones.
        void LateUpdate()
        {
            if (rUpper == null) return;
            // The suit's boots are rigid: hold the toe caps as modelled. Humanoid retargeting rolls this rig's toe
            // bones and the clips flex them, which made the toe caps hang like flaps.
            if (lToes != null) lToes.localRotation = lToesRest;
            if (rToes != null) rToes.localRotation = rToesRest;
            Vector3 forward = transform.forward, up = Vector3.up, right = transform.right;
            if (move != Move.None && moveTime < moveLength)
            {
                float k = moveTime / moveLength, weight = Mathf.Sin(Mathf.PI * k);
                switch (move)
                {
                    case Move.PunchRight: Limbs.Aim(rUpper, rLower, rHand, moveTarget - rUpper.position, weight); break;
                    case Move.PunchLeft: Limbs.Aim(lUpper, lLower, lHand, moveTarget - lUpper.position, weight); break;
                    case Move.Uppercut:
                        Limbs.Aim(rUpper, rLower, rHand, Vector3.Slerp(forward * .6f - up, up + forward * .3f, k), weight); break;
                    case Move.Kick: Limbs.Aim(rThigh, rShin, rFoot, moveTarget - rThigh.position, weight); break;
                    case Move.Slam:
                        Limbs.Aim(rUpper, rLower, rHand, forward * .3f - up, weight);
                        Limbs.Aim(lUpper, lLower, lHand, forward * .3f - up, weight); break;
                }
            }
            else move = Move.None;
            if (windmill > 0)
            {
                // Tornado Arms: both arms whirl in front of the chest.
                float a = Time.time * 28;
                Limbs.Aim(rUpper, rLower, rHand, forward * .7f + (up * Mathf.Cos(a) + right * Mathf.Sin(a)) * .8f, windmill);
                Limbs.Aim(lUpper, lLower, lHand, forward * .7f + (up * Mathf.Cos(a + Mathf.PI) - right * Mathf.Sin(a + Mathf.PI)) * .8f, windmill);
            }
            if (flinch > 0 && chest != null) chest.rotation = Quaternion.AngleAxis(-14 * flinch / .3f, right) * chest.rotation;
        }
        public void ClearTrails() { lightning.Clear(); }
    }
}
