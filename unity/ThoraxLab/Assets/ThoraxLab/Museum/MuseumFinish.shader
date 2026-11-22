Shader "ThoraxLab/Museum finish"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo (RGB), smoothness (A)", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _Sheen ("Satin reflection", Range(0,1)) = 0.12
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Write depth", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Gallery"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Sheen;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 lighting : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 lighting : TEXCOORD1;
                half facing : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.lighting = v.lighting;
                half3 normal = TransformObjectToWorldNormal(v.normalOS);
                o.facing = saturate(dot(normal, GetWorldSpaceNormalizeViewDir(world)));
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 surface = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 albedo = lerp(half3(1,1,1), surface.rgb, i.lighting.a);
                half3 diffuse = albedo * _BaseColor.rgb * i.lighting.rgb;
                half rim = 1.0h - i.facing;
                half sheen = rim * rim * lerp(.65h, surface.a, i.lighting.a) * _Sheen;
                return half4(diffuse + sheen * i.lighting.rgb * half3(.42h,.39h,.32h), 1);
            }
            ENDHLSL
        }
    }
}
