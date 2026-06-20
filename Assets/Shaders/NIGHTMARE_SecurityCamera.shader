// 監視カメラ映像ポストプロセス: Graphics.Blit で RenderTexture に適用する
Shader "NIGHTMARE/SecurityCamera"
{
    Properties
    {
        _MainTex       ("Camera Feed",       2D)            = "white" {}
        _ChromaStr     ("Chromatic Aber.",   Range(0,0.02)) = 0.004
        _ScanlineAlpha ("Scanline Strength", Range(0,1))    = 0.30
        _ScanlineCount ("Scanline Count",    Float)         = 180.0
        _GrainStr      ("Film Grain",        Range(0,0.3))  = 0.06
        _VignetteStr   ("Vignette",          Range(0,2))    = 1.0
        _Saturation    ("Saturation",        Range(0,1))    = 0.0
        _Brightness    ("Brightness",        Range(0.5,2))  = 1.10
        _WhiteTint     ("White Tint",        Range(0,1))    = 0.15
        _PixelScale    ("Pixel Scale",       Range(32,512)) = 160.0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            float _ChromaStr;
            float _ScanlineAlpha;
            float _ScanlineCount;
            float _GrainStr;
            float _VignetteStr;
            float _Saturation;
            float _Brightness;
            float _WhiteTint;
            float _PixelScale;

            float hash(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                // ── 低解像度化 (監視カメラの粗さ) ──
                float2 pixUV = (floor(uv * _PixelScale) + 0.5) / _PixelScale;

                // ── クロマティックアベレーション (ピクセル化UV基準) ──
                float2 fromCenter = pixUV - 0.5;
                float  dist       = length(fromCenter);
                float2 radialDir  = normalize(fromCenter + float2(0.00001, 0.00001));
                float  ca         = _ChromaStr * (1.0 + dist * 2.8);
                float  r = tex2D(_MainTex, pixUV + radialDir * ca).r;
                float  g = tex2D(_MainTex, pixUV                 ).g;
                float  b = tex2D(_MainTex, pixUV - radialDir * ca).b;
                fixed3 col = fixed3(r, g, b) * _Brightness;

                // ── 脱彩色 ──
                float luma = dot(col, float3(0.2126, 0.7152, 0.0722));
                col = lerp(fixed3(luma, luma, luma), col, _Saturation);

                // ── 白みトーン (寒色CCTVの色温度) ──
                fixed3 coolWhite = fixed3(0.92, 0.96, 1.0);
                col = lerp(col, luma * coolWhite, _WhiteTint);

                // ── スキャンライン ──
                float scanPhase = frac(uv.y * _ScanlineCount);
                float scanGap   = smoothstep(0.50, 0.85, scanPhase)
                                - smoothstep(0.85, 1.00, scanPhase) * 0.4;
                float scanGlow  = smoothstep(0.0, 0.25, scanPhase)
                                * (1.0 - smoothstep(0.25, 0.55, scanPhase));
                col *= 1.0 - scanGap * _ScanlineAlpha;
                col += scanGlow * 0.016;

                // ── フィルムグレイン ──
                float t     = floor(_Time.y * 24.0);
                float g1    = hash(uv        + float2(t * 0.017, t * 0.011));
                float g2    = hash(uv * 0.55 + float2(t * 0.031, t * 0.027));
                float g3    = hash(uv * 2.10 + float2(t * 0.009, t * 0.043));
                float grain = (g1 * 0.50 + g2 * 0.32 + g3 * 0.18) - 0.5;
                col += grain * _GrainStr;

                // ── ビネット ──
                float2 vigUV = fromCenter * float2(1.05, 1.35);
                float  vigR  = length(vigUV) * _VignetteStr;
                float  vig   = 1.0 - smoothstep(0.50, 1.50, vigR);
                col *= vig;

                col = saturate(col);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
