Shader "Custom/BattleVsUiFlameBackdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 1.0
        _PulseSpeed ("Pulse Speed", Range(0, 6)) = 1.5
        _ForceSide ("Force Side", Float) = 0
        _ClashReach ("Clash Reach", Range(0.4, 0.75)) = 0.62
        _ClashWidth ("Clash Width", Range(0.05, 0.35)) = 0.16
        _StripInnerFade ("Strip Inner Fade", Range(0, 2)) = 1.15
        _LeftDeepColor ("Left Deep", Color) = (0.02, 0.04, 0.18, 1)
        _LeftMidColor ("Left Mid", Color) = (0.08, 0.42, 0.95, 1)
        _LeftHotColor ("Left Hot", Color) = (0.55, 0.88, 1, 1)
        _RightDeepColor ("Right Deep", Color) = (0.18, 0.02, 0.01, 1)
        _RightMidColor ("Right Mid", Color) = (0.95, 0.28, 0.04, 1)
        _RightHotColor ("Right Hot", Color) = (1, 0.78, 0.18, 1)
        _VoidColor ("Void", Color) = (0.01, 0.01, 0.02, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _GlowIntensity;
                float _PulseSpeed;
                float _ForceSide;
                float _ClashReach;
                float _ClashWidth;
                float _StripInnerFade;
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
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float SmoothNoise(float2 p)
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

            float SoftFbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.55;
                for (int i = 0; i < 3; i++)
                {
                    value += amplitude * SmoothNoise(p);
                    p = p * 2.08 + float2(1.7, 2.3);
                    amplitude *= 0.5;
                }
                return value;
            }

            float WarpedFireNoise(float2 p)
            {
                float2 q = float2(SoftFbm(p), SoftFbm(p + float2(4.1, 2.7)));
                return SoftFbm(p + 2.2 * q);
            }

            float FireHeightMask(float uvY)
            {
                float bottom = smoothstep(0.0, 0.05, uvY);
                float top = smoothstep(1.0, 0.18, uvY);
                return bottom * top;
            }

            half3 FireColorRamp(float intensity, half3 deep, half3 mid, half3 hot)
            {
                float t = saturate(intensity);
                half3 color = lerp(deep, mid, smoothstep(0.0, 0.55, t));
                color = lerp(color, hot, smoothstep(0.25, 0.82, t));
                color = lerp(color, half3(1.0, 0.97, 0.82), smoothstep(0.62, 1.0, t));
                return color;
            }

            float ComputeSideFire(float2 uv, float reach, bool fromLeft, float scrollSpeed)
            {
                float dist = fromLeft ? uv.x : (1.0 - uv.x);
                float spread = pow(saturate(1.0 - dist / reach), 0.42);
                float widthLimit = lerp(reach * 1.05, reach * 0.18, pow(uv.y, 0.75));
                spread *= smoothstep(widthLimit, widthLimit * 0.25, dist);

                float time = _Time.y * scrollSpeed;
                float rise = uv.y - time * 1.35;
                float wobble = sin(rise * 7.0 + dist * 11.0 + time) * 0.055 * (0.35 + uv.y);
                float2 p = float2((dist + wobble) * 5.5, rise * 6.5);

                float n1 = WarpedFireNoise(p);
                float n2 = WarpedFireNoise(p * 1.35 + float2(0.8, 1.4) - float2(0.0, time * 0.45));
                float n3 = WarpedFireNoise(p * 2.1 + float2(2.3, 0.6) - float2(0.0, time * 0.75));
                float noise = n1 * 0.5 + n2 * 0.32 + n3 * 0.18;

                float tongue = sin(rise * 10.0 + dist * 14.0 + time * 1.6) * 0.5 + 0.5;
                float shape = smoothstep(0.34, 0.7, noise + tongue * 0.18);
                shape *= lerp(0.55, 1.0, smoothstep(0.2, 0.75, tongue));

                float height = FireHeightMask(uv.y);
                float bottomFuel = pow(smoothstep(0.0, 0.28, uv.y), 0.55);
                return shape * spread * height * lerp(0.7, 1.0, bottomFuel);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half4 SampleClashFlame(float2 uv)
            {
                float scroll = _PulseSpeed * 0.62;
                float pulse = 0.92 + 0.08 * sin(_Time.y * _PulseSpeed);
                float reach = _ClashReach;

                float leftFire = ComputeSideFire(uv, reach, true, scroll);
                float rightFire = ComputeSideFire(uv, reach, false, scroll * 1.03);

                float clashDist = abs(uv.x - 0.5);
                float clashCore = pow(saturate(1.0 - clashDist / _ClashWidth), 2.6);
                float clashRise = FireHeightMask(uv.y);
                float clashTime = _Time.y * scroll * 1.2;
                float clashNoise = WarpedFireNoise(float2(clashDist * 12.0, uv.y * 5.0 - clashTime * 1.5));
                float clashFire = clashCore * clashRise * smoothstep(0.3, 0.72, clashNoise + 0.2) * pulse;

                float leftIntensity = leftFire * _GlowIntensity;
                float rightIntensity = rightFire * _GlowIntensity;
                float clashIntensity = clashFire * _GlowIntensity * 1.15;

                half3 color = _VoidColor.rgb;
                color = lerp(color, FireColorRamp(leftIntensity, _LeftDeepColor.rgb, _LeftMidColor.rgb, _LeftHotColor.rgb), saturate(leftIntensity * 1.15));
                color = lerp(color, FireColorRamp(rightIntensity, _RightDeepColor.rgb, _RightMidColor.rgb, _RightHotColor.rgb), saturate(rightIntensity * 1.15));

                half3 clashColor = (_LeftHotColor.rgb + _RightHotColor.rgb) * 0.5;
                clashColor = lerp(clashColor, half3(1.0, 0.95, 0.72), 0.55);
                color = lerp(color, clashColor, saturate(clashIntensity * 1.2));

                float alpha = saturate(leftIntensity + rightIntensity + clashIntensity * 1.1);
                return half4(color, alpha);
            }

            half4 SampleStripFlame(float2 uv, bool isLeft)
            {
                float edgeT = isLeft ? (1.0 - uv.x) : uv.x;
                float inner = isLeft ? uv.x : (1.0 - uv.x);
                float innerFade = saturate(1.0 - inner * _StripInnerFade);
                float fire = ComputeSideFire(uv, 0.58, isLeft, _PulseSpeed * 0.55) * innerFade * pow(saturate(edgeT), 0.9);
                float intensity = fire * _GlowIntensity;

                half3 deep = isLeft ? _LeftDeepColor.rgb : _RightDeepColor.rgb;
                half3 mid = isLeft ? _LeftMidColor.rgb : _RightMidColor.rgb;
                half3 hot = isLeft ? _LeftHotColor.rgb : _RightHotColor.rgb;
                half3 color = lerp(_VoidColor.rgb, FireColorRamp(intensity, deep, mid, hot), saturate(intensity * 1.15));
                return half4(color, saturate(intensity));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half4 flameColor = abs(_ForceSide) < 0.5
                    ? SampleClashFlame(uv)
                    : SampleStripFlame(uv, _ForceSide < 0.0);

                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return flameColor * mainTex * input.color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
