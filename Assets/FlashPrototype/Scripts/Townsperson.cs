using UnityEngine;

namespace FlashGame
{
    // A person of Central City (Resources/Townsperson, built on Barry's base body by BarrySetup): each one gets a skin
    // tone, an outfit (a tee or a jacket over trousers) in its own colours, a hair colour and a build. Poses for
    // crimes and rescues are layered over the locomotion clips: hands up, cowering, cheering, waving, aiming a
    // pistol, and lying knocked out.
    public sealed class Townsperson : MonoBehaviour
    {
        public enum Pose { None, HandsUp, Cower, Cheer, Wave, AimGun, Down }
        [SerializeField] Texture2D[] skins = new Texture2D[0];     // light, tan, dark
        [SerializeField] Texture2D masked;                          // knitted ski mask
        [SerializeField] Texture2D tee, pants, jacket, hair;        // neutral textures, tinted per person

        public Pose Posing;
        public Vector3 AimTarget;
        public Animator Animator { get; private set; }
        public bool Masked { get; private set; }
        public Transform RightHand => rH;
        Transform rU, rL, rH, lU, lL, lH, spine, head;
        GameObject gun, bag;
        Renderer[] renderers;
        float phase;
        Vector3 standing;

        static readonly Color[] TeeColours = { new Color(.9f, .9f, .88f), new Color(.75f, .2f, .15f), new Color(.2f, .35f, .65f), new Color(.15f, .5f, .3f),
            new Color(.95f, .75f, .2f), new Color(.15f, .15f, .17f), new Color(.6f, .35f, .6f), new Color(.85f, .55f, .4f) };
        static readonly Color[] JacketColours = { new Color(.12f, .13f, .16f), new Color(.3f, .22f, .15f), new Color(.25f, .3f, .2f), new Color(.15f, .2f, .35f),
            new Color(.45f, .45f, .47f), new Color(.5f, .12f, .12f), new Color(.6f, .5f, .35f) };
        static readonly Color[] PantsColours = { new Color(.2f, .28f, .5f), new Color(.12f, .12f, .14f), new Color(.75f, .65f, .45f), new Color(.4f, .4f, .42f),
            new Color(.18f, .22f, .35f), new Color(.3f, .25f, .2f) };
        static readonly Color[] HairColours = { new Color(.05f, .04f, .04f), new Color(.15f, .1f, .06f), new Color(.3f, .2f, .12f), new Color(.7f, .58f, .35f),
            new Color(.55f, .55f, .55f), new Color(.35f, .15f, .08f) };

        // Skin textures and garment textures are wired by BarrySetup on the Resources prefab.
        public void SetTextures(Texture2D[] skinSet, Texture2D mask, Texture2D teeTex, Texture2D pantsTex, Texture2D jacketTex, Texture2D hairTex)
        { skins = skinSet; masked = mask; tee = teeTex; pants = pantsTex; jacket = jacketTex; hair = hairTex; }

