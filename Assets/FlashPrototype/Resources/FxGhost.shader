Shader "FlashGame/Ghost"
{
    // Speed-Force afterimages: a glowing silhouette that is strongest at grazing angles.
    Properties
    {
        _Color("Colour", Color) = (1,0.5,0.1,1)
        _Alpha("Strength", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Alpha;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; };
            Varyings vert(Attributes a)
            {
                Varyings v;
                v.world = TransformObjectToWorld(a.positionOS.xyz);
                v.positionCS = TransformWorldToHClip(v.world);
                v.normal = TransformObjectToWorldNormal(a.normalOS);
                return v;
            }
            half4 frag(Varyings v) : SV_Target
            {
                half rim = 1 - saturate(abs(dot(normalize(v.normal), normalize(GetCameraPositionWS() - v.world))));
                return half4(_Color.rgb * (.12 + 1.6 * rim * rim) * _Alpha, 0);
            }
            ENDHLSL
        }
    }
}
