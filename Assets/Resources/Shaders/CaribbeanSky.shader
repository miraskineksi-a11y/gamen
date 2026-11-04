// Небо: градиент + солнце + лёгкие облачные полосы. Используется как Skybox.
Shader "Caribbean/Sky"
{
    Properties { _Clouds ("Clouds", Range(0, 1)) = 0.35 }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CaribbeanCommon.cginc"
            float _Clouds;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float3 col = y > 0 ? lerp(_CaribHorizonColor.rgb, _CaribSkyColor.rgb, pow(saturate(y), 0.55))
                                   : lerp(_CaribHorizonColor.rgb, _CaribFogColor.rgb * 0.85, saturate(-y * 4.0));
                if (y > 0.01)
                {
                    float2 uv = d.xz / (y + 0.15) * 1.6;
                    float c = vnoise(uv * 1.3 + float2(_Time.x * 0.6, 0)) * 0.6 + vnoise(uv * 3.1) * 0.4;
                    c = smoothstep(1.0 - _Clouds, 1.0, c) * saturate(y * 3.0);
                    col = lerp(col, float3(1, 1, 1), c * 0.75);
                }
                float3 l = normalize(_CaribSunDir.xyz);
                float sd = saturate(dot(d, l));
                col += _CaribSunColor.rgb * (pow(sd, 900.0) * 4.0 + pow(sd, 12.0) * 0.25);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
