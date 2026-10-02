Shader "FlashGame/Prototype"
{
    Properties
    {
        _BaseColor("Color", Color) = (1,1,1,1)
        _Glow("Unlit amount", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Glow;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half shade : TEXCOORD0; half fog : TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shade = 0.55h + 0.45h * saturate(dot(normalWS, normalize(float3(-0.4, 0.8, -0.25))));
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half3 color = _BaseColor.rgb * lerp(input.shade, 1.0h, _Glow);
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
