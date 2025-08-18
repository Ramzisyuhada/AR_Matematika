Shader "Hidden/RotateWebcamBlit"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _RotationDeg ("Rotation (deg)", Float) = 0
        _FlipX ("Flip X (0/1)", Float) = 0
        _FlipY ("Flip Y (0/1)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // x = 1/w, y = 1/h, z = w, w = h
            float _RotationDeg;
            float _FlipX;
            float _FlipY;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            float2 RotateUV(float2 uv, float deg)
            {
                // rotate around center (0.5, 0.5)
                float rad = radians(deg);
                float s = sin(rad);
                float c = cos(rad);

                uv -= 0.5;
                float2x2 m = float2x2(c, -s, s, c);
                uv = mul(m, uv);
                uv += 0.5;

                return uv;
            }

            float2 ApplyFlip(float2 uv, float flipX, float flipY)
            {
                if (flipX > 0.5) uv.x = 1.0 - uv.x;
                if (flipY > 0.5) uv.y = 1.0 - uv.y;
                return uv;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // Apply rotation first, then flips (order chosen to match webcam expectations)
                uv = RotateUV(uv, _RotationDeg);
                uv = ApplyFlip(uv, _FlipX, _FlipY);

                // Clamp to avoid sampling outside after rotate
                uv = clamp(uv, 0.0, 1.0);

                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}
