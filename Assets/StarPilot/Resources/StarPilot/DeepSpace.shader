Shader "StarPilot/DeepSpace"
{
    Properties { _SunDirection("Sun direction",Vector)=(.65,.24,.72,0) }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _SunDirection;
            struct A {float4 p:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(a.p.xyz);o.direction=a.p.xyz;return o;}
            float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
            float noise(float3 p)
            {
                float3 g=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(g),hash(g+float3(1,0,0)),f.x),lerp(hash(g+float3(0,1,0)),hash(g+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(g+float3(0,0,1)),hash(g+float3(1,0,1)),f.x),lerp(hash(g+float3(0,1,1)),hash(g+1),f.x),f.y),f.z);
            }
            float3 stars(float2 uv,float density,float seed)
            {
                float2 cell=uv*density,base=floor(cell),f=frac(cell);
                float footprint=max(length(ddx(cell)),length(ddy(cell)));
                float3 color=0;
                // Neighbouring cells and integrated Gaussian energy keep tiny stars stable during rotation.
                for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                {
                    float2 g=base+float2(x,y);float h=hash(float3(g,seed));
                    float2 location=float2(hash(float3(g,seed+3)),hash(float3(g,seed+11)))*.7+.15;
                    float2 delta=float2(x,y)+location-f;
                    float radius=lerp(.009,.026,pow(h,8))*(density<100?.55:1);float width2=radius*radius+footprint*footprint*.32;
                    float intensity=exp(-dot(delta,delta)/max(width2,.0001))*radius*radius/width2;
                    color+=intensity*step(.86,h)*lerp(float3(.62,.77,1),float3(1,.83,.6),hash(float3(g,seed+17)))*lerp(2,6,pow(h,5));
                }
                return color;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d=normalize(i.direction);
                float band=pow(saturate(1-abs(dot(d,normalize(float3(.35,.83,-.18))))),9);
                float cloud=noise(d*5+5)*.52+noise(d*13+9)*.28+noise(d*37)*.13+noise(d*92)*.07;
                float filaments=pow(saturate(cloud*1.6-.36),2);
                float dust=pow(noise(d*21+3),3);
                float3 color=float3(.001,.002,.006)+band*filaments*lerp(float3(.024,.046,.072),float3(.075,.028,.061),noise(d*8))*2.5;
                color*=1-dust*band*.6;
                float2 uv=float2(atan2(d.z,d.x)/(2*PI)+.5,asin(d.y)/PI+.5);
                color+=stars(uv*float2(2,1),145,1)+stars(uv*float2(2,1),54,52);
                float sunDistance=length(d-normalize(_SunDirection.xyz));
                float aa=max(fwidth(sunDistance),.0001);
                float disc=1-smoothstep(.009-aa,.009+aa,sunDistance);
                float halo=exp(-sunDistance*45)*.55+exp(-sunDistance*8)*.035;
                color+=float3(1,.78,.48)*(disc*18+halo);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
