// Простой неосвещённый цвет с туманом игры (дно океана и служебные объекты).
Shader "Caribbean/Unlit"
{
    Properties { _Color ("Color", Color) = (0.02, 0.12, 0.2, 1) }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CaribbeanCommon.cginc"
            fixed4 _Color;
            struct v2f { float4 pos : SV_POSITION; float3 wpos : TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(lerp(_Color.rgb, _CaribFogColor.rgb * 0.6, CaribFog(i.wpos) * 0.5), 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
