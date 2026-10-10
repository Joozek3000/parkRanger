// Faint additive torch beam for other players' flashlights: bright along the axis, soft at the silhouette,
// fading in from the lens and out along the length (mesh uv.y = 0 at the lens, 1 at the end).
Shader "ParkRangers/S06/BeamCone"
{
    Properties
    {
        _Color ("Colour (alpha = strength)", Color) = (0.9, 0.95, 1, 0.04)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float along : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
            };

            Varyings Vert(Attributes a)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(w);
                o.along = a.uv.y;
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                o.viewWS = GetWorldSpaceViewDir(w);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float facing = abs(dot(normalize(i.normalWS), normalize(i.viewWS)));
                float fade = (1 - i.along) * (1 - i.along) * saturate(i.along * 10);
                return half4(_Color.rgb * (_Color.a * facing * facing * fade), 1);
            }
            ENDHLSL
        }
    }
}
