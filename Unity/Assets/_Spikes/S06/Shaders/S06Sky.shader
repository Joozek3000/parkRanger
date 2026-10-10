// Gradient sky for MapLighting: zenith/horizon gradient (lighter horizon so treelines read as silhouettes),
// a warm band towards the sun, a sun/moon disc and faint stars.
Shader "ParkRangers/S06/Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.01, 0.015, 0.04, 1)
        _Horizon ("Horizon", Color) = (0.05, 0.07, 0.12, 1)
        _Ground ("Below horizon", Color) = (0.01, 0.01, 0.012, 1)
        [HDR] _Glow ("Horizon glow towards the disc", Color) = (0, 0, 0, 1)
        [HDR] _Disc ("Disc colour", Color) = (0, 0, 0, 1)
        _DiscDir ("Direction to the disc", Vector) = (0, 1, 0, 0)
        _DiscCos ("Disc radius (cosine)", Float) = 0.9997
        _Stars ("Star brightness", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon, _Ground, _Glow, _Disc;
                float4 _DiscDir;
                float _DiscCos, _Stars;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            Varyings Vert(float4 positionOS : POSITION)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(positionOS.xyz);
                o.dir = positionOS.xyz;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1) * 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                half3 col = h > 0 ? lerp(_Horizon.rgb, _Zenith.rgb, sqrt(h)) : lerp(_Horizon.rgb, _Ground.rgb, saturate(-h * 10));

                float3 disc = normalize(_DiscDir.xyz);
                float towards = dot(normalize(d.xz + 1e-5), normalize(disc.xz + 1e-5)) * 0.5 + 0.5;
                col += _Glow.rgb * pow(towards, 3) * exp(-abs(h) * 10);

                float c = dot(d, disc);
                col += _Disc.rgb * (smoothstep(_DiscCos - 0.00004, _DiscCos + 0.00004, c) + 0.03 * pow(saturate(c), 400));

                // About one cell in 250 holds a star; they fade out towards the horizon.
                float3 p = d * 350;
                float3 cell = floor(p);
                float star = step(0.996, Hash(cell)) * saturate(1 - length(frac(p) - 0.5) * 3) * saturate(h * 6);
                col += _Stars * star * (0.4 + Hash(cell + 7.1));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
