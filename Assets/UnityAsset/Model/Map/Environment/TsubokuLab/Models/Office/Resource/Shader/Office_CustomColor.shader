Shader "Office/Office_CustomColor"
{
    Properties
    {
        [Header(Parameters)] [Space(10)]
        _UVScale("UV Scale",float) = 1
        _UVOffsetX("UV Offset X",float) = 0
        _UVOffsetY("UV Offset Y",float) = 0
        [Space(10)]

        _ColorTint("Color Tint",Color) = (1,1,1,1)
        _CustomColor_A("Custom Color A",Color) = (1,0,0,1)
        _CustomColor_B("Custom Color B",Color) = (0,1,0,1)
        _CustomColor_C("Custom Color C",Color) = (0,0,1,1)
        _MetallicValue("Metallic Value",Range(0,1)) = 0
        _RoughnessAjustment("Roughness Ajustment",Range(-1,1)) = 0
        _NormalFlatness("Normal Flatness",Range(0,1)) = 0
        [Space(10)]


        [Header(Maps)] [Space(10)]
        [NoScaleOffset] _ColorMap("Color Map",2D) = "white"
        [NoScaleOffset] _MetallicMap("Metallic Map",2D) = "black"
        [NoScaleOffset] _RoughnessMap("Roughness Map",2D) = "white"
        [Normal][NoScaleOffset] _NormalMap("Normal Map",2D) = "bump"
        [NoScaleOffset] _ColorMaskMap("Color Mask Map",2D) = "black"


        [Header(Switch)][Space(10)]
        [MaterialToggle]_UseCustomColor_A("Use Custom Color A",int) = 0
        [MaterialToggle]_UseCustomColor_B("Use Custom Color B",int) = 0
        [MaterialToggle]_UseCustomColor_C("Use Custom Color C",int) = 0
        [KeywordEnum(TEXTURE,Value)]_MetallicSource("MetallicSource",float) = 1
    }
        SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM

        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        #pragma shader_feature _METALLICSOURCE_TEXTURE _METALLICSOURCE_VALUE

        float _UVScale;
        float _UVOffsetX;
        float _UVOffsetY;

        fixed4 _ColorTint;
        fixed4 _CustomColor_A;
        fixed4 _CustomColor_B;
        fixed4 _CustomColor_C;
        half _MetallicValue;
        half _RoughnessAjustment;
        half _NormalFlatness;

        half _UseCustomColor_A;
        half _UseCustomColor_B;
        half _UseCustomColor_C;


        sampler2D _ColorMap;
        sampler2D _MetallicMap;
        sampler2D _RoughnessMap;
        sampler2D _NormalMap;
        sampler2D _ColorMaskMap;

        struct Input
        {
            float2 uv_ColorMap;
        };


        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_INSTANCING_BUFFER_END(Props)


        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_ColorMap;
            uv *= _UVScale;
            uv += float2(_UVOffsetX, _UVOffsetY);

            fixed3 mask = tex2D(_ColorMaskMap, uv);

            fixed3 color = tex2D(_ColorMap, uv);

            #ifdef _METALLICSOURCE_TEXTURE
                half metallic = tex2D(_MetallicMap, uv);

            #elif _METALLICSOURCE_VALUE
            half metallic = _MetallicValue;

            #endif

            half roughness = tex2D(_RoughnessMap, uv);
            fixed3 normal = UnpackNormal(tex2D(_NormalMap, uv));

            color *= _ColorTint;
            color = lerp(color, _CustomColor_A, mask.r * _UseCustomColor_A);
            color = lerp(color, _CustomColor_B, mask.g * _UseCustomColor_B);
            color = lerp(color, _CustomColor_C, mask.b * _UseCustomColor_C);
            

            roughness += _RoughnessAjustment;
            normal = lerp(normal, fixed3(0, 0, 1), _NormalFlatness);

            o.Albedo = color;
            o.Metallic = metallic;
            o.Smoothness = 1 - roughness;
            o.Normal = normal;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
