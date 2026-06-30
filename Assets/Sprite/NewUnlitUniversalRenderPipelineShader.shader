Shader "URP/UI/LockOnOnTop"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Màu sắc (Color)", Color) = (1, 0, 0, 1) // Mặc định màu đỏ rực rỡ như ảnh mẫu
        _Radius ("Bán kính (Radius)", Range(0.0, 0.5)) = 0.4
        _Thickness ("Độ dày vòng (Thickness)", Range(0.0, 0.5)) = 0.05
        _Smoothness ("Độ mượt cạnh (Smoothness)", Range(0.001, 0.1)) = 0.005
        
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+100" // Đẩy hàng đợi lên cao hơn vật thể 3D thông thường
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline" = "UniversalPipeline"
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
        
        // 🔥 CHÌA KHÓA Ở ĐÂY: Luôn luôn vẽ đè lên mọi thứ, bất chấp bị Model hay tường che khuất
        ZTest Always 
        
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Radius;
                float _Thickness;
                float _Smoothness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uvCentered = input.uv - float2(0.5, 0.5);
                float dist = length(uvCentered);

                float halfThickness = _Thickness * 0.5;
                float innerRadius = _Radius - halfThickness;
                float outerRadius = _Radius + halfThickness;

                float sampleInside = smoothstep(innerRadius - _Smoothness, innerRadius, dist);
                float sampleOutside = smoothstep(outerRadius, outerRadius + _Smoothness, dist);
                
                float circleAlpha = sampleInside * (1.0 - sampleOutside);

                half4 finalColor = input.color;
                finalColor.a *= circleAlpha;

                return finalColor;
            }
            ENDHLSL
        }
    }
}