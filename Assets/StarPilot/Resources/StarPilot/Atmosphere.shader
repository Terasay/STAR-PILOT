Shader "StarPilot/Atmosphere"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent"}
        Blend SrcAlpha One ZWrite Off Cull Back
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A{float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.n);return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normal),v=GetWorldSpaceNormalizeViewDir(i.world);
                float nv=saturate(dot(n,v)),nl=dot(n,GetMainLight().direction);
                float rim=pow(1-nv,7)*smoothstep(0,.16,nv);
                float light=smoothstep(-.22,.5,nl);
                return half4(lerp(float3(.25,.25,.35),float3(.12,.42,1),light),rim*light*.75);
            }
            ENDHLSL
        }
    }
}
