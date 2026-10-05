using UnityEngine;

namespace FlashGame
{
    // Original primitive mannequin, intentionally replaceable by a Humanoid model.
    public sealed class RunnerVisual : MonoBehaviour
    {
        Transform leftArm, rightArm, leftLeg, rightLeg, torso;
        SpeedLightning lightning;
        float stride;
        Animator animator;
        SpeedsterMotor motor;
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
                if (animator != null) animator.applyRootMotion = false;
                BuildTrails(world.Material("Speed lightning", new Color(1, 0.66f, 0.12f), 0.6f));
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
            foreach (var child in GetComponentsInChildren<Transform>()) child.gameObject.layer = 2;
        }
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
        public void Tick(float speed, bool grounded, float dt, bool effects, float verticalSpeed = 0)
        {
            bool onWall = motor != null && motor.WallRunning;
            if (animator != null)
            {
                Quaternion pose = onWall && !motor.Cresting
                    ? Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(Vector3.up, motor.WallNormal)
                    : Quaternion.identity;
                torso.localRotation = Quaternion.Slerp(torso.localRotation, pose, 1 - Mathf.Exp(-12 * dt));
                Vector3 offset = onWall && !motor.Cresting
                    ? transform.InverseTransformDirection(-motor.WallNormal * 1.55f) : Vector3.zero;
                torso.localPosition = Vector3.Lerp(torso.localPosition, offset, 1 - Mathf.Exp(-12 * dt));
                animator.SetFloat("Speed", speed, 0.12f, dt);
                animator.SetBool("Grounded", grounded || onWall);
                animator.SetFloat("VerticalSpeed", verticalSpeed);
                animator.SetFloat("RunRate", Mathf.Lerp(1, 1.8f, Mathf.InverseLerp(7, 65, speed)));
                lightning.Tick(torso, speed, dt, effects);
                return;
            }
            stride += dt * Mathf.Lerp(0, 24, Mathf.Clamp01(speed / 25));
            float swing = Mathf.Sin(stride) * Mathf.Clamp01(speed / 5) * (grounded ? 48 : 18);
            leftArm.localRotation = Quaternion.Euler(-swing - 15, 0, -8);
            rightArm.localRotation = Quaternion.Euler(swing - 15, 0, 8);
            leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            torso.localRotation = Quaternion.Euler(Mathf.Lerp(0, 13, speed / 130), 0, 0);
            lightning.Tick(torso, speed, dt, effects);
        }
        public void ClearTrails() { lightning.Clear(); }
    }
}
