// Original desktop shader: smooth staircase corners in the low-resolution
// background art, preserving flat regions and horizontal/vertical strokes.
// This is an edge-directed filter, not a texture replacement or an FXAA SDK copy.
#if OPENGL
#define SV_POSITION POSITION
#define SV_TARGET COLOR
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
sampler2D SceneTexture : register(s0);
#define SAMPLE(uv) tex2D(SceneTexture, uv)
#else
#define SV_TARGET SV_Target0
#define VS_SHADERMODEL vs_6_0
#define PS_SHADERMODEL ps_6_0
Texture2D SceneTexture : register(t0);
SamplerState SceneSampler : register(s0);
#define SAMPLE(uv) SceneTexture.Sample(SceneSampler, uv)
#endif

float4x4 MatrixTransform;
float2 TexelSize;
struct VSInput { float4 Position : POSITION0; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; };
struct VSOutput { float4 Position : SV_POSITION; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; };
VSOutput MainVS(VSInput input)
{
    VSOutput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color; output.TexCoord = input.TexCoord;
    return output;
}
float Luma(float3 rgb) { return dot(rgb, float3(0.299, 0.587, 0.114)); }
float4 MainPS(VSOutput input) : SV_TARGET
{
    float2 uv = input.TexCoord;
    // Map art is drawn at 2 logical pixels per source texel. Detect the
    // underlying staircase at that scale instead of blurring the whole frame.
    float2 d = TexelSize * 2.0;
    float4 center = SAMPLE(uv);
    float nw = Luma(SAMPLE(uv + d * float2(-1, -1)).rgb);
    float ne = Luma(SAMPLE(uv + d * float2( 1, -1)).rgb);
    float sw = Luma(SAMPLE(uv + d * float2(-1,  1)).rgb);
    float se = Luma(SAMPLE(uv + d * float2( 1,  1)).rgb);
    float n = Luma(SAMPLE(uv + float2(0, -d.y)).rgb);
    float s = Luma(SAMPLE(uv + float2(0,  d.y)).rgb);
    float w = Luma(SAMPLE(uv + float2(-d.x, 0)).rgb);
    float e = Luma(SAMPLE(uv + float2( d.x, 0)).rgb);
    float2 gradient = float2(ne + 2 * e + se - nw - 2 * w - sw,
                             sw + 2 * s + se - nw - 2 * n - ne);
    float major = max(abs(gradient.x), abs(gradient.y));
    float minor = min(abs(gradient.x), abs(gradient.y));
    float diagonal = saturate((minor / max(major, 0.0001) - 0.08) / 0.65);
    float low = min(min(min(nw, ne), min(sw, se)), min(min(n, s), min(w, e)));
    float high = max(max(max(nw, ne), max(sw, se)), max(max(n, s), max(w, e)));
    float contrast = smoothstep(0.035, 0.14, high - low);
    float2 tangent = float2(-gradient.y, gradient.x) / max(length(gradient), 0.0001);
    float2 offset = tangent * TexelSize * 1.5;
    float3 along = (SAMPLE(uv - offset).rgb + SAMPLE(uv + offset).rgb) * 0.5;
    float3 color = lerp(center.rgb, along, diagonal * contrast * 0.7);
    return float4(color, center.a) * input.Color;
}
technique SceneEdges
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
