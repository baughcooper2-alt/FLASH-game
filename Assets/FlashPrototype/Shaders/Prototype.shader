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
            // S.T.A.R. Labs interior lighting, set globally by StarLabs.cs. A zero radius disables it.
            float4 _LabZone;            // x,z centre; y interior ceiling; w radius
            float4 _LabAmbient;
            float4 _LabRing;            // accelerator lamps: x radius, y height, z spacing (radians), w range
            float4 _LabRingColor;       // rgb; w inner radius where the ring lamps start
            float4 _LabLights[48];      // xyz position, w range
            float4 _LabLightColors[48];
            float _LabLightCount;
            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
            // Planar coordinates for the face's dominant axis, so patterns work on any box side.
            float2 FaceUV(float3 p, float3 n)
            {
                float3 a = abs(n);
                return a.y > max(a.x, a.z) ? p.xz : (a.x > a.z ? p.zy : p.xy);
            }
            float GridLine(float2 uv, float size, float width)
            {
                float2 d = abs(frac(uv / size + .5) - .5) * size;
                float2 l = 1 - smoothstep(width, width + max(fwidth(uv), 1e-4), d);
                return max(l.x, l.y);
            }
            // Distance from the centre of the nearest hexagon: 0 at the centre, 0.5 on its edge.
            float HexEdge(float2 p)
            {
                const float2 r = float2(1, 1.7320508);
                float2 a = p - r * floor(p / r) - r * .5;
                float2 b = p - r * .5 - r * floor((p - r * .5) / r) - r * .5;
                float2 q = abs(dot(a, a) < dot(b, b) ? a : b);
                return max(dot(q, normalize(r)), q.x);
            }
            float3 LabLight(float3 world, float3 n, float3 position, float range, float3 tint)
            {
                float3 l = position - world;
                float d2 = dot(l, l), r2 = range * range;
                if (d2 >= r2) return 0;
                float falloff = 1 - d2 / r2;
                return tint * falloff * falloff * (saturate(dot(n, l * rsqrt(max(d2, 1e-4)))) * .9 + .1);
            }
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
            half4 frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                half3 color = _BaseColor.rgb * lerp(input.shade, 1.0h, _Glow);
                float3 n = normalize(input.normal);
                float2 uv = FaceUV(input.world, n);
                // Pattern brightness is tracked separately so glowing surfaces keep their pattern.
                half pattern = 1, emissive = 0;
                if (abs(_Surface - 3) < .5) // Hazard stripes
                {
                    float s = dot(input.world, float3(1, 1, 1)) * 1.6, aa = max(fwidth(s), 1e-4) * 2;
                    pattern *= lerp(.07, 1, smoothstep(.5 - aa, .5 + aa, abs(frac(s) - .5) * 2));
                }
                if (abs(_Surface - 4) < .5 || (abs(_Surface - 5) < .5 && n.y < .5) || (abs(_Surface - 7) < .5 && n.y > .5))
                {
                    // Panel seams; _WindowStyle sets the panel size in metres.
                    float size = _WindowStyle > 0 ? _WindowStyle : 2;
                    pattern *= (1 - .5 * GridLine(uv, size, .02)) * (.93 + .14 * Hash(floor(uv / size)));
                }
                if (abs(_Surface - 5) < .5 && n.y >= .5) // Open hex grating
                {
                    float e = HexEdge(input.world.xz / .32), aa = fwidth(e);
                    pattern *= lerp(.16, 1, smoothstep(.33, .37 + aa, e));
                }
                if (abs(_Surface - 6) < .5) // Hex floor tiles
                {
                    float2 p = uv / (_WindowStyle > 0 ? _WindowStyle : 2.2);
                    float e = HexEdge(p);
                    pattern *= (1 - .45 * smoothstep(.465, .49, e)) * (.92 + .16 * Hash(floor(p * 1.7)));
                }
                if (abs(_Surface - 7) < .5 && n.y <= .5) // Studded panels, lit from above
                {
                    float2 g = uv / .28, f = frac(g) - .5;
                    float stud = step(.42, Hash(floor(g))) * smoothstep(.3, .2, length(f));
                    pattern *= (1 + stud * clamp(f.y * 3, -.6, .9)) * (1 - .55 * GridLine(uv, 1.4, .015));
                }
                color *= pattern;
                if (abs(_Surface - 8) < .5) // Glowing grid lines
                    emissive = GridLine(uv, _WindowStyle > 0 ? _WindowStyle : 1.2, .03);
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
                if(_Surface>1.5 && _Surface<2.5)
                {
                    float ripple=sin(input.world.x*.42+_Time.y*.7+sin(input.world.z*.14))*sin(input.world.z*.3-_Time.y*.4);
                    color+=ripple*.025;
                    float3 v=normalize(GetCameraPositionWS()-input.world);
                    color=lerp(color,float3(.46,.61,.68),pow(1-saturate(v.y),4)*.65);
                }
                float2 fromLab = input.world.xz - _LabZone.xz;
                float labRadius = length(fromLab);
                if (labRadius < _LabZone.w && input.world.y < _LabZone.y)
                {
                    // Inside the lab the roof would shadow everything, so local lamps replace the sun.
                    float3 ln = front ? n : -n;
                    float3 light = _LabAmbient.rgb * (.8 + .2 * ln.y);
                    [loop] for (int i = 0; i < 48; i++)
                    {
                        if (i >= (int)_LabLightCount) break;
                        light += LabLight(input.world, ln, _LabLights[i].xyz, _LabLights[i].w, _LabLightColors[i].rgb);
                    }
                    if (labRadius > _LabRingColor.w)
                    {
                        float k = round(atan2(fromLab.x, fromLab.y) / _LabRing.z);
                        [unroll] for (int j = -1; j <= 1; j++)
                        {
                            float a = (k + j) * _LabRing.z;
                            float3 lamp = float3(_LabZone.x + sin(a) * _LabRing.x, _LabRing.y, _LabZone.z + cos(a) * _LabRing.x);
                            light += LabLight(input.world, ln, lamp, _LabRing.w, _LabRingColor.rgb);
                        }
                    }
                    color *= light;
                }
                else
                {
                    Light sun=GetMainLight(TransformWorldToShadowCoord(input.world));
                    color*=lerp(.48,1,sun.shadowAttenuation*(.65+.35*saturate(dot(normalize(input.normal),sun.direction))));
                }
                color=lerp(color,_BaseColor.rgb*pattern,_Glow);
                color=lerp(color,half3(.9,.95,1),emissive);
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
