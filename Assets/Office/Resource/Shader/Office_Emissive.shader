Shader "Office/Office_Emissive"
{
    Properties
    {
        [Header(Parameters)] [Space(10)]
        _UVScale("UV Scale",float) = 1
        _UVOffsetX("UV Offset X",float) = 0
        _UVOffsetY("UV Offset Y",float) = 0
        [Space(10)]

        _ColorTint("Color Tint",Color) = (1,1,1,1)
        _EmissiveTint("Emissive Tint",Color) = (1,1,1,1)
        _MetallicValue("Metallic Value",Range(0,1)) = 0
        _RoughnessAjustment("Roughness Ajustment",Range(-1,1)) = 0
        _NormalFlatness("Normal Flatness",Range(0,1)) = 0
        [Space(10)]


        [Header(Maps)] [Space(10)]
        [NoScaleOffset] _ColorMap("Color Map",2D) = "white"
        [NoScaleOffset] _MetallicMap("Metallic Map",2D) = "black"
        [NoScaleOffset] _RoughnessMap("Roughness Map",2D) = "white"
        [Normal][NoScaleOffset] _NormalMap("Normal Map",2D) = "bump"
        [NoScaleOffset] _EmissiveMap("Emissive Map",2D) = "black"


        [Header(Switch)][Space(10)]
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
        fixed4 _EmissiveTint;
        half _MetallicValue;
        half _RoughnessAjustment;
        half _NormalFlatness;


        sampler2D _ColorMap;
        sampler2D _MetallicMap;
        sampler2D _RoughnessMap;
        sampler2D _NormalMap;
        sampler2D _EmissiveMap;

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


            fixed3 color = tex2D(_ColorMap, uv);

            #ifdef _METALLICSOURCE_TEXTURE
                half metallic = tex2D(_MetallicMap, uv);

            #elif _METALLICSOURCE_VALUE
            half metallic = _MetallicValue;

            #endif

            half roughness = tex2D(_RoughnessMap, uv);

            fixed3 normal = UnpackNormal(tex2D(_NormalMap, uv));

            fixed3 emissive = tex2D(_EmissiveMap, uv);

            color *= _ColorTint;
            color = lerp(color, color * _EmissiveTint, step(0.1 , emissive.r));
            roughness += _RoughnessAjustment;
            normal = lerp(normal, fixed3(0, 0, 1), _NormalFlatness);
            emissive *= _EmissiveTint;

            o.Albedo = color;
            o.Metallic = metallic;
            o.Smoothness = 1 - roughness;
            o.Normal = normal;
            o.Emission = emissive;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
