Shader "FlashGame/Prototype"
{
    Properties
    {
        _BaseColor("Color", Color) = (1,1,1,1)
        _Glow("Unlit amount", Range(0,1)) = 0
        _Surface("Surface pattern", Float) = 0
        _WindowStyle("Facade variation", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Glow;
                half _Surface;
                half _WindowStyle;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half shade : TEXCOORD0; half fog : TEXCOORD1; float3 world:TEXCOORD2; float3 normal:TEXCOORD3; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.world=TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.normal=normalWS;
                output.shade = 0.55h + 0.45h * saturate(dot(normalWS, normalize(float3(-0.4, 0.8, -0.25))));
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half3 color = _BaseColor.rgb * lerp(input.shade, 1.0h, _Glow);
                if (_Surface > .5 && _Surface < 1.5 && abs(input.normal.y)<.5)
                {
                    float2 grid=float2(abs(input.normal.x)>.5?input.world.z:input.world.x,input.world.y);
                    grid/=float2(2.8+_WindowStyle*.3,3.7);
                    float2 cell=frac(grid), aa=max(fwidth(grid),.003);
                    float2 edge=smoothstep(float2(.09,.13),float2(.09,.13)+aa,cell)*(1-smoothstep(float2(.91,.86)-aa,float2(.91,.86),cell));
                    float window=edge.x*edge.y;
                    float seed=frac(sin(dot(floor(grid),float2(12.9898,78.233)))*43758.5453);
                    float3 viewDir=normalize(GetCameraPositionWS()-input.world);
                    float fresnel=pow(1-saturate(abs(dot(normalize(input.normal),viewDir))),3);
                    float3 pane=lerp(float3(.09,.16,.2),float3(.48,.64,.71),saturate(input.world.y/240+fresnel*.6));
                    pane*=.8+seed*.3;
                    pane=lerp(pane,float3(.5,.43,.28),step(.91,seed)*.65);
                    color=lerp(color,pane*input.shade,window);
                }
                if(_Surface>1.5)
                {
                    float ripple=sin(input.world.x*.42+_Time.y*.7+sin(input.world.z*.14))*sin(input.world.z*.3-_Time.y*.4);
                    color+=ripple*.025;
                    float3 v=normalize(GetCameraPositionWS()-input.world);
                    color=lerp(color,float3(.46,.61,.68),pow(1-saturate(v.y),4)*.65);
                }
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.world));
                color*=lerp(.48,1,sun.shadowAttenuation*(.65+.35*saturate(dot(normalize(input.normal),sun.direction))));
                color=lerp(color,_BaseColor.rgb,_Glow);
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A {float4 vertex:POSITION;float3 normal:NORMAL;};
            float4 shadowVert(A a):SV_POSITION
            {
                float3 p=TransformObjectToWorld(a.vertex.xyz);
                float4 clip=TransformWorldToHClip(ApplyShadowBias(p,TransformObjectToWorldNormal(a.normal),_LightDirection));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return clip;
            }
            half4 shadowFrag():SV_Target{return 0;}
            ENDHLSL
        }
    }
}
