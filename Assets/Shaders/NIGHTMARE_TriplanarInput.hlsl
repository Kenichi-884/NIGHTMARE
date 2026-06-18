#ifndef NIGHTMARE_TRIPLANAR_INPUT_INCLUDED
#define NIGHTMARE_TRIPLANAR_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// LitInput.hlsl と同じレイアウト + トリプラナー用プロパティを追加
// SRP Batcher はすべてのプロパティが UnityPerMaterial に入っている必要がある
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _DetailAlbedoMap_ST;
    half4  _BaseColor;
    half4  _SpecColor;
    half4  _EmissionColor;
    half   _Cutoff;
    half   _Smoothness;
    half   _Metallic;
    half   _BumpScale;
    half   _Parallax;
    half   _OcclusionStrength;
    half   _ClearCoatMask;
    half   _ClearCoatSmoothness;
    half   _DetailAlbedoMapScale;
    half   _DetailNormalMapScale;
    half   _Surface;
    // ── トリプラナー専用 ──
    float  _TriplanarScale;
    float  _BlendSharpness;
CBUFFER_END

#endif
