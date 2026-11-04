// Вода: волны Герстнера в вершинном шейдере (те же формулы, что в WaveField.cs),
// прозрачность (видно песчаное мелководье у островов), блики солнца, пена на гребнях, туман.
Shader "Caribbean/Water"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.03, 0.22, 0.38, 1)
        _ShallowColor ("Shallow Color", Color) = (0.12, 0.70, 0.72, 1)
        _FoamColor ("Foam Color", Color) = (0.95, 0.98, 1.0, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.62
    }
    SubShader
    {
        Tags { "Queue"="Transparent-50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "CaribbeanCommon.cginc"

            float4 _CaribWaveA0, _CaribWaveA1, _CaribWaveA2, _CaribWaveA3; // dirX, dirZ, k, amplitude
            float4 _CaribWaveB0, _CaribWaveB1, _CaribWaveB2, _CaribWaveB3; // q, omega, phase
            float _CaribWaveTime;
            float _CaribWaveScale;
            fixed4 _DeepColor, _ShallowColor, _FoamColor;
            float _Alpha;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
                float3 nrm : TEXCOORD1;
                float crest : TEXCOORD2;
            };

            void Gerstner(float4 A, float4 B, float2 p0, inout float3 disp, inout float3 tang, inout float3 binr)
            {
                float2 D = A.xy;
                float k = A.z;
                float a = A.w * _CaribWaveScale;
                float q = B.x;
                float th = k * dot(D, p0) - B.y * _CaribWaveTime + B.z;
                float s, c;
                sincos(th, s, c);
                disp.x += q * a * D.x * c;
                disp.z += q * a * D.y * c;
                disp.y += a * s;
                float wa = k * a;
                tang += float3(-q * D.x * D.x * wa * s, D.x * wa * c, -q * D.x * D.y * wa * s);
                binr += float3(-q * D.x * D.y * wa * s, D.y * wa * c, -q * D.y * D.y * wa * s);
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 disp = float3(0, 0, 0);
                float3 t = float3(1, 0, 0);
                float3 b = float3(0, 0, 1);
                Gerstner(_CaribWaveA0, _CaribWaveB0, wp.xz, disp, t, b);
                Gerstner(_CaribWaveA1, _CaribWaveB1, wp.xz, disp, t, b);
                Gerstner(_CaribWaveA2, _CaribWaveB2, wp.xz, disp, t, b);
                Gerstner(_CaribWaveA3, _CaribWaveB3, wp.xz, disp, t, b);
                wp += disp;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                o.wpos = wp;
                o.nrm = normalize(cross(b, t));
                o.crest = disp.y;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.nrm);
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.wpos);
                if (dot(n, v) < 0) n = -n;
                float3 l = normalize(_CaribSunDir.xyz);
                float ndv = saturate(dot(n, v));
                float fres = pow(1.0 - ndv, 4.0);

                float3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(1.0 - ndv * 1.15));
                col *= 0.62 + 0.38 * saturate(dot(n, l));
                col = lerp(col, _CaribHorizonColor.rgb, fres * 0.65);

                float3 h = normalize(l + v);
                float spec = pow(saturate(dot(n, h)), 220.0) * 2.2 + pow(saturate(dot(n, h)), 30.0) * 0.12;

                float amp = 1.15 * max(_CaribWaveScale, 0.01);
                float foam = smoothstep(0.5, 0.95, i.crest / amp);
                float pat = 0.5 + 0.25 * (sin(i.wpos.x * 0.83 + _CaribWaveTime * 0.9) + sin(i.wpos.z * 0.67 - _CaribWaveTime * 0.7));
                pat += 0.2 * sin((i.wpos.x + i.wpos.z) * 2.3 + _CaribWaveTime * 1.7);
                foam *= smoothstep(0.35, 0.8, pat);
                col = lerp(col, _FoamColor.rgb, foam * 0.85);

                float alpha = lerp(_Alpha, 0.96, fres) + foam * 0.6 + spec;
                col += spec * _CaribSunColor.rgb;

                float f = CaribFog(i.wpos);
                col = lerp(col, _CaribFogColor.rgb, f);
                alpha = lerp(saturate(alpha), 1.0, f);
                return fixed4(col, saturate(alpha));
            }
            ENDCG
        }
    }
    FallBack Off
}
