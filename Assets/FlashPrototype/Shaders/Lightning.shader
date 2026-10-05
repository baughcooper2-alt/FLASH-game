Shader "FlashGame/Lightning"
{
    // Additive Speed Force lightning. uv: x along the bolt, y across (-1..1), z strength, w layer (0 glow, 1 core).
    // Vertex colour tints the bolt (white keeps the material colours).
    // Output is HDR so bloom turns the core into a glowing filament.
    Properties
    {
        _GlowColor("Glow", Color) = (1, 0.42, 0.06, 1)
        _CoreColor("Core", Color) = (1, 0.9, 0.62, 1)
        _Intensity("HDR intensity", Float) = 5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+50" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                half4 _CoreColor;
                half _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 uv : TEXCOORD0; half fog : TEXCOORD1; half4 color : COLOR; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half across = saturate(abs(input.uv.y));
                half strength = input.uv.z;
                half3 color;
                if (input.uv.w < .5)
                {
                    half halo = 1 - across;
                    color = _GlowColor.rgb * input.color.rgb * halo * halo * .5;
                }
                else
                {
                    // White-hot centre, tinted toward the edges.
                    half body = 1 - across * across;
                    color = lerp(_CoreColor.rgb * lerp(half3(1, 1, 1), input.color.rgb, .5), half3(1, 1, 1), pow(1 - across, 4)) * body * 2.2;
                }
                color *= _Intensity * strength * strength;
                return half4(MixFogColor(color, half3(0, 0, 0), input.fog), 1);
            }
            ENDHLSL
        }
    }
}
