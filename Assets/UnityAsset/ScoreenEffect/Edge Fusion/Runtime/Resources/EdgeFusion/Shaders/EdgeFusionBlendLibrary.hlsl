#ifndef EDGE_FUSION_BLEND_LIB_INCLUDED
#define EDGE_FUSION_BLEND_LIB_INCLUDED

static const float2 sunflower_points[32] = {
    float2(-0.08756255f, 0.08021447f),
    float2(0.01468209f, -0.16729483f),
    float2(0.12514433f, 0.16322862f),
    float2(-0.23386945f, -0.04136821f),
    float2(0.22404494f, -0.14251905f),
    float2(-0.07551290f, 0.28090421f),
    float2(-0.14480914f, -0.27882118f),
    float2(0.31549522f, 0.11521835f),
    float2(-0.32929810f, 0.13592947f),
    float2(0.15916285f, -0.34012176f),
    float2(0.11787271f, 0.37579677f),
    float2(-0.35591507f, -0.20626006f),
    float2(0.41817273f, -0.09193410f),
    float2(-0.25554255f, 0.36348298f),
    float2(-0.05910422f, -0.45610320f),
    float2(0.36320827f, 0.30611232f),
    float2(-0.48920069f, 0.02022998f),
    float2(0.35711789f, -0.35537999f),
    float2(-0.02390958f, 0.51706675f),
    float2(-0.34025894f, -0.40774392f),
    float2(0.53932101f, 0.07256488f),
    float2(-0.45720732f, 0.31811294f),
    float2(0.12499574f, -0.55561858f),
    float2(0.28923687f, 0.50475690f),
    float2(-0.56566134f, -0.18046139f),
    float2(0.54967531f, -0.25396393f),
    float2(-0.23821633f, 0.56920574f),
    float2(-0.21267188f, -0.59128201f),
    float2(0.56606831f, 0.29750964f),
    float2(-0.62893715f, 0.16578581f),
    float2(0.35758678f, -0.55612960f),
    float2(0.11377869f, 0.66204563f)
};

inline float2 GetOffset(int index) {
    return sunflower_points[index];
}

float _JitterFrame;

#define dot2(x) dot(x, x)

float3 HashToColor(float x) {
    float3 v = frac(sin(float3(x, x * 1.37, x * 2.17)) * 4378.5453);
    return saturate(v);
}

float GetLuma(float3 rgb) { 
    //const float3 lum = float3(0.2627, 0.6780, 0.0593); // Rec.2020 (HDR/wide gamut)
    //const float3 lum = float3(0.2126, 0.7152, 0.0722); // Rec709 (sRGB/HD)
    const float3 lum = float3(0.299, 0.587, 0.114); // Rec601 (SD/broadcast)
    return dot(rgb, lum);
}

float3 ColorLerp(float3 a, float3 b, float t) {
    #if UNITY_COLORSPACE_GAMMA
        a = SRGBToLinear(a);
        b = SRGBToLinear(b);
    #endif
    float3 c = lerp(a, b, t);
    #if UNITY_COLORSPACE_GAMMA
        c = LinearToSRGB(c);
    #endif
    return c;
}


