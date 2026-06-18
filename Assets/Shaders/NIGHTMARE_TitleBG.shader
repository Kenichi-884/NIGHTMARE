// 3D背景RenderTexture用 : クロマティックアベレーション + スキャンライン + フィルムグレイン
Shader "NIGHTMARE/TitleBG"
{
    Properties
    {
        _MainTex       ("Texture",              2D)            = "white" {}
        _ChromaStr     ("Chromatic Aberration", Range(0,0.025))= 0.005
        _ScanlineAlpha ("Scanline Alpha",       Range(0,1))    = 0.18
        _ScanlineCount ("Scanline Count",       Float)         = 160
        _GrainStr      ("Grain Strength",       Range(0,1))    = 0.06
        _Brightness    ("Brightness",           Range(0,2))    = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct a2v
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            sampler2D _MainTex;
            float _ChromaStr;
            float _ScanlineAlpha;
            float _ScanlineCount;
            float _GrainStr;
            float _Brightness;

            v2f vert(a2v v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                o.color  = v.color;
                return o;
            }

            float rand(float2 co)
            {
                return frac(sin(dot(co, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // ── クロマティックアベレーション ──
                // 画面中心からの距離に応じて強度増加（レンズ収差の再現）
                float2 fromCenter  = uv - 0.5;
                float  dist        = length(fromCenter);
                float  chromaScale = 1.0 + dist * 1.8;
                float  ca          = _ChromaStr * chromaScale;

                float r = tex2D(_MainTex, uv + float2( ca, 0.0)).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - float2( ca, 0.0)).b;
                float a = tex2D(_MainTex, uv).a;

                fixed4 col = fixed4(r, g, b, a) * _Brightness;

                // ── スキャンライン ──
                float scan = sin(uv.y * _ScanlineCount * 3.14159265) * 0.5 + 0.5;
                col.rgb *= lerp(1.0 - _ScanlineAlpha, 1.0, scan);

                // ── フィルムグレイン（24fps ステップで更新） ──
                float t     = floor(_Time.y * 24.0);
                float grain = rand(uv * 1.5 + float2(t * 0.017, t * 0.011));
                col.rgb    += (grain - 0.5) * _GrainStr;
                col.rgb     = saturate(col.rgb);

                col *= i.color;
                return col;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
