Shader "ThoraxLab/Organ Surface"
{
    Properties
    {
        [MainTexture] _BaseMap ("Tissue colour (RGB), smoothness (A)", 2D) = "white" {}
        [MainColor] _BaseColor ("Selection tint", Color) = (1,1,1,1)
        _Smoothness ("Surface smoothness", Range(0,1)) = 0.5
        _BumpScale ("Fine surface relief", Range(0,0.1)) = 0.02
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _ZWrite ("Write depth", Float) = 1
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
        Pass
        {
            Name "OrganForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment OrganFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            half4 OrganFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // A tile spans ~12.5 cm at the FBX source scale. It stays on the model during inspection.
                float3 p = TransformWorldToObject(input.positionWS) * (800.0 * _BaseMap_ST.x);
                half3 normalOS = TransformWorldToObjectNormal(input.normalWS);
                half3 weights = abs(normalOS);
                weights *= weights;
                weights /= max(dot(weights, half3(1,1,1)), 0.001h);
                half4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.yz) * weights.x
                          + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.zx) * weights.y
                          + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xy) * weights.z;
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = map.rgb * _BaseColor.rgb;
                surface.smoothness = map.a * _Smoothness;
                surface.normalTS = half3(0,0,1);
                surface.occlusion = 1;
                surface.alpha = 1;
                InputData lighting;
                InitializeInputData(input, surface.normalTS, lighting);
                // The filtered alpha gradient gives slight continuous relief without a UV tangent frame.
                float3 dx = ddx(p), dy = ddy(p);
                float3 acrossY = cross(dy, normalOS), acrossX = cross(normalOS, dx);
                float area = dot(dx, acrossY);
                float3 gradient = (ddx(map.a) * acrossY + ddy(map.a) * acrossX) * sign(area) / max(abs(area), 1e-7);
                lighting.normalWS = TransformObjectToWorldNormal(normalOS - clamp(gradient, -1.0, 1.0) * _BumpScale);
                InitializeBakedGIData(input, lighting);
                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, lighting.fogCoord);
                return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
