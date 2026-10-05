using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Unity retargets a Humanoid clip as "pose minus the source avatar's reference pose, plus the target's", so
// every avatar's reference pose must have the same shape. Ours did not: the bot is modelled in an A-pose, the
// HatchXR rig has splayed legs, and the realistic Flash was unposed with a ruler-straight spine, while Mixamo's
// T-pose curves the spine back 5 degrees a segment and tips the head 18. Clips came out hunched and bent.
// This rebuilds a file's reference pose so every limb points as in Mixamo's T-pose (read from the supplied
// Running clip, re-exported by Tools/Blender/fix_mixamo_clip.py), keeping each bone's own twist. It is what
// Unity's "Enforce T-Pose" button does, against a fixed reference.
public static class HumanoidTPose
{
    public const string Reference = "Assets/FlashPrototype/Character/Animations/Running.fbx";
    static readonly string[] Spine = { "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head" };
    static readonly string[][] Limbs = {
        new[] { "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand" },
        new[] { "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand" },
        new[] { "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes" },
        new[] { "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes" },
    };

    public static void Enforce(string path)
    {
        var reference = Directions(Reference);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isHuman) throw new System.InvalidOperationException(path + " has no Humanoid avatar to fix.");
        var description = avatar.humanDescription;
        var go = Pose(path, description, out var bones);
        try
        {
            var frame = BodyFrame(bones);
            foreach (var (bone, child) in Pairs(bones))
            {
                if (!reference.TryGetValue((bone, child), out var direction)) continue;
                Vector3 current = (bones[child].position - bones[bone].position).normalized;
                Vector3 target = frame * direction;
                bones[bone].rotation = Quaternion.FromToRotation(current, target) * bones[bone].rotation;
            }
            var all = go.GetComponentsInChildren<Transform>(true).Where(t => t != go.transform).ToDictionary(t => t.name, t => t);
            var skeleton = description.skeleton;
            for (int i = 0; i < skeleton.Length; i++)
                if (all.TryGetValue(skeleton[i].name, out var t)) { skeleton[i].position = t.localPosition; skeleton[i].rotation = t.localRotation; }
            description.skeleton = skeleton;
            importer.humanDescription = description;
            importer.SaveAndReimport();
        }
        finally { Object.DestroyImmediate(go); }
    }

    // Largest angle between a file's reference pose and the realistic Flash's T-pose (for checks and logs).
    public static float Error(string path)
    {
        var reference = Directions(Reference);
        var mine = Directions(path, out var frame);
        float worst = 0;
        foreach (var pair in mine)
            if (reference.TryGetValue(pair.Key, out var direction))
                worst = Mathf.Max(worst, Vector3.Angle(frame * pair.Value, frame * direction));
        return worst;
    }

    // Per-bone angles between a file's reference pose and the realistic Flash's T-pose.
    public static string Report(string path)
    {
        var reference = Directions(Reference);
        var mine = Directions(path, out var frame);
        return string.Join(", ", mine.Where(p => reference.ContainsKey(p.Key))
            .Select(p => (p.Key, angle: Vector3.Angle(p.Value, reference[p.Key])))
            .Where(p => p.angle > 3).OrderByDescending(p => p.angle)
            .Select(p => $"{p.Key.Item1}>{p.Key.Item2} {p.angle:F0}"));
    }

    static Dictionary<(string, string), Vector3> Directions(string path) => Directions(path, out _);
    // Bone-to-child directions in the body frame (right from hip to hip, world up, forward = right x up).
    static Dictionary<(string, string), Vector3> Directions(string path, out Quaternion frame)
    {
        var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (avatar == null && importer.sourceAvatar != null) avatar = importer.sourceAvatar;
        var go = Pose(path, avatar.humanDescription, out var bones);
        try
        {
            frame = BodyFrame(bones);
            var inverse = Quaternion.Inverse(frame);
            var result = new Dictionary<(string, string), Vector3>();
            foreach (var (bone, child) in Pairs(bones))
                result[(bone, child)] = inverse * (bones[child].position - bones[bone].position).normalized;
            return result;
        }
        finally { Object.DestroyImmediate(go); }
    }

    // Instantiates a model in its avatar's reference pose; maps human bone names to transforms.
    static GameObject Pose(string path, HumanDescription description, out Dictionary<string, Transform> bones)
    {
        var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        var byName = go.GetComponentsInChildren<Transform>(true).Where(t => t != go.transform).ToDictionary(t => t.name, t => t);
        foreach (var sb in description.skeleton)
            if (byName.TryGetValue(sb.name, out var t)) { t.localPosition = sb.position; t.localRotation = sb.rotation; }
        bones = new Dictionary<string, Transform>();
        foreach (var hb in description.human)
            if (byName.TryGetValue(hb.boneName, out var t)) bones[hb.humanName] = t;
        return go;
    }

    static IEnumerable<(string, string)> Pairs(Dictionary<string, Transform> bones)
    {
        // Spine first (it carries everything else), skipping optional bones a rig does not have.
        var spine = Spine.Where(bones.ContainsKey).ToArray();
        for (int i = 0; i + 1 < spine.Length; i++) yield return (spine[i], spine[i + 1]);
        foreach (var limb in Limbs)
        {
            var chain = limb.Where(bones.ContainsKey).ToArray();
            for (int i = 0; i + 1 < chain.Length; i++) yield return (chain[i], chain[i + 1]);
        }
    }

    static Quaternion BodyFrame(Dictionary<string, Transform> bones)
    {
        Vector3 right = bones["RightUpperLeg"].position - bones["LeftUpperLeg"].position;
        right.y = 0;
        return Quaternion.LookRotation(Vector3.Cross(right.normalized, Vector3.up), Vector3.up);
    }
}
