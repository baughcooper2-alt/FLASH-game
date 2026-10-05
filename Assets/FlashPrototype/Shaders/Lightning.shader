Shader "FlashGame/Lightning"
{
 Properties { _BaseColor("Tint",Color)=(1,1,1,1) }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
 Pass {
 Blend SrcAlpha One
 ZWrite Off
 Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A {float4 positionOS:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
 struct V {float4 positionCS:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
 V vert(A a){V v;v.positionCS=TransformObjectToHClip(a.positionOS.xyz);v.color=a.color;v.uv=a.uv;return v;}
 half4 frag(V v):SV_Target {float softness=pow(saturate(1-abs(v.uv.y*2-1)),1.8);return half4(v.color.rgb,v.color.a*softness);}
 ENDHLSL
 }
 }
}
