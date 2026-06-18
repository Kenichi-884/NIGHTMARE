// UIテキスト等の上にかかる薄いスキャンライン + グレインオーバーレイ
Shader "NIGHTMARE/TitleCRTOverlay"
{
    Properties
    {
        _ScanlineAlpha ("Scanline Alpha", Range(0,1)) = 0.13
        _ScanlineCount ("Scanline Count", Float)      = 160
        _GrainStr      ("Grain Strength", Range(0,1)) = 0.04
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
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

            struct a2v { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            float _ScanlineAlpha;
            float _ScanlineCount;
            float _GrainStr;

            v2f vert(a2v v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                return o;
            }

            float rand(float2 co)
            {
                return frac(sin(dot(co, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // スキャンライン（暗い水平バンド）
                float scan  = sin(i.uv.y * _ScanlineCount * 3.14159265) * 0.5 + 0.5;
                float scanA = (1.0 - scan) * _ScanlineAlpha;

                // フィルムグレイン
                float t     = floor(_Time.y * 24.0);
                float grain = rand(i.uv * 1.5 + float2(t * 0.017, t * 0.011));
                float grainA = (grain - 0.5) * _GrainStr * 0.6;

                float alpha = saturate(scanA + grainA);
                return fixed4(0.0, 0.0, 0.0, alpha);
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
