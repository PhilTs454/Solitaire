Shader "Solitaire/UISpriteBake"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always

        // No blending: the bake target is cleared to transparent and drawn once, so
        // writing straight RGBA keeps alpha unpremultiplied and avoids dark fringes.
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

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
            };

            sampler2D _MainTex;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                return tex2D(_MainTex, input.uv) * input.color;
            }
            ENDCG
        }
    }
}
