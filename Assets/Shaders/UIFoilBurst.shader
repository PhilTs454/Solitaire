Shader "Solitaire/UIFoilBurst"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _TintA ("Tint A", Color) = (0.25, 1, 0.98, 1)
        _TintB ("Tint B", Color) = (1, 0.34, 0.93, 1)
        _FoilStrength ("Foil Strength", Range(0, 4)) = 1.1
        _FoilSpeed ("Foil Speed", Float) = 1.2
        _FoilScale ("Foil Scale", Float) = 6
        _Power ("Power", Float) = 8
        _Burst ("Burst", Range(0, 1)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _ClipRect;
            fixed4 _TintA;
            fixed4 _TintB;
            float _FoilStrength;
            float _FoilSpeed;
            float _FoilScale;
            float _Power;
            float _Burst;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.worldPosition = input.position;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float4 tex = tex2D(_MainTex, input.uv);
                float2 uv = input.uv;

                // Traveling candy-foil bands (tight, not a flat wash).
                float band = sin(uv.x * _FoilScale + uv.y * _FoilScale * 0.35 + _Time.y * _FoilSpeed);
                band = pow(saturate(band * 0.5 + 0.5), max(1.0, _Power));
                float spec = pow(band, 3.0);

                float3 foil = lerp(_TintA.rgb, _TintB.rgb, band);
                foil = foil * (0.55 + spec * 1.8) * _FoilStrength;

                float dist = length(uv - 0.5);
                float burst = saturate(_Burst);
                float mask = 1.0 - smoothstep(burst, burst + 0.14, dist);

                // Sprite RGB is a bright glow plate — using it as color makes white mush.
                // Keep its alpha as the pill shape only.
                float4 col;
                col.rgb = foil * mask * tex.a;
                col.a = tex.a * input.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
