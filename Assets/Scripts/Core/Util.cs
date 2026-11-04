using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    /// <summary>Команда (сторона) — кто кому может наносить урон.</summary>
    public enum Team { Player, Pirates, Monsters, Neutral }

    /// <summary>Всё, что можно ранить: корабли, персонажи, монстры.</summary>
    public interface IDamageable
    {
        Team Team { get; }
        bool IsAlive { get; }
        void TakeDamage(float amount, Vector3 point, Vector3 direction, Team source);
    }

    /// <summary>Мелкие вспомогательные функции.</summary>
    public static class Util
    {
        public const int IgnoreRaycastLayer = 2;

        /// <summary>Найти потомка по точному имени (поиск в глубину).</summary>
        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>Все потомки, чьё имя начинается с prefix.</summary>
        public static List<Transform> FindAllPrefix(Transform root, string prefix, List<Transform> result = null)
        {
            if (result == null) result = new List<Transform>();
            if (root == null) return result;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name.StartsWith(prefix)) result.Add(c);
                FindAllPrefix(c, prefix, result);
            }
            return result;
        }

        /// <summary>"Wood (Instance)" / "Wood.001" / "Wood 1" -> "Wood"</summary>
        public static string CleanName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "";
            int i = n.IndexOf(" (");
            if (i >= 0) n = n.Substring(0, i);
            i = n.IndexOf('.');
            if (i >= 0) n = n.Substring(0, i);
            return n.Trim();
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursively(t.gameObject, layer);
        }

        /// <summary>Габариты всех мешей объекта в локальных координатах space.</summary>
        public static Bounds LocalBounds(Transform space, Transform target)
        {
            bool has = false;
            var b = new Bounds();
            foreach (var mf in target.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = space.InverseTransformPoint(mf.transform.TransformPoint(c));
                    if (!has) { b = new Bounds(p, Vector3.zero); has = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>Габариты рендереров в мировых координатах.</summary>
        public static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>Плавное приближение, не зависящее от FPS.</summary>
        public static float Damp(float a, float b, float lambda, float dt) => Mathf.Lerp(a, b, 1f - Mathf.Exp(-lambda * dt));
        public static Vector3 Damp(Vector3 a, Vector3 b, float lambda, float dt) => Vector3.Lerp(a, b, 1f - Mathf.Exp(-lambda * dt));
        public static float DampAngle(float a, float b, float lambda, float dt) => Mathf.LerpAngle(a, b, 1f - Mathf.Exp(-lambda * dt));

        public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Угол (в градусах, -180..180) от forward до направления на точку, по часовой стрелке.</summary>
        public static float SignedAngleTo(Transform from, Vector3 point)
        {
            var d = Flat(point - from.position);
            if (d.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(Flat(from.forward), d, Vector3.up);
        }

        public static float YawOf(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        /// <summary>Высота земли под точкой (луч сверху вниз). Возвращает false, если земли нет.</summary>
        public static bool GroundHeight(Vector3 p, out float h, float from = 80f)
        {
            if (Physics.Raycast(new Vector3(p.x, p.y + from, p.z), Vector3.down, out var hit, from + 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                h = hit.point.y;
                return true;
            }
            h = -100f;
            return false;
        }

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float SmoothStep01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    }
}
