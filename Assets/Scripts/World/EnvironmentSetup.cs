using UnityEngine;
using UnityEngine.Rendering;

namespace Caribbean
{
    /// <summary>Солнце, небо, туман и фоновое освещение — тёплый карибский день.</summary>
    public class EnvironmentSetup : MonoBehaviour
    {
        public Light Sun { get; private set; }

        public Color skyColor = new Color(0.30f, 0.58f, 0.92f);
        public Color horizonColor = new Color(0.72f, 0.86f, 0.96f);
        public Color fogColor = new Color(0.70f, 0.84f, 0.94f);
        public Color sunColor = new Color(1.0f, 0.95f, 0.84f);
        public float fogStart = 180f, fogEnd = 950f;

        Material skyMat;
        float stormBlend; // 0 — ясно, 1 — мрачно (у Острова Черепа)

        public void Build(Camera cam)
        {
            // Солнце: используем существующий направленный свет сцены или создаём новый
            Sun = null;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                    if (l.type == LightType.Directional) { Sun = l; break; }
            if (Sun == null)
            {
                var go = new GameObject("Sun");
                Sun = go.AddComponent<Light>();
                Sun.type = LightType.Directional;
            }
            Sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            Sun.color = sunColor;
            Sun.intensity = 1.15f;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.75f;

            var skyShader = Palette.LoadShader("CaribbeanSky", "Skybox/Procedural");
            if (skyShader != null)
            {
                skyMat = new Material(skyShader) { name = "CaribbeanSky" };
                RenderSettings.skybox = skyMat;
            }
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.backgroundColor = horizonColor;
                cam.farClipPlane = 2500f;
                cam.nearClipPlane = 0.2f;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            QualitySettings.shadowDistance = 160f;
            Apply();
            // пересчитать окружающее освещение/отражения от нового неба (иначе металл может быть чёрным)
            try { DynamicGI.UpdateEnvironment(); } catch (System.Exception) { }
        }

        /// <summary>Мрачность неба (0..1) — около Острова Черепа.</summary>
        public void SetGloom(float target, float dt)
        {
            stormBlend = Mathf.MoveTowards(stormBlend, Mathf.Clamp01(target), dt * 0.25f);
        }

        void Update() => Apply();

        void Apply()
        {
            float g = stormBlend;
            var gloomSky = new Color(0.32f, 0.33f, 0.40f);
            var gloomFog = new Color(0.42f, 0.44f, 0.50f);
            var sky = Color.Lerp(skyColor, gloomSky, g);
            var hor = Color.Lerp(horizonColor, gloomFog, g);
            var fog = Color.Lerp(fogColor, gloomFog, g);
            var sun = Color.Lerp(sunColor, new Color(0.7f, 0.72f, 0.8f), g);
            float fs = Mathf.Lerp(fogStart, 60f, g), fe = Mathf.Lerp(fogEnd, 520f, g);

            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = fs;
            RenderSettings.fogEndDistance = fe;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.62f, 0.72f, 0.86f), new Color(0.42f, 0.44f, 0.5f), g);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.58f, 0.62f, 0.62f), new Color(0.36f, 0.37f, 0.4f), g);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.38f, 0.36f, 0.30f), new Color(0.2f, 0.2f, 0.22f), g);
            if (Sun != null)
            {
                Sun.color = sun;
                Sun.intensity = Mathf.Lerp(1.15f, 0.55f, g);
            }

            Vector3 toSun = Sun != null ? -Sun.transform.forward : new Vector3(0.4f, 0.7f, 0.5f).normalized;
            Shader.SetGlobalVector("_CaribSunDir", toSun);
            Shader.SetGlobalColor("_CaribSunColor", sun);
            Shader.SetGlobalColor("_CaribSkyColor", sky);
            Shader.SetGlobalColor("_CaribHorizonColor", hor);
            Shader.SetGlobalColor("_CaribFogColor", fog);
            Shader.SetGlobalVector("_CaribFogParams", new Vector4(fs, fe, 0f, 0f));
            if (skyMat != null && skyMat.HasProperty("_Clouds")) skyMat.SetFloat("_Clouds", Mathf.Lerp(0.35f, 0.75f, g));
        }
    }
}
