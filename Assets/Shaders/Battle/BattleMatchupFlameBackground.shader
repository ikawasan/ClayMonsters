Shader "Custom/BattleMatchupFlameBackground"
{
    Properties
    {
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.65
        _PulseSpeed ("Pulse Speed", Range(0, 6)) = 1.2
        _FlameSide ("Flame Side", Float) = 0
        _LeftDeepColor ("Left Deep", Color) = (0.01, 0.02, 0.1, 1)
        _LeftMidColor ("Left Mid", Color) = (0.1, 0.32, 0.82, 1)
        _LeftHotColor ("Left Hot", Color) = (0.45, 0.75, 1, 1)
        _RightDeepColor ("Right Deep", Color) = (0.08, 0.01, 0.01, 1)
        _RightMidColor ("Right Mid", Color) = (0.82, 0.14, 0.03, 1)
        _RightHotColor ("Right Hot", Color) = (1, 0.68, 0.18, 1)
        _VoidColor ("Void", Color) = (0.01, 0.01, 0.02, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry-20"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "AuraBackground"
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _GlowIntensity;
                float _PulseSpeed;
                float _FlameSide;
                half4 _LeftDeepColor;
                half4 _LeftMidColor;
                half4 _LeftHotColor;
                half4 _RightDeepColor;
                half4 _RightMidColor;
                half4 _RightHotColor;
                half4 _VoidColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD1;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * Noise(p);
                    p *= 2.03;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.uv = input.uv;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                bool isLeft = _FlameSide < 0.0;
                float edgeT = isLeft ? (1.0 - uv.x) : uv.x;
                float edgeGlow = pow(saturate(edgeT), 1.35);
                float verticalMask = smoothstep(0.0, 0.08, uv.y) * smoothstep(1.0, 0.35, uv.y);
                float pulse = 0.82 + 0.18 * sin(_Time.y * _PulseSpeed + edgeT * 3.2);

                half3 deepColor = isLeft ? _LeftDeepColor.rgb : _RightDeepColor.rgb;
                half3 midColor = isLeft ? _LeftMidColor.rgb : _RightMidColor.rgb;
                half3 hotColor = isLeft ? _LeftHotColor.rgb : _RightHotColor.rgb;

                float2 flameUv = float2(uv.x * 4.2 + edgeT * 1.6, uv.y * 7.5 - _Time.y * (_PulseSpeed * 0.55));
                float flameNoise = Fbm(flameUv);
                float tongue = smoothstep(0.18, 0.82, flameNoise + edgeGlow * 0.35);
                float flameBody = edgeGlow * verticalMask * tongue;

                half3 color = lerp(_VoidColor.rgb, deepColor, flameBody * 0.72);
                color = lerp(color, midColor, flameBody * 0.78 * _GlowIntensity);
                color += hotColor * flameBody * 0.22 * _GlowIntensity * pulse;

                float spark = Hash21(uv * float2(8.0, 14.0) + float2(_Time.y * 0.7, -_Time.y * 1.1));
                color += hotColor * step(0.93, spark) * flameBody * 0.35;

                float outerEdge = isLeft ? uv.x : (1.0 - uv.x);
                color = lerp(_VoidColor.rgb, color, saturate(outerEdge * 2.2 + flameBody * 0.35));

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
