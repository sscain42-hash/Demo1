Shader "Custom/DissolveUnlitUniversalRenderPipelineShader"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}

        [Header(Dissolve Settings)]
        _DissolveMap("Dissolve Noise Map", 2D) = "white" {}
        _DissolveAmount("Dissolve Amount", Range(0.0, 1.0)) = 0.0
        _EdgeWidth("Edge Width", Range(0.0, 0.2)) = 0.05
        [HDR] _EdgeColor("Edge Color", Color) = (1, 2, 0, 1)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "TransparentCutout" 
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline" 
        }

        Pass
        {
            // Tắt Cull nếu muốn nhìn thấy cả mặt sau khi vật thể bị rỗng/tan biến
            Cull Off 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uvDissolve : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DissolveMap);    SAMPLER(sampler_DissolveMap);

            // CBUFFER bắt buộc chứa tất cả thuộc tính Material để hỗ trợ URP SRP Batcher
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _DissolveMap_ST;
                half4 _EdgeColor;
                half _DissolveAmount;
                half _EdgeWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.uvDissolve = TRANSFORM_TEX(IN.uv, _DissolveMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 1. Lấy giá trị Noise từ Dissolve Map
                half dissolveNoise = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, IN.uvDissolve).r;

                // 2. Tính khoảng cách tan biến
                half dissolveValue = dissolveNoise - _DissolveAmount;

                // 3. Loại bỏ những Pixel có giá trị < 0 (Tan biến)
                clip(dissolveValue);

                // 4. Sample Texture & Màu chính
                half4 mainColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // 5. Tạo hiệu ứng viền cháy/viền sáng (Glow Edge)
                half edgeStep = step(dissolveValue, _EdgeWidth);
                half4 finalColor = lerp(mainColor, _EdgeColor, edgeStep);

                return finalColor;
            }
            ENDHLSL
        }
    }
}