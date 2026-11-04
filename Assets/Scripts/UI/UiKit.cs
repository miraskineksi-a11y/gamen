using UnityEngine;

namespace Caribbean
{
    public static class UiColors
    {
        public static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
        public static readonly Color Text = new Color(0.97f, 0.95f, 0.88f);
        public static readonly Color Muted = new Color(0.78f, 0.76f, 0.7f);
        public static readonly Color Ink = new Color(0.23f, 0.15f, 0.09f);
        public static readonly Color Red = new Color(0.9f, 0.3f, 0.24f);
        public static readonly Color Green = new Color(0.45f, 0.85f, 0.4f);
        public static readonly Color Blue = new Color(0.45f, 0.75f, 1f);
        public static readonly Color Purple = new Color(0.75f, 0.45f, 0.9f);
        public static readonly Color PanelDark = new Color(0.09f, 0.07f, 0.06f, 0.78f);
    }

    /// <summary>Стили и текстуры интерфейса (всё рисуется кодом).</summary>
    public static class UiKit
    {
        public static Texture2D White, Panel, Parchment, Circle, Ring, Arrow, Coin, ButtonN, ButtonH, ButtonA, ButtonD, Vignette;
        public static GUIStyle Title, H1, H2, Body, Small, Center, CenterSmall, Ink, InkCenter, InkSmall, Button, BoxPanel, BoxParchment, Big;
        static bool ready;
        static int builtForHeight;

        public static void Init()
        {
            if (!ready)
            {
                White = Solid(Color.white);
                Panel = Framed(new Color(0.1f, 0.08f, 0.06f, 0.82f), new Color(0.78f, 0.6f, 0.25f, 0.95f), 2);
                Parchment = ParchmentTex();
                Circle = Disc(64, false);
                Ring = Disc(64, true);
                Arrow = ArrowTex();
                Coin = CoinTex();
                ButtonN = Framed(new Color(0.32f, 0.2f, 0.1f, 0.95f), new Color(0.85f, 0.65f, 0.28f), 2);
                ButtonH = Framed(new Color(0.48f, 0.3f, 0.13f, 0.98f), new Color(1f, 0.85f, 0.4f), 2);
                ButtonA = Framed(new Color(0.22f, 0.13f, 0.06f, 1f), new Color(1f, 0.85f, 0.4f), 2);
                ButtonD = Framed(new Color(0.25f, 0.22f, 0.2f, 0.8f), new Color(0.45f, 0.42f, 0.38f), 2);
                Vignette = VignetteTex();
                ready = true;
            }
            if (Title != null && builtForHeight == 1) return;
            builtForHeight = 1;
            var font = GUI.skin != null ? GUI.skin.font : null;
            Title = Make(font, 74, FontStyle.Bold, UiColors.Gold, TextAnchor.MiddleCenter);
            Big = Make(font, 52, FontStyle.Bold, UiColors.Gold, TextAnchor.MiddleCenter);
            H1 = Make(font, 38, FontStyle.Bold, UiColors.Gold, TextAnchor.UpperLeft);
            H2 = Make(font, 27, FontStyle.Bold, UiColors.Text, TextAnchor.UpperLeft);
            Body = Make(font, 22, FontStyle.Normal, UiColors.Text, TextAnchor.UpperLeft);
            Small = Make(font, 18, FontStyle.Normal, UiColors.Muted, TextAnchor.UpperLeft);
            Center = Make(font, 24, FontStyle.Normal, UiColors.Text, TextAnchor.MiddleCenter);
            CenterSmall = Make(font, 19, FontStyle.Normal, UiColors.Muted, TextAnchor.MiddleCenter);
            Ink = Make(font, 23, FontStyle.Normal, UiColors.Ink, TextAnchor.UpperLeft);
            InkCenter = Make(font, 24, FontStyle.Bold, UiColors.Ink, TextAnchor.MiddleCenter);
            InkSmall = Make(font, 18, FontStyle.Normal, UiColors.Ink, TextAnchor.UpperLeft);
            foreach (var s in new[] { Body, Small, Ink, InkSmall, H2, Center, CenterSmall }) s.wordWrap = true;
            Button = new GUIStyle
            {
                font = font,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(6, 6, 6, 6),
                padding = new RectOffset(12, 12, 8, 8),
            };
            Button.normal.background = ButtonN; Button.normal.textColor = UiColors.Text;
            Button.hover.background = ButtonH; Button.hover.textColor = Color.white;
            Button.active.background = ButtonA; Button.active.textColor = UiColors.Gold;
            Button.focused.background = ButtonN; Button.focused.textColor = UiColors.Text;
            BoxPanel = new GUIStyle { border = new RectOffset(6, 6, 6, 6) };
            BoxPanel.normal.background = Panel;
            BoxParchment = new GUIStyle { border = new RectOffset(12, 12, 12, 12) };
            BoxParchment.normal.background = Parchment;
        }

        static GUIStyle Make(Font f, int size, FontStyle st, Color c, TextAnchor a)
        {
            var s = new GUIStyle { font = f, fontSize = size, fontStyle = st, alignment = a, richText = true };
            s.normal.textColor = c;
            return s;
        }

