// Общие параметры освещения/тумана, которые задаёт скрипт EnvironmentSetup.cs.
// Шейдеры без LightMode-тега работают и во встроенном рендере, и в URP.
#ifndef CARIBBEAN_COMMON_INCLUDED
#define CARIBBEAN_COMMON_INCLUDED

float4 _CaribSunDir;        // направление НА солнце (мировое)
float4 _CaribSunColor;
float4 _CaribSkyColor;      // цвет зенита
float4 _CaribHorizonColor;  // цвет горизонта
float4 _CaribFogColor;
float4 _CaribFogParams;     // x = начало, y = конец тумана (метры)

float CaribFog(float3 worldPos)
{
    float d = distance(_WorldSpaceCameraPos.xyz, worldPos);
    float span = max(_CaribFogParams.y - _CaribFogParams.x, 1.0);
    return saturate((d - _CaribFogParams.x) / span);
}
#endif
