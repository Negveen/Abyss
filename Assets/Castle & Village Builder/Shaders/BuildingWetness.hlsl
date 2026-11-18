// Wetness in the rain for Castle & Village Builder's Building Lit shader. Whatever runs the weather sets the
// globals: _Wetness rises while it rains and slowly falls as things dry, _RainNow is how hard it rains right now.
// Wet things are darker and richer in colour, with a sharp shine of the sun or moon on them and the sky reflected at
// low angles; on the land, puddles gather in flat places (more on paths), with rings where the drops land.
#ifndef CVB_BUILDING_WETNESS_INCLUDED
#define CVB_BUILDING_WETNESS_INCLUDED

float _Wetness;   // 0 = dry, 1 = soaked
float _RainNow;   // 0-1: how hard it rains right now (for the rings in the puddles)
float _WetOvercast; // 0-1: how overcast it is (a hidden sun hardly glints on anything wet)

half WetHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

half WetNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    half a = WetHash(i), b = WetHash(i + float2(1, 0)), c = WetHash(i + float2(0, 1)), d = WetHash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// Darker and richer, as soaked ground and grass are
half3 WetAlbedo(half3 albedo, half wet)
{
    half grey = dot(albedo, half3(0.3, 0.59, 0.11));
    half3 richer = lerp(grey.xxx, albedo, 1.3);
    return lerp(albedo, max(richer, 0.0) * 0.6, wet);
}

// The shine on something wet: a sharp highlight of the sun or moon, and the sky in it at low angles
half3 WetShine(float3 normalWS, float3 viewWS, Light light, half wet, half smoothness)
{
    if (wet <= 0.001) return 0;
    float3 halfway = normalize(light.direction + viewWS);
    half highlight = pow(saturate(dot(normalWS, halfway)), lerp(24.0, 400.0, smoothness)) * lerp(0.3, 2.0, smoothness)
                   * (1.0 - 0.9 * saturate(_WetOvercast));
    half fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), 5.0);
    half3 sky = SampleSH(reflect(-viewWS, normalWS));
    half3 lit = light.color * (light.shadowAttenuation * light.distanceAttenuation);
    return wet * (lit * highlight + sky * min(fresnel, 0.6) * lerp(0.2, 0.35, smoothness));
}

// Where puddles lie on the land (0-1): flat places, in patches, more on paths; only once it's well soaked
half PuddleAmount(float3 positionWS, float3 normalWS, half path)
{
    half soaked = saturate((_Wetness - 0.45) / 0.55);
    if (soaked <= 0.001) return 0;
    half flat = smoothstep(0.985, 0.998, normalWS.y);
    half patches = WetNoise(positionWS.xz * 0.09) * 0.7 + WetNoise(positionWS.xz * 0.35) * 0.3;
    half threshold = lerp(0.8, 0.58, path) + (1.0 - soaked) * 0.2;
    return flat * smoothstep(threshold, threshold + 0.12, patches);
}

// Rings spreading where raindrops land in a puddle: a little bend of the normal
float3 PuddleRipples(float3 positionWS)
{
    if (_RainNow <= 0.01) return float3(0, 1, 0);
    float2 bend = 0;
    [unroll]
    for (int layer = 0; layer < 2; layer++)
    {
        float2 p = positionWS.xz * 3.0 + layer * 17.3;
        float2 cell = floor(p);
        float2 inCell = frac(p) - 0.5;
        half random = WetHash(cell + layer * 3.1);
        half age = frac(_Time.y * 0.9 + random);                   // each cell's drop lands at its own time
        float2 at = inCell - (float2(WetHash(cell + 1.7), WetHash(cell + 4.2)) - 0.5) * 0.6;
        half distance = length(at);
        half ring = sin((distance - age * 0.45) * 60.0) * saturate(1.0 - abs(distance - age * 0.45) * 12.0) * (1.0 - age);
        bend += at / max(distance, 0.001) * ring * step(random, _RainNow);
    }
    return normalize(float3(bend.x * 0.25, 1.0, bend.y * 0.25));
}

#endif
