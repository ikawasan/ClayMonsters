Shader "Custom/ClayMonster"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.94, 0.86, 0.74, 1)
        _VertexColorStrength ("Vertex Color Strength", Range(0,1)) = 1

        [Space(10)]
        [Header(Clay Surface)]
        _ClayNoiseScale ("Clay Noise Scale", Range(0.1, 24)) = 4.8
        _ClayNoiseStrength ("Clay Color Variation", Range(0, 0.35)) = 0.08
        _FingerprintScale ("Fingerprint Scale", Range(1, 48)) = 16
        _FingerprintStrength ("Fingerprint Strength", Range(0, 0.3)) = 0.1
        _NormalPerturb ("Surface Bump", Range(0, 0.6)) = 0.18
        _CavityStrength ("Cavity Darken", Range(0, 1)) = 0.42

        [Space(10)]
        [Header(Clay Lighting)]
        _ShadowColor ("Shadow Tint", Color) = (0.55, 0.48, 0.46, 1)
        _DiffuseSoftness ("Diffuse Softness", Range(0.2, 2)) = 0.82
        _ShadowDepth ("Shadow Depth", Range(0, 1)) = 0.38
        _Wrap ("Light Wrap", Range(0, 1)) = 0.62
        _AmbientIntensity ("Ambient Intensity", Range(0, 1)) = 0.48

        [Space(10)]
        [Header(Clay Highlight)]
        [HDR] _SpecularColor ("Specular Color", Color) = (0.42, 0.4, 0.38, 1)
        _SpecularStrength ("Specular Strength", Range(0, 1)) = 0.22
        _SpecularSmoothness ("Specular Softness", Range(0, 1)) = 0.72

        [Space(10)]
        [Header(Subsurface)]
        [HDR] _SubsurfaceColor ("Subsurface Tint", Color) = (1, 0.72, 0.55, 1)
        _SubsurfaceStrength ("Subsurface Strength", Range(0, 2)) = 0.9
        _SubsurfacePower ("Subsurface Power", Range(0.5, 8)) = 1.6

        [Space(10)]
        [Header(Rim Light)]
        [HDR] _RimColor ("Rim Color", Color) = (1.0, 0.92, 0.82, 1)
        _RimPower ("Rim Power", Range(0.1, 10)) = 3.2
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.48

        [Space(10)]
        [Header(Outline)]
        _OutlineDarkness ("Outline Darkness", Range(0, 1)) = 0.32
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.018
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ClayMonsterSurface.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float4 color       : COLOR;
            };

            Varyings OutlineVert(Attributes IN)
            {
                Varyings OUT;
                float3 normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                positionWS += normalWS * _OutlineWidth;
                OUT.positionWS = positionWS;
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.color = IN.color;
                return OUT;
            }

            half4 OutlineFrag(Varyings IN) : SV_Target
            {
                half3 albedo = ClayMonsterSampleAlbedo(IN.positionWS, float3(0, 1, 0), IN.color);
                return half4(ClayMonsterClampOutput(albedo * (1.0h - _OutlineDarkness)), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "ClayMonsterSurface.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 color       : COLOR;
                float fogCoord     : TEXCOORD2;
            };

            half3 ClayMonsterSoftAdditionalDiffuse(float3 positionWS, float3 normalWS, half3 albedo)
            {
                half3 additionalDiffuse = 0;
#if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < lightCount; lightIndex++)
                {
                    Light light = GetAdditionalLight(lightIndex, positionWS);
                    half halfLambert = saturate(dot(normalWS, light.direction) * 0.5h + 0.5h);
                    half wrapLight = saturate((halfLambert + _Wrap) / (1.0h + _Wrap));
                    half lightAmount = pow(wrapLight, _DiffuseSoftness);
                    additionalDiffuse += albedo * light.color * light.distanceAttenuation * lightAmount * 0.55h;
                }
#endif
                return additionalDiffuse;
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                OUT.fogCoord = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 normalWS = ClayMonsterPerturbNormal(IN.positionWS, normalize(IN.normalWS));
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                half3 albedo = ClayMonsterSampleAlbedo(IN.positionWS, normalWS, IN.color);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDirWS = normalize(mainLight.direction);
                half3 lightColor = mainLight.color;
                half shadowAttenuation = mainLight.shadowAttenuation;

                float ndotl = dot(normalWS, lightDirWS);
                half halfLambert = ndotl * 0.5h + 0.5h;
                half wrapLight = saturate((halfLambert + _Wrap) / (1.0h + _Wrap));
                half lightAmount = pow(wrapLight * shadowAttenuation, _DiffuseSoftness);

                half cavity = ClayMonsterSampleCavity(IN.positionWS, normalWS);
                half3 litColor = albedo * lightColor;
                half shadowMix = lerp(1.0h - _ShadowDepth, 1.0h, lightAmount);
                half3 shadowTint = albedo * _ShadowColor.rgb;
                half3 diffuseColor = lerp(shadowTint * shadowMix, litColor, lightAmount);
                diffuseColor *= lerp(1.0h, 0.68h, cavity * _CavityStrength);
                diffuseColor += ClayMonsterSoftAdditionalDiffuse(IN.positionWS, normalWS, albedo);

                half glossMask = ClayMonsterGlossMask(IN.color.a);

                float3 halfVector = normalize(lightDirWS + viewDirWS);
                float ndoth = max(0.0, dot(normalWS, halfVector));
                half specPower = lerp(48.0h, 6.0h, _SpecularSmoothness);
                half specIntensity = pow(ndoth, specPower) * _SpecularStrength;
                specIntensity *= glossMask;
                specIntensity *= 1.0h - cavity * 0.75h;
                half3 specularColor = specIntensity * saturate(_SpecularColor.rgb) * lightColor;

                float ndotv = max(0.0, dot(normalWS, viewDirWS));
                float rim = 1.0 - ndotv;
                half rimIntensity = pow(rim, _RimPower);
                rimIntensity *= glossMask;
                half3 rimTint = lerp(albedo, saturate(_RimColor.rgb), 0.55h);
                half3 rimLight = rimIntensity * rimTint * _RimStrength * 0.38h;

                half backScatter = pow(saturate(dot(viewDirWS, -lightDirWS) * 0.5h + 0.5h), _SubsurfacePower);
                half edgeScatter = pow(rim, _SubsurfacePower * 0.55h);
                half3 subsurfaceTint = albedo * saturate(_SubsurfaceColor.rgb);
                half3 subsurface = (backScatter * 0.42h + edgeScatter * 0.58h)
                    * subsurfaceTint
                    * _SubsurfaceStrength
                    * lightColor
                    * glossMask
                    * lerp(0.35h, 1.0h, lightAmount);

                half3 ambient = albedo * SampleSH(normalWS) * _AmbientIntensity;
                ambient *= lerp(1.0h, 0.72h, cavity * _CavityStrength);

                half3 finalColor = ClayMonsterClampOutput(
                    diffuseColor + specularColor + rimLight + subsurface + ambient);
                finalColor = MixFog(finalColor, IN.fogCoord);

                return half4(finalColor, 1.0);
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
            Cull Back

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
            Cull Back

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
            Cull Back

            HLSLPROGRAM
            #pragma vertex ClayDepthVertex
            #pragma fragment ClayDepthNormalsFragment
            #pragma multi_compile_instancing

            #include "../Common/OpaqueDepthPasses.hlsl"
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
