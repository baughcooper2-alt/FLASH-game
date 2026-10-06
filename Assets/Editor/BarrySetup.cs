using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the out-of-costume Barry Allen looks from Character/Barry/BarryAllen.fbx: the user's skinny base mesh,
// rigged by Tools/Blender/rig_tpose_mesh.py, wearing the face and outfit of their Meshy "CSI" Barry design
// (Tools/Blender/build_csi_barry.py, Tools/csi_textures.py). One model gives two playable looks:
//   BarryAllen.prefab  white tee, jeans and sneakers (the overshirt, watch and jacket hidden)
//   CSIBarry.prefab    the full CSI outfit: open plaid overshirt and watch on top
//   Resources/Townsperson.prefab  the same body for the people of Central City: generic faces in several skin
//                      tones, a ski-masked variant for robbers, and neutral garments the game tints per person.
// All are driven by the Flash's locomotion controller through the Humanoid avatar.
public static class BarrySetup
{
    const string Folder = "Assets/FlashPrototype/Character/Barry/";
    const string Fbx = Folder + "BarryAllen.fbx";
    // FBX material slot -> colour texture, max size, metallic, smoothness.
    static readonly (string slot, string texture, int size, float metal, float smooth)[] Slots = {
        ("Barry body", "Barry_Body.png", 2048, 0, .36f),
        ("Barry shirt", "Barry_Shirt.png", 2048, 0, .18f),
        ("Barry tee", "Barry_Tee.png", 1024, 0, .2f),
        ("Barry jeans", "Barry_Jeans.png", 1024, 0, .22f),
        ("Barry jacket", "../Townsperson/Townsperson_Jacket.png", 1024, 0, .3f),
        ("Barry hair", "Barry_Hair.png", 1024, 0, .42f),
        ("Barry shoes", "Barry_Shoes.png", 1024, 0, .25f),
        ("Barry watch", "Barry_Watch.png", 256, .6f, .62f),
    };
    // Parts hidden on the casual look.
    static readonly string[] CsiOnly = { "Shirt", "Watch", "Jacket" };
    const string People = Folder + "Townsperson/";

