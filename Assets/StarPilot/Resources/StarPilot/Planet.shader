Shader "StarPilot/Planet"
{
    Properties { _Surface("Blue Marble surface",2D)="white"{} _Night("Black Marble night lights",2D)="black"{} }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_Surface);SAMPLER(sampler_Surface);
            TEXTURE2D(_Night);SAMPLER(sampler_Night);
            struct A{float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float3 local:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.n);o.local=a.p.xyz;return o;}
            float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
            float n(float3 p)
            {
                float3 g=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(g),hash(g+float3(1,0,0)),f.x),lerp(hash(g+float3(0,1,0)),hash(g+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(g+float3(0,0,1)),hash(g+float3(1,0,1)),f.x),lerp(hash(g+float3(0,1,1)),hash(g+1),f.x),f.y),f.z);
            }
            float fbm(float3 p){return n(p)*.5+n(p*2.03)*.25+n(p*4.07)*.125+n(p*8.1)*.0625+n(p*16.1)*.03125;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 p=normalize(i.local),normal=normalize(i.normal),view=GetWorldSpaceNormalizeViewDir(i.world);
                Light sun=GetMainLight();
                float2 uv=float2(atan2(p.z,p.x)/(2*PI)+.5,asin(p.y)/PI+.5);
                float3 surface=SAMPLE_TEXTURE2D(_Surface,sampler_Surface,uv).rgb;
                float ocean=saturate((surface.b-surface.r)*7);
                surface=lerp(surface,float3(.009,.038,.07),ocean*.55);
                float3 warp=p*19+float3(fbm(p*6),fbm(p*7+12),fbm(p*5+31))*3;
                float cloud=smoothstep(.50,.68,fbm(warp));
                cloud*=smoothstep(.2,.6,fbm(p*9+7));
                float shadow=smoothstep(.48,.65,fbm(warp+sun.direction*.12))*.4;
                surface*=1-shadow;
                surface=lerp(surface,float3(.86,.9,.93),cloud);
                float nl=dot(normal,sun.direction),nv=saturate(dot(normal,view));
                float3 color=surface*(.009+max(0,nl)*sun.color*.5);
                // Remove the blue reference land/ocean from NASA's colour composite.
                // Warm city lights fade out before the terminator, beneath the cloud layer.
                float3 night=SAMPLE_TEXTURE2D(_Night,sampler_Night,uv).rgb;
                float cities=max(0,night.r-night.b*.85-.004);
                color+=float3(1,.64,.27)*cities*2.2*(1-smoothstep(-.22,.08,nl))*(1-cloud*.9);
                float spec=pow(saturate(dot(normal,normalize(sun.direction+view))),160)*ocean*(1-cloud);
                color+=spec*float3(.4,.55,.65);
                float haze=pow(1-nv,3)*smoothstep(-.14,.5,nl);
                color=lerp(color,float3(.065,.28,.52),haze*.65);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
