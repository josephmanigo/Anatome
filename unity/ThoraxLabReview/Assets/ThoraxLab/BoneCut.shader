Shader "ThoraxLab/BoneCut" {
 Properties { [MainTexture] _BaseMap("Cancellous texture",2D)="white"{} _CortexColor("Cortical bone",Color)=(.72,.65,.48,1) [MainColor] _BaseColor("Tint",Color)=(1,1,1,1) }
 SubShader { Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
  Pass { Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_CortexColor,_BaseColor;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;float depth:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
   Varyings vert(Attributes input){Varyings o;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionWS=TransformObjectToWorld(input.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(input.normalOS);o.uv=TRANSFORM_TEX(input.uv,_BaseMap);o.depth=input.color.r;return o;}
   half4 frag(Varyings input):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);float3 normal=normalize(input.normalWS);float3 cancellous=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb;float3 color=lerp(_CortexColor.rgb,cancellous,smoothstep(.20,.30,input.depth))*_BaseColor.rgb;float3 lightColor=max(SampleSH(normal),float3(.2,.2,.2));Light main=GetMainLight();lightColor+=main.color*saturate(dot(normal,main.direction))*main.distanceAttenuation;
   #ifdef _ADDITIONAL_LIGHTS
   uint count=GetAdditionalLightsCount();for(uint i=0;i<count;i++){Light light=GetAdditionalLight(i,input.positionWS);lightColor+=light.color*saturate(dot(normal,light.direction))*light.distanceAttenuation;}
   #endif
   return half4(color*lightColor,1);}
   ENDHLSL
  }
 }
}
