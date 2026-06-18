#ifndef EDGE_FUSION_BLEND_PASS_INCLUDED
#define EDGE_FUSION_BLEND_PASS_INCLUDED

#include "EdgeFusionBlendLibrary.hlsl"

float4 FragBlendEdges(EFVaryings i) : SV_Target
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
	float2 uv = UnityStereoTransformScreenSpaceTex(i.texcoord);

	float4 c0 = SampleColor(uv);

	float2 uvSurfaceB;
	float edgeFactor = FindEdgeNeighbour(uv, uvSurfaceB);
	if (edgeFactor <= 0.0) return c0;

	#if EDGE_FUSION_MSAA_EDGE_FIX
		float2 dir = normalize(uvSurfaceB - uv) * _SrcColorTexture_TexelSize.xy * MSAA_FIX_POWER;
		c0 = SampleColor(uv - dir);
		float4 surf0 = SampleColor(uvSurfaceB + dir);
	#else
		float4 surf0 = SampleColor(uvSurfaceB);
	#endif

	float fallOff = 2;

	// screen-edge "vignette" fade
	float2 distUV = min(uv, 1.0 - uv);
	float edgeFade = min(distUV.x, distUV.y);
	float2 distUVB = min(uvSurfaceB, 1.0 - uvSurfaceB);
	float edgeFadeB = min(distUVB.x, distUVB.y);
	edgeFade = min(edgeFade, edgeFadeB);
	edgeFade = 1 - smoothstep(0, 0.05, edgeFade);
	fallOff += edgeFade * 4.0;

	// avoid brightening shadows
	float lumaBase = GetLuma(c0.rgb);
	float invLuma2 = rcp(lumaBase * lumaBase + 1e-6);	
	fallOff += clamp( SHADOW_PROTECTION * invLuma2, 0, 16);

	// apply falloff
	float blendAmount = pow(edgeFactor, fallOff);

	// blend
	blendAmount *= GLOBAL_INTENSITY * 0.5;
	float3 blended = ColorLerp(c0.rgb, surf0.rgb, blendAmount);

	return float4(blended, c0.a);

}

#endif // EDGE_FUSION_BLEND_PASS_INCLUDED