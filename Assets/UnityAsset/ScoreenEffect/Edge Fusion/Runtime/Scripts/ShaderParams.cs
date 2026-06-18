using UnityEngine;

namespace EdgeFusion {

    public static class ShaderParams {

        public enum Passes {
            Blend = 0,
            Debug = 1,
            Compare = 2
        }

        public const int FillPassIndex = 0;

        // ========== Texture Properties ==========
        public static readonly int ObjectIDTexture = Shader.PropertyToID("_ObjectIDTexture");
        public static readonly int SrcColorTexture = Shader.PropertyToID("_SrcColorTexture");
        public static readonly int CompareTex = Shader.PropertyToID("_CompareTex");
        public static readonly int NoiseTex3D = Shader.PropertyToID("_NoiseTex3D");
        public static readonly int CustomGroupTexture = Shader.PropertyToID("_CustomGroupTexture");

        // ========== ObjectID Pass Parameters ==========
        public static readonly int EdgeFusionRadius = Shader.PropertyToID("_EdgeFusionRadius");
        public static readonly int DefaultRadiusWorld = Shader.PropertyToID("_DefaultRadiusWorld");
        public static readonly int RadiusScale = Shader.PropertyToID("_RadiusScale");
        public static readonly int MaxBlendDistance = Shader.PropertyToID("_MaxBlendDistance");
        public static readonly int DistanceCompensation = Shader.PropertyToID("_DistanceCompensation");
        public static readonly int TerrainObjectId = Shader.PropertyToID("_TerrainObjectId");
        public static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
        public static readonly int ZTest = Shader.PropertyToID("_ZTest");
        public static readonly int Cull = Shader.PropertyToID("_Cull");
        public static readonly int CustomObjectId = Shader.PropertyToID("_CustomObjectId");
        public static readonly int DisallowIntraFusion = Shader.PropertyToID("_DisallowIntraFusion");

        // ========== Blend Pass Parameters ==========
        public static readonly int BlendData1 = Shader.PropertyToID("_BlendData1"); // x: GlobalIntensity, y: MaxBlendDistance
        public static readonly int BlendData2 = Shader.PropertyToID("_BlendData2"); // x: MaxScreenRadius, y: ShadowProtection, z: NoiseIntensity, w: NoiseScale
        public static readonly int SampleCount = Shader.PropertyToID("_SampleCount");
        public static readonly int BinarySearchSteps = Shader.PropertyToID("_BinarySearchSteps");
        public static readonly int JitterFrame = Shader.PropertyToID("_JitterFrame");
        public static readonly int EarlyExitHits = Shader.PropertyToID("_EarlyExitHits");
        public static readonly int CompareParams = Shader.PropertyToID("_CompareParams");
        public static readonly int CompareLineColor = Shader.PropertyToID("_CompareLineColor");
        public static readonly int DepthDebugMultiplier = Shader.PropertyToID("_DepthDebugMultiplier");
        public static readonly int DebugMode = Shader.PropertyToID("_DebugMode");
        public static readonly int MsaaFixPower = Shader.PropertyToID("_MSAAFixPower");

        // ========== ID Exclusion Parameters ==========
        public static readonly int ExclusionMask = Shader.PropertyToID("_ExclusionMask");

        // ========== Shader Keywords ==========
        public const string SKW_DEBUG_BLEND_NORMALS = "EDGE_FUSION_DEBUG_BLEND_NORMALS";
        public const string SKW_INTRA_OBJECT_FUSION = "EDGE_FUSION_INTRA_OBJECT";
        public const string SKW_ENABLE_JITTER = "EDGE_FUSION_ENABLE_JITTER";
        public const string SKW_NOISE = "EDGE_FUSION_NOISE";
        public const string SKW_CONCAVE_ONLY = "EDGE_FUSION_CONCAVE_ONLY";
        public const string SKW_SPECIAL_GROUP = "EDGE_FUSION_SPECIAL_GROUP";
        public const string SKW_MSAA_ON = "EDGE_FUSION_MSAA_ON";
        public const string SKW_MSAA_EDGE_FIX = "EDGE_FUSION_MSAA_EDGE_FIX";
        public const string SKW_ID_EXCLUSION = "EDGE_FUSION_ID_EXCLUSION";
        public const string SKW_ANTI_FLICKER = "EDGE_FUSION_ANTI_FLICKER";

    }

}