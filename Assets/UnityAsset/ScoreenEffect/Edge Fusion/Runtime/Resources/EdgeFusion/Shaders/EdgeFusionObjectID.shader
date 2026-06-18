Shader "Kronnect/EdgeFusion/ObjectID"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        _EdgeFusionRadius("Edge Fusion Radius", Float) = 0
        _CustomObjectId("Custom Object ID", Float) = 0
        _DisallowIntraFusion("Disallow Intra Fusion", Float) = 0
        [HideInInspector] _ZWrite("ZWrite", Int) = 0
        [HideInInspector] _ZTest("ZTest", Int) = 3 // Equal = Make it work with cutout materials
    }
    
    SubShader
    {
        Pass
        {
            Name "Edge Fusion Object ID"
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]
            
            HLSLPROGRAM
            #if UNITY_PLATFORM_ANDROID || (UNITY_PLATFORM_WEBGL && !SHADER_API_WEBGPU) || UNITY_PLATFORM_UWP
                #pragma target 3.5
            #else
                #pragma target 4.5
            #endif
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options nolightprobe nolightmap
            #pragma multi_compile_local_fragment _ EDGE_FUSION_MSAA_ON
            #pragma multi_compile_local_fragment _ EDGE_FUSION_INTRA_OBJECT EDGE_FUSION_CONCAVE_ONLY
            #pragma multi_compile_local_fragment _ EDGE_FUSION_DEBUG_BLEND_NORMALS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityDOTSInstancing.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalDOTSInstancing.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			#include "EdgeFusionCommon.hlsl"

            #if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            struct ObjIDAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct ObjIDVaryings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float objectID : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float  viewDepth : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float _MaxBlendDistance;

            CBUFFER_START(UnityPerMaterial)
                float _EdgeFusionRadius;
                float _CustomObjectId;
                float _DisallowIntraFusion;
            CBUFFER_END

#ifdef UNITY_DOTS_INSTANCING_ENABLED
            UNITY_DOTS_INSTANCING_START(ObjectIDMetadata)
                UNITY_DOTS_INSTANCED_PROP(float, _EdgeFusionRadius)
                UNITY_DOTS_INSTANCED_PROP(float, _CustomObjectId)
                UNITY_DOTS_INSTANCED_PROP(float, _DisallowIntraFusion)
            UNITY_DOTS_INSTANCING_END(ObjectIDMetadata)

            #define _EdgeFusionRadius UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _EdgeFusionRadius)
            #define _CustomObjectId UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _CustomObjectId)
            #define _DisallowIntraFusion UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _DisallowIntraFusion)
#endif

            float GetIdFromObject() {
                float4x4 m = UNITY_MATRIX_M;
                float3 r0 = m[0].xyz;
                float3 r1 = m[1].xyz;
                float3 r2 = m[2].xyz;
                float3 pos = float3(m[0][3], m[1][3], m[2][3]);
                float objectID = dot(r0, float3(73.0, 157.0, 131.0))
                               + dot(r1, float3(269.0, 97.0, 211.0))
                               + dot(r2, float3(337.0, 41.0, 173.0))
                               + dot(pos, float3(521.0, 313.0, 463.0));
                return objectID;
            }
            
            ObjIDVaryings vert(ObjIDAttributes input)
            {
                ObjIDVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                float4 positionOS = input.positionOS;
                float3 normalOS = input.normalOS;
                
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                float3 positionVS = TransformWorldToView(positionWS);
                output.viewDepth = -positionVS.z;

                output.positionCS = TransformObjectToHClip(positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);

                float objectId = _CustomObjectId;
                if (objectId < 0.5) {
                    objectId = GetIdFromObject();
                    #if UNITY_ANY_INSTANCING_ENABLED
                        objectId += unity_InstanceID;
                    #endif
                    objectId = fmod(abs(objectId), 4096.0);
                }
                
                output.objectID = floor(objectId);

                output.worldPos = positionWS;

                return output;
            }
            
            float4 frag(ObjIDVaryings input) : SV_Target0
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                if (input.viewDepth > _MaxBlendDistance) return float4(0, 0, 0, 0);

                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif

                float objectId = input.objectID;
                float radius = _EdgeFusionRadius;

                if (radius <= 0) {
                    radius = _DefaultRadiusWorld;
                } else if (radius > 0.9 * _RadiusScale) {
                    return float4(0, -1, 0, 0);
                }

                float2 uv = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = SampleSceneDepth(uv);

                float rawDepth;
                #if EDGE_FUSION_MSAA_ON
                    rawDepth = input.positionCS.z;
                    #if UNITY_REVERSED_Z
                        if (rawDepth < sceneDepth) return float4(0, 0, 0, 0);
                    #else
                        if (rawDepth > sceneDepth) return float4(0, 0, 0, 0);
                    #endif
                #else
                    rawDepth = sceneDepth;
                #endif

                float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float uvRadius = ConvertWorldRadiusToUV(radius, linearDepth);
                float screenRadiusPx = uvRadius * _ScreenParams.y;
                if (screenRadiusPx < 2.0) {
                    return float4(0, -1, 0, 0);
		        }

                float packedR = PackObjectIdRadius(objectId, radius / _RadiusScale);
                
                float3 nVS;
                #if EDGE_FUSION_INTRA_OBJECT || EDGE_FUSION_CONCAVE_ONLY || EDGE_FUSION_DEBUG_BLEND_NORMALS
                    float3 nWS = ReconstructNormalWS(input.worldPos);
                    nVS = mul((float3x3)UNITY_MATRIX_V, nWS);
                    nVS = normalize(nVS);
                    if (_DisallowIntraFusion > 0.5) {
                        nVS.x = 999.0;
                    }
                #else
                    nVS = float3(0, 0, 1);
                #endif
		        return float4(packedR, rawDepth, nVS.xy);
            }
            ENDHLSL
        }
    }
}
