Shader "FlashGame/FXAlpha"
{
    // Soft alpha-blended particles (smoke, gas, dust, water spray). A little procedural breakup keeps
    // overlapping puffs from reading as flat discs.
    Properties
    {
        _Tint("Tint", Color) = (1,1,1,1)
        _Softness("Edge softness", Float) = 1.4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
                half4 _Tint;
                half _Softness;
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
                half breakup = .75 + .25 * sin(p.x * 7.1 + sin(p.y * 5.3) * 2) * sin(p.y * 6.7 + 1.3);
                half alpha = saturate(falloff * breakup * v.color.a);
                return half4(MixFog(v.color.rgb, v.fog), alpha);
            }
            ENDHLSL
        }
    }
}
