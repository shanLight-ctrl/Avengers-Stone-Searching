Shader "Cosmic/EarthStatus"
{
    // Procedural Earth. _Health 0 = burning, ash-covered world; _Health 1 = calm blue-and-green world.
    // Unlit and with no fog, so it stays visible from the ship even with fog turned on.
    Properties
    {
        _Health ("Health", Range(0, 1)) = 0
        _Spin ("Spin speed", Float) = 0.02
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Health;
                float _Spin;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 posOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.posOS = IN.positionOS.xyz;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(pos.positionWS);
                return OUT;
            }

            float CE_Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float CE_Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float a = lerp(CE_Hash(i + float3(0, 0, 0)), CE_Hash(i + float3(1, 0, 0)), f.x);
                float b = lerp(CE_Hash(i + float3(0, 1, 0)), CE_Hash(i + float3(1, 1, 0)), f.x);
                float c = lerp(CE_Hash(i + float3(0, 0, 1)), CE_Hash(i + float3(1, 0, 1)), f.x);
                float d = lerp(CE_Hash(i + float3(0, 1, 1)), CE_Hash(i + float3(1, 1, 1)), f.x);
                return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
            }

            float CE_Fbm(float3 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 5; i++)
                {
                    v += amp * CE_Noise(p);
                    p *= 2.0;
                    amp *= 0.5;
                }
                return v;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 p = normalize(IN.posOS);

                // Slow spin around the vertical axis.
                float ang = _Time.y * _Spin;
                float s = sin(ang);
                float c = cos(ang);
                p = float3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z);

                float land = CE_Fbm(p * 2.2);
                float isLand = step(0.5, land);
                float clouds = CE_Fbm(p * 4.0 + float3(0.0, _Time.y * 0.02, 0.0));

                // Healthy world: blue oceans, green land, white clouds.
                float3 healthy = lerp(float3(0.04, 0.18, 0.5), float3(0.12, 0.4, 0.15), isLand);
                healthy = lerp(healthy, float3(0.95, 0.97, 1.0), smoothstep(0.55, 0.8, clouds) * 0.8);

                // Dying world: dark scorched land and sea, glowing fire cracks, ash clouds.
                float3 dying = lerp(float3(0.1, 0.06, 0.07), float3(0.12, 0.07, 0.06), isLand);
                float cracks = smoothstep(0.62, 0.68, CE_Fbm(p * 7.0 + 3.1));
                float flicker = 0.6 + 0.4 * sin(_Time.y * 3.0 + CE_Fbm(p * 9.0) * 20.0);
                dying += float3(1.0, 0.35, 0.05) * cracks * flicker * 1.5;
                dying = lerp(dying, float3(0.25, 0.08, 0.06), smoothstep(0.5, 0.8, clouds) * 0.7);

                float3 col = lerp(dying, healthy, _Health);

                // Day side faces the viewer, with a soft terminator.
                float3 n = normalize(IN.normalWS);
                float ndl = saturate(dot(n, normalize(float3(-0.4, 0.4, -0.8))) * 0.8 + 0.3);
                col *= lerp(0.25, 1.0, ndl);

                // Atmosphere rim: pulsing red when dying, steady blue when healthy.
                float3 v = normalize(IN.viewDirWS);
                float rim = pow(1.0 - saturate(dot(n, v)), 3.0);
                float pulse = lerp(0.7 + 0.3 * sin(_Time.y * 2.0), 1.0, _Health);
                float3 rimCol = lerp(float3(1.0, 0.2, 0.1), float3(0.4, 0.75, 1.0), _Health);
                col += rimCol * rim * pulse * 1.2;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
