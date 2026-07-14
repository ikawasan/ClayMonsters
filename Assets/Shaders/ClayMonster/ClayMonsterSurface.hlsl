#ifndef CLAY_MONSTER_SURFACE_INCLUDED
#define CLAY_MONSTER_SURFACE_INCLUDED

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float _VertexColorStrength;
    float _ClayNoiseScale;
    float _ClayNoiseStrength;
    float _FingerprintScale;
    float _FingerprintStrength;
    float _NormalPerturb;
    float _CavityStrength;
    float4 _ShadowColor;
    float _DiffuseSoftness;
    float _ShadowDepth;
    float _Wrap;
    float _AmbientIntensity;
    half4 _SpecularColor;
    float _SpecularStrength;
    float _SpecularSmoothness;
    half4 _SubsurfaceColor;
    float _SubsurfaceStrength;
    float _SubsurfacePower;
    half4 _RimColor;
    float _RimPower;
    float _RimStrength;
    float _OutlineDarkness;
    float _OutlineWidth;
CBUFFER_END

float ClayMonsterHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float ClayMonsterNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = ClayMonsterHash21(i);
    float b = ClayMonsterHash21(i + float2(1, 0));
    float c = ClayMonsterHash21(i + float2(0, 1));
    float d = ClayMonsterHash21(i + float2(1, 1));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float ClayMonsterFbm(float2 p)
{
    float value = 0.0;
    float amplitude = 0.5;
    float2 shift = float2(100.0, 100.0);

    for (int i = 0; i < 4; i++)
    {
        value += amplitude * ClayMonsterNoise(p);
        p = p * 2.03 + shift;
        amplitude *= 0.5;
    }

    return value;
}

float2 ClayMonsterSurfaceUv(float3 positionWS, float3 normalWS)
{
    float3 tangent = abs(normalWS.y) < 0.99
        ? normalize(cross(normalWS, float3(0, 1, 0)))
        : normalize(cross(normalWS, float3(1, 0, 0)));
    float3 bitangent = normalize(cross(normalWS, tangent));
    float3 samplePos = positionWS * _ClayNoiseScale;
    return float2(dot(samplePos, tangent), dot(samplePos, bitangent));
}

float ClayMonsterSampleFingerprint(float2 uv)
{
    float streakY = sin((uv.y + ClayMonsterFbm(uv * 0.35) * 1.8) * _FingerprintScale);
    float streakX = sin((uv.x * 0.72 + ClayMonsterFbm(uv * 0.48 + 17.3) * 2.1) * _FingerprintScale * 0.88);
    float grain = ClayMonsterFbm(uv * 2.4 + float2(streakY * 0.15, streakX * 0.12));
    float smear = ClayMonsterFbm(uv * 6.8 + float2(streakX, streakY)) * 0.28;
    return streakY * 0.34 + streakX * 0.26 + 0.5 + (grain - 0.5) * 0.32 + smear;
}

float ClayMonsterSampleCavity(float3 positionWS, float3 normalWS)
{
    float2 uv = ClayMonsterSurfaceUv(positionWS, normalWS);
    float macro = ClayMonsterFbm(uv * 0.55);
    float micro = ClayMonsterFbm(uv * 3.2 + macro);
    float fingerprint = ClayMonsterSampleFingerprint(uv);
    return saturate((1.0 - macro) * 0.45 + (1.0 - micro) * 0.35 + (1.0 - fingerprint) * 0.2);
}

half3 ClayMonsterClampAlbedo(half3 albedo)
{
    return min(albedo, 1.0h);
}

half ClayMonsterGlossMask(float vertexAlpha)
{
    return lerp(0.08h, 0.65h, saturate(vertexAlpha));
}

half3 ClayMonsterClampOutput(half3 color)
{
    half peak = max(color.r, max(color.g, color.b));
    return peak > 1.0h ? color * (1.0h / peak) : color;
}

half3 ClayMonsterSampleAlbedo(float3 positionWS, float3 normalWS, float4 vertexColor)
{
    half3 albedo = lerp(_BaseColor.rgb, saturate(vertexColor.rgb), _VertexColorStrength);

    float2 uv = ClayMonsterSurfaceUv(positionWS, normalWS);
    float macro = ClayMonsterFbm(uv * 0.7);
    float fingerprint = ClayMonsterSampleFingerprint(uv);
    float variation = (macro - 0.5) * _ClayNoiseStrength;
    variation += (fingerprint - 0.5) * _FingerprintStrength;
    albedo *= 1.0 + variation;

    float cavity = ClayMonsterSampleCavity(positionWS, normalWS);
    albedo *= lerp(1.0, 0.76, cavity * _CavityStrength);

    return ClayMonsterClampAlbedo(albedo);
}

float3 ClayMonsterPerturbNormal(float3 positionWS, float3 normalWS)
{
    float2 uv = ClayMonsterSurfaceUv(positionWS, normalWS);
    float macro = ClayMonsterFbm(uv * 1.35);
    float fingerprint = ClayMonsterSampleFingerprint(uv);
    float height = macro * 0.55 + fingerprint * 0.45;

    float delta = 0.035;
    float heightX = ClayMonsterFbm((uv + float2(delta, 0.0)) * 1.35) * 0.55
        + ClayMonsterSampleFingerprint(uv + float2(delta, 0.0)) * 0.45;
    float heightY = ClayMonsterFbm((uv + float2(0.0, delta)) * 1.35) * 0.55
        + ClayMonsterSampleFingerprint(uv + float2(0.0, delta)) * 0.45;
    float3 tangent = abs(normalWS.y) < 0.99
        ? normalize(cross(normalWS, float3(0, 1, 0)))
        : normalize(cross(normalWS, float3(1, 0, 0)));
    float3 bitangent = normalize(cross(normalWS, tangent));
    float3 perturb = tangent * (height - heightX) + bitangent * (height - heightY);
    return normalize(normalWS - perturb * _NormalPerturb * 5.5);
}

#endif
