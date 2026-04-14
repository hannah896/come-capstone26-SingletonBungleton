#ifndef KGB_STOCHASTIC_SAMPLING_INCLUDED
#define KGB_STOCHASTIC_SAMPLING_INCLUDED

inline float2 Hash22(float2 p)
{
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(float2(p.x * p.y, p.x + p.y));
}

inline float2 Rotate2D(float2 v, float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    return float2((c * v.x) - (s * v.y), (s * v.x) + (c * v.y));
}

inline float4 SampleStochasticColor(
    TEXTURE2D_PARAM(tex, texSampler),
    float2 uv,
    float2 worldXZ,
    float cellSize,
    float jitter)
{
    float2 cell = floor(worldXZ / max(0.0001, cellSize));
    float2 h = Hash22(cell);

    float2 o0 = (h - 0.5) * jitter;
    float2 o1 = (h.yx - 0.5) * (jitter * 0.73);
    float2 o2 = ((1.0 - h) - 0.5) * (jitter * 1.21);

    float a0 = (h.x * 2.0 - 1.0) * 3.14159265;
    float a1 = (h.y * 2.0 - 1.0) * 3.14159265;
    float a2 = ((h.x + h.y) * 0.5 * 2.0 - 1.0) * 3.14159265;

    float2 uv0 = Rotate2D(uv + o0, a0);
    float2 uv1 = Rotate2D(uv + o1, a1);
    float2 uv2 = Rotate2D(uv + o2, a2);

    float4 c0 = SAMPLE_TEXTURE2D(tex, texSampler, uv0);
    float4 c1 = SAMPLE_TEXTURE2D(tex, texSampler, uv1);
    float4 c2 = SAMPLE_TEXTURE2D(tex, texSampler, uv2);

    return (c0 * 0.5) + (c1 * 0.3) + (c2 * 0.2);
}

#endif