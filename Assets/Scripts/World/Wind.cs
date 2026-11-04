using UnityEngine;

namespace Caribbean
{
    /// <summary>Ветер: медленно меняет направление. Влияет на скорость кораблей и флаги.</summary>
    public static class Wind
    {
        /// <summary>Куда дует ветер (угол поворота по Y, градусы).</summary>
        public static float Angle = 20f;
        static float baseAngle = 20f;

        public static Vector3 Dir => Quaternion.Euler(0f, Angle, 0f) * Vector3.forward;

        public static void Tick(float time)
        {
            Angle = baseAngle + (Mathf.PerlinNoise(time * 0.004f, 3.3f) - 0.5f) * 220f;
        }

        /// <summary>Множитель скорости корабля (0.75 — против ветра, ~1 — бакштаг/фордевинд).</summary>
        public static float SpeedFactor(Vector3 heading)
        {
            heading.y = 0f;
            if (heading.sqrMagnitude < 1e-4f) return 1f;
            float c = Vector3.Dot(heading.normalized, Dir);
            return 0.75f + 0.2f * (c + 1f) * 0.5f + 0.1f * (1f - c * c);
        }
    }
}
