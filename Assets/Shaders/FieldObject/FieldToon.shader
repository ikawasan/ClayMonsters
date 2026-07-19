Shader "Custom/FieldToon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (Albedo)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        
        [Space(10)]
        [Header(Toon Shading)]
        _ShadowColor("Shadow Color (Warm Tone)", Color) = (0.72, 0.56, 0.46, 1)
        _ToonThreshold("Toon Threshold", Range(-1.0, 1.0)) = 0.5
        _ToonSmoothness("Toon Smoothness", Range(0.0, 1.0)) = 0.18
        
        [Space(10)]
        [Header(Metal Highlight)]
        // デフォルトを黒にして既存マテリアルの見た目を変えない
        [HDR] _SpecularColor("Specular Color", Color) = (0.0, 0.0, 0.0, 0.0)
        _SpecularThreshold("Specular Threshold", Range(0.0, 1.0)) = 0.95
        _SpecularSmoothness("Specular Smoothness", Range(0.0, 1.0)) = 0.01

        [Space(10)]
        [Header(Rim Light (Nostalgic Sun))]
        [HDR] _RimColor("Rim Light Color", Color) = (1.0, 0.85, 0.5, 1)
        _RimPower("Rim Power (Spread)", Range(0.1, 10.0)) = 3.0

        [Space(10)]
        [Header(Rendering)]
        [Toggle(_SHOW_BACK_FACES_ONLY)] _ShowBackFacesOnly("Show Back Faces Only", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
    }
    
    CustomEditor "FieldToonShaderGUI"
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry" 
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ _SHOW_BACK_FACES_ONLY

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
                float2 uv          : TEXCOORD1;
                float fogCoord     : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                float _ToonThreshold;
                float _ToonSmoothness;
                half4 _SpecularColor;
                float _SpecularThreshold;
                float _SpecularSmoothness;
                half4 _RimColor;
                float _RimPower;
            CBUFFER_END

            half3 FieldToonSoftAdditionalDiffuse(float3 positionWS, float3 normalWS, half3 albedo)
            {
                half3 additionalDiffuse = 0;
#if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < lightCount; lightIndex++)
                {
                    Light light = GetAdditionalLight(lightIndex, positionWS);
                    // 追加ライトはトゥーン帯を使わず柔らかい半ランバートのみにする
                    // ポイントライトのトゥーン帯は金属テカリに見えやすい
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
#if defined(_SHOW_BACK_FACES_ONLY)
                OUT.normalWS = -OUT.normalWS;
#endif
                OUT.uv = IN.uv;
                OUT.fogCoord = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDirWS = normalize(mainLight.direction);
                half3 lightColor = mainLight.color;
                half shadowAttenuation = mainLight.shadowAttenuation;

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = texColor.rgb * _BaseColor.rgb;

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
                diffuseColor += FieldToonSoftAdditionalDiffuse(IN.positionWS, normalWS, albedo);

                // 金属ハイライトはSpecularColorが実質有効なときだけ適用する
                half3 specularColor = 0;
                half specularStrength = max(_SpecularColor.r, max(_SpecularColor.g, _SpecularColor.b));
                if (specularStrength > 0.001h)
                {
                    float3 halfVector = normalize(lightDirWS + viewDirWS);
                    float NdotH = max(0, dot(normalWS, halfVector));
                    float specIntensity = smoothstep(
                        _SpecularThreshold - _SpecularSmoothness,
                        _SpecularThreshold + _SpecularSmoothness,
                        NdotH);
                    specIntensity *= smoothstep(0.0, 0.1, toonStep);
                    specularColor = specIntensity * _SpecularColor.rgb * lightColor * shadowAttenuation;
                }

                // リムはアルベドに乗せる柔らかい輪郭光にしてテカリを抑える
                float NdotV = max(0, dot(normalWS, viewDirWS));
                float rim = 1.0 - NdotV;
                float rimIntensity = pow(saturate(rim), _RimPower + 1.5);
                rimIntensity *= smoothstep(0.15, 0.55, toonStep);
                rimIntensity *= shadowAttenuation;
                half3 rimLight = rimIntensity * albedo * saturate(_RimColor.rgb) * 0.18h;

                half3 finalColor = diffuseColor + specularColor + rimLight + ambient;

                finalColor = MixFog(finalColor, IN.fogCoord);

                return half4(finalColor, texColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ZTest LEqual
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ClayDepthVertex
            #pragma fragment ClayDepthOnlyFragment
            #pragma multi_compile_instancing

            #include "../Common/OpaqueDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ClayDepthVertex
            #pragma fragment ClayDepthNormalsFragment
            #pragma multi_compile_instancing

            #include "../Common/OpaqueDepthPasses.hlsl"
            ENDHLSL
        }
    }
}
