Shader "Solitaire/VictoryBubbleGloss"
{
    Properties
    {
        [HDR] _BroadSpec ("Broad Highlight", Color) = (1.15, 1.12, 1.18, 0.55)
        _BroadStrength ("Broad Strength", Range(0, 3)) = 1.15
        _BroadSize ("Broad Size", Range(0.12, 1.2)) = 0.38
        _BroadAspect ("Broad Oval Stretch", Range(0.4, 2.5)) = 1.45
        [HDR] _SharpSpec ("Sharp Highlight", Color) = (1.6, 1.55, 1.65, 1)
        _SharpStrength ("Sharp Strength", Range(0, 4)) = 0.85
        _SharpSize ("Sharp Size", Range(0.02, 0.4)) = 0.11
        [HDR] _RimBoost ("Rim Boost", Color) = (1.05, 0.92, 1.02, 1)
        _RimBoostStrength ("Rim Boost Strength", Range(0, 3)) = 0.72
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.6
        [HDR] _FilmColor ("Pink Rim Tint", Color) = (1.05, 0.35, 0.82, 1)
        _FilmStrength ("Pink Rim Strength", Range(0, 1.5)) = 0.34
        _Fade ("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+12"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BubbleGloss"
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BroadSpec;
                half _BroadStrength;
                half _BroadSize;
                half _BroadAspect;
                half4 _SharpSpec;
                half _SharpStrength;
                half _SharpSize;
                half4 _RimBoost;
                half _RimBoostStrength;
                half _FresnelPower;
                half4 _FilmColor;
                half _FilmStrength;
                half _Fade;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.viewDirWS = normalize(GetWorldSpaceViewDir(positionWS));
                return output;
            }

            float GaussianBlob(float2 delta, float sigma)
            {
                float safeSigma = max(0.02, sigma);
                return exp(-dot(delta, delta) / (2.0 * safeSigma * safeSigma));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);
                float fresnel = pow(saturate(1.0 - dot(n, v)), _FresnelPower);

                float3 nVS = TransformWorldToViewDir(n);
                float2 uv = nVS.xy;
                float2 dir = uv / max(length(uv), 1e-5);

                // Candy language from PinkBlob / ribbon: one large top oval + a smaller rounder spot.
                float2 broadDelta = uv - float2(-0.34, 0.40);
                broadDelta.x /= max(0.4, _BroadAspect);
                float broad = GaussianBlob(broadDelta, _BroadSize);

                float2 sharpDelta = uv - float2(-0.30, 0.48);
                float sharp = GaussianBlob(sharpDelta, _SharpSize);

                half3 spec =
                    _BroadSpec.rgb * _BroadSpec.a * broad * _BroadStrength +
                    _SharpSpec.rgb * sharp * _SharpStrength;

                // Cream rim, a little brighter on the upper-left like the jelly glyphs.
                float rimBias = saturate(dot(dir, float2(-0.55, 0.65)) * 0.35 + 0.75);
                half3 rim = _RimBoost.rgb * fresnel * _RimBoostStrength * rimBias;

                float2 filmAxis = float2(-0.55, -0.62);
                float filmSide = saturate(dot(dir, filmAxis) * 0.65 + 0.42);
                float film = saturate((fresnel - 0.16) * 2.3) * pow(filmSide, 1.9) * _FilmStrength;
                half3 filmRgb = _FilmColor.rgb * film;

                half3 rgb = spec + rim + filmRgb;
                half a = saturate(
                    fresnel * 0.62 +
                    broad * _BroadSpec.a * 0.6 +
                    sharp * 0.55 +
                    film * 0.2);
                half fade = saturate(_Fade);
                return half4(rgb * fade, a * fade);
            }
            ENDHLSL
        }
    }
}
