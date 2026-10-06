using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Builds a natural standing idle (Character/Animations/Idle.anim) for any Humanoid: feet under the hips,
// knees soft, arms hanging slightly away from the body with relaxed elbows, gaze level, and slow breathing.
// The supplied HatchXR "stand" clip is a stylised pose with crossed feet and a dangling forearm. Each muscle is
// solved against a geometric target on the realistic Flash (e.g. "upper arm 8 degrees from vertical"), so the
// pose holds up on other rigs too; the clip is authored in Unity's muscle space and retargets like any other.
public static class IdleClipBuilder
{
    public const string Path = "Assets/FlashPrototype/Character/Animations/Idle.anim";
    const float Length = 4;

    public static AnimationClip Build(GameObject prefab)
    {
        var go = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var animator = go.GetComponentInChildren<Animator>();
            // Start from the avatar's T-pose (the prefab is saved in a sampled pose) so "level" means level.
            var bones = animator.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            foreach (var sb in animator.avatar.humanDescription.skeleton)
                if (bones.TryGetValue(sb.name, out var t) && t != animator.transform) { t.localPosition = sb.position; t.localRotation = sb.rotation; }
            var handler = new HumanPoseHandler(animator.avatar, animator.transform);
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            var muscles = new float[HumanTrait.MuscleCount];
            Transform B(HumanBodyBones b) => animator.GetBoneTransform(b);
            Vector3 Local(HumanBodyBones b) => go.transform.InverseTransformPoint(B(b).position);
            Vector3 Dir(HumanBodyBones a, HumanBodyBones b) => (Local(b) - Local(a)).normalized;
            var referenceHead = Quaternion.Inverse(go.transform.rotation) * B(HumanBodyBones.Head).rotation;
            // Palms face the floor in the T-pose; remember each hand's rotation there to track where its palm points.
            var referenceHand = new Dictionary<HumanBodyBones, Quaternion>();
            foreach (var h in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
                referenceHand[h] = Quaternion.Inverse(go.transform.rotation) * B(h).rotation;
            Vector3 Palm(HumanBodyBones h) => (Quaternion.Inverse(go.transform.rotation) * B(h).rotation) * Quaternion.Inverse(referenceHand[h]) * Vector3.down;
            // Soles are flat in the mesh's bind pose (the avatar's reference pose tips the toes down when it lines
            // the feet up with Mixamo's): remember how steeply each foot and toe bone points down there.
            float Drop(Vector3 d) => Mathf.Asin(Mathf.Clamp(-d.y, -1, 1)) * Mathf.Rad2Deg;
            Vector3 TipOf(HumanBodyBones toes) => B(toes).childCount > 0 ? go.transform.InverseTransformPoint(B(toes).GetChild(0).position) : Local(toes) + Dir(toes - 1, toes) * .05f;
            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
            Vector3 BindLocal(HumanBodyBones bone)
            {
                int i = Array.IndexOf(skin.bones, B(bone));
                var world = skin.transform.localToWorldMatrix * skin.sharedMesh.bindposes[i].inverse;
                return go.transform.InverseTransformPoint(world.GetColumn(3));
            }
            var flat = new Dictionary<HumanBodyBones, float>();
            foreach (var (foot, toes) in new[] { (HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes), (HumanBodyBones.RightFoot, HumanBodyBones.RightToes) })
            {
                flat[foot] = Drop((BindLocal(toes) - BindLocal(foot)).normalized);
                flat[toes] = 3;   // toe tips level with the sole
            }
            void Apply()
            {
                pose.muscles = muscles; pose.bodyRotation = Quaternion.identity;
                handler.SetHumanPose(ref pose);
            }
            // Coordinate search: for each muscle (or mirrored pair), pick the value that best meets its target.
            void Solve(string[] names, Func<float> error)
            {
                var ids = names.Select(Index).ToArray();
                float best = 0, bestError = float.MaxValue;
                for (float v = -1; v <= 1.0001f; v += .01f)
                {
                    foreach (var i in ids) muscles[i] = v;
                    Apply();
                    float e = error();
                    if (e < bestError) { bestError = e; best = v; }
                }
                foreach (var i in ids) muscles[i] = best;
                Apply();
            }
            float Pitch(Vector3 d) => Mathf.Atan2(d.z, -d.y) * Mathf.Rad2Deg;           // forward lean of a downward bone
            float Away(Vector3 d, float side) => Mathf.Atan2(d.x * side, -d.y) * Mathf.Rad2Deg; // outward lean
            for (int pass = 0; pass < 3; pass++)
            {
                // Spine upright with a 2 degree forward set; gaze level.
                Solve(new[] { "Spine Front-Back", "Chest Front-Back", "UpperChest Front-Back" },
                    () => Mathf.Abs(Pitch(-Dir(HumanBodyBones.Hips, HumanBodyBones.Neck)) + 2));
                Solve(new[] { "Neck Nod Down-Up", "Head Nod Down-Up" }, () =>
                {
                    var face = (Quaternion.Inverse(go.transform.rotation) * B(HumanBodyBones.Head).rotation) * Quaternion.Inverse(referenceHead) * Vector3.forward;
                    return Mathf.Abs(face.y);
                });
                foreach (var (side, s) in new[] { ("Left", -1f), ("Right", 1f) })
                {
                    var hip = side == "Left" ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
                    var knee = side == "Left" ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
                    var ankle = side == "Left" ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
                    var shoulder = side == "Left" ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
                    var elbow = side == "Left" ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
                    var wrist = side == "Left" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                    Solve(new[] { side + " Upper Leg Front-Back" }, () => Mathf.Abs(Pitch(Dir(hip, knee)) - 4));
                    Solve(new[] { side + " Upper Leg In-Out" }, () => Mathf.Abs(Local(ankle).x - Local(hip).x - s * .03f));
                    Solve(new[] { side + " Lower Leg Stretch" }, () => Mathf.Abs(Vector3.Angle(Dir(hip, knee), Dir(knee, ankle)) - 7));
                    Solve(new[] { side + " Arm Down-Up" }, () => Mathf.Abs(Away(Dir(shoulder, elbow), s) - 9));
                    // Arms hang at the sides with a small bend; hands beside the thighs, not in front of them.
                    Solve(new[] { side + " Arm Front-Back" }, () => Mathf.Abs(Pitch(Dir(shoulder, elbow)) - 0));
                    Solve(new[] { side + " Forearm Stretch" }, () => Mathf.Abs(Vector3.Angle(Dir(shoulder, elbow), Dir(elbow, wrist)) - 11));
                    // Feet flat on the floor, toes level.
                    var toes = side == "Left" ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes;
                    Solve(new[] { side + " Foot Up-Down" }, () => Mathf.Abs(Drop(Dir(ankle, toes)) - flat[ankle]));
                    Solve(new[] { side + " Toes Up-Down" }, () => Mathf.Abs(Drop((TipOf(toes) - Local(toes)).normalized) - flat[toes]));
                    // Turn the upper arm so the elbow bends forward, not in toward the hips.
                    Solve(new[] { side + " Arm Twist In-Out" }, () => Mathf.Abs(Away(Dir(elbow, wrist), s) - 6) + Mathf.Max(0, -Pitch(Dir(elbow, wrist))));
                    // Palms turned in toward the thighs (a touch back), as people stand, not facing forward.
                    var hand = wrist;
                    var inward = new Vector3(-s, 0, -.3f).normalized;
                    Solve(new[] { side + " Forearm Twist In-Out" }, () => Vector3.Angle(Palm(hand), inward));
                }
            }
            var standing = (float[])muscles.Clone();
            var body = pose.bodyPosition;

            var clip = new AnimationClip { name = "Idle", frameRate = 30 };
            void Curve(string property, Func<float, float> value)
            {
                var curve = new AnimationCurve();
                for (float t = 0; t <= Length + .001f; t += .25f) curve.AddKey(t, value(t));
                for (int k = 0; k < curve.length; k++) curve.SmoothTangents(k, 0);
                clip.SetCurve("", typeof(Animator), property, curve);
            }
            float Wave(float t, float period, float phase = 0) => Mathf.Sin((t / period + phase) * Mathf.PI * 2);
            // Breathing (4 s), a slow weight shift (4 s) and small head movements, all looping.
            var motion = new Dictionary<string, Func<float, float>>
            {
                ["Chest Front-Back"] = t => .025f * Wave(t, 4),
                ["UpperChest Front-Back"] = t => .02f * Wave(t, 4),
                ["Left Shoulder Down-Up"] = t => .04f * Wave(t, 4),
                ["Right Shoulder Down-Up"] = t => .04f * Wave(t, 4),
                ["Spine Left-Right"] = t => .03f * Wave(t, 4, .25f),
                ["Head Turn Left-Right"] = t => .05f * Wave(t, 4, .6f),
                ["Head Tilt Left-Right"] = t => .02f * Wave(t, 2, .1f),
                ["Left Upper Leg In-Out"] = t => .02f * Wave(t, 4, .25f),
                ["Right Upper Leg In-Out"] = t => -.02f * Wave(t, 4, .25f),
            };
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string name = HumanTrait.MuscleName[i];
                float baseline = standing[i];
                if (motion.TryGetValue(name, out var extra)) Curve(name, t => baseline + extra(t));
                else Curve(name, _ => baseline);
            }
            Curve("RootT.x", t => .012f * Wave(t, 4, .25f));
            // Body height: solve so the soles rest on the character's floor (Unity's humanoid units vary).
            var baked = new Mesh();
            float Height(float y)
            {
                Curve("RootT.y", _ => y);
                var graph = PlayableGraph.Create();
                try
                {
                    var output = AnimationPlayableOutput.Create(graph, "idle", animator);
                    var playable = AnimationClipPlayable.Create(graph, clip);
                    playable.SetApplyFootIK(false);   // the clip has no IK goal curves
                    output.SetSourcePlayable(playable);
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    playable.SetTime(0); graph.Evaluate();
                    skin.BakeMesh(baked, true);
                    return baked.vertices.Min(v => go.transform.InverseTransformPoint(skin.transform.TransformPoint(v)).y);
                }
                finally { graph.Destroy(); }
            }
            Curve("RootT.z", _ => 0);
            Curve("RootQ.x", _ => 0); Curve("RootQ.y", _ => 0); Curve("RootQ.z", _ => 0); Curve("RootQ.w", _ => 1);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true; settings.loopBlend = true;
            settings.loopBlendOrientation = true; settings.loopBlendPositionY = true; settings.loopBlendPositionXZ = true;
            settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true; settings.heightFromFeet = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            // Secant search on the body height (the relation is linear, so this settles in a few steps).
            float y0 = body.y, y1 = body.y + .1f, e0 = Height(y0), e1 = Height(y1);
            for (int i = 0; i < 6 && Mathf.Abs(e1) > .002f && Mathf.Abs(e1 - e0) > 1e-6f; i++)
            {
                float y2 = y1 - e1 * (y1 - y0) / (e1 - e0);
                y0 = y1; e0 = e1; y1 = y2; e1 = Height(y1);
            }
            Height(y1);
            UnityEngine.Object.DestroyImmediate(baked);
            Debug.Log($"Idle clip: body height {y1:F3}, soles {e1 * 100:F1} cm from the floor.");
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(Path);
            if (existing != null) { EditorUtility.CopySerialized(clip, existing); clip = existing; }
            else AssetDatabase.CreateAsset(clip, Path);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            return clip;
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static int Index(string name)
    {
        int i = Array.IndexOf(HumanTrait.MuscleName, name);
        if (i < 0) throw new ArgumentException("Unknown muscle " + name);
        return i;
    }
}
