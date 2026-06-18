#ifndef EDGE_FUSION_FILL_PASS_INCLUDED
#define EDGE_FUSION_FILL_PASS_INCLUDED

#include "EdgeFusionCommon.hlsl"

// Reserved object ID for special group (ID 32)
#define CUSTOM_OBJECT_ID  32.0

// Terrain object ID passed from script (31.0 when blendWithWorld is true, 0.0 otherwise)
float _TerrainObjectId;

float4 FragFillObjectID(EFVaryings i) : SV_Target
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
	float2 uv = UnityStereoTransformScreenSpaceTex(i.texcoord);

	float4 objectData = SampleObjectData(uv);
	
    // Negative depth marks excluded pixels
    if (objectData.y < 0) {
        return float4(0,0,0,0);
    }

	// If pixel already has an object ID, keep it unchanged
	if (abs(objectData.r) > 1e-6) {
		return objectData;
	}
	
	// This pixel is empty (terrain/excluded geometry)
	float rawDepth = SampleSceneDepth(uv);
	float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
	
	// Only fill if within blend distance
	if (linearDepth >= MAX_BLEND_DISTANCE) {
		return objectData; // Keep empty
	}
	
    float3 nVS;
    #if EDGE_FUSION_INTRA_OBJECT || EDGE_FUSION_CONCAVE_ONLY || EDGE_FUSION_DEBUG_BLEND_NORMALS
        float3 worldPos = GetWorldPos(uv, rawDepth);
        float3 nWS = ReconstructNormalWS(worldPos);
        nVS = mul((float3x3)UNITY_MATRIX_V, nWS);
        nVS = normalize(nVS);
    #else
        nVS = float3(0, 0, 1);
    #endif

    #if EDGE_FUSION_SPECIAL_GROUP
        float3 marker = SAMPLE_TEXTURE2D_X_LOD(_CustomGroupTexture, sampler_PointClamp, uv, 0).rgb;
        bool isCustom = any(marker > 0.0001);
        float objectId = isCustom ? CUSTOM_OBJECT_ID : _TerrainObjectId;
    #else
        float objectId = _TerrainObjectId;
    #endif

    // If objectId is 0, return empty (no blending for terrain when blendWithWorld is false)
    if (objectId < 0.5) {
        return objectData;
    }

    float packedR = PackObjectIdRadius(objectId, _DefaultRadiusWorld / _RadiusScale);
    return float4(packedR, rawDepth, nVS.xy);
}

#endif // EDGE_FUSION_FILL_PASS_INCLUDED