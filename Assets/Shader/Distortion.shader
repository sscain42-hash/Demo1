Shader "Custom/SlashDistortionURP"
{
    Properties
    {
        [Header(Distortion Settings)]
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
        _NoiseSpeed ("Noise Speed (X,Y)", Vector) = (0.2, 0.2, 0, 0)
        _DistortionAmount ("Distortion Amount", Range(0, 0.1)) = 0.02
        
        [Header(Masking Settings)]
        _MaskTex ("Alpha Mask Texture (Slash Shape)", 2D) = "white" {}
        _DistortionStrength ("Alpha/Strength Multiplier", Range(0, 1)) = 1.0
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
        }
        LOD 100

        // Cho phép hiển thị nền đè lên background cũ
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "DistortionPass"
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
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            // Khai báo Textures
            TEXTURE2D(_MaskTex);   SAMPLER(sampler_MaskTex);
            TEXTURE2D(_NoiseTex);  SAMPLER(sampler_NoiseTex);
            
            // Texture lấy hình ảnh của màn hình trong URP
            TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _MaskTex_ST;
                float4 _NoiseTex_ST;
                float2 _NoiseSpeed;
                float _DistortionAmount;
                float _DistortionStrength;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MaskTex);
                
                // Lấy tọa độ màn hình (Screen Position)
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // 1 & 2. Panner & Noise Texture: Làm Noise trôi đi theo thời gian
                float2 noiseUV = input.uv * _NoiseTex_ST.xy + _NoiseTex_ST.zw;
                noiseUV += _Time.y * _NoiseSpeed;
                half4 noiseColor = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV);
                
                // 3. Remap: Chuyển dải màu (0..1) của Noise sang biên độ vặn xoắn (-_DistortionAmount..+_DistortionAmount)
                float2 distortion = (noiseColor.xy * 2.0 - 1.0) * _DistortionAmount;
                
                // Chuyển Screen Position sang UV hợp lệ (0..1)
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                
                // 4. Add: Cộng thêm độ vặn xoắn vào tọa độ UV của màn hình
                screenUV += distortion;
                
                // 5. Scene Color: Lấy hình ảnh phía sau với tọa độ đã bị vặn xoắn
                half4 sceneColor = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV);
                
                // 6. Alpha Mask & Multiply: Xác định vùng hiển thị dựa trên Mask và Strength
                // (Dùng giá trị đỏ .r hoặc alpha .a tùy theo tấm ảnh mask của bạn)
                half maskAlpha = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv).r; 
                
                // 7. Base Color + Alpha
                sceneColor.a = maskAlpha * _DistortionStrength;
                
                return sceneColor;
            }
            ENDHLSL
        }
    }
}