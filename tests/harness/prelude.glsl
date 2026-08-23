#version 330 core

// =============================================================================
// HLSL -> GLSL 互換レイヤ
//
// Illust2DCore.hlsl を「そのまま」GLSL としてコンパイルするための前置きコード。
// ここに無いものをコアで使うとテストが落ちるので、コアの記述可能範囲を
// 定義するファイルでもある。
// =============================================================================

#define half   float
#define half2  vec2
#define half3  vec3
#define half4  vec4
#define fixed  float
#define fixed2 vec2
#define fixed3 vec3
#define fixed4 vec4
#define float2 vec2
#define float3 vec3
#define float4 vec4

#define lerp(a, b, t) mix((a), (b), (t))
#define frac(x)       fract(x)
#define rsqrt(x)      inversesqrt(x)
#define fmod(x, y)    mod((x), (y))
#define ddx(x)        dFdx(x)
#define ddy(x)        dFdy(x)

float saturate(float x) { return clamp(x, 0.0, 1.0); }
vec2  saturate(vec2 x)  { return clamp(x, 0.0, 1.0); }
vec3  saturate(vec3 x)  { return clamp(x, 0.0, 1.0); }
vec4  saturate(vec4 x)  { return clamp(x, 0.0, 1.0); }
