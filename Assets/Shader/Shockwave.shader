Shader "Hidden/ShockwaveDistortion"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Float) = 0
        _Strength ("Strength", Float) = 0
        _WaveWidth ("Wave Width", Float) = 0.15
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

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
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float2 _Center;
            float _Radius;
            float _Strength;
            float _WaveWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // Vector từ tâm đến pixel hiện tại
                float2 dir = uv - _Center;

                // Sửa tỷ lệ aspect để méo không bị dẹt
                dir.x *= _ScreenParams.x / _ScreenParams.y;

                float dist = length(dir);
                float2 dirNorm = dist > 0.0001 ? dir / dist : float2(0, 0);

                // Vành sóng: đỉnh tại _Radius, giảm dần theo _WaveWidth
                float ring = smoothstep(_Radius - _WaveWidth, _Radius, dist)
                           * (1.0 - smoothstep(_Radius, _Radius + _WaveWidth, dist));

                // Đẩy pixel ra ngoài theo hướng dirNorm
                float2 offset = dirNorm * ring * _Strength;

                // Sửa lại aspect cho offset
                offset.x /= _ScreenParams.x / _ScreenParams.y;

                // Sample màu tại vị trí bị đẩy
                float2 distortedUV = uv + offset;
                fixed4 col = tex2D(_MainTex, distortedUV);

                return col;
            }
            ENDCG
        }
    }
}