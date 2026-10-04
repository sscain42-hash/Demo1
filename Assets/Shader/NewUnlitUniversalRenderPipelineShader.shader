Shader "Custom/URP_GlassAirSliceTrail"
{
    Properties
    {
        [Header(Distortion)]
        _BumpMap ("Normal / Distortion Map", 2D) = "bump" {}
        _DistortionStrength ("Distortion Amount", Range(0.0, 0.2)) = 0.04

        [Header(Glass Color and Edge)]
        _GlassTint ("Glass Tint Color", Color) = (0.9, 0.95, 1.0, 1.0)
        _EdgeColor ("Edge Specular Highlight", Color) = (1.5, 1.5, 1.5, 1.0)
        _EdgePow ("Edge Sharpness", Range(1.0, 10.0)) = 3.0

        [Header(Masking)]
        _MainTex ("Trail Alpha Mask (R Channel)", 2D) = "white" {}

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2 // 0=Off, 1=Front, 2=Back
        [Toggle] _DebugFaces ("Debug Front/Back Faces", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+120"
            "RenderType" = "Transparent"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

        struct Attributes
        {
            float4 positionOS   : POSITION;
            float3 normalOS     : NORMAL;
            float2 uv           : TEXCOORD0;
            half4 color         : COLOR;
        };

        struct Varyings
        {
            float4 positionHCS  : SV_POSITION;
            float2 uv           : TEXCOORD0;
            float4 screenPos    : TEXCOORD1;
            float3 normalWS     : TEXCOORD3;
            float3 viewDirWS    : TEXCOORD4;
            half4 color         : COLOR;
        };

        TEXTURE2D(_MainTex);          SAMPLER(sampler_MainTex);
        TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _BumpMap_ST;
            half4 _GlassTint;
            half4 _EdgeColor;
            half _DistortionStrength;
            half _EdgePow;
            float _Cull;
            float _DebugFaces;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "FrostedGlassSlice"
            ZWrite Off
            Cull [_Cull]
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = vertexInput.positionCS;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionHCS);
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                output.color = input.color;

                return output;
            }

            half4 frag(Varyings input, bool isFront : SV_IsFrontFace) : SV_Target
            {
                // 1. Mask vệt chém
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r * input.color.a;
                if (mask <= 0.001) discard;

                // === DEBUG: tô đỏ mặt trước, xanh mặt sau ===
                if (_DebugFaces > 0.5)
                {
                    if (!isFront) return half4(0, 0, 1, mask);
                    return half4(1, 0, 0, mask);
                }

                // 2. Sample Normal Map
                float2 bumpUV = TRANSFORM_TEX(input.uv, _BumpMap);
                float3 normalOffset = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUV));

                // 3. Tọa độ màn hình + distortion (đảo hướng ở back face)
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float2 distort = normalOffset.xy * _DistortionStrength * mask;
                if (!isFront) distort = -distort; // lật hướng gợn ở mặt sau

                float2 distortedUV = screenUV + distort;

                // Clamp để tránh sample ra ngoài màn hình (gây trắng)
                distortedUV = saturate(distortedUV);

                // 4. Đọc Scene Color
                half3 sceneColor = SampleSceneColor(distortedUV);

                // 5. Fresnel + viền UV (lật normal ở back face)
                float3 N = normalize(input.normalWS);
                if (!isFront) N = -N;
                float3 V = normalize(input.viewDirWS);

                float NdotV = saturate(dot(N, V));
                half rim = pow(1.0 - NdotV, _EdgePow);

                half uvEdge = pow(abs(input.uv.y - 0.5) * 2.0, _EdgePow);
                half finalEdge = max(rim, uvEdge) * mask;

                // 6. Tổng hợp màu kính
                half3 finalRGB = sceneColor * _GlassTint.rgb;
                finalRGB += _EdgeColor.rgb * finalEdge;

                return half4(finalRGB, mask);
            }
            ENDHLSL
        }
    }

    Fallback Off
}