Shader "UI/PixelTransition"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TransitionTex ("Transition Pattern (Grayscale)", 2D) = "white" {}
        _Color ("Screen Color", Color) = (0,0,0,1)
        _Cutoff ("Cutoff", Range(0, 1)) = 0
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _TransitionTex;
            fixed4 _Color;
            float _Cutoff;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Legge il colore grigio dalla texture di transizione
                fixed4 transit = tex2D(_TransitionTex, IN.texcoord);
                
                // Matematica pura: Se il rosso (grigio) è minore del cutoff, alpha = 0 (trasparente)
                if(transit.r < _Cutoff)
                {
                    return fixed4(0,0,0,0);
                }
                
                // Altrimenti, disegna il colore a tinta unita (es. Nero)
                return IN.color;
            }
            ENDCG
        }
    }
}