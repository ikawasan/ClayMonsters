Shader "Custom/ClayMonsterThumbnail"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.94, 0.86, 0.74, 1)
        _VertexColorStrength ("Vertex Color Strength", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ThumbnailCapture"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : NORMAL;
                float4 color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _VertexColorStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                half3 albedo = lerp(_BaseColor.rgb, IN.color.rgb, _VertexColorStrength);
                float3 keyDir = normalize(float3(0.25, 1.0, 0.35));
                float3 fillDir = normalize(float3(-0.45, 0.35, -0.55));
                float lighting = saturate(dot(normalWS, keyDir) * 0.42 + dot(normalWS, fillDir) * 0.28 + 0.58);
                lighting = lighting * lighting;
                half3 color = albedo * lighting;
                return half4(saturate(color), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
