Shader "Custom/SwordTrail"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _EmissionColor ("Emission Color", Color) = (2,2,2,1)

        [Header(Dissolve)]
        _DissolveSoftness ("Dissolve Softness", Range(0.001, 0.5)) = 0.08
        _DissolveDirection ("Direction (0=X, 1=Y)", Range(0,1)) = 1
        _DissolveInvert ("Invert Direction", Range(0,1)) = 0

        [Header(Edge Glow)]
        _EdgeColor ("Edge Color", Color) = (1, 0.6, 0.1, 1)
        _EdgeWidth ("Edge Width", Range(0, 0.3)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _EmissionColor;

            float _DissolveSoftness;
            float _DissolveDirection;
            float _DissolveInvert;

            fixed4 _EdgeColor;
            float _EdgeWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // age: 0 = mới nhất, 1 = cũ nhất (từ UV.x do script gán)
                float age = saturate(i.uv.x);

                // Chọn trục dissolve: 0 = theo X (dọc trail), 1 = theo Y (ngang lưỡi)
                float dissolveAxis = lerp(i.uv.x, i.uv.y, _DissolveDirection);
                if (_DissolveInvert > 0.5) dissolveAxis = 1.0 - dissolveAxis;

                // Ngưỡng dissolve chạy theo tuổi
                float threshold = age;

                // Vùng bị cắt: dissolveAxis < threshold
                // Softness tạo viền mềm thay vì cắt cứng
                float mask = smoothstep(threshold, threshold + _DissolveSoftness, dissolveAxis);

                // Nếu mask = 0 → đã bị dissolve → bỏ pixel
                clip(mask - 0.001);

                // Viền phát sáng ở rìa dissolve
                float edge = smoothstep(threshold, threshold + _EdgeWidth, dissolveAxis)
                           * (1.0 - smoothstep(threshold + _EdgeWidth, threshold + _EdgeWidth * 2.0, dissolveAxis));

                // Fade alpha cơ bản theo tuổi + mềm 2 mép trail
                float baseAlpha = pow(1.0 - age, 1.5);
                float edgeFade = sin(i.uv.y * 3.14159265);
                float alpha = baseAlpha * edgeFade;

                // Màu cuối
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 col = tex * _Color * i.color;
                col.rgb *= _EmissionColor.rgb;
                col.a *= alpha * mask;

                // Cộng viền sáng
                col.rgb += _EdgeColor.rgb * edge * 2.0;
                col.a += edge * 0.5;

                return col;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}