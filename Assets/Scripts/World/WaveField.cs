using UnityEngine;

namespace Caribbean
{
    /// <summary>
    /// Волны Герстнера. Одна и та же формула считается на CPU (для плавучести кораблей,
    /// ядер, камеры) и в шейдере воды (Resources/Shaders/CaribbeanWater.shader),
    /// поэтому корабли качаются ровно на тех волнах, которые видно.
    /// </summary>
    public static class WaveField
    {
        struct Wave { public float dx, dz, k, amp, q, omega, phase; }

        static Wave[] waves;
        /// <summary>Время волн (секунды). Обновляется океаном каждый кадр.</summary>
        public static float Time;
        /// <summary>Множитель высоты волн (1 — обычное море, больше — шторм).</summary>
        public static float Scale = 1f;
        public const float MaxAmplitude = 1.15f;

        static WaveField() { Setup(); }

        public static void Setup()
        {
            waves = new Wave[4];
            Set(0, 25f, 58f, 0.55f, 0.45f, 0.1f);
            Set(1, 110f, 31f, 0.32f, 0.5f, 1.7f);
            Set(2, -35f, 17f, 0.17f, 0.55f, 3.1f);
            Set(3, 70f, 9f, 0.08f, 0.5f, 4.4f);
        }

        static void Set(int i, float dirDeg, float wavelength, float amp, float steepness, float phase)
        {
            float a = dirDeg * Mathf.Deg2Rad;
            float k = 2f * Mathf.PI / wavelength;
            waves[i] = new Wave
            {
                dx = Mathf.Sin(a),
                dz = Mathf.Cos(a),
                k = k,
                amp = amp,
                q = steepness / (k * amp * 4f),
                omega = Mathf.Sqrt(9.81f * k) * 0.85f,
                phase = phase
            };
        }

        /// <summary>Смещение точки исходной сетки (x0, z0): x, y, z.</summary>
        public static Vector3 Displacement(float x0, float z0)
        {
            float x = 0f, y = 0f, z = 0f;
            for (int i = 0; i < waves.Length; i++)
            {
                var w = waves[i];
                float a = w.amp * Scale;
                float th = w.k * (w.dx * x0 + w.dz * z0) - w.omega * Time + w.phase;
                float s = Mathf.Sin(th), c = Mathf.Cos(th);
                x += w.q * a * w.dx * c;
                z += w.q * a * w.dz * c;
                y += a * s;
            }
            return new Vector3(x, y, z);
        }

        /// <summary>Высота воды в мировой точке (x, z).</summary>
        public static float Height(float x, float z)
        {
            float px = x, pz = z;
            for (int it = 0; it < 3; it++)
            {
                var d = Displacement(px, pz);
                px = x - d.x;
                pz = z - d.z;
            }
            return Displacement(px, pz).y;
        }

        public static float Height(Vector3 p) => Height(p.x, p.z);

        /// <summary>Нормаль поверхности воды (по конечным разностям).</summary>
        public static Vector3 Normal(float x, float z, float e = 0.8f)
        {
            float hx = Height(x + e, z) - Height(x - e, z);
            float hz = Height(x, z + e) - Height(x, z - e);
            return new Vector3(-hx, 2f * e, -hz).normalized;
        }

        /// <summary>Передать параметры волн шейдеру воды.</summary>
        public static void PushToShaders()
        {
            for (int i = 0; i < waves.Length; i++)
            {
                var w = waves[i];
                Shader.SetGlobalVector("_CaribWaveA" + i, new Vector4(w.dx, w.dz, w.k, w.amp));
                Shader.SetGlobalVector("_CaribWaveB" + i, new Vector4(w.q, w.omega, w.phase, 0f));
            }
            Shader.SetGlobalFloat("_CaribWaveTime", Time);
            Shader.SetGlobalFloat("_CaribWaveScale", Scale);
        }
    }
}
