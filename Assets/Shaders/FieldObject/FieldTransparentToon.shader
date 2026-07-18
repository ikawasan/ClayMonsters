Shader "Custom/FieldTransparentToon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (Albedo)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color (RGB) & Alpha (A)", Color) = (0.6, 0.8, 0.9, 0.5)
        
        [Space(10)]
        [Header(Toon Shading)]
        _ShadowColor("Shadow Color", Color) = (0.45, 0.58, 0.62, 0.5)
        _ToonThreshold("Toon Threshold", Range(-1.0, 1.0)) = 0.5
        _ToonSmoothness("Toon Smoothness", Range(0.0, 1.0)) = 0.12
        
        [Space(10)]
        [Header(Glass Highlight (Specular))]
        [HDR] _HighlightColor("Highlight Color", Color) = (1.0, 1.0, 1.0, 0.8)
        _HighlightThreshold("Highlight Threshold", Range(0.0, 1.0)) = 0.95
        _HighlightSmoothness("Highlight Smoothness", Range(0.0, 1.0)) = 0.02
        
        [Space(10)]
        [Header(Rim Light)]
        [HDR] _RimColor("Rim Light Color", Color) = (1.0, 1.0, 1.0, 0.0)
        _RimPower("Rim Power (Spread)", Range(0.1, 10.0)) = 3.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Transparent" 
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // 半透明用のブレンド設定と深度書き込みの無効化
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : NORMAL;
                float3 viewDirWS   : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float fogCoord     : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // SRP Batcher対応。Propertiesと完全に一致させる必要があります。
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                float _ToonThreshold;
                float _ToonSmoothness;
                half4 _HighlightColor;
                float _HighlightThreshold;
                float _HighlightSmoothness;
                half4 _RimColor;
                float _RimPower;
            CBUFFER_END

            half3 FieldTransparentSoftAdditionalDiffuse(float3 positionWS, float3 normalWS, half3 albedo)
            {
                half3 additionalDiffuse = 0;
#if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < lightCount; lightIndex++)
                {
                    Light light = GetAdditionalLight(lightIndex, positionWS);
                    half halfLambert = saturate(dot(normalWS, light.direction) * 0.5h + 0.5h);
                    half attenuation = light.distanceAttenuation * light.shadowAttenuation;
                    additionalDiffuse += albedo * light.color * attenuation * halfLambert * 0.22h;
                }
#endif
                return min(additionalDiffuse, albedo * 0.35h);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(OUT.positionWS);
                OUT.uv = IN.uv;
                OUT.fogCoord = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDirWS = normalize(mainLight.direction);
                half3 lightColor = mainLight.color;
                half shadowAttenuation = mainLight.shadowAttenuation;

                // ベースカラーとアルファ
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = texColor.rgb * _BaseColor.rgb;
                half baseAlpha = texColor.a * _BaseColor.a;

                half3 ambient = SampleSH(normalWS) * albedo;

                float NdotL = dot(normalWS, lightDirWS);
                float halfLambert = saturate(NdotL * 0.5 + 0.5);
                float toonStep = smoothstep(
                    _ToonThreshold - _ToonSmoothness,
                    _ToonThreshold + _ToonSmoothness,
                    halfLambert);

                half3 litColor = albedo * lightColor;
                half3 shadowTint = albedo * _ShadowColor.rgb * lightColor;
                half3 diffuseColor = lerp(shadowTint, litColor, toonStep);
                diffuseColor = lerp(shadowTint, diffuseColor, shadowAttenuation);
                diffuseColor += FieldTransparentSoftAdditionalDiffuse(IN.positionWS, normalWS, albedo);

                // ガラスのハイライト（スペキュラ）の計算
                // アニメ調のパキッとしたハイライトを作るため、ハーフベクトルを使用
                float3 halfVector = normalize(lightDirWS + viewDirWS);
                float NdotH = max(0, dot(normalWS, halfVector));
                float highlightIntensity = smoothstep(_HighlightThreshold - _HighlightSmoothness, _HighlightThreshold + _HighlightSmoothness, NdotH);
                
                half3 highlightColor = highlightIntensity * _HighlightColor.rgb * lightColor;

                // リムライトの計算
                float NdotV = max(0, dot(normalWS, viewDirWS));
                float rim = 1.0 - NdotV;
                float rimIntensity = smoothstep(0.5, 1.0, pow(rim, _RimPower)) * smoothstep(0.0, 0.1, toonStep);
                half3 rimLight = rimIntensity * _RimColor.rgb;

                // カラーの合成
                half3 finalColor = diffuseColor + highlightColor + rimLight + ambient;

                // アルファの合成（ハイライトが乗っている部分は不透明度を上げる）
                half finalAlpha = saturate(baseAlpha + (highlightIntensity * _HighlightColor.a) + (rimIntensity * _RimColor.a));

                // フォグの適用
                finalColor = MixFog(finalColor, IN.fogCoord);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}