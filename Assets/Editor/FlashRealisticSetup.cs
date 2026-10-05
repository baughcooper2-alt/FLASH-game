using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the playable realistic Flash: the supplied high-detail reference model, rigged and unposed to a
// T-pose in Blender (Character/Realistic/FlashRealistic.fbx), driven by the existing locomotion clips.
public static class FlashRealisticSetup
{
    const string Root = "Assets/FlashPrototype/Character/";
    const string Folder = Root + "Realistic/";
    const string Textures = "Assets/FlashPrototype/ReferenceCharacter/";
    // OBJ material slot -> runtime material name (SuitPainter keys on these), colour texture, normal map.
    static readonly (string slot, string name, string color, string normal)[] Slots = {
        ("Mat.9", "Flash suit body", "Body_6.png", "suit_3d_body_Normal.png"),
        ("Mat.8", "Flash suit legs", "Pants_6.png", "pants_Normal_OpenGL.png"),
        ("Mat.10", "Flash boots", "Pants_6.png", "pants_Normal_OpenGL.png"),
        ("Mat.5", "Flash cowl", "mask_Base_Color.png", "mask_Normal_OpenGL.png"),
        ("Mat.3", "Flash gold trim", "golden_Base_Color.png", null),
        ("Mat.4", "Flash ear lightning", "earing_round_Base_Color.png", "earing_round_Normal_OpenGL.png"),
        ("Mat.1", "Flash emblem field", "emblem_Base_Color_1.png", null),
        ("Mat.2", "Flash emblem bolt", "lightning_embelm_Base_Color.png", null),
        ("Mat.7", "Flash face", "face_Base_Color.png", "face_Normal_OpenGL.png"),
        ("Mat.6", "Flash eyes", "eyes_Base_Color.png", "eyes_Normal_OpenGL.png"),
    };

    public static void ConfigureBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/FlashPrototype.unity");
        Configure();
        EnemyBotSetup.Configure();
    }

    // The supplied Mixamo "Running" clip replaces the run in the locomotion blend (Animations/Running.fbx).
    public static AnimationClip ConfigureRun()
    {
        string path = Root + "Animations/Running.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.name = "Running";
            clip.loopTime = true; clip.loopPose = true;
            clip.lockRootRotation = true; clip.lockRootPositionXZ = true; clip.lockRootHeightY = true;
            clip.keepOriginalOrientation = true; clip.keepOriginalPositionXZ = true; clip.keepOriginalPositionY = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        var running = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "FlashLocomotion.controller");
        var blend = (BlendTree)controller.layers[0].stateMachine.states.First(s => s.state.name == "Locomotion").state.motion;
        var children = blend.children;
        children[children.Length - 1].motion = running;
        blend.children = children;
        EditorUtility.SetDirty(blend); EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"Running clip: {running.length:F3} s, loop {running.isLooping}, human {running.isHumanMotion}.");
        return running;
    }

    [MenuItem("Flash/Configure realistic character")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        ConfigureRun();
        if (!AssetDatabase.IsValidFolder(Folder + "Materials")) AssetDatabase.CreateFolder(Folder.TrimEnd('/'), "Materials");
        string fbx = Folder + "FlashRealistic.fbx";
        // Materials first: reimporting their textures reloads importers, which would drop unsaved model settings.
        var materials = Slots.Select(s => MakeMaterial(s.name, s.color, s.normal)).ToArray();
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        for (int i = 0; i < Slots.Length; i++)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), Slots[i].slot), materials[i]);
        importer.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().First();
        if (!avatar.isValid || !avatar.isHuman) throw new System.InvalidOperationException("Realistic Flash avatar is not a valid Humanoid.");

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "FlashLocomotion.controller");
        var stand = AssetDatabase.LoadAllAssetsAtPath(Root + "Animations/stand.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        var root = new GameObject("FlashRealistic");
        GameObject prefab;
        float scale;
        int meshes;
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), root.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Rig";
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            stand.SampleAnimation(model, 0);
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in renderers) renderer.updateWhenOffscreen = true;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            scale = 1.85f / bounds.size.y;
            model.transform.localScale *= scale;
            model.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
            prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "FlashRealistic.prefab");
            meshes = renderers.Length;
        }
        finally { Object.DestroyImmediate(root); }
        // The temporary build object is gone before the scene is saved.
        var bootstrap = Object.FindFirstObjectByType<FlashGame.FlashPrototype>();
        if (bootstrap == null) throw new System.InvalidOperationException("Open the FlashPrototype scene first.");
        var serialized = new SerializedObject(bootstrap);
        serialized.FindProperty("characterPrefab").objectReferenceValue = prefab;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
        EditorSceneManager.SaveScene(bootstrap.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Realistic Flash configured: Humanoid avatar, {meshes} skinned meshes, scale {scale:F3}, scene reference saved.");
    }

    static Material MakeMaterial(string name, string color, string normal)
    {
        string path = Folder + "Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        Texture(color, false);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + color));
        material.SetColor("_BaseColor", Color.white);
        if (normal != null)
        {
            Texture(normal, true);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + normal));
            material.SetFloat("_BumpScale", 1);
            material.EnableKeyword("_NORMALMAP");
        }
        bool gold = name.Contains("gold") || name.Contains("ear") || name.Contains("bolt");
        // Suit: slightly glossy synthetic leather. Gold trim: brushed metal. Skin and eyes: soft and wet.
        material.SetFloat("_Metallic", gold ? .85f : name.Contains("eyes") ? 0 : .08f);
        material.SetFloat("_Smoothness", gold ? .62f : name.Contains("eyes") ? .85f : name.Contains("face") ? .38f : .5f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void Texture(string file, bool normal)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Textures + file);
        int size = file.Contains("Body") || file.Contains("Pants") || file.Contains("face") || file.Contains("mask") || file.Contains("suit") || file.Contains("pants") ? 2048 : 1024;
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType == type && importer.maxTextureSize == size) return;
        importer.textureType = type;
        importer.maxTextureSize = size;
        importer.SaveAndReimport();
    }
}