        // ---------------------------------------------------------------- textures
        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { name = "UiWhite" };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        static Texture2D Framed(Color fill, Color border, int bw)
        {
            const int n = 16;
            var t = new Texture2D(n, n) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool edge = x < bw || y < bw || x >= n - bw || y >= n - bw;
                    var c = edge ? border : Color.Lerp(fill, fill * 1.25f, y / (float)n);
                    c.a = edge ? border.a : fill.a;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        static Texture2D ParchmentTex()
        {
            const int n = 64;
            var t = new Texture2D(n, n) { wrapMode = TextureWrapMode.Clamp };
            var a = new Color(0.93f, 0.86f, 0.68f);
            var b = new Color(0.8f, 0.68f, 0.45f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float ex = Mathf.Min(x, n - 1 - x) / 12f, ey = Mathf.Min(y, n - 1 - y) / 12f;
                    float e = Mathf.Clamp01(Mathf.Min(ex, ey));
                    float noise = Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.12f;
                    var c = Color.Lerp(b, a, e * (0.9f + noise));
                    c.a = Mathf.Min(x, n - 1 - x, Mathf.Min(y, n - 1 - y)) < 1 ? 0.6f : 0.97f;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        static Texture2D Disc(int n, bool ring)
        {
            var t = new Texture2D(n, n) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * n * 0.5f);
                    if (ring) a *= Mathf.Clamp01((d - 0.78f) * n * 0.5f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }

        static Texture2D ArrowTex()
        {
            const int n = 32;
            var t = new Texture2D(n, n) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // треугольник остриём вверх (в GUI вверх — меньший y, текстура перевёрнута)
                    float u = (x + 0.5f) / n - 0.5f;
                    float v = (y + 0.5f) / n;          // 0 — низ текстуры
                    float half = (1f - v) * 0.45f;
                    bool inside = v > 0.05f && v < 0.95f && Mathf.Abs(u) < half;
                    bool notch = v < 0.35f && Mathf.Abs(u) < (0.35f - v) * 0.6f;
                    t.SetPixel(x, n - 1 - y, new Color(1f, 1f, 1f, inside && !notch ? 1f : 0f));
                }
            t.Apply();
            return t;
        }

        static Texture2D CoinTex()
        {
            const int n = 32;
            var t = new Texture2D(n, n) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    var c = d < 0.62f ? new Color(1f, 0.83f, 0.3f) : new Color(0.85f, 0.6f, 0.15f);
                    if (d > 0.5f && d < 0.62f) c = new Color(0.95f, 0.7f, 0.2f);
                    c.a = Mathf.Clamp01((1f - d) * 16f);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        static Texture2D VignetteTex()
        {
            const int n = 64;
            var t = new Texture2D(n, n) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.414f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, (d - 0.35f) / 0.65f)));
                }
            t.Apply();
            return t;
        }

        // ---------------------------------------------------------------- drawing helpers
        public static void Rect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = old;
        }

        public static void Tex(Rect r, Texture t, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, t);
            GUI.color = old;
        }

        public static void PanelBox(Rect r) => GUI.Box(r, GUIContent.none, BoxPanel);
        public static void ParchmentBox(Rect r) => GUI.Box(r, GUIContent.none, BoxParchment);

        public static void Shadow(Rect r, string text, GUIStyle s, Color? color = null, float alpha = 1f)
        {
            var c = color ?? s.normal.textColor;
            var old = s.normal.textColor;
            s.normal.textColor = new Color(0f, 0f, 0f, 0.75f * alpha);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, s);
            s.normal.textColor = new Color(c.r, c.g, c.b, c.a * alpha);
            GUI.Label(r, text, s);
            s.normal.textColor = old;
        }

        public static void Bar(Rect r, float v, Color fill, string label = null)
        {
            Rect(r, new Color(0f, 0f, 0f, 0.6f));
            var inner = new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(v), r.height - 4);
            Rect(inner, fill);
            Rect(new Rect(inner.x, inner.y, inner.width, (r.height - 4) * 0.35f), new Color(1f, 1f, 1f, 0.18f));
            if (!string.IsNullOrEmpty(label)) Shadow(r, label, CenterSmall, Color.white);
        }

        public static bool Btn(Rect r, string text, bool enabled = true)
        {
            var old = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(r, text, Button);
            GUI.enabled = old;
            if (clicked) AudioManager.PlayUI(Sfx.Click, 0.6f);
            return clicked;
        }

        /// <summary>Нарисовать текстуру, повёрнутую на angle градусов вокруг центра.</summary>
        public static void Rotated(Rect r, Texture t, float angle, Color c)
        {
            var m = GUI.matrix;
            // RotateAroundPivot ожидает точку в экранных координатах (с учётом масштаба GUI.matrix)
            GUIUtility.RotateAroundPivot(angle, m.MultiplyPoint3x4(r.center));
            Tex(r, t, c);
            GUI.matrix = m;
        }
    }
}
