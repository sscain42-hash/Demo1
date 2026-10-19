Shader "Custom/URP_GlassAirSliceTrail"
{
    Properties
    {
        [Header(Distortion)]
        _BumpMap ("Normal / Distortion Map", 2D) = "bump" {}
        _DistortionStrength ("Distortion Amount", Range(0.0, 1.0)) = 0.04
        _NoiseStrength ("Noise Strength (Normal XY Scale)", Range(0.0, 5.0)) = 1.0

        [Header(Procedural Noise)]
        [Toggle] _UseProcNoise ("Use Procedural Noise", Float) = 0
        _NoiseScale ("Noise Scale", Range(0.0, 50.0)) = 10.0
        _NoiseSpeed ("Noise Speed", Range(0.0, 5.0)) = 1.0
        _NoiseAmount ("Noise Amount", Range(0.0, 1.0)) = 0.5

        [Header(Glass Color and Edge)]
        _GlassTint ("Glass Tint Color", Color) = (0.9, 0.95, 1.0, 1.0)
        _EdgeColor ("Edge Specular Highlight", Color) = (1.0, 1.0, 1.0, 1.0)
        _EdgePow ("Edge Sharpness", Range(1.0, 10.0)) = 3.0

        [Header(Masking)]
        _MainTex ("Trail Alpha Mask (R Channel)", 2D) = "white" {}
        _MaskBoost ("Mask Boost (Distortion)", Range(1.0, 10.0)) = 5.0

        [Header(Alpha)]
        _AlphaGamma ("Alpha Gamma (sqrt mượt)", Range(0.3, 2.0)) = 0.5

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
            half4  _GlassTint;
            half4  _EdgeColor;
            half   _DistortionStrength;
            half   _NoiseStrength;
            float  _UseProcNoise;
            half   _NoiseScale;
            half   _NoiseSpeed;
            half   _NoiseAmount;
            half   _EdgePow;
            half   _MaskBoost;
            half   _AlphaGamma;
            float  _Cull;
            float  _DebugFaces;
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
                // 1. Mask — 🔥 FIX: dùng TRANSFORM_TEX để Tiling/Offset hoạt động
                float2 maskUV = TRANSFORM_TEX(input.uv, _MainTex);
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, maskUV).r * input.color.a;
                if (mask <= 0.001) discard;

                // === DEBUG ===
                if (_DebugFaces > 0.5)
                {
                    if (!isFront) return half4(0, 0, 1, mask);
                    return half4(1, 0, 0, mask);
                }

                // 2. Sample Normal Map + khuếch đại noise
                float2 bumpUV = TRANSFORM_TEX(input.uv, _BumpMap);
                float3 normalOffset = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUV));

                normalOffset.xy *= _NoiseStrength;
                normalOffset = normalize(normalOffset);

                // 3. Procedural noise
                float2 noiseOffset = float2(0, 0);
                if (_UseProcNoise > 0.5)
                {
                    float2 noiseUV = input.uv * _NoiseScale + _Time.y * _NoiseSpeed;
                    noiseOffset = float2(
                        sin(noiseUV.x) * cos(noiseUV.y * 1.3),
                        cos(noiseUV.y) * sin(noiseUV.x * 0.7)
                    ) * _NoiseAmount;
                }

                // 4. Screen UV + distortion
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // 🔥 FIX: boost mask cho distortion — distortion đều hơn (không chỉ mạnh ở tip)
                half distortMask = saturate(mask * _MaskBoost);

                float2 distort = (normalOffset.xy + noiseOffset) * _DistortionStrength * distortMask;
                if (!isFront) distort = -distort;

                // Edge fade — tránh streak khi vượt biên màn hình
                float2 sampleUV = screenUV + distort;
                float2 edgeFade = smoothstep(0.0, 0.05, sampleUV)
                                * smoothstep(0.0, 0.05, 1.0 - sampleUV);
                distort *= edgeFade.x * edgeFade.y;

                float2 distortedUV = clamp(screenUV + distort, 0.001, 0.999);

                // 5. Scene Color
                half3 sceneColor = SampleSceneColor(distortedUV);

                // 6. Fresnel + UV edge
                float3 N = normalize(input.normalWS);
                if (!isFront) N = -N;
                float3 V = normalize(input.viewDirWS);

                float NdotV = saturate(dot(N, V));
                half rim = pow(1.0 - NdotV, _EdgePow);

                half uvEdge = pow(abs(input.uv.y - 0.5) * 2.0, _EdgePow);
                half finalEdge = max(rim, uvEdge) * mask;

                // 7. Composite — 🔥 FIX: lerp theo mask để tint tự nhiên
                half3 tintedScene = sceneColor * _GlassTint.rgb;
                half3 finalRGB = lerp(sceneColor, tintedScene, mask);
                finalRGB += _EdgeColor.rgb * finalEdge;

                // 8. Alpha — 🔥 FIX: pow(mask, _AlphaGamma) mượt hơn ở vùng thấp
                half outAlpha = pow(mask, _AlphaGamma);

                return half4(finalRGB, outAlpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}