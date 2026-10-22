Shader "StarPilot/Canopy"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-5" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);o.n=TransformObjectToWorldNormal(a.n);o.uv=a.uv;return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.n),v=GetWorldSpaceNormalizeViewDir(i.world);Light sun=GetMainLight();
                float edge=pow(1-saturate(abs(dot(n,v))),4);
                float highlight=pow(saturate(dot(reflect(-sun.direction,n),v)),160);
                float seal=pow(saturate(1-min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y))*12),5);
                return half4(float3(.13,.25,.30)+sun.color*highlight*2,.012+edge*.06+seal*.07+highlight*.10);
            }
            ENDHLSL
        }
    }
}
