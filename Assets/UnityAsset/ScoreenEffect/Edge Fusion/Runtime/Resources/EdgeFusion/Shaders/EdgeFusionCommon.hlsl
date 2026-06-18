#ifndef EDGE_FUSION_COMMON_INCLUDED
#define EDGE_FUSION_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

TEXTURE2D_X(_ObjectIDTexture);
TEXTURE2D_X(_SrcColorTexture);
TEXTURE3D(_NoiseTex3D);
TEXTURE2D_X(_CustomGroupTexture);

// Packed float parameters
float4 _BlendData1; // x: GlobalIntensity, y: MaxBlendDistance, z: NormalThreshold, w: NoiseContrast
float4 _BlendData2; // x: MaxScreenRadius, y: ShadowProtection, z: NoiseIntensity, w: NoiseScale

// Macros for accessing packed parameters
#define GLOBAL_INTENSITY _BlendData1.x
#define MAX_BLEND_DISTANCE _BlendData1.y
#define NORMAL_THRESHOLD _BlendData1.z
#define NOISE_CONTRAST _BlendData1.w
#define MAX_SCREEN_RADIUS _BlendData2.x
#define SHADOW_PROTECTION _BlendData2.y
#define NOISE_INTENSITY _BlendData2.z
#define NOISE_SCALE _BlendData2.w

float _DefaultRadiusWorld;
float _RadiusScale;
float _DistanceCompensation;
int _SampleCount;
int _BinarySearchSteps;
int _EarlyExitHits;
int _DebugMode;
float _DepthDebugMultiplier;
float4 _SrcColorTexture_TexelSize;
float _MSAAFixPower;

#define MSAA_FIX_POWER _MSAAFixPower

inline float4 SampleColor(float2 uv) { 
    return SAMPLE_TEXTURE2D_X_LOD(_SrcColorTexture, sampler_LinearClamp, uv, 0); 
}

inline float4 SampleObjectData(float2 uv) {
    return SAMPLE_TEXTURE2D_X_LOD(_ObjectIDTexture, sampler_PointClamp, uv, 0);
}

inline bool SameObjectIds(float4 objA, float4 objB) {
    return abs(objA.r - objB.r) < 0.8;
}

// Packing helpers: pack object ID and normalized radius into the red channel
#define EF_PACK_QUANT 0.01

inline float PackObjectIdRadius(float objectId, float radius) {
    #if defined(SHADER_API_MOBILE) || defined(SHADER_API_GLES3) || defined(SHADER_API_GLES) || defined(SHADER_API_WEBGPU)
        uint id = asuint(objectId);
        id = ((id >> 16) ^ id) * 0x45d9f3b;
        id = ((id >> 16) ^ id) * 0x45d9f3b;
        id = (id >> 16) ^ id;
        float baseId = float(id % 32u);
    #else
        float baseId = floor(objectId / EF_PACK_QUANT);
        baseId = fmod(baseId, 4096.0);
    #endif
    return baseId + radius;
}

inline float UnpackIdFromPacked(float packed) {
    #if defined(SHADER_API_MOBILE) || defined(SHADER_API_GLES3) || defined(SHADER_API_GLES) || defined(SHADER_API_WEBGPU)
        return float(uint(floor(packed) + 0.5));
    #else
        return floor(packed) * EF_PACK_QUANT;
    #endif
}

inline float UnpackRadiusFromPacked(float packed) {
    return frac(packed);
}

float ConvertWorldRadiusToUV(float radius, float linearDepth) {
    radius *= (1.0 + _DistanceCompensation * linearDepth);
    float denom = lerp(max(linearDepth, 1.0), 1.0, unity_OrthoParams.w);
    return 0.5 * radius * unity_CameraProjection._m11 / denom;
}

float GetRadiusUV(float4 objectData, float linearDepth) {
    float radius = UnpackRadiusFromPacked(objectData.r) * _RadiusScale;
    return ConvertWorldRadiusToUV(radius, linearDepth);
}

float3 GetWorldPos(float2 uv, float depth) {
    #if !UNITY_REVERSED_Z
        depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
    #endif

    float3 viewPos = ComputeViewSpacePosition(uv, depth, UNITY_MATRIX_I_P);
    return mul(UNITY_MATRIX_I_V, float4(viewPos, 1.0)).xyz;
}

float3 ReconstructNormal(float2 uv, float depthC) {
    // Use 3-tap reconstruction
    float2 texelSize = _CameraDepthTexture_TexelSize.xy;

    // Sample depths at neighbor pixels
    float depthR = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, uv + float2(texelSize.x, 0), 0).r;
    float depthU = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, uv + float2(0, texelSize.y), 0).r;

    // Convert to world positions
    float3 pC = GetWorldPos(uv, depthC);
    float3 pR = GetWorldPos(uv + float2(texelSize.x, 0), depthR);
    float3 pU = GetWorldPos(uv + float2(0, texelSize.y), depthU);

    // Compute derivatives
    float3 dpdx = normalize(pR - pC);
    float3 dpdy = normalize(pU - pC);

    // Cross product for normal
    float3 n = cross(dpdy, dpdx);
    return n;
}

float3 ReconstructNormalWS(float3 worldPos) {
    float3 dpdx = ddx(worldPos);
    float3 dpdy = ddy(worldPos);
    float3 normalWS = cross(dpdy, dpdx);
    return dot(normalWS, normalWS) != 0 ? normalize(normalWS) : float3(0, 0, 1);
}

inline float3 GetNormal(float4 objectData) {
    float2 nxy = objectData.zw;
    float  nz  = sqrt(saturate(1.0 - dot(nxy, nxy)));
    return float3(nxy, nz);
}

float2 WorldToScreenPos(float3 wpos) {
	float4 clip = TransformWorldToHClip(wpos);
	float4 sp = ComputeScreenPos(clip);
	float2 uv = saturate(sp.xy / sp.w);
	return UnityStereoTransformScreenSpaceTex(uv);
}

struct EFVaryings
{
	float4 positionCS : SV_POSITION;
	float2 texcoord : TEXCOORD0;
	UNITY_VERTEX_OUTPUT_STEREO
};

struct EFFullscreenAttributes
{
	uint vertexID : SV_VertexID;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

EFVaryings EFVertFullscreen(EFFullscreenAttributes input)
{
	EFVaryings o;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	o.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
	o.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
	return o;
}

#if EDGE_FUSION_ID_EXCLUSION
float _ExclusionMask[32];

inline uint GetExclusionSlot(float4 objData) {
    float id = UnpackIdFromPacked(objData.r);
    uint slot = uint(id + 0.5);
    return (slot >= 1u && slot <= 32u) ? slot : 0u;
}

inline bool IsExcludedIdPair(float4 objA, float4 objB) {
    uint slotA = GetExclusionSlot(objA);
    uint slotB = GetExclusionSlot(objB);
    
    if (slotA == 0u || slotB == 0u) return false;
    
    uint idxA = slotA - 1u;
    uint idxB = slotB - 1u;
    
    return (((uint)_ExclusionMask[idxA] & (1u << idxB)) |
            ((uint)_ExclusionMask[idxB] & (1u << idxA))) != 0u;
}
#endif

#endif // EDGE_FUSION_COMMON_INCLUDED