float FindEdgeNeighbour(float2 uvA, out float2 neighbourUV) {

    neighbourUV = uvA;
    float4 oidA = SampleObjectData(uvA);
    if (abs(oidA.r) < 1e-6) return 0.0;

    float4 oidEdge = oidA;
    float2 edgeUV = uvA;
    float linearDepthA = LinearEyeDepth(oidA.y, _ZBufferParams);
    float compensatedDefaultRadius = _DefaultRadiusWorld * (1.0 + _DistanceCompensation * linearDepthA);
    float maxRadiusWorldSqr = compensatedDefaultRadius * compensatedDefaultRadius;
    float bestDistSqr = maxRadiusWorldSqr;
    
    #if EDGE_FUSION_ANTI_FLICKER
        float sumSoftW = 0.0;
        float2 accumEdgeUV = float2(0.0, 0.0);
        float3 accumEdgeWpos = float3(0.0, 0.0, 0.0);
    #endif

    float3 wposA = GetWorldPos(uvA, oidA.y);
    float3 edgeWpos = wposA;
    #if EDGE_FUSION_INTRA_OBJECT || EDGE_FUSION_CONCAVE_ONLY
        float3 normalA = GetNormal(oidA);
        bool edgeCanUseSameIds = normalA.x < 100.0;
    #endif
    float uvRadiusBase = GetRadiusUV(oidA, linearDepthA);
    float radiusLimit = MAX_SCREEN_RADIUS;
    float uvRadiusEff = (radiusLimit * uvRadiusBase) / (uvRadiusBase + radiusLimit + 1e-6);
    float aspectRatio = _SrcColorTexture_TexelSize.y / _SrcColorTexture_TexelSize.x;
    float2 uvRadiusSearch = float2(uvRadiusBase, uvRadiusBase * aspectRatio);
    
    #if EDGE_FUSION_ENABLE_JITTER
        float ca, sa;
        float2 pixel = uvA * _ScreenParams.xy;
        float jitter = InterleavedGradientNoise(pixel, _JitterFrame);
        float angle = jitter * 2.0 * 3.1415927;
        sincos(angle, sa, ca);
        float2 axisX = float2(ca, sa) * uvRadiusSearch;
        float2 axisY = float2(-sa, ca) * uvRadiusSearch;
    #endif

    float edgeFactor = 0.0;

    for (int d = 0; d < _SampleCount; d++) {
        float2 offset;
        float2 pdir = GetOffset(d);
        #if EDGE_FUSION_ENABLE_JITTER
            offset = axisX * pdir.x + axisY * pdir.y;
        #else
            offset = pdir * uvRadiusSearch;
        #endif

        float2 uvB = uvA + offset;
        if (uvB.x < 0.0 || uvB.x > 1.0 || uvB.y < 0.0 || uvB.y > 1.0) continue;
        
        float4 oidB = SampleObjectData(uvB);
        if (abs(oidB.r) < 1e-6) continue;

        // Check if it's a different valid object
        bool isSameObject = SameObjectIds(oidB, oidA);

        // If intra-object fusion is enabled, check for edges within the same object
        #if EDGE_FUSION_INTRA_OBJECT || EDGE_FUSION_CONCAVE_ONLY
        bool sameIds = isSameObject && edgeCanUseSameIds;
        if (sameIds) {
            float3 normalB = GetNormal(oidB);
            float normalDot = dot(normalA, normalB);
            isSameObject = normalDot >= NORMAL_THRESHOLD;
        }
        #endif
        
        if (isSameObject) continue;

        #if EDGE_FUSION_ID_EXCLUSION
            if (IsExcludedIdPair(oidA, oidB)) continue;
        #endif
        
        // Binary refine between center (uv) and this outer point (uvB)
        float2 a = uvA, b = uvB;
        for (int k = 0; k < _BinarySearchSteps; k++) {
            float2 m = (a + b) * 0.5;
            float4 oidM = SampleObjectData(m);

            // Check object ID sameness (inter-object edges)
            bool sameM = SameObjectIds(oidM, oidA);

            // For intra-object edges, check normal/depth discontinuities
            #if EDGE_FUSION_INTRA_OBJECT || EDGE_FUSION_CONCAVE_ONLY
            if (sameIds && sameM) {
                float3 normalM = GetNormal(oidM);
                sameM = dot(normalA, normalM) >= NORMAL_THRESHOLD;
            }
            #endif

            if (sameM) { a = m; } else { b = m; oidB = oidM; }
        }

        float3 wposB = GetWorldPos(b, oidB.y);
        float distSqr = dot2(wposB - wposA);

        if (distSqr >= maxRadiusWorldSqr) continue;

        #if EDGE_FUSION_ANTI_FLICKER
            float softW = 1.0 / (distSqr + 1e-10);
            sumSoftW += softW;
            accumEdgeUV += b * softW;
            accumEdgeWpos += wposB * softW;
        #else
            if (distSqr >= bestDistSqr) continue;
        #endif

        // Winner tracking drives oidEdge and the early-exit count. When anti-flicker is OFF, this is also where edgeUV/edgeWpos are set
        if (distSqr < bestDistSqr) {
            bestDistSqr = distSqr;
            oidEdge = oidB;
            #if !EDGE_FUSION_ANTI_FLICKER
                edgeUV = b;
                edgeWpos = wposB;
            #endif
            edgeFactor++;
            if (edgeFactor >= _EarlyExitHits) break;

            #if EDGE_FUSION_INTRA_OBJECT || EDGE_FUSION_CONCAVE_ONLY
                edgeCanUseSameIds = sameIds;
            #endif
        }
    }

    if (edgeFactor <= 0.0) return 0.0;

    #if EDGE_FUSION_ANTI_FLICKER
        // Replace edgeUV/edgeWpos with the weighted average and recompute bestDistSqr from the resolved edgeWpos
        edgeUV = accumEdgeUV / sumSoftW;
        edgeWpos = accumEdgeWpos / sumSoftW;
        bestDistSqr = dot2(edgeWpos - wposA);
    #endif

    #if EDGE_FUSION_NOISE
        float noise = SAMPLE_TEXTURE3D_LOD(_NoiseTex3D, sampler_LinearRepeat, wposA * NOISE_SCALE, 0).r;
        noise = smoothstep(0.5 - NOISE_CONTRAST, 0.5 + NOISE_CONTRAST, noise);
        noise *= NOISE_INTENSITY;
        edgeUV -= (uvA - edgeUV) * noise;
    #endif

    // Compute neighbour/mirrored pixel UV
    float2 deltaUV = edgeUV - uvA;
    neighbourUV = edgeUV + deltaUV;
    if (neighbourUV.x < 0.0 || neighbourUV.x > 1.0 || neighbourUV.y < 0.0 || neighbourUV.y > 1.0) return 0.0;
    float4 neighbourData = SampleObjectData(neighbourUV);

    // Is still different object?
    #if !EDGE_FUSION_INTRA_OBJECT && !EDGE_FUSION_CONCAVE_ONLY
        if (SameObjectIds(neighbourData, oidA)) return 0.0;
    #endif

    // Is still the edge object?
    if (!SameObjectIds(neighbourData, oidEdge)) return 0.0;

    // Check neighbour distance (real 3D, using the neighbour's own depth).
    float3 neighbourWpos = GetWorldPos(neighbourUV, neighbourData.y);
    float neighbourDistSqr = dot2(neighbourWpos - edgeWpos);

    // Distance falloff
    float neighbourLinearDepth = LinearEyeDepth(neighbourData.y, _ZBufferParams);
    float neighbourUvRadiusBase = GetRadiusUV(neighbourData, neighbourLinearDepth);
    float neighbourUvRadiusEff = (radiusLimit * neighbourUvRadiusBase) / (neighbourUvRadiusBase + radiusLimit + 1e-6);
    float falloffRadius = min(uvRadiusEff, neighbourUvRadiusEff);
    float reductionFactorSS = length(deltaUV) / falloffRadius;
    float reductionFactorWS = sqrt( max(bestDistSqr, neighbourDistSqr)) / compensatedDefaultRadius;
    edgeFactor = saturate(1.0 - max(reductionFactorSS, reductionFactorWS));

    // Concavity test
    #if EDGE_FUSION_CONCAVE_ONLY
	if (SameObjectIds(neighbourData, oidA)) {
        float3 normalNVS = GetNormal(neighbourData);
        float3 dVS = mul((float3x3)UNITY_MATRIX_V, neighbourWpos - wposA);
        float3 dnVS = normalNVS - normalA;
        float s = dot(dVS, dnVS);
        float nn = dot(dnVS, dnVS);
        const float epsilon = 0.0004;
        bool isConcave = (2.0 * s + epsilon * nn) < 0.0;
        if (!isConcave) return 0.0;
    }
    #endif    

    return edgeFactor;
}

#endif // EDGE_FUSION_BLEND_LIB_INCLUDED