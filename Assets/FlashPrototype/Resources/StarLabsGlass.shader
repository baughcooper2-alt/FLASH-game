Shader "FlashGame/Glass"
{
    // Lives in Resources so player builds keep it; StarLabs loads it by name.
    Properties
    {
        _BaseColor("Tint and opacity", Color) = (0.55,0.75,0.9,0.18)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.world);
                output.normal = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float3 view = normalize(GetCameraPositionWS() - input.world);
                // Grazing angles reflect more, which keeps flat panes readable as glass.
                half fresnel = pow(1 - saturate(abs(dot(normalize(input.normal), view))), 3);
                half3 color = _BaseColor.rgb + fresnel * .35;
                return half4(MixFog(color, input.fog), saturate(_BaseColor.a + fresnel * .5));
            }
            ENDHLSL
        }
    }
}
