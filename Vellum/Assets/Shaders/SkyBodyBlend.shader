// URP Unlit shader that crossfades between two textures by a GLOBAL float `_BodyBlend`
// (set from C# via Shader.SetGlobalFloat, like the project's `_GlobalDissolveRadius`).
// Used by the Act_03 celestial bodies: the sky sphere morphs Sun->Moon and the water sphere
// Moon->Sun, both driven by the same global, in sync with the sky transition (MirrorFlipDirector).
// Unlit + HDR tint so the body self-glows (bloom).
Shader "Vellum/SkyBodyBlend"
{
    Properties
    {
        _TexA ("Texture A (blend = 0)", 2D) = "white" {}
        _TexB ("Texture B (blend = 1)", 2D) = "white" {}
        [HDR] _Tint ("Tint (HDR)", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_TexA); SAMPLER(sampler_TexA);
            TEXTURE2D(_TexB); SAMPLER(sampler_TexB);

            CBUFFER_START(UnityPerMaterial)
                float4 _TexA_ST;
                float4 _TexB_ST;
                float4 _Tint;
            CBUFFER_END

            // GLOBAL (NOT in the per-material CBUFFER): set via Shader.SetGlobalFloat("_BodyBlend", ...).
            float _BodyBlend;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uvA : TEXCOORD0;
                float2 uvB : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uvA = TRANSFORM_TEX(IN.uv, _TexA);
                OUT.uvB = TRANSFORM_TEX(IN.uv, _TexB);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 a = SAMPLE_TEXTURE2D(_TexA, sampler_TexA, IN.uvA);
                half4 b = SAMPLE_TEXTURE2D(_TexB, sampler_TexB, IN.uvB);
                half4 c = lerp(a, b, saturate(_BodyBlend));
                return c * _Tint;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
