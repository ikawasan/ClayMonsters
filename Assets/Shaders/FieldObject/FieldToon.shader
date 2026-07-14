Shader "Custom/FieldToon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (Albedo)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        
        [Space(10)]
        [Header(Toon Shading)]
        _ShadowColor("Shadow Color (Warm Tone)", Color) = (0.75, 0.6, 0.5, 1)
        _ToonThreshold("Toon Threshold", Range(-1.0, 1.0)) = 0.5
        _ToonSmoothness("Toon Smoothness", Range(0.0, 1.0)) = 0.1
        
        [Space(10)]
        [Header(Metal Highlight)]
        // デフォルトを黒にして、既存のマテリアルの見た目が変わらないように設定
        [HDR] _SpecularColor("Specular Color", Color) = (0.0, 0.0, 0.0, 0.0)
        _SpecularThreshold("Specular Threshold", Range(0.0, 1.0)) = 0.95
        _SpecularSmoothness("Specular Smoothness", Range(0.0, 1.0)) = 0.01

        [Space(10)]
        [Header(Rim Light (Nostalgic Sun))]
        [HDR]         _RimColor("Rim Light Color", Color) = (1.0, 0.85, 0.5, 1)
        _RimPower("Rim Power (Spread)", Range(0.1, 10.0)) = 3.0
    }
    
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

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
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

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
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

                // 金属のハイライト計算
                float3 halfVector = normalize(lightDirWS + viewDirWS);
                float NdotH = max(0, dot(normalWS, halfVector));
                
                float specIntensity = smoothstep(_SpecularThreshold - _SpecularSmoothness, _SpecularThreshold + _SpecularSmoothness, NdotH);
                specIntensity *= smoothstep(0.0, 0.1, toonStep); // 影の部分にはハイライトを乗せない
                
                half3 specularColor = specIntensity * _SpecularColor.rgb * lightColor;

                // リムライト計算
                float NdotV = max(0, dot(normalWS, viewDirWS));
                float rim = 1.0 - NdotV;
                float rimIntensity = smoothstep(0.5, 1.0, pow(rim, _RimPower)) * smoothstep(0.0, 0.1, toonStep);
                half3 rimLight = rimIntensity * _RimColor.rgb;

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

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
}