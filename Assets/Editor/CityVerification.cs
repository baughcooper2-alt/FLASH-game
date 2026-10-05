using UnityEngine;
using UnityEditor;
using FlashGame;
using System.Linq;

public static class CityVerification
{
    public static string Run()
    {
        if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Run in Play Mode.");
        Capture("city-waterfront",new Vector3(1360,450,-950),new Vector3(-100,45,-30));
        Capture("star-labs",new Vector3(-530,85,-465),new Vector3(-390,34,-255));
        Capture("flash-reference",new Vector3(-384,4.4f,-250),new Vector3(-390,3.3f,-242),38);
        var go=new GameObject("Temporary entrance test");go.layer=2;
        try
        {
            var motor=go.AddComponent<SpeedsterMotor>();motor.Respawn(new Vector3(-390,.12f,-410));Physics.SyncTransforms();
            for(int i=0;i<480;i++)motor.TickControls(Vector2.up,false,false,0,1f/60);
            if(motor.transform.position.z < -365)throw new System.Exception("Lab entrance blocked: "+motor.transform.position);
        }
        finally{Object.DestroyImmediate(go);}
        var shader=Shader.Find("FlashGame/Prototype");
        if(ShaderUtil.ShaderHasError(shader))throw new System.Exception("City shader compilation failed.");
        if(ShaderUtil.ShaderHasError(Shader.Find("FlashGame/Lightning")))throw new System.Exception("Lightning shader compilation failed.");
        return "S.T.A.R. Labs entrance passed; city/lightning shaders compiled; " + WallRunVerification.Run();
    }
    public static void Capture(string name,Vector3 position,Vector3 target,float fov=58)
    {
        var go=new GameObject("Temporary review camera");var c=go.AddComponent<Camera>();
        var rt=new RenderTexture(1440,900,24);var tex=new Texture2D(1440,900,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try
        {
            c.transform.position=position;c.transform.LookAt(target);c.fieldOfView=fov;c.farClipPlane=4000;c.clearFlags=CameraClearFlags.Skybox;
            c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1440,900),0,0);tex.Apply();
            System.IO.File.WriteAllBytes("/private/tmp/"+name+".png",tex.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;c.targetTexture=null;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
    }
}
