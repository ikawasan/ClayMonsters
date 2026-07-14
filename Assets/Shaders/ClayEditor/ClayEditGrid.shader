Shader "ClayEditor/ClayEditGrid"
{
    Properties
    {
        [HDR] _GridColor ("Grid Color", Color) = (0.1, 0.85, 1.0, 1.0)
        _FaceColor ("Face Tint", Color) = (0.02, 0.06, 0.18, 0.12)
        _Divisions ("Grid Divisions", Float) = 10
        _LineWidth ("Line Width", Range(0.001, 0.08)) = 0.018
        _DotSize ("Vertex Dot Size", Range(0.001, 0.08)) = 0.028
        [Toggle] _HideCameraFacingFace ("Hide Camera Facing Face", Float) = 1
        _ArrowFillColor ("Front Arrow Fill", Color) = (0.82, 0.92, 1.0, 0.32)
        [HDR] _ArrowOutlineColor ("Front Arrow Outline", Color) = (1.0, 0.88, 0.2, 1.0)
        _ArrowOutlineWidth ("Front Arrow Outline Width", Range(0.001, 0.04)) = 0.007
        [Toggle] _ShowFrontArrows ("Show Front Arrows", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+10"
        }

        Pass
        {
            Name "ClayEditGrid"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GridColor;
                float4 _FaceColor;
                float _Divisions;
                float _LineWidth;
                float _DotSize;
                float _HideCameraFacingFace;
                float4 _ArrowFillColor;
                float4 _ArrowOutlineColor;
                float _ArrowOutlineWidth;
                float _ShowFrontArrows;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float3 GetFaceNormalOS(float3 positionOS)
            {
                float3 absPos = abs(positionOS);

                if (absPos.x >= absPos.y && absPos.x >= absPos.z)
                {
                    return float3(sign(positionOS.x), 0.0, 0.0);
                }

                if (absPos.y >= absPos.x && absPos.y >= absPos.z)
                {
                    return float3(0.0, sign(positionOS.y), 0.0);
                }

                return float3(0.0, 0.0, sign(positionOS.z));
            }

            float2 GetFaceUv(float3 positionOS)
            {
                float3 absPos = abs(positionOS);
                float maxAxis = max(max(absPos.x, absPos.y), absPos.z);

                if (absPos.x >= absPos.y && absPos.x >= absPos.z)
                {
                    return positionOS.yz + 0.5;
                }

                if (absPos.y >= absPos.x && absPos.y >= absPos.z)
                {
                    return positionOS.xz + 0.5;
                }

                return positionOS.xy + 0.5;
            }

            bool IsHorizontalFace(float3 positionOS)
            {
                float3 absPos = abs(positionOS);

                return absPos.y >= absPos.x && absPos.y >= absPos.z;
            }

            void GetWideChevronMask(float2 uv, out float fillMask, out float outlineMask)
            {
                fillMask = 0.0;
                outlineMask = 0.0;

                const float tipY = 0.84;
                const float baseY = 0.44;
                const float centerX = 0.5;
                const float halfWidthBack = 0.42;
                const float halfWidthTip = 0.018;

                if (uv.y < baseY || uv.y > tipY)
                {
                    return;
                }

                float progress = (uv.y - baseY) / (tipY - baseY);
                float allowedX = lerp(halfWidthBack, halfWidthTip, progress);
                float distToBorder = allowedX - abs(uv.x - centerX);

                if (distToBorder < 0.0)
                {
                    return;
                }

                outlineMask = 1.0 - smoothstep(0.0, _ArrowOutlineWidth, distToBorder);
                fillMask = smoothstep(_ArrowOutlineWidth, _ArrowOutlineWidth * 2.5, distToBorder);
            }

            void GetFrontArrowMask(float3 positionOS, out float fillMask, out float outlineMask)
            {
                fillMask = 0.0;
                outlineMask = 0.0;

                if (_ShowFrontArrows < 0.5 || !IsHorizontalFace(positionOS))
                {
                    return;
                }

                float2 faceUv = positionOS.xz + 0.5;
                GetWideChevronMask(faceUv, fillMask, outlineMask);
            }

            float4 frag(Varyings input) : SV_Target
            {
                if (_HideCameraFacingFace > 0.5)
                {
                    float3 normalWS = TransformObjectToWorldNormal(GetFaceNormalOS(input.positionOS));
                    float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                    if (dot(normalWS, viewDirWS) > 0.0)
                    {
                        discard;
                    }
                }

                float2 faceUv = GetFaceUv(input.positionOS);
                float2 grid = faceUv * _Divisions;
                float2 gridFrac = frac(grid);
                float2 distToLine = min(gridFrac, 1.0 - gridFrac);

                float lineDist = min(distToLine.x, distToLine.y);
                float gridLineMask = 1.0 - smoothstep(0.0, _LineWidth, lineDist);

                float2 nearest = round(grid);
                float2 nearestUv = nearest / _Divisions;
                float vertexDist = length((faceUv - nearestUv) * _Divisions);
                float vertexMask = 1.0 - smoothstep(_DotSize * 0.5, _DotSize, vertexDist);

                float4 color = _FaceColor;
                color.rgb = lerp(color.rgb, _GridColor.rgb, gridLineMask);
                color.a = max(color.a, gridLineMask * _GridColor.a);
                color.rgb = max(color.rgb, _GridColor.rgb * vertexMask);
                color.a = max(color.a, vertexMask * _GridColor.a);

                float arrowFillMask;
                float arrowOutlineMask;
                GetFrontArrowMask(input.positionOS, arrowFillMask, arrowOutlineMask);

                color.rgb = lerp(color.rgb, _ArrowFillColor.rgb, arrowFillMask);
                color.a = max(color.a, arrowFillMask * _ArrowFillColor.a);
                color.rgb = lerp(color.rgb, _ArrowOutlineColor.rgb, arrowOutlineMask);
                color.a = max(color.a, arrowOutlineMask * _ArrowOutlineColor.a);

                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
