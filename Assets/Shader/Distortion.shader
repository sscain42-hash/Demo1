Shader "Custom/URP_ArcSmearShader"
{
    Properties
    {
        _MainTex ("Texture (Ramp/Noise)", 2D) = "white" {}
        [HDR] _BaseColor ("Color & Intensity (HDR)", Color) = (1,1,1,1)
        
        [Header(Wipe Effect)]
        _Progress ("Wipe Progress", Range(0, 1)) = 0.0
        _Softness ("Wipe Softness", Range(0.01, 0.5)) = 0.1

        [Header(Edge Softening)]
        _EdgeFeatherY ("Top/Bottom Edge Feather", Range(0.001, 0.5)) = 0.15
        _SideFeatherX ("Start/End Edge Feather", Range(0.001, 0.5)) = 0.1
    }
    SubShader
    {
        Tags { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
        }
        
        // Dùng Additive Blend cho hiệu ứng kiếm sáng rực, hoặc Alpha Blend
        Blend SrcAlpha One // Đổi thành "Blend SrcAlpha OneMinusSrcAlpha" nếu không muốn hiệu ứng phát sáng (Glow)
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float _Progress;
                float _Softness;
                float _EdgeFeatherY;
                float _SideFeatherX;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // 1. Làm mềm mép trên (Tip) và mép dưới (Base) để ẩn hoàn toàn cạnh Mesh cứng
                float featherY = smoothstep(0.0, _EdgeFeatherY, input.uv.y) * 
                                 smoothstep(1.0, 1.0 - _EdgeFeatherY, input.uv.y);

                // 2. Làm mờ nhẹ ở 2 đầu vết chém (Start / End)
                float featherX = smoothstep(0.0, _SideFeatherX, input.uv.x) * 
                                 smoothstep(1.0, 1.0 - _SideFeatherX, input.uv.x);

                // 3. Hiệu ứng Quét UV (Wipe) theo _Progress
                float wipeAlpha = 1.0 - smoothstep(_Progress - _Softness, _Progress + _Softness, input.uv.x);

                // 4. Tổng hợp màu và Alpha hòa trộn cùng Vertex Color
                half4 finalColor = texColor * _BaseColor * input.color;
                finalColor.a *= featherY * featherX * wipeAlpha;

                return finalColor;
            }
            ENDHLSL
        }
    }
}