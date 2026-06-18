Shader "Hidden/Kronnect/EdgeFusion/EdgeFusion"
{
    SubShader
    {
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "EdgeFusion Blend"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex EFVertFullscreen
            #pragma fragment FragBlendEdges
            #pragma multi_compile_local_fragment _ EDGE_FUSION_INTRA_OBJECT EDGE_FUSION_CONCAVE_ONLY
            #pragma multi_compile_local_fragment _ EDGE_FUSION_ENABLE_JITTER
            #pragma multi_compile_local_fragment _ EDGE_FUSION_NOISE
            #pragma multi_compile_local_fragment _ EDGE_FUSION_MSAA_EDGE_FIX
            #pragma multi_compile_local_fragment _ EDGE_FUSION_ID_EXCLUSION
            #pragma multi_compile_local_fragment _ EDGE_FUSION_ANTI_FLICKER
            #include "EdgeFusionCommon.hlsl"
            #include "EdgeFusionBlendPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "EdgeFusion Debug"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex EFVertFullscreen
            #pragma fragment FragDebug
            #pragma multi_compile_local_fragment _ EDGE_FUSION_INTRA_OBJECT EDGE_FUSION_CONCAVE_ONLY
            #include "EdgeFusionCommon.hlsl"
            #include "EdgeFusionDebugPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "EdgeFusion Compare"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex EFVertFullscreen
            #pragma fragment FragCompare
            #include "EdgeFusionCommon.hlsl"
            #include "EdgeFusionComparePass.hlsl"
            ENDHLSL
        }
    }
}
