using UnityEngine;

namespace FlashGame
{
    // Short jagged filaments follow actual travel history, including vertical traversal.
    public sealed class SpeedLightning : MonoBehaviour
    {
        const int Count=8, Samples=28;
        readonly Vector3[] history=new Vector3[Samples];
        readonly LineRenderer[] cores=new LineRenderer[Count], halos=new LineRenderer[Count];
        Material material;
        int filled;
        float clock;
        public void Build(Material source)
        {
            material=source;
            for(int i=0;i<Count;i++)
            {
                halos[i]=Line("Amber lightning aura",.12f,new Color(1,.21f,.015f,.18f));
                cores[i]=Line("Lightning filament",.022f,new Color(1,.83f,.38f,.95f));
            }
        }
        LineRenderer Line(string label,float width,Color color)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);go.layer=2;
            var l=go.AddComponent<LineRenderer>();l.sharedMaterial=material;l.useWorldSpace=true;
            l.widthMultiplier=width;l.widthCurve=AnimationCurve.Linear(0,1,1,.05f);
            l.startColor=color;l.endColor=new Color(color.r,color.g*.5f,color.b,0);
            l.numCornerVertices=1;l.numCapVertices=2;l.positionCount=Samples;
            l.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;l.receiveShadows=false;l.enabled=false;
            return l;
        }
        public void Clear(){filled=0;foreach(var l in cores)if(l!=null)l.enabled=false;foreach(var l in halos)if(l!=null)l.enabled=false;}
        public void Tick(Transform pose,float speed,float dt,bool enabled)
        {
            if(cores[0]==null)return;
            if(!enabled || speed<12){Clear();return;}
            clock+=dt;
            Vector3 center=pose.TransformPoint(new Vector3(0,1,0));
            if(filled>0 && Vector3.Distance(center,history[0])>12)filled=0;
            for(int i=Samples-1;i>0;i--)history[i]=history[i-1];
            history[0]=center;filled=Mathf.Min(filled+1,Samples);
            while(filled>2 && Vector3.Distance(history[0],history[filled-1])>32)filled--;
            int phase=Mathf.FloorToInt(clock*24);
            for(int bolt=0;bolt<Count;bolt++)
            {
                bool visible=filled>2 && (phase+bolt)%7!=0;
                cores[bolt].enabled=halos[bolt].enabled=visible;
                if(!visible)continue;
                cores[bolt].positionCount=halos[bolt].positionCount=filled;
                float angle=bolt*Mathf.PI*2/Count;
                Vector3 radial=pose.right*Mathf.Cos(angle)*.28f+pose.up*Mathf.Sin(angle)*.68f;
                for(int j=0;j<filled;j++)
                {
                    float fade=1-j/(float)filled;
                    float n=Mathf.Sin((phase*13+bolt*47+j*17)*2.17f);
                    float n2=Mathf.Sin((phase*7+bolt*29+j*31)*1.41f);
                    Vector3 p=history[j]+radial+pose.right*n*.21f+pose.up*n2*.2f;
                    p=Vector3.Lerp(history[j],p,fade);
                    cores[bolt].SetPosition(j,p);halos[bolt].SetPosition(j,p);
                }
            }
        }
    }
}
