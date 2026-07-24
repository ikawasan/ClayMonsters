Shader "Custom/ClayMonster"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Texture (Matte)", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (0.6, 0.5, 0.4, 1.0)
        _VertexColorStrength ("Vertex Color Strength", Range(0, 1)) = 1

        // 粘土の凹凸（スカルプト）設定
        _SculptNoiseTex ("Sculpt Noise (Greyscale)", 2D) = "gray" {}
        _SculptStrength ("Sculpt Strength", Range(0.0, 0.1)) = 0.02
        _SculptScale ("Sculpt Scale", Range(0.1, 10.0)) = 1.0

        // 階調（トーン）ライティング設定
        _LightSteps ("Lighting Steps (Clay Bands)", Range(1, 10)) = 3.0
        _AmbientColor ("Ambient Color", Color) = (0.2, 0.2, 0.2, 1.0)

        // 粘土のこね直しアニメーション（0で静止）
        _Animate ("Animate Sculpt (0-1)", Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SculptNoiseTex); SAMPLER(sampler_SculptNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _SculptNoiseTex_ST;
                float4 _BaseColor;
                float4 _AmbientColor;
                float _VertexColorStrength;
                float _SculptStrength;
                float _SculptScale;
                float _LightSteps;
                float _Animate;
            CBUFFER_END

            float3 ClayMonsterDisplaceOS(float3 positionOS, float3 normalOS, float2 uv)
            {
                float2 noiseUV = uv * _SculptScale + (_Time.y * _Animate * 0.1);
                float noise = SAMPLE_TEXTURE2D_LOD(_SculptNoiseTex, sampler_SculptNoiseTex, noiseUV, 0).r;
                float sculptOffset = (noise * 2.0 - 1.0) * _SculptStrength;
                return positionOS + normalOS * sculptOffset;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 displacedPositionOS = ClayMonsterDisplaceOS(input.positionOS.xyz, input.normalOS, input.uv);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(displacedPositionOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 tint = lerp(_BaseColor.rgb, saturate(input.color.rgb), _VertexColorStrength);
                half4 albedo = half4(baseMap.rgb * tint, baseMap.a * _BaseColor.a);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 normalWS = normalize(input.normalWS);

                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float steps = max(1.0, _LightSteps);
                float bandedNdotL = floor(NdotL * steps) / max(1.0, steps - 1.0);

                half3 lighting = mainLight.color * (bandedNdotL * mainLight.distanceAttenuation * mainLight.shadowAttenuation);
                half3 finalColor = albedo.rgb * (lighting + _AmbientColor.rgb);

                return half4(finalColor, albedo.a);
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
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            TEXTURE2D(_SculptNoiseTex); SAMPLER(sampler_SculptNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _SculptNoiseTex_ST;
                float4 _BaseColor;
                float4 _AmbientColor;
                float _VertexColorStrength;
                float _SculptStrength;
                float _SculptScale;
                float _LightSteps;
                float _Animate;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 ClayMonsterDisplaceOS(float3 positionOS, float3 normalOS, float2 uv)
            {
                float2 noiseUV = uv * _SculptScale + (_Time.y * _Animate * 0.1);
                float noise = SAMPLE_TEXTURE2D_LOD(_SculptNoiseTex, sampler_SculptNoiseTex, noiseUV, 0).r;
                float sculptOffset = (noise * 2.0 - 1.0) * _SculptStrength;
                return positionOS + normalOS * sculptOffset;
            }

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 displacedPositionOS = ClayMonsterDisplaceOS(input.positionOS.xyz, input.normalOS, input.uv);
                float3 positionWS = TransformObjectToWorld(displacedPositionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
                float3 lightDirectionWS = _LightDirection;
#endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
                return positionCS;
            }

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_TARGET
            {
                return 0;
            }
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
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_SculptNoiseTex); SAMPLER(sampler_SculptNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _SculptNoiseTex_ST;
                float4 _BaseColor;
                float4 _AmbientColor;
                float _VertexColorStrength;
                float _SculptStrength;
                float _SculptScale;
                float _LightSteps;
                float _Animate;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float2 noiseUV = input.uv * _SculptScale + (_Time.y * _Animate * 0.1);
                float noise = SAMPLE_TEXTURE2D_LOD(_SculptNoiseTex, sampler_SculptNoiseTex, noiseUV, 0).r;
                float sculptOffset = (noise * 2.0 - 1.0) * _SculptStrength;
                float3 displacedPositionOS = input.positionOS.xyz + input.normalOS * sculptOffset;

                output.positionCS = TransformObjectToHClip(displacedPositionOS);
                return output;
            }

            half DepthFrag(Varyings input) : SV_TARGET
            {
                return input.positionCS.z;
            }
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
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_SculptNoiseTex); SAMPLER(sampler_SculptNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _SculptNoiseTex_ST;
                float4 _BaseColor;
                float4 _AmbientColor;
                float _VertexColorStrength;
                float _SculptStrength;
                float _SculptScale;
                float _LightSteps;
                float _Animate;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float2 noiseUV = input.uv * _SculptScale + (_Time.y * _Animate * 0.1);
                float noise = SAMPLE_TEXTURE2D_LOD(_SculptNoiseTex, sampler_SculptNoiseTex, noiseUV, 0).r;
                float sculptOffset = (noise * 2.0 - 1.0) * _SculptStrength;
                float3 displacedPositionOS = input.positionOS.xyz + input.normalOS * sculptOffset;

                output.positionCS = TransformObjectToHClip(displacedPositionOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_TARGET
            {
                return half4(normalize(input.normalWS), 0.0h);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
