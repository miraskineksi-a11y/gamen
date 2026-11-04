using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Caribbean
{
    /// <summary>
    /// Материалы игры. Цвета берутся из Assets/Resources/Palette.json — того же файла,
    /// по которому Blender-скрипты красят модели. Материалы создаются под текущий
    /// рендер-пайплайн (Built-in: Standard, URP: Universal Render Pipeline/Lit), поэтому
    /// модели не станут розовыми ни в одном из них.
    /// </summary>
    public static class Palette
    {
#pragma warning disable 0649 // поля заполняет JsonUtility
        [Serializable] class Entry { public string name; public string color; public float smoothness; public float metallic; public float emission; }
        [Serializable] class PaletteFile { public Entry[] materials; }
#pragma warning restore 0649

        static Dictionary<string, Entry> entries;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Shader lit;
        static Material particleAlpha, particleAdd;
        static Texture2D softDot;

        public static bool IsSRP => GraphicsSettings.currentRenderPipeline != null;

        public static void Init()
        {
            if (entries != null) return;
            entries = new Dictionary<string, Entry>();
            var ta = Resources.Load<TextAsset>("Palette");
            if (ta != null)
            {
                try
                {
                    var f = JsonUtility.FromJson<PaletteFile>(ta.text);
                    foreach (var e in f.materials) entries[e.name] = e;
                }
                catch (Exception ex) { Debug.LogError("Palette.json: " + ex.Message); }
            }
            else Debug.LogWarning("Resources/Palette.json не найден — используются цвета из моделей.");
            lit = FindLitShader();
        }

        static Shader FindLitShader()
        {
            Shader s = null;
            if (IsSRP)
            {
                s = Shader.Find("Universal Render Pipeline/Lit");
                if (s == null) s = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (s == null) s = Shader.Find("HDRP/Lit");
            }
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Legacy Shaders/Diffuse");
            return s;
        }

        public static bool Has(string name) { Init(); return entries.ContainsKey(name); }

        public static Color ColorOf(string name)
        {
            Init();
            if (entries.TryGetValue(name, out var e) && ColorUtility.TryParseHtmlString(e.color, out var c)) return c;
            return Color.magenta;
        }

        /// <summary>Материал палитры по имени (кэшируется).</summary>
        public static Material Get(string name)
        {
            Init();
            if (cache.TryGetValue(name, out var m) && m != null) return m;
            if (!entries.TryGetValue(name, out var e)) return null;
            m = Create(ColorOf(name), e.smoothness, e.metallic, e.emission, lit);
            m.name = name;
            cache[name] = m;
            return m;
        }

        public static Material Create(Color c, float smoothness = 0.1f, float metallic = 0f, float emission = 0f, Shader shader = null)
        {
            Init();
            var s = shader != null ? shader : lit;
            var m = new Material(s);
            SetColor(m, c);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (emission > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }

        /// <summary>
        /// Заменяет материалы модели на материалы палитры (по имени материала из Blender).
        /// Неизвестные материалы оставляем, если у них рабочий шейдер.
        /// </summary>
        public static void Apply(GameObject go)
        {
            Init();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    string key = src != null ? Util.CleanName(src.name) : "";
                    var pm = Get(key);
                    if (pm != null) { mats[i] = pm; changed = true; continue; }
                    if (src == null || src.shader == null || !src.shader.isSupported || src.shader.name == "Hidden/InternalErrorShader")
                    {
                        Color c = Color.gray;
                        if (src != null && src.HasProperty("_Color")) c = src.GetColor("_Color");
                        mats[i] = Create(c);
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        // ------------------------------------------------------------------
        // Специальные материалы (свои шейдеры работают и в Built-in, и в URP)
        // ------------------------------------------------------------------

        public static Shader LoadShader(string resName, string fallback)
        {
            var s = Resources.Load<Shader>("Shaders/" + resName);
            if (s == null || !s.isSupported) s = Shader.Find(fallback);
            return s;
        }

        public static Texture2D SoftDot
        {
            get
            {
                if (softDot != null) return softDot;
                const int n = 64;
                softDot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftDot" };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (3f - 2f * a);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                softDot.SetPixels32(px);
                softDot.Apply();
                return softDot;
            }
        }

        static Material MakeParticle(bool additive)
        {
            var s = LoadShader("CaribbeanParticle", "Sprites/Default");
            var m = new Material(s) { name = additive ? "ParticleAdd" : "ParticleAlpha" };
            m.mainTexture = SoftDot;
            if (m.HasProperty("_SrcBlend"))
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            }
            m.renderQueue = 3100;
            return m;
        }

        public static Material ParticleAlpha => particleAlpha != null ? particleAlpha : (particleAlpha = MakeParticle(false));
        public static Material ParticleAdditive => particleAdd != null ? particleAdd : (particleAdd = MakeParticle(true));

        /// <summary>Неосвещённый цветной материал (дно океана, служебные объекты).</summary>
        public static Material Unlit(Color c)
        {
            var s = LoadShader("CaribbeanUnlit", "Unlit/Color");
            var m = new Material(s);
            SetColor(m, c);
            return m;
        }
    }
}
