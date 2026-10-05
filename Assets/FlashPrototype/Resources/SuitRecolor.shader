Shader "Hidden/FlashGame/SuitRecolor"
{
    // Repaints a suit texture for a skin: suit-red pixels take _Primary, gold pixels _Accent and
    // white pixels _Light, keeping the original shading. _Cover paints over everything (full masks).
    // Lives in Resources so player builds keep it; SuitPainter blits textures through it once per skin.
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
        _Primary("Suit colour", Color) = (1,0,0,1)
        _Accent("Trim colour", Color) = (1,0.8,0,1)
        _Light("Light colour", Color) = (1,1,1,1)
        _Cover("Cover colour (alpha = amount)", Color) = (0,0,0,0)
        _Classes("Recolour red, gold, white", Vector) = (1,1,0,0)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Primary, _Accent, _Light, _Cover, _Classes;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
            float4 frag(v2f i) : SV_Target
            {
                float4 src = tex2D(_MainTex, i.uv);
                float3 s = LinearToGammaSpace(src.rgb);
                float hi = max(s.r, max(s.g, s.b)), lo = min(s.r, min(s.g, s.b));
                float sat = (hi - lo) / max(hi, 1e-4);
                // Hue in degrees, 0 = red.
                float hue = 0;
                if (hi - lo > 1e-4)
                {
                    if (hi == s.r) hue = 60 * fmod((s.g - s.b) / (hi - lo) + 6, 6);
                    else if (hi == s.g) hue = 60 * ((s.b - s.r) / (hi - lo) + 2);
                    else hue = 60 * ((s.r - s.g) / (hi - lo) + 4);
                }
                float fromRed = min(hue, 360 - hue);
                float red = smoothstep(.28, .42, sat) * (1 - smoothstep(13, 21, fromRed)) * _Classes.x;
                float gold = smoothstep(.18, .3, sat) * smoothstep(16, 22, hue) * (1 - smoothstep(56, 66, hue)) * smoothstep(.42, .58, hi) * _Classes.y;
                float white = (1 - smoothstep(.12, .24, sat)) * smoothstep(.62, .78, hi) * _Classes.z;
                // Shade relative to each class's reference brightness (suit red ~0.47, gold and white ~1).
                float3 lin = src.rgb;
                float value = max(lin.r, max(lin.g, lin.b));
                float3 color = lin * saturate(1 - red - gold - white);
                color += _Primary.rgb * clamp(value / .195, .25, 2.2) * red;
                color += _Accent.rgb * clamp(value, .2, 1.2) * gold;
                color += _Light.rgb * clamp(value, .2, 1.2) * white;
                float luma = dot(lin, float3(.2126, .7152, .0722));
                color = lerp(color, _Cover.rgb * (.55 + 1.2 * luma), _Cover.a);
                return float4(color, src.a);
            }
            ENDCG
        }
    }
}
