using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds Resources/EnemyBot.prefab from the supplied enemy bot (Enemies/Bot): Humanoid avatar, URP Lit
// material, and a locomotion blend of the Flash stand clip, the bot's own walk and the supplied run.
public static class EnemyBotSetup
{
    const string Folder = "Assets/FlashPrototype/Enemies/Bot/";
    const string Character = "Assets/FlashPrototype/Character/";

    [MenuItem("Flash/Configure enemy bot")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        // Textures and material first: reimports reload importers and would drop unsaved model settings.
        Texture("EnemyBot_BaseColor.png", TextureImporterType.Default, true);
        Texture("EnemyBot_Normal.png", TextureImporterType.NormalMap, false);
        Texture("EnemyBot_MetallicSmoothness.png", TextureImporterType.Default, false);
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "EnemyBot.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "EnemyBot" };
            AssetDatabase.CreateAsset(material, Folder + "EnemyBot.mat");
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "EnemyBot_BaseColor.png"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "EnemyBot_Normal.png"));
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "EnemyBot_MetallicSmoothness.png"));
        material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetFloat("_Smoothness", 1); material.SetFloat("_Metallic", 1);
        EditorUtility.SetDirty(material);

        string fbx = Folder + "EnemyBot.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "material"), material);
        var takes = importer.defaultClipAnimations;
        foreach (var take in takes)
        {
            take.name = "BotWalk";
            take.loopTime = true; take.loopPose = true;
            take.lockRootRotation = true; take.lockRootPositionXZ = true; take.lockRootHeightY = true;
            take.keepOriginalOrientation = true; take.keepOriginalPositionXZ = true; take.keepOriginalPositionY = true;
        }
        importer.clipAnimations = takes;
        importer.SaveAndReimport();
        // The head and neck carry no skin weights, so Unity's auto-mapper skips them; map the HumanIK names explicitly.
        importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
        var description = importer.humanDescription;
        description.human = HumanMap.Select(pair => new HumanBone
        {
            humanName = pair.human, boneName = "Character1_" + pair.bone, limit = new HumanLimit { useDefaultValues = true },
        }).ToArray();
        description.skeleton = AssetDatabase.LoadAssetAtPath<GameObject>(fbx).GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
        {
            name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale,
        }).ToArray();
        importer.humanDescription = description;
        importer.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().First();
        if (!avatar.isValid || !avatar.isHuman) throw new System.InvalidOperationException("Enemy bot avatar is not a valid Humanoid.");
        var walk = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        var stand = Clip(Character + "Animations/stand.fbx");
        var run = Clip(Character + "Animations/Running.fbx");

        string controllerPath = Folder + "BotLocomotion.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("RunRate", AnimatorControllerParameterType.Float);
        var sm = controller.layers[0].stateMachine;
        var state = sm.AddState("Locomotion");
        var blend = new BlendTree { name = "Idle Walk Run", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(blend, controller);
        blend.AddChild(stand, 0); blend.AddChild(walk, 1.6f); blend.AddChild(run, 6);
        state.motion = blend; state.speedParameter = "RunRate"; state.speedParameterActive = true;
        sm.defaultState = state;

        var root = new GameObject("EnemyBot");
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), root.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Rig";
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.avatar = avatar; animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            stand.SampleAnimation(model, 0);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) { bounds.Encapsulate(r.bounds); r.updateWhenOffscreen = true; }
            float scale = 1.95f / bounds.size.y;
            model.transform.localScale *= scale;
            model.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
            if (!AssetDatabase.IsValidFolder("Assets/FlashPrototype/Resources")) AssetDatabase.CreateFolder("Assets/FlashPrototype", "Resources");
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/FlashPrototype/Resources/EnemyBot.prefab");
            Debug.Log($"Enemy bot configured: Humanoid avatar, walk {walk.length:F2} s, scale {scale:F3}.");
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
    }

    static readonly (string human, string bone)[] HumanMap = {
        ("Hips", "Hips"), ("Spine", "Spine"), ("Chest", "Spine1"), ("UpperChest", "Spine2"), ("Neck", "Neck"), ("Head", "Head"),
        ("LeftShoulder", "LeftShoulder"), ("LeftUpperArm", "LeftArm"), ("LeftLowerArm", "LeftForeArm"), ("LeftHand", "LeftHand"),
        ("RightShoulder", "RightShoulder"), ("RightUpperArm", "RightArm"), ("RightLowerArm", "RightForeArm"), ("RightHand", "RightHand"),
        ("LeftUpperLeg", "LeftUpLeg"), ("LeftLowerLeg", "LeftLeg"), ("LeftFoot", "LeftFoot"), ("LeftToes", "LeftToeBase"),
        ("RightUpperLeg", "RightUpLeg"), ("RightLowerLeg", "RightLeg"), ("RightFoot", "RightFoot"), ("RightToes", "RightToeBase"),
    };

    static AnimationClip Clip(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));

    static void Texture(string file, TextureImporterType type, bool srgb)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + file);
        if (importer.textureType == type && importer.sRGBTexture == srgb && importer.maxTextureSize == 2048) return;
        importer.textureType = type; importer.sRGBTexture = srgb; importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }
}