    public static void ConfigureBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/FlashPrototype.unity");
        Configure();
    }

    [MenuItem("Flash/Configure Barry Allen")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        if (!AssetDatabase.IsValidFolder(Folder + "Materials")) AssetDatabase.CreateFolder(Folder.TrimEnd('/'), "Materials");
        // Materials first: reimporting their textures reloads importers, which would drop unsaved model settings.
        var materials = Slots.Select(s => MakeMaterial(s.slot, s.texture, s.size, s.metal, s.smooth)).ToArray();
        var importer = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        // Start from the file's own pose, not one saved by an earlier Enforce: a rebuilt rig would keep the old one.
        var description = importer.humanDescription;
        description.human = new HumanBone[0]; description.skeleton = new SkeletonBone[0];
        importer.humanDescription = description;
        for (int i = 0; i < Slots.Length; i++)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), Slots[i].slot), materials[i]);
        importer.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Avatar>().First();
        if (!avatar.isValid || !avatar.isHuman) throw new System.InvalidOperationException("Barry Allen avatar is not a valid Humanoid.");
        // Same reference pose as every other avatar, so the shared clips retarget without bending him.
        HumanoidTPose.Enforce(Fbx);
        avatar = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Avatar>().First();

        var casual = BuildPrefab("BarryAllen", avatar, CsiOnly);
        var csi = BuildPrefab("CSIBarry", avatar, new[] { "Jacket" });
        var townsperson = BuildTownsperson(avatar);
        var bootstrap = Object.FindFirstObjectByType<FlashGame.FlashPrototype>();
        if (bootstrap == null) throw new System.InvalidOperationException("Open the FlashPrototype scene first.");
        var serialized = new SerializedObject(bootstrap);
        var looks = serialized.FindProperty("civilianPrefabs");
        looks.arraySize = 2;
        looks.GetArrayElementAtIndex(0).objectReferenceValue = casual;
        looks.GetArrayElementAtIndex(1).objectReferenceValue = csi;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
        EditorSceneManager.SaveScene(bootstrap.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Barry Allen configured: Humanoid avatar (T-pose error {HumanoidTPose.Error(Fbx):F1} deg), prefabs {casual.name}, {csi.name} and {townsperson.name}, scene references saved.");
    }

    // The townsperson: Barry's body with generic skin, neutral (tintable) garments and the Townsperson component.
    static GameObject BuildTownsperson(Avatar avatar)
    {
        Texture2D Tex(string file, int size)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(People + file);
            if (importer.maxTextureSize != size || !importer.sRGBTexture) { importer.sRGBTexture = true; importer.maxTextureSize = size; importer.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(People + file);
        }
        var light = Tex("Townsperson_Body_Light.png", 1024);
        var skins = new[] { light, Tex("Townsperson_Body_Tan.png", 1024), Tex("Townsperson_Body_Dark.png", 1024) };
        var masked = Tex("Townsperson_Body_Masked.png", 1024);
        var pants = Tex("Townsperson_Pants.png", 1024);
        var hair = Tex("Townsperson_Hair.png", 512);
        var jacket = Tex("Townsperson_Jacket.png", 1024);
        var tee = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Textures/Barry_Tee.png");
        var swaps = new System.Collections.Generic.Dictionary<string, Material>
        {
            ["Body"] = MakePlain("Townsperson body", light, .3f), ["Jeans"] = MakePlain("Townsperson trousers", pants, .2f),
            ["Hair"] = MakePlain("Townsperson hair", hair, .35f), ["Jacket"] = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Materials/Barry jacket.mat"),
        };
        var root = BuildRoot("Townsperson", avatar, new[] { "Shirt", "Jacket", "Watch" }, out var model);
        try
        {
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (swaps.TryGetValue(r.name, out var m)) r.sharedMaterial = m;
            root.AddComponent<FlashGame.Townsperson>().SetTextures(skins, masked, tee, pants, jacket, hair);
            // Many of them: only skin the ones on screen.
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = false;
            return PrefabUtility.SaveAsPrefabAsset(root, "Assets/FlashPrototype/Resources/Townsperson.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    static Material MakePlain(string name, Texture2D texture, float smooth)
    {
        string path = Folder + "Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(material, path); }
        material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(material);
        return material;
    }

    static GameObject BuildPrefab(string name, Avatar avatar, string[] hidden)
    {
        var root = BuildRoot(name, avatar, hidden, out _);
        try { return PrefabUtility.SaveAsPrefabAsset(root, Folder + name + ".prefab"); }
        finally { Object.DestroyImmediate(root); }
    }

    // A prefab root holding the scaled, animated model (unsaved; the caller saves and destroys it).
    static GameObject BuildRoot(string name, Avatar avatar, string[] hidden, out GameObject model)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/FlashPrototype/Character/FlashLocomotion.controller");
        var stand = AssetDatabase.LoadAllAssetsAtPath("Assets/FlashPrototype/Character/Animations/stand.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        var root = new GameObject(name);
        {
            model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx), root.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Rig";
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var part in hidden)
                model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == part).gameObject.SetActive(false);
            stand.SampleAnimation(model, 0);
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in renderers) renderer.updateWhenOffscreen = true;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            // The same standing height as the suited Flash, so the shared run plants his feet the same way.
            float scale = 1.85f / bounds.size.y;
            model.transform.localScale *= scale;
            model.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
            return root;
        }
    }

    static Material MakeMaterial(string name, string file, int size, float metal, float smooth)
    {
        string path = Folder + "Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        string texturePath = System.IO.Path.GetFullPath(Folder + "Textures/" + file).Replace('\\', '/');
        texturePath = texturePath.Substring(texturePath.IndexOf("Assets/"));
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        if (importer.maxTextureSize != size || importer.textureType != TextureImporterType.Default || !importer.sRGBTexture)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.maxTextureSize = size;
            importer.SaveAndReimport();
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", metal);
        material.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(material);
        return material;
    }
}
