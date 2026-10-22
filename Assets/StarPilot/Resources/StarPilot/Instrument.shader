Shader "StarPilot/Instrument"
{
    Properties
    {
        [PerRendererData] _MainTex("Texture",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"}
        Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
        Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);float4 _Color;float4 _TextureSampleAdd;
            struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;o.color=a.color*_Color;return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 texel=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)+_TextureSampleAdd;
                return half4(texel.rgb*i.color.rgb*1.35,texel.a*i.color.a);
            }
            ENDHLSL
        }
    }
}
