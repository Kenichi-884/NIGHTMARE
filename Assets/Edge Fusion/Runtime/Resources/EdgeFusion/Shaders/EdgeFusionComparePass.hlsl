#ifndef EDGE_FUSION_COMPARE_PASS_INCLUDED
#define EDGE_FUSION_COMPARE_PASS_INCLUDED

TEXTURE2D_X(_CompareTex);
float4 _CompareParams; // x: cos(angle), y: sin(angle), z: panning or flag, w: line width
float4 _CompareLineColor; // rgba line color

float4 FragCompare(EFVaryings i) : SV_Target
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
	float2 uv = UnityStereoTransformScreenSpaceTex(i.texcoord);

	float2 dd = uv - 0.5.xx;
	float co = dot(_CompareParams.xy, dd);
	float dist = distance(_CompareParams.xy * co, dd);
    float aa = saturate((_CompareParams.w - dist) / abs(_SrcColorTexture_TexelSize.y));

	float sameSide = (_CompareParams.z > -5);
	float2 pixelUV = lerp(uv, float2(uv.x + _CompareParams.z, uv.y), sameSide);
	float2 pixelNiceUV = lerp(uv, float2(uv.x - 0.5 + _CompareParams.z, uv.y), sameSide);
	float4 original = SAMPLE_TEXTURE2D_X(_SrcColorTexture, sampler_PointClamp, pixelUV);
	float4 effected = SAMPLE_TEXTURE2D_X(_CompareTex, sampler_PointClamp, pixelNiceUV);

	float2 cp = float2(_CompareParams.y, -_CompareParams.x);
	float t = dot(dd, cp) > 0;
    float4 outCol = lerp(original, effected, t);
    outCol = lerp(outCol, _CompareLineColor, aa);
    return outCol;
}

#endif // EDGE_FUSION_COMPARE_PASS_INCLUDED


