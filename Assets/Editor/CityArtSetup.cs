using System.IO;
using UnityEditor;
using UnityEngine;

public static class CityArtSetup
{
    public static string Configure()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode before importing city art.");
        const string root="Assets/FlashPrototype/ReferenceCharacter";
        const string resources="Assets/FlashPrototype/Resources";
        if(!AssetDatabase.IsValidFolder(root))AssetDatabase.CreateFolder("Assets/FlashPrototype","ReferenceCharacter");
        if(!AssetDatabase.IsValidFolder(resources))AssetDatabase.CreateFolder("Assets/FlashPrototype","Resources");
        File.Copy("/private/tmp/flash-import/FlashReference.obj",root+"/FlashReference.obj",true);
        File.WriteAllText(root+"/FlashReference.obj","mtllib FlashReference.mtl\n"+File.ReadAllText(root+"/FlashReference.obj"));
        string[] keys={"Mat.1","Mat.2","Mat.3","Mat.4","Mat.5","Mat.6","Mat.7","Mat.8","Mat.9"};
        string[] textures={"emblem_Base_Color_1.png","lightning_embelm_Base_Color.png","golden_Base_Color.png","earing_round_Base_Color.png","mask_Base_Color.png","eyes_Base_Color.png","face_Base_Color.png","Pants_6.png","Body_6.png"};
        string[] normals={null,null,null,"earing_round_Normal_OpenGL.png","mask_Normal_OpenGL.png","eyes_Normal_OpenGL.png","face_Normal_OpenGL.png","pants_Normal_OpenGL.png","suit_3d_body_Normal.png"};
        string mtl="";
        for(int i=0;i<keys.Length;i++)mtl+="newmtl "+keys[i]+"\nKd 1 1 1\nmap_Kd "+textures[i]+"\n\n";
        File.WriteAllText(root+"/FlashReference.mtl",mtl);
        foreach(var t in textures)File.Copy("/private/tmp/flash-reference/textures/"+t,root+"/"+t,true);
        foreach(var t in normals)if(t!=null)File.Copy("/private/tmp/flash-reference/textures/"+t,root+"/"+t,true);
        AssetDatabase.Refresh();
        foreach(var t in textures)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(root+"/"+t);importer.maxTextureSize=1024;importer.SaveAndReimport();
        }
        foreach(var t in normals)if(t!=null)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(root+"/"+t);importer.textureType=TextureImporterType.NormalMap;importer.maxTextureSize=1024;importer.SaveAndReimport();
        }
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(root+"/FlashReference.obj");
        var instance=Object.Instantiate(source);instance.name="Flash reference - posed OBJ";
        try
        {
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int n=0;n<materials.Length;n++)
                {
                    string materialName=materials[n]!=null?materials[n].name:"";
                    int index=System.Array.IndexOf(keys,materialName);
                    if(index<0)continue;
                    string path=root+"/"+keys[index]+".mat";
                    var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
                    m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/"+textures[index]));
                    m.SetColor("_BaseColor",Color.white);m.SetFloat("_Smoothness",.32f);m.SetFloat("_Metallic",index==2||index==3?.6f:.05f);
                    if(normals[index]!=null){m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/"+normals[index]));m.EnableKeyword("_NORMALMAP");}
                    materials[n]=m;EditorUtility.SetDirty(m);
                }
                renderer.sharedMaterials=materials;
            }
            PrefabUtility.SaveAsPrefabAsset(instance,resources+"/FlashReference.prefab");
        }
        finally{Object.DestroyImmediate(instance);}
        var lightning=AssetDatabase.LoadAssetAtPath<Material>(resources+"/SpeedLightning.mat");
        if(lightning==null){lightning=new Material(Shader.Find("FlashGame/Lightning"));AssetDatabase.CreateAsset(lightning,resources+"/SpeedLightning.mat");}
        else lightning.shader=Shader.Find("FlashGame/Lightning");
        var suit=AssetDatabase.LoadAssetAtPath<Material>("Assets/FlashPrototype/Character/Flash suit.mat");
        if(suit!=null){suit.SetColor("_BaseColor",new Color(.68f,.62f,.57f));suit.SetFloat("_Smoothness",.38f);suit.SetFloat("_Metallic",.16f);EditorUtility.SetDirty(suit);}
        AssetDatabase.SaveAssets();
        return "Reference OBJ imported with nine texture materials; lightning material saved; animated suit darkened.";
    }
}
