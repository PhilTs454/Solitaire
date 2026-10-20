Shader "Solitaire/VictoryBubbleInterior"
{
    Properties
    {
        [HDR] _TintA ("Inner Tint A", Color) = (0.55, 0.95, 1.0, 1)
        [HDR] _TintB ("Inner Tint B", Color) = (1.0, 0.45, 0.85, 1)
        [HDR] _Highlight ("Caustic Highlight", Color) = (1.2, 1.1, 0.95, 1)
        _Blend ("Depth Blend", Range(0, 1)) = 0.55
        _CausticScale ("Caustic Scale", Range(0.5, 8)) = 2.8
        _CausticSpeed ("Caustic Speed", Range(0, 2)) = 0.35
        _Alpha ("Alpha", Range(0, 1)) = 0.42
        _Fade ("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+8"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BubbleInterior"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Front

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
                float3 normalOS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TintA;
                half4 _TintB;
                half4 _Highlight;
                half _Blend;
                half _CausticScale;
                half _CausticSpeed;
                half _Alpha;
                half _Fade;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalOS = normalize(input.normalOS);
                output.viewDirWS = normalize(GetWorldSpaceViewDir(positionWS));
                output.positionOS = input.positionOS.xyz;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = input.normalOS;
                float3 v = normalize(input.viewDirWS);
                float3 nVS = TransformWorldToViewDir(normalize(input.normalWS));

                float facing = saturate(dot(n, v));
                float depth = saturate(length(input.positionOS) * 1.6);
                // Pearlescent fill: pale upper-left, rose lower-right — same axis as the jelly UI.
                float blush = saturate(-nVS.x * 0.28 - nVS.y * 0.38 + depth * _Blend);
                half3 base = lerp(_TintA.rgb, _TintB.rgb, blush);

                float t = _Time.y * _CausticSpeed;
                float caustic =
                    sin((n.x + t) * _CausticScale)
                    * sin((n.y * 1.3 - t * 0.7) * _CausticScale)
                    * sin((n.z * 0.8 + t * 0.5) * _CausticScale);
                caustic = pow(saturate(caustic * 0.5 + 0.5), 3.0);

                half3 rgb = base + _Highlight.rgb * caustic * (0.08 + facing * 0.12);
                half a = _Alpha * (0.4 + facing * 0.45) * (0.75 + caustic * 0.15) * saturate(_Fade);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
