Shader "FlashGame/FXAdditive"
{
    // Glowing particles and rings (sparks, flames, wind, shockwaves). Soft round sprite from the UVs, so no
    // texture is needed; vertex colour times _Tint, scaled by _Intensity so bloom catches the bright ones.
    Properties
    {
        _Tint("Tint", Color) = (1,1,1,1)
        _Intensity("Intensity", Float) = 2
        _Softness("Edge softness", Float) = 1.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
                half4 _Tint;
                half _Intensity, _Softness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            Varyings vert(Attributes a)
            {
                Varyings v;
                v.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                v.color = a.color * _Tint;
                v.uv = a.uv;
                v.fog = ComputeFogFactor(v.positionCS.z);
                return v;
            }
            half4 frag(Varyings v) : SV_Target
            {
                float2 p = v.uv * 2 - 1;
                half falloff = pow(saturate(1 - dot(p, p)), _Softness);
                half3 color = v.color.rgb * v.color.a * falloff * _Intensity;
                return half4(MixFogColor(color, half3(0, 0, 0), v.fog), 0);
            }
            ENDHLSL
        }
    }
}