        void Awake()
        {
            Animator = GetComponentInChildren<Animator>();
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (Animator != null && Animator.isHuman)
            {
                rU = Animator.GetBoneTransform(HumanBodyBones.RightUpperArm); rL = Animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                rH = Animator.GetBoneTransform(HumanBodyBones.RightHand); lU = Animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                lL = Animator.GetBoneTransform(HumanBodyBones.LeftLowerArm); lH = Animator.GetBoneTransform(HumanBodyBones.LeftHand);
                spine = Animator.GetBoneTransform(HumanBodyBones.Chest) ?? Animator.GetBoneTransform(HumanBodyBones.Spine);
                head = Animator.GetBoneTransform(HumanBodyBones.Head);
                Animator.applyRootMotion = false;
                Animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
            phase = Random.value * 10;
        }

        Renderer Part(string name) { foreach (var r in renderers) if (r.name == name) return r; return null; }
        static void Paint(Renderer r, Texture tex, Color tint)
        {
            if (r == null) return;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            if (tex != null) block.SetTexture("_BaseMap", tex);
            block.SetColor("_BaseColor", tint);
            r.SetPropertyBlock(block);
        }

        // Dresses this person. Robbers wear a ski mask and a dark jacket.
        public void Dress(System.Random rng, bool robber = false)
        {
            T Pick<T>(T[] a) => a[rng.Next(a.Length)];
            Masked = robber;
            bool jacketOn = robber || rng.NextDouble() < .45;
            foreach (var r in renderers) r.gameObject.SetActive(true);
            Part("Shirt")?.gameObject.SetActive(false);
            Part("Watch")?.gameObject.SetActive(rng.NextDouble() < .3);
            Part("Tee")?.gameObject.SetActive(!jacketOn);
            Part("Jacket")?.gameObject.SetActive(jacketOn);
            Part("Hair")?.gameObject.SetActive(!robber && rng.NextDouble() < .9);
            Paint(Part("Body"), robber ? masked : skins.Length > 0 ? Pick(skins) : null, Color.white);
            Paint(Part("Tee"), tee, Pick(TeeColours));
            Paint(Part("Jacket"), jacket, robber ? new Color(.1f, .1f, .11f) : Pick(JacketColours));
            Paint(Part("Jeans"), pants, robber ? new Color(.15f, .15f, .17f) : Pick(PantsColours));
            Paint(Part("Hair"), hair, Pick(HairColours));
            float height = .93f + (float)rng.NextDouble() * .1f, build = .96f + (float)rng.NextDouble() * .12f;
            transform.localScale = new Vector3(height * build, height, height * build);
        }

        public void SetSpeed(float speed)
        {
            if (Animator == null || !Animator.isActiveAndEnabled) return;
            Animator.SetFloat("Speed", speed, .15f, Time.deltaTime);
            Animator.SetFloat("RunRate", speed > 5 ? .55f : 1);
            Animator.SetBool("Grounded", true);
        }

        // A small black pistol in the right hand.
        public void HoldGun(bool on)
        {
            if (on && gun == null && rH != null)
            {
                gun = new GameObject("Pistol");
                gun.transform.SetParent(rH, false);
                gun.transform.localPosition = new Vector3(0, .09f, .02f);
                var mat = Shared(new Color(.06f, .06f, .065f));
                Piece(gun.transform, new Vector3(0, .02f, .06f), new Vector3(.03f, .035f, .17f), mat);
                Piece(gun.transform, new Vector3(0, -.04f, 0), new Vector3(.028f, .09f, .04f), mat);
            }
            if (gun != null) gun.SetActive(on);
        }
        // A handbag or cash bag carried in the left hand.
        public void HoldBag(bool on, Color colour)
        {
            if (on && bag == null && lH != null)
            {
                bag = new GameObject("Bag");
                bag.transform.SetParent(lH, false);
                bag.transform.localPosition = new Vector3(0, .16f, 0);
                Piece(bag.transform, Vector3.zero, new Vector3(.12f, .26f, .3f), Shared(colour));
            }
            if (bag != null) bag.SetActive(on);
        }
        public void DropBag() { if (bag != null) { bag.transform.SetParent(null, true); bag.transform.position = new Vector3(bag.transform.position.x, transform.position.y + .12f, bag.transform.position.z); } }
        static readonly System.Collections.Generic.Dictionary<Color, Material> props = new System.Collections.Generic.Dictionary<Color, Material>();
        static Material Shared(Color c)
        {
            if (props.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Prop" };
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", .35f);
            return props[c] = m;
        }
        static void Piece(Transform parent, Vector3 at, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = at; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.layer = parent.gameObject.layer;
        }

        public void KnockDown()
        {
            Posing = Pose.Down;
            HoldGun(false);
            if (Animator != null) { SetSpeed(0); Animator.Update(1); Animator.enabled = false; }
            standing = transform.eulerAngles;
            // Fall to the floor (they may be knocked out in mid-air), lying on the back rather than half through it.
            Vector3 p = transform.position;
            if (Physics.Raycast(p + Vector3.up * .5f, Vector3.down, out RaycastHit hit, 60, ~(1 << 2), QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            transform.position = p + Vector3.up * .13f;
        }

        void LateUpdate()
        {
            if (rU == null) return;
            float t = Time.time + phase;
            Vector3 fwd = transform.forward, right = transform.right, up = Vector3.up;
            switch (Posing)
            {
                case Pose.HandsUp:
                    Limbs.Aim(rU, rL, rH, up * .95f + right * .35f + fwd * .1f, 1);
                    Limbs.Aim(lU, lL, lH, up * .95f - right * .35f + fwd * .1f, 1);
                    if (head != null) head.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 2.3f) * 6, up) * head.rotation;
                    break;
                case Pose.Cower:
                    if (spine != null) spine.rotation = Quaternion.AngleAxis(28, right) * spine.rotation;
                    Limbs.Aim(rU, rL, rH, up * .6f + fwd * .7f + right * .2f, 1);
                    Limbs.Aim(lU, lL, lH, up * .6f + fwd * .7f - right * .2f, 1);
                    break;
                case Pose.Cheer:
                    Limbs.Aim(rU, rL, rH, up + right * (.3f + .15f * Mathf.Sin(t * 10)), 1);
                    Limbs.Aim(lU, lL, lH, up - right * (.3f + .15f * Mathf.Sin(t * 10)), 1);
                    break;
                case Pose.Wave:
                    Limbs.Aim(rU, rL, rH, up + right * (.5f + .4f * Mathf.Sin(t * 8)), 1);
                    break;
                case Pose.AimGun:
                {
                    Vector3 dir = AimTarget - rU.position;
                    Limbs.Aim(rU, rL, rH, dir, 1);
                    Limbs.Aim(lU, lL, lH, AimTarget - lU.position + right * .1f, .85f);
                    break;
                }
                case Pose.Down:
                    // Flat on the back, arms out.
                    transform.rotation = Quaternion.Euler(-90, standing.y, 0);
                    Limbs.Aim(rU, rL, rH, right * .8f + fwd * .3f, 1);
                    Limbs.Aim(lU, lL, lH, -right * .8f + fwd * .3f, 1);
                    break;
            }
        }
    }
}
