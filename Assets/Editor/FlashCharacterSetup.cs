using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Rebuildable asset setup. The movement controller remains the authority for displacement.
public static class FlashCharacterSetup
{
    const string Root = "Assets/FlashPrototype/Character/";

    [MenuItem("Flash/Configure animated character")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        var modelImporter = (ModelImporter)AssetImporter.GetAtPath(Root + "Flash.fbx");
        modelImporter.animationType = ModelImporterAnimationType.Human;
        modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        modelImporter.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Root + "Flash.fbx").OfType<Avatar>().First();
        if (!avatar.isValid || !avatar.isHuman) throw new System.InvalidOperationException("Invalid Flash avatar.");

        string[] names = { "stand", "walk", "run", "jumpUp", "jumpDown" };
        var clips = new AnimationClip[names.Length];
        for (int n = 0; n < names.Length; n++)
        {
            string path = Root + "Animations/" + names[n] + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.importAnimation = true;
            var settings = importer.defaultClipAnimations;
            foreach (var clip in settings)
            {
                clip.name = names[n];
                clip.loopTime = n < 3;
                clip.loopPose = n < 3;
                clip.lockRootRotation = true;
                clip.lockRootPositionXZ = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalPositionY = true;
            }
            importer.clipAnimations = settings;
            importer.SaveAndReimport();
            clips[n] = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        }

        string controllerPath = Root + "FlashLocomotion.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("RunRate", AnimatorControllerParameterType.Float);
            var sm = controller.layers[0].stateMachine;
            var locomotion = sm.AddState("Locomotion");
            var blend = new BlendTree { name = "Idle Walk Run", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(blend, controller);
            blend.AddChild(clips[0], 0);
            blend.AddChild(clips[1], 2);
            blend.AddChild(clips[2], 7);
            locomotion.motion = blend;
            locomotion.speedParameter = "RunRate";
            locomotion.speedParameterActive = true;
            var rise = sm.AddState("Jump rising"); rise.motion = clips[3];
            var fall = sm.AddState("Jump falling"); fall.motion = clips[4];
            sm.defaultState = locomotion;
            var t = Transition(locomotion, rise);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            t.AddCondition(AnimatorConditionMode.Greater, 0, "VerticalSpeed");
            t = Transition(locomotion, fall);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            t.AddCondition(AnimatorConditionMode.Less, 0.01f, "VerticalSpeed");
            Transition(rise, fall).AddCondition(AnimatorConditionMode.Less, 0, "VerticalSpeed");
            Transition(rise, locomotion).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            Transition(fall, locomotion).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
        }

        var body = MakeMaterial("Flash suit", "texture_2_14126322532748253036.png");
        var emblem = MakeMaterial("Flash emblem", "texture_0_17832851910588591680.png");
        var root = new GameObject("FlashCharacter");
        try
        {
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Flash.fbx"), root.transform);
            model.name = "Rig";
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            clips[0].SampleAnimation(model, 0);
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m.name.Contains("Logo") ? emblem : body).ToArray();
                renderer.updateWhenOffscreen = true;
            }
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float scale = 1.85f / bounds.size.y;
            model.transform.localScale *= scale;
            model.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "FlashCharacter.prefab");
            var bootstrap = Object.FindFirstObjectByType<FlashGame.FlashPrototype>();
            if (bootstrap == null) throw new System.InvalidOperationException("Open FlashPrototype scene first.");
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("characterPrefab").objectReferenceValue = prefab;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
            EditorSceneManager.SaveScene(bootstrap.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Flash character configured: valid Humanoid, five clips, textured prefab, scene reference saved.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false; t.duration = 0.12f; t.hasFixedDuration = true;
        return t;
    }

    static Material MakeMaterial(string name, string texture)
    {
        string path = Root + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/" + texture));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.25f);
        material.SetFloat("_Metallic", 0.05f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
