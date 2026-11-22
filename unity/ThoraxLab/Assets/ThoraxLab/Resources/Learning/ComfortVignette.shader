Shader "ThoraxLab/ComfortVignette"
{
    Properties
    {
        [HideInInspector] _Amount ("Movement vignette", Range(0,1)) = 0
        [HideInInspector] _ReadingDim ("Reading dimming", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO };
            float _Amount, _ReadingDim;
            Varyings vert(Attributes input){Varyings output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);output.positionCS=TransformObjectToHClip(input.positionOS.xyz);output.color=input.color;return output;}
            half4 frag(Varyings input):SV_Target {return half4(0,0,0,lerp(_ReadingDim,1.0,input.color.a*_Amount));}
            ENDHLSL
        }
    }
}
