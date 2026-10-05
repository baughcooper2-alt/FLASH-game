using System;
using System.Collections.Generic;
using System.Reflection;
using FlashGame;
using UnityEngine;

public static class WallRunVerification
{
    public static string Run()
    {
        var results = new List<string>();
        foreach (float dt in new[] { 1f / 60, 1f / 30, 0.05f })
        {
            var root = new GameObject("Temporary wall-run verification");
            try
            {
                Vector3 origin = new Vector3(5000, 0, 0);
                Box(root, origin + new Vector3(0,-1,0), new Vector3(80,2,80));
                Box(root, origin + new Vector3(0,10,10), new Vector3(20,20,10));
                Box(root, origin + new Vector3(0,20.3f,10), new Vector3(22,0.6f,12));
                var go = new GameObject("Test runner"); go.layer = 2; go.transform.SetParent(root.transform);
                var motor = go.AddComponent<SpeedsterMotor>();
                motor.Respawn(origin + new Vector3(0,0.1f,-15));
                SetTier(motor, 1); Physics.SyncTransforms();
                bool climbed=false, crested=false;
                for (int i=0;i<6/dt;i++)
                {
                    motor.TickControls(Vector2.up,false,false,0,dt);
                    climbed |= motor.WallRunning; crested |= motor.Cresting;
                    if (crested && !motor.WallRunning) break;
                }
                Require(climbed && crested && !motor.WallRunning && go.transform.position.y > 20.5f && go.transform.position.z > 4,
                    "roof at " + dt + ": " + go.transform.position);
                results.Add("roof transition at " + (1/dt).ToString("F0") + " FPS passed");

                motor.Respawn(origin + new Vector3(0,0.1f,-15)); SetTier(motor,1);
                for(int i=0;i<3/dt && !motor.WallRunning;i++) motor.TickControls(Vector2.up,false,false,0,dt);
                Require(motor.WallRunning,"attach for jump");
                motor.TickControls(Vector2.up,false,true,0,dt);
                Require(!motor.WallRunning && motor.VerticalSpeed>0,"jump detach");
                float z=go.transform.position.z;
                motor.TickControls(Vector2.zero,false,false,0,dt);
                Require(go.transform.position.z<z,"jump pushes away");
                results.Add("wall jump passed");

                motor.Respawn(origin + new Vector3(0,0.1f,-15)); SetTier(motor,1);
                for(int i=0;i<3/dt && !motor.WallRunning;i++) motor.TickControls(Vector2.up,false,false,0,dt);
                motor.TickControls(Vector2.up,true,false,0,dt);
                Require(!motor.WallRunning,"brake detach");
                motor.Respawn(origin + new Vector3(0,0.1f,-15));
                for(int i=0;i<5/dt;i++) motor.TickControls(Vector2.up,false,false,0,dt);
                Require(!motor.WallRunning && go.transform.position.y<1 && go.transform.position.z<5,"normal collision");
                results.Add("braking and normal-speed collision passed");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        return string.Join("; ",results);
    }
    static void SetTier(SpeedsterMotor m,int tier) => typeof(SpeedsterMotor).GetField("<Tier>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(m,tier);
    static void Require(bool value,string message) { if(!value) throw new Exception("Wall verification failed: "+message); }
    static void Box(GameObject root,Vector3 position,Vector3 scale)
    {
        var go=new GameObject("Test surface");go.transform.SetParent(root.transform);go.transform.position=position;
        go.AddComponent<BoxCollider>().size=scale;
    }
}
