using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    /// <summary>
    /// Загрузка моделей из Assets/Resources/Models (FBX, сгенерированные в Blender).
    /// Если модель не найдена — создаётся простая заглушка, чтобы игра не падала.
    /// </summary>
    public static class Models
    {
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly HashSet<string> warned = new HashSet<string>();

        public static GameObject Prefab(string name)
        {
            if (prefabs.TryGetValue(name, out var p)) return p;
            p = Resources.Load<GameObject>("Models/" + name);
            prefabs[name] = p;
            if (p == null && warned.Add(name))
                Debug.LogWarning("Модель Models/" + name + " не найдена. Запустите Blender/build_models.py (см. README).");
            return p;
        }

        /// <summary>
        /// Создать экземпляр модели. expectedSize — ожидаемый наибольший габарит в метрах:
        /// если модель импортирована в 100 раз больше/меньше (настройки FBX), масштаб исправится.
        /// </summary>
        public static GameObject Spawn(string name, Vector3 pos, Quaternion rot, Transform parent = null, float expectedSize = 0f)
        {
            var prefab = Prefab(name);
            GameObject go;
            if (prefab != null)
            {
                go = Object.Instantiate(prefab, pos, rot, parent);
                go.name = name;
                Palette.Apply(go);
                if (expectedSize > 0f) FixScale(go, expectedSize);
            }
            else
            {
                go = Placeholder(name, expectedSize > 0 ? expectedSize : 1f);
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, rot);
            }
            return go;
        }

        static void FixScale(GameObject go, float expected)
        {
            var b = Util.WorldBounds(go);
            float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (size <= 0f) return;
            float ratio = size / expected;
            float fix = 1f;
            if (ratio > 20f) fix = 0.01f;
            else if (ratio < 0.05f) fix = 100f;
            if (fix != 1f)
            {
                go.transform.localScale *= fix;
                if (warned.Add("scale_" + go.name))
                    Debug.LogWarning($"Модель {go.name} импортирована с неверным масштабом (x{1f / fix}). Масштаб исправлен автоматически.");
            }
        }

        static GameObject Placeholder(string name, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name + " (заглушка)";
            go.transform.localScale = new Vector3(size * 0.3f, size * 0.3f, size);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.GetComponent<Renderer>().sharedMaterial = Palette.Create(Color.magenta);
            return go;
        }

        /// <summary>Скопировать часть модели (например, сегмент щупальца) как отдельный объект.</summary>
        public static GameObject ClonePart(Transform part, Transform parent)
        {
            var go = Object.Instantiate(part.gameObject, parent);
            go.name = part.name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.SetActive(true);
            return go;
        }
    }
}
