Shader "StarPilot/Surface"
{
    Properties
    {
        _BaseColor("Color", Color) = (0.06,0.08,0.1,1)
        _EmissionColor("Emission", Color) = (0,0,0,0)
        _Metallic("Metallic", Range(0,1)) = 0.65
        _Smoothness("Smoothness", Range(0,1)) = 0.55
        _Rock("Triplanar rock", Float) = 0
        _TextureScale("Triplanar scale",Float)=5.5
        _NormalStrength("Normal strength",Float)=.8
        _RockColor("Rock albedo",2D)="white"{}
        _RockNormal("Rock normal",2D)="bump"{}
        _RockRoughness("Rock roughness",2D)="white"{}
        [HideInInspector] _BaseMap("Base Map", 2D) = "white" {}
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
        [HideInInspector] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _EmissionColor;
                half _Metallic, _Smoothness, _Rock, _TextureScale, _NormalStrength;
            CBUFFER_END
            TEXTURE2D(_RockColor);SAMPLER(sampler_RockColor);
            TEXTURE2D(_RockNormal);SAMPLER(sampler_RockNormal);
            TEXTURE2D(_RockRoughness);SAMPLER(sampler_RockRoughness);
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float3 local:TEXCOORD2; float3 normalOS:TEXCOORD3; float3 meters:TEXCOORD4; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes a)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(a); UNITY_TRANSFER_INSTANCE_ID(a,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.local=a.positionOS.xyz;o.normalOS=a.normalOS;
                float4x4 m=GetObjectToWorldMatrix();
                float3 scale=float3(length(float3(m[0][0],m[1][0],m[2][0])),length(float3(m[0][1],m[1][1],m[2][1])),length(float3(m[0][2],m[1][2],m[2][2])));
                o.meters=a.positionOS.xyz*scale;
                o.normalWS = TransformObjectToWorldNormal(a.normalOS); return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.normalWS = normalize(i.normalWS);
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.bakedGI = SampleSH(d.normalWS);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1,1,1,1);
                SurfaceData s = (SurfaceData)0;
                s.albedo = _BaseColor.rgb; s.metallic = _Metallic;
                s.specular = half3(0.04,0.04,0.04); s.smoothness = _Smoothness;
                if(_Rock>.5)
                {
                    float3 w=pow(abs(normalize(i.normalOS)),4);w/=max(w.x+w.y+w.z,.001);
                    float3 p=(_Rock<1.5?i.local:i.meters)*_TextureScale;
                    half3 tex=SAMPLE_TEXTURE2D(_RockColor,sampler_RockColor,p.zy).rgb*w.x+SAMPLE_TEXTURE2D(_RockColor,sampler_RockColor,p.xz).rgb*w.y+SAMPLE_TEXTURE2D(_RockColor,sampler_RockColor,p.xy).rgb*w.z;
                    float grey=dot(tex,float3(.2126,.7152,.0722));
                    s.albedo=(_Rock<1.5?tex:lerp(.65,1.45,saturate(grey*2)))*_BaseColor.rgb;
                    half rough=SAMPLE_TEXTURE2D(_RockRoughness,sampler_RockRoughness,p.zy).r*w.x+SAMPLE_TEXTURE2D(_RockRoughness,sampler_RockRoughness,p.xz).r*w.y+SAMPLE_TEXTURE2D(_RockRoughness,sampler_RockRoughness,p.xy).r*w.z;
                    s.smoothness=_Rock<1.5?1-rough*.95:_Smoothness*lerp(.45,1.15,1-rough);
                    float3 nx=UnpackNormal(SAMPLE_TEXTURE2D(_RockNormal,sampler_RockNormal,p.zy));
                    float3 ny=UnpackNormal(SAMPLE_TEXTURE2D(_RockNormal,sampler_RockNormal,p.xz));
                    float3 nz=UnpackNormal(SAMPLE_TEXTURE2D(_RockNormal,sampler_RockNormal,p.xy));
                    float3 bump=float3(0,nx.y,nx.x)*w.x+float3(ny.x,0,ny.y)*w.y+float3(nz.x,nz.y,0)*w.z;
                    d.normalWS=TransformObjectToWorldNormal(normalize(i.normalOS+bump*_NormalStrength));
                    d.bakedGI=SampleSH(d.normalWS);
                }
                s.normalTS = half3(0,0,1); s.occlusion = 1; s.alpha = 1;
                s.emission = _EmissionColor.rgb;
                return UniversalFragmentPBR(d,s);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
