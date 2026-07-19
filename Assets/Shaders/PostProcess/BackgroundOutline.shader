Shader "Hidden/ClayMonsters/BackgroundOutline"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "BackgroundOutline"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            TEXTURE2D(_FieldNormalTexture);
            SAMPLER(sampler_FieldNormalTexture);
            TEXTURE2D(_TransparentMaskTexture);
            SAMPLER(sampler_TransparentMaskTexture);
            TEXTURE2D_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            float4 _BlitTexture_TexelSize;
            float _OutlineIntensity;
            float _OutlineThickness;
            float _NormalSensitivity;
            float4 _OutlineColor;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
                OUT.uv = GetFullScreenTriangleTexCoord(IN.vertexID);
                return OUT;
            }

            float4 SampleField(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_FieldNormalTexture, sampler_FieldNormalTexture, uv);
            }

            float SampleTransparentMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_TransparentMaskTexture, sampler_TransparentMaskTexture, uv).r;
            }

            float3 DecodeNormal(float3 packedNormal)
            {
                return normalize(packedNormal * 2.0 - 1.0);
            }

            float SampleSceneRawDepth(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
            }

            float2 ResolveTexelSize()
            {
                float2 texel = abs(_BlitTexture_TexelSize.xy);
                if (texel.x <= 1e-8 || texel.y <= 1e-8)
                {
                    texel = float2(1.0 / max(_ScreenParams.x, 1.0), 1.0 / max(_ScreenParams.y, 1.0));
                }

                return texel * max(_OutlineThickness, 0.01);
            }

            float NormalEdge(float3 a, float3 b)
            {
                return saturate(1.0 - saturate(dot(a, b)));
            }

            float IsFieldVisibleByDepth(float2 uv)
            {
                float4 field = SampleField(uv);
                float hasField = step(0.01, length(field.rgb));

                float fieldRaw = field.a;
                float sceneRaw = SampleSceneRawDepth(uv);
                float fieldLin = Linear01Depth(fieldRaw, _ZBufferParams);
                float sceneLin = Linear01Depth(sceneRaw, _ZBufferParams);

                float depthDelta = abs(fieldLin - sceneLin);
                float depthMatch = 1.0 - saturate(depthDelta * 80.0);

#if UNITY_REVERSED_Z
                float sceneCloser = step(fieldRaw + 1e-4, sceneRaw);
#else
                float sceneCloser = step(sceneRaw + 1e-4, fieldRaw);
#endif
                return hasField * depthMatch * (1.0 - sceneCloser);
            }

            float ComputeEdge(float2 uv, float2 texel)
            {
                float4 c = SampleField(uv);
                if (length(c.rgb) < 1e-4)
                {
                    return 0.0;
                }

                float3 n = DecodeNormal(c.rgb);
                float3 nl = DecodeNormal(SampleField(uv + float2(-texel.x, 0.0)).rgb);
                float3 nr = DecodeNormal(SampleField(uv + float2(texel.x, 0.0)).rgb);
                float3 nu = DecodeNormal(SampleField(uv + float2(0.0, texel.y)).rgb);
                float3 nd = DecodeNormal(SampleField(uv + float2(0.0, -texel.y)).rgb);
                float3 nl2 = DecodeNormal(SampleField(uv + float2(-texel.x * 2.0, 0.0)).rgb);
                float3 nr2 = DecodeNormal(SampleField(uv + float2(texel.x * 2.0, 0.0)).rgb);
                float3 nu2 = DecodeNormal(SampleField(uv + float2(0.0, texel.y * 2.0)).rgb);
                float3 nd2 = DecodeNormal(SampleField(uv + float2(0.0, -texel.y * 2.0)).rgb);

                float edge = NormalEdge(nl, nr) + NormalEdge(nu, nd);
                edge += (NormalEdge(nl2, nr2) + NormalEdge(nu2, nd2)) * 0.5;
                edge += NormalEdge(n, nl) + NormalEdge(n, nr) + NormalEdge(n, nu) + NormalEdge(n, nd);
                return saturate(edge * _NormalSensitivity);
            }

            float ComputeDilatedVisibility(float2 uv, float2 texel)
            {
                float visibility = IsFieldVisibleByDepth(uv);
                visibility = min(visibility, IsFieldVisibleByDepth(uv + float2(texel.x, 0.0)));
                visibility = min(visibility, IsFieldVisibleByDepth(uv + float2(-texel.x, 0.0)));
                visibility = min(visibility, IsFieldVisibleByDepth(uv + float2(0.0, texel.y)));
                visibility = min(visibility, IsFieldVisibleByDepth(uv + float2(0.0, -texel.y)));
                visibility = min(visibility, IsFieldVisibleByDepth(uv + float2(texel.x, texel.y)));
                visibility = min(visibility, IsFieldVisibleByDepth(uv + float2(-texel.x, texel.y)));
                visibility = min(visibility, IsFieldVisibleByDepth(uv + float2(texel.x, -texel.y)));
                visibility = min(visibility, IsFieldVisibleByDepth(uv + float2(-texel.x, -texel.y)));
                return visibility;
            }

            // 半透明が覆う画素では輪郭を出さない
            float ComputeDilatedTransparentBlock(float2 uv, float2 texel)
            {
                float mask = SampleTransparentMask(uv);
                mask = max(mask, SampleTransparentMask(uv + float2(texel.x, 0.0)));
                mask = max(mask, SampleTransparentMask(uv + float2(-texel.x, 0.0)));
                mask = max(mask, SampleTransparentMask(uv + float2(0.0, texel.y)));
                mask = max(mask, SampleTransparentMask(uv + float2(0.0, -texel.y)));
                return saturate(mask);
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

                float2 texel = ResolveTexelSize();
                float edge = ComputeEdge(uv, texel);
                float fieldVisible = ComputeDilatedVisibility(uv, texel);
                float transparentBlock = ComputeDilatedTransparentBlock(uv, texel);
                float outline = saturate(edge * fieldVisible * (1.0 - transparentBlock) * _OutlineIntensity);
                color.rgb = lerp(color.rgb, _OutlineColor.rgb, outline);
                return color;
            }
            ENDHLSL
        }
    }
}
