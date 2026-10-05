using UnityEngine;
using System.Collections.Generic;

namespace FlashGame
{
    // Meter-scale waterfront district. Window grids are shaded rather than separate objects.
    public static class CityDistrict
    {
        public static readonly Vector3 LabCenter = new Vector3(-390, 0, -260);
        public static void Build(PrototypeWorld w, Transform root)
        {
            var asphalt=w.Material("Weathered asphalt",new Color(.105f,.12f,.13f));
            var concrete=w.Material("Limestone paving",new Color(.53f,.52f,.48f));
            var stone=w.Material("Pale architectural stone",new Color(.66f,.65f,.59f));
            var steel=w.Material("Graphite metal",new Color(.16f,.19f,.21f));
            var glass=w.Material("Curtain wall",new Color(.25f,.38f,.42f));
            var grass=w.Material("River park grass",new Color(.18f,.28f,.14f));
            var bark=w.Material("Tree trunks",new Color(.2f,.16f,.12f));
            var foliage=w.Material("Tree canopy",new Color(.14f,.25f,.12f));
            var paint=w.Material("Lane paint",new Color(.79f,.75f,.6f));
            var water=w.Material("Harbour water",new Color(.12f,.29f,.34f)); water.SetFloat("_Surface",2);
            var facades=new Material[4];
            Color[] colors={new Color(.38f,.46f,.49f),new Color(.59f,.56f,.5f),new Color(.29f,.37f,.42f),new Color(.42f,.29f,.23f)};
            for(int i=0;i<4;i++){facades[i]=w.Material("Facade grid "+i,colors[i]);facades[i].SetFloat("_Surface",1);facades[i].SetFloat("_WindowStyle",i);}
            w.Box("City foundation",new Vector3(0,-1,0),new Vector3(1560,2,1560),asphalt);
            w.Box("Regional landscape",new Vector3(-1450,-6,0),new Vector3(4400,4,5000),grass);
            for(int i=0;i<15;i++)
                w.Shape("Distant wooded ridge",PrimitiveType.Sphere,root,new Vector3(-1550-i%3*200,20,-1900+i*280),new Vector3(750,190+i%4*55,650),foliage,false);
            w.Box("Harbour",new Vector3(1120,-2,0),new Vector3(680,.3f,2400),water,false);
            // Fast runners can cross the harbour surface (SpeedsterMotor water running).
            WaterZones.Clear();
            WaterZones.Add(new Rect(781,-1200,1390-781,2400),-1.85f);
            w.Box("Far shore",new Vector3(1480,-1,0),new Vector3(180,2,1560),grass);
            w.Box("Training straight",new Vector3(0,-1,-1130),new Vector3(42,2,700),asphalt);
            for(int i=-5;i<=5;i++)
            for(int n=-75;n<=75;n++)
            {
                float p=n*10;
                if(Mathf.Abs(p-Mathf.Round(p/130)*130)<15)continue;
                if(!OnLabPlaza(i*130,p))w.Box("Road dash",new Vector3(i*130,.015f,p),new Vector3(.17f,.02f,4),paint,false);
                if(!OnLabPlaza(p,i*130))w.Box("Road dash",new Vector3(p,.015f,i*130),new Vector3(4,.02f,.17f),paint,false);
            }
            var random=new System.Random(4026);
            for(int x=-6;x<6;x++) for(int z=-6;z<6;z++)
            {
                float cx=x*130+65,cz=z*130+65;
                if(Mathf.Abs(cx-LabCenter.x)<190 && Mathf.Abs(cz-LabCenter.z)<160)continue;
                w.Box("Raised city block",new Vector3(cx,.12f,cz),new Vector3(106,.24f,106),concrete);
                if((x+z+20)%9==0)
                {
                    w.Box("Neighbourhood park",new Vector3(cx,.28f,cz),new Vector3(92,.12f,92),grass,false);
                    for(int t=0;t<9;t++)Tree(w,root,new Vector3(cx-32+(t%3)*32,0,cz-32+(t/3)*32),bark,foliage);
                    continue;
                }
                float h=45+random.Next(95)+(x>0?random.Next(110):0);
                float bw=54+random.Next(20),depth=60+random.Next(20);
                bool enterable=(x+z+20)%7==0;
                Building(w,new Vector3(cx,0,cz),bw,depth,h,facades[random.Next(4)],stone,steel,glass,enterable);
                for(int t=-1;t<=1;t+=2)Tree(w,root,new Vector3(cx+t*45,0,cz-43),bark,foliage);
            }
            // Continuous promenade, with a barrier except where bridges meet the street grid.
            w.Box("Waterfront promenade",new Vector3(763,.25f,0),new Vector3(30,.5f,1560),concrete);
            for(int z=-750;z<=750;z+=30)
            {
                if(Mathf.Abs(z)<30 || Mathf.Abs(z-520)<30)continue;
                // Gaps in the sea wall let runners sprint straight out onto the water.
                if(Mathf.Abs(z+330)<40 || Mathf.Abs(z-270)<40)continue;
                w.Box("Sea wall",new Vector3(780,1,z),new Vector3(1.5f,2,29),stone);
                Tree(w,root,new Vector3(751,0,z),bark,foliage);
            }
            Bridge(w,root,0,asphalt,stone,steel);
            Bridge(w,root,520,asphalt,stone,steel);
            StarLabs.Build(w,root,LabCenter);
            for(int i=0;i<12;i++)
                Building(w,new Vector3(1460,0,-700+i*120),55,70,35+i%4*28,facades[i%4],stone,steel,glass,false);
            w.Labels.Add(new WorldLabel(new Vector3(0,7,-800),"ACCELERATION STRAIGHT"));
            w.Labels.Add(new WorldLabel(new Vector3(750,7,20),"CENTRAL CITY WATERFRONT"));
            foreach(float z in new[]{-330f,270f})
                w.Labels.Add(new WorldLabel(new Vector3(772,5,z),"WATER RUN\nKeep above 24 m/s"));
            Tunnel(w);
            CombineDistrict(root);
        }
        // Covered stretch of the acceleration straight for ceiling running: wall-run a side wall into
        // the roof, or speed-jump into it at Super Speed or faster.
        static void Tunnel(PrototypeWorld w)
        {
            var shell=w.Material("Tunnel concrete",new Color(.42f,.43f,.44f));
            shell.SetFloat("_Surface",4);shell.SetFloat("_WindowStyle",3);
            var lamp=w.Material("Tunnel lights",new Color(1,.92f,.75f),1);
            const float z0=-900,z1=-1100,height=5.2f;
            for(int side=-1;side<=1;side+=2)
                w.Box("Tunnel wall",new Vector3(side*20.5f,height/2,(z0+z1)/2),new Vector3(1,height,z0-z1),shell);
            w.Box("Tunnel roof",new Vector3(0,height+.3f,(z0+z1)/2),new Vector3(42,.6f,z0-z1),shell);
            for(float z=z0-10;z>z1;z-=20)
                for(int side=-1;side<=1;side+=2)
                    w.Box("Tunnel light",new Vector3(side*8,height-.03f,z),new Vector3(1.2f,.06f,6),lamp,false);
            w.Labels.Add(new WorldLabel(new Vector3(0,7,z0+12),"CEILING RUN TUNNEL\nSpeed-jump into the roof"));
        }
        static bool OnLabPlaza(float x,float z)=>new Vector2(x-LabCenter.x,z-LabCenter.z).sqrMagnitude<120*120;
        static void CombineDistrict(Transform root)
        {
            var groups=new Dictionary<(Material,int,int),List<CombineInstance>>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();
                if(renderer==null || filter.sharedMesh==null || renderer.sharedMaterials.Length!=1 || !filter.sharedMesh.isReadable)continue;
                if(filter.GetComponentInParent<StarLabsDynamic>()!=null)continue;
                var key=(renderer.sharedMaterial,Mathf.FloorToInt(filter.transform.position.x/390),Mathf.FloorToInt(filter.transform.position.z/390));
                if(!groups.TryGetValue(key,out var list))groups[key]=list=new List<CombineInstance>();
                list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                renderer.enabled=false;
                Object.Destroy(renderer);Object.Destroy(filter);
            }
            foreach(var group in groups)
            {
                var mesh=new Mesh{name="District mesh",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                mesh.CombineMeshes(group.Value.ToArray(),true,true);mesh.UploadMeshData(true);
                var go=new GameObject("District render cluster");go.transform.SetParent(root,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=group.Key.Item1;
                go.AddComponent<RuntimeMeshOwner>().Mesh=mesh;
            }
        }
        static void Building(PrototypeWorld w,Vector3 p,float width,float depth,float height,Material facade,Material stone,Material metal,Material glass,bool entry)
        {
            float ground=entry?8:0;
            w.Box("Tower facade",p+Vector3.up*((height-ground)/2+ground),new Vector3(width,height-ground,depth),facade);
            if(entry)
            {
                w.Box("Lobby floor",p+Vector3.up*.25f,new Vector3(width,.5f,depth),stone);
                w.Box("Lobby back",p+new Vector3(0,4,depth/2-1),new Vector3(width,8,2),facade);
                w.Box("Lobby west",p+new Vector3(-width/2+1,4,0),new Vector3(2,8,depth),facade);
                w.Box("Lobby east",p+new Vector3(width/2-1,4,0),new Vector3(2,8,depth),facade);
                float side=(width-10)/2;
                for(int s=-1;s<=1;s+=2)
                    w.Box("Lobby entrance glazing",p+new Vector3(s*(5+side/2),4,-depth/2+1),new Vector3(side,8,2),glass);
                w.Box("Reception desk",p+new Vector3(0,1.2f,5),new Vector3(10,2.4f,3),metal);
                w.Box("Lobby canopy",p+new Vector3(0,7,-depth/2-3),new Vector3(18,.5f,7),metal);
                w.Labels.Add(new WorldLabel(p+new Vector3(0,9,-depth/2-3),"OPEN LOBBY"));
            }
            for(int side=-1;side<=1;side+=2)
            {
                w.Box("Stone corner",p+new Vector3(side*(width/2-.5f),height/2,-depth/2-.12f),new Vector3(1.6f,height,.4f),stone,false);
                w.Box("Stone corner",p+new Vector3(side*(width/2-.5f),height/2,depth/2+.12f),new Vector3(1.6f,height,.4f),stone,false);
            }
            w.Box("Roof cornice",p+Vector3.up*(height+.35f),new Vector3(width+1.2f,.7f,depth+1.2f),metal);
            w.Box("Mechanical penthouse",p+new Vector3(width*.15f,height+3,depth*.12f),new Vector3(width*.32f,6,depth*.28f),metal);
            for(int i=-1;i<=1;i++)
            {
                w.Box("Facade mullion",p+new Vector3(i*width*.28f,height/2,-depth/2-.3f),new Vector3(.35f,height,.6f),metal,false);
                w.Box("Roof ventilation unit",p+new Vector3(i*8,height+1.5f,-depth*.26f),new Vector3(5,3,7),stone,false);
            }
            w.Box("Ground floor cornice",p+new Vector3(0,8.3f,0),new Vector3(width+1,.6f,depth+1),stone,false);
            if(height>140)
                w.Box("Stepped crown",p+Vector3.up*(height+12),new Vector3(width*.64f,24,depth*.64f),facade);
        }
        static void Tree(PrototypeWorld w,Transform root,Vector3 p,Material trunk,Material leaves)
        {
            w.Shape("Street tree trunk",PrimitiveType.Cylinder,root,p+Vector3.up*2.6f,new Vector3(.65f,2.6f,.65f),trunk,false);
            w.Shape("Street tree crown",PrimitiveType.Sphere,root,p+Vector3.up*6,new Vector3(6,8,6),leaves,false);
        }
        static void Bridge(PrototypeWorld w,Transform root,float z,Material road,Material stone,Material metal)
        {
            w.Box("Harbour bridge deck",new Vector3(1120,0,z),new Vector3(680,2,26),road);
            for(int s=-1;s<=1;s+=2)
            {
                w.Box("Bridge railing",new Vector3(1120,1.5f,z+s*13),new Vector3(680,1.7f,.7f),metal);
                for(int x=880;x<=1360;x+=160)
                {
                    w.Box("Bridge pylon",new Vector3(x,35,z+s*16),new Vector3(5,80,5),stone);
                    for(int offset=-65;offset<=65;offset+=26)
                    {
                        Vector3 a=new Vector3(x,72,z+s*16),b=new Vector3(x+offset,2,z+s*12);
                        var cable=w.Shape("Suspension cable",PrimitiveType.Cylinder,root,(a+b)/2,new Vector3(.3f,Vector3.Distance(a,b)/2,.3f),metal,false);
                        cable.transform.up=(a-b).normalized;
                    }
                }
            }
        }
    }
    public sealed class RuntimeMeshOwner:MonoBehaviour
    {
        public Mesh Mesh;
        void OnDestroy(){if(Mesh!=null)Destroy(Mesh);}
    }
}
