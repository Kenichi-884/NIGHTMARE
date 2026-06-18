Shader "Hidden/Kronnect/EdgeFusion/EdgeFusionFill"
{
    SubShader
    {
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "EdgeFusion Fill Object ID"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex EFVertFullscreen
            #pragma fragment FragFillObjectID
            #pragma multi_compile_local_fragment _ EDGE_FUSION_SPECIAL_GROUP
            #pragma multi_compile_local_fragment _ EDGE_FUSION_INTRA_OBJECT EDGE_FUSION_CONCAVE_ONLY
            #pragma multi_compile_local_fragment _ EDGE_FUSION_DEBUG_BLEND_NORMALS
            #include "EdgeFusionCommon.hlsl"
            #include "EdgeFusionFillPass.hlsl"
            ENDHLSL
        }
    }
}
