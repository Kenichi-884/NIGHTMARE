Shader "NIGHTMARE/TriplanarLit"
{
    Properties
    {
        _BaseMap        ("Albedo",          2D)           = "white" {}
        _BaseColor      ("Color",           Color)        = (1,1,1,1)
        _Metallic       ("Metallic",        Range(0,1))   = 0.0
        _Smoothness     ("Smoothness",      Range(0,1))   = 0.5
        [HDR] _EmissionColor ("Emission",  Color)        = (0,0,0,0)
        _TriplanarScale ("Triplanar Scale", Float)        = 1.0
        _BlendSharpness ("Blend Sharpness", Range(1,16)) = 4.0
        // Lit.shader と CBUFFER レイアウトを合わせるためのダミー
        [HideInInspector] _Cutoff         ("", Range(0,1))   = 0.5
        [HideInInspector] _BumpScale      ("", Float)        = 1.0
        [HideInInspector] _OcclusionStrength("", Range(0,1)) = 1.0
        [HideInInspector] _Surface        ("", Float)        = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── ForwardLit ────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            ZTest  LEqual
            Cull   Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            // CommonMaterial + 統合 CBUFFER (LitInput + トリプラナー) + Lighting
            #include "Assets/Shaders/NIGHTMARE_TriplanarInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                half   fogFactor   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos    = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normal = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS   = normal.normalWS;
                OUT.fogFactor  = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 posWS    = IN.positionWS;
                float3 normalWS = NormalizeNormalPerPixel(IN.normalWS);

                float3 blend = pow(abs(normalWS), _BlendSharpness);
                blend /= dot(blend, 1.0);

                float s = _TriplanarScale;
                half4 xS = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, posWS.yz * s);
                half4 yS = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, posWS.xz * s);
                half4 zS = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, posWS.xy * s);
                half4 albedo = (xS * blend.x + yS * blend.y + zS * blend.z) * _BaseColor;

                InputData inputData = (InputData)0;
                inputData.positionWS      = posWS;
                inputData.positionCS      = IN.positionCS;
                inputData.normalWS        = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(posWS);
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(posWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                inputData.fogCoord                = InitializeInputDataFog(float4(posWS, 1.0), IN.fogFactor);
                inputData.bakedGI                 = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask              = SAMPLE_SHADOWMASK(float2(0, 0));

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo      = albedo.rgb;
                surfaceData.alpha       = 1.0;
                surfaceData.metallic    = _Metallic;
                surfaceData.smoothness  = _Smoothness;
                surfaceData.emission    = _EmissionColor.rgb;
                surfaceData.normalTS    = half3(0, 0, 1);
                surfaceData.occlusion   = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb   = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // ── ShadowCaster (Lit.shader と同じ方式) ─────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest  LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ── DepthOnly (Lit.shader と同じ方式) ────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex   DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
