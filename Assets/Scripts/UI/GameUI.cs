using UnityEngine;

namespace Caribbean
{
    /// <summary>
    /// Весь интерфейс игры (IMGUI): меню, HUD, порт, карта, пауза, экраны итогов.
    /// Масштабируется под высоту экрана (эталон 1080 пикселей).
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public bool showHelp;
        float W, H;
        string[] rumors;
        int rumorIndex;
        float uiTime;

        static readonly string[] Story =
        {
            "Капитан! Перед смертью старый боцман открыл тебе тайну: знаменитый пират <b>Кровавая Борода</b> " +
            "зарыл несметные сокровища на <b>Острове Черепа</b>.",
            "Его карту разорвали на три части и спрятали на островах — на <b>Острове Попугаев</b>, " +
            "на <b>Черепашьем острове</b> и на <b>Острове Висельника</b>. Красные кресты на карте (M) — места кладов.",
            "Собери все три части, одолей <b>Кракена</b>, что сторожит бухту Острова Черепа, — и золото Кровавой Бороды твоё!",
            "Берегись пиратских кораблей и морского змея. В порту <b>Санта-Роза</b> можно починить корабль и купить улучшения.",
        };

        static readonly string[] HelpSea =
        {
            "<b>В море</b>",
            "W / S — поднять / убрать паруса (4 положения)",
            "A / D — руль",
            "Мышь — осмотреться, колесо — приблизить",
            "ЛКМ — залп бортом, куда смотрит камера",
            "Q / E — залп левым / правым бортом",
            "ПКМ (удерживать) — траектория ядер; наклон камеры = дальность",
            "F — высадиться на остров / войти в порт",
            "M — карта,  Esc — пауза,  F1 — подсказка",
        };

        static readonly string[] HelpFoot =
        {
            "<b>На суше</b>",
            "WASD — идти, Shift — бежать, Пробел — прыжок",
            "ЛКМ — удар саблей, ПКМ — блок",
            "E (удерживать) — копать клад у красного креста",
            "F — вернуться на корабль (у шлюпки)",
        };

        void Start()
        {
            rumors = new[]
            {
                "«Говорят, на Острове Попугаев среди руин зарыт обрывок старой карты».",
                "«Черепаший остров кишит пиратами. Их лагерь — у скалы, похожей на черепаху».",
                "«На Острове Висельника пираты построили частокол. Что они там прячут?»",
                "«В водах у Острова Туманов видели морского змея! Держитесь от него подальше… или стреляйте, когда вынырнет».",
                "«Кракен бьёт щупальцами туда, где вода вспенилась кольцом. Не стойте на месте!»",
                "«Пиратские корабли стреляют, когда вы у них на траверзе. Заходите с носа или кормы!»",
                "«Против ветра корабль идёт медленнее. Флаг на мачте показывает, куда дует ветер».",
            };
        }

        void Update()
        {
            uiTime += Time.unscaledDeltaTime;
            var g = Game.I;
            if (g == null) return;
            if (GameInput.KeyDown(GKey.F1) || (GameInput.KeyDown(GKey.H) && g.State != GameState.MainMenu)) showHelp = !showHelp;
        }

        void OnGUI()
        {
            var g = Game.I;
            if (g == null) return;
            UiKit.Init();
            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            W = Screen.width / s;
            H = 1080f;
            GUI.depth = 0;

            if (GameInput.Unavailable)
            {
                UiKit.PanelBox(new Rect(W / 2 - 520, 20, 1040, 90));
                UiKit.Shadow(new Rect(W / 2 - 500, 28, 1000, 80),
                    "Ввод недоступен: Edit → Project Settings → Player → Active Input Handling = «Both», затем перезапустите Unity.", UiKit.Center, UiColors.Red);
            }

            switch (g.State)
            {
                case GameState.MainMenu: DrawMainMenu(g); break;
                case GameState.Intro: DrawIntro(g); break;
                case GameState.Sailing: DrawSeaHud(g); break;
                case GameState.OnFoot: DrawFootHud(g); break;
                case GameState.Port: DrawPort(g); break;
                case GameState.Map: DrawMap(g); break;
                case GameState.Paused: DrawPause(g); break;
                case GameState.ShipLost: DrawShipLost(g); break;
                case GameState.CaptainDown: DrawCaptainDown(g); break;
                case GameState.Victory: DrawVictory(g); break;
            }
            if (g.State == GameState.Sailing || g.State == GameState.OnFoot)
            {
                DrawToasts(g);
                DrawBanner(g);
                if (showHelp) DrawHelp(g.State == GameState.OnFoot);
            }
            if (g.DamageFlash > 0f) UiKit.Tex(new Rect(0, 0, W, H), UiKit.Vignette, new Color(0.8f, 0.05f, 0.02f, g.DamageFlash * 0.7f));
            if (g.Fade > 0f) UiKit.Rect(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, g.Fade));
        }

        // ================================================================== меню

        void DrawMainMenu(Game g)
        {
            UiKit.Tex(new Rect(0, 0, W, H), UiKit.Vignette, new Color(0f, 0f, 0f, 0.55f));
            float pulse = 1f + Mathf.Sin(uiTime * 1.5f) * 0.015f;
            var tr = new Rect(W / 2 - 700, 120, 1400, 110);
            UiKit.Shadow(tr, "КЛАД КРОВАВОЙ БОРОДЫ", UiKit.Title, UiColors.Gold * pulse);
            UiKit.Shadow(new Rect(W / 2 - 600, 225, 1200, 50), "Приключение в Карибском море", UiKit.Center, UiColors.Text);

            float bw = 420, bh = 64, x = W / 2 - bw / 2, y = 380;
            if (UiKit.Btn(new Rect(x, y, bw, bh), "Новая игра")) g.StartNewGame();
            y += bh + 18;
            if (UiKit.Btn(new Rect(x, y, bw, bh), "Продолжить", SaveData.Exists)) g.ContinueGame();
            y += bh + 18;
            if (UiKit.Btn(new Rect(x, y, bw, bh), showHelp ? "Скрыть управление" : "Управление")) showHelp = !showHelp;
            y += bh + 18;
            if (UiKit.Btn(new Rect(x, y, bw, bh), "Выход")) g.Quit();
            if (showHelp) DrawHelp(false, true);
            UiKit.Shadow(new Rect(20, H - 44, 900, 30), "Модели: Blender (процедурно, Python) • Игра: Unity • Музыка и звуки синтезируются кодом", UiKit.Small);
        }

        void DrawIntro(Game g)
        {
            var r = new Rect(W / 2 - 520, 150, 1040, 700);
            UiKit.ParchmentBox(r);
            GUI.Label(new Rect(r.x, r.y + 30, r.width, 60), "Легенда о Кровавой Бороде", Styled(UiKit.Big, UiColors.Ink));
            float y = r.y + 120;
            foreach (var p in Story)
            {
                var c = new GUIContent(p);
                float h = UiKit.Ink.CalcHeight(c, r.width - 120);
                GUI.Label(new Rect(r.x + 60, y, r.width - 120, h), c, UiKit.Ink);
                y += h + 18;
            }
            if (UiKit.Btn(new Rect(W / 2 - 200, r.yMax - 100, 400, 64), "Поднять паруса!") || GameInput.KeyDown(GKey.Enter))
                g.BeginVoyage();
        }

        static GUIStyle Styled(GUIStyle s, Color c)
        {
            var n = new GUIStyle(s);
            n.normal.textColor = c;
            return n;
        }

        void DrawPause(Game g)
        {
            UiKit.Rect(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.45f));
            var r = new Rect(W / 2 - 300, 220, 600, 560);
            UiKit.PanelBox(r);
            UiKit.Shadow(new Rect(r.x, r.y + 24, r.width, 60), "Пауза", UiKit.Big);
            float y = r.y + 110, x = r.x + 90, bw = 420;
            if (UiKit.Btn(new Rect(x, y, bw, 58), "Продолжить")) g.Resume();
            y += 74;
            if (UiKit.Btn(new Rect(x, y, bw, 58), showHelp ? "Скрыть управление" : "Управление")) showHelp = !showHelp;
            y += 84;
            var am = AudioManager.I;
            if (am != null)
            {
                UiKit.Shadow(new Rect(x, y, bw, 30), "Музыка: " + Mathf.RoundToInt(am.musicVolume * 100) + "%", UiKit.Body);
                if (UiKit.Btn(new Rect(x + 260, y - 6, 70, 42), "–")) am.musicVolume = Mathf.Max(0f, am.musicVolume - 0.1f);
                if (UiKit.Btn(new Rect(x + 340, y - 6, 70, 42), "+")) am.musicVolume = Mathf.Min(1f, am.musicVolume + 0.1f);
                y += 56;
            }
            UiKit.Shadow(new Rect(x, y, bw, 30), "Мышь: " + GameInput.MouseSensitivity.ToString("0.0"), UiKit.Body);
            if (UiKit.Btn(new Rect(x + 260, y - 6, 70, 42), "–")) GameInput.MouseSensitivity = Mathf.Max(0.2f, GameInput.MouseSensitivity - 0.1f);
            if (UiKit.Btn(new Rect(x + 340, y - 6, 70, 42), "+")) GameInput.MouseSensitivity = Mathf.Min(3f, GameInput.MouseSensitivity + 0.1f);
            y += 70;
            if (UiKit.Btn(new Rect(x, y, bw, 58), "Сохранить и выйти в меню")) g.QuitToMenu();
            if (showHelp) DrawHelp(g.PausedFrom == GameState.OnFoot, true);
        }

        void DrawHelp(bool foot, bool both = false)
        {
            var lines = both ? Concat(HelpSea, HelpFoot) : foot ? HelpFoot : HelpSea;
            float h = 34 * lines.Length + 30;
            var r = new Rect(W - 640, H - h - 120, 610, h);
            UiKit.PanelBox(r);
            for (int i = 0; i < lines.Length; i++)
                UiKit.Shadow(new Rect(r.x + 20, r.y + 14 + i * 34, r.width - 40, 34), lines[i], UiKit.Body, i == 0 || lines[i].StartsWith("<b>") ? UiColors.Gold : UiColors.Text);
        }

        static string[] Concat(string[] a, string[] b)
        {
            var r = new string[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        // ================================================================== HUD

        void DrawGold(Game g, float x, float y)
        {
            UiKit.Tex(new Rect(x, y, 34, 34), UiKit.Coin, Color.white);
            UiKit.Shadow(new Rect(x + 44, y - 2, 200, 40), g.Data.gold.ToString(), UiKit.H2, UiColors.Gold);
        }

        void DrawObjective(Game g, float x, float y, float w)
        {
            var c = new GUIContent(g.Objective);
            float h = UiKit.Body.CalcHeight(c, w - 32) + 50;
            var r = new Rect(x, y, w, h);
            UiKit.PanelBox(r);
            UiKit.Shadow(new Rect(x + 16, y + 8, w - 32, 26), "Цель", UiKit.Small, UiColors.Gold);
            UiKit.Shadow(new Rect(x + 16, y + 32, w - 32, h - 36), g.Objective, UiKit.Body);
            // части карты
            for (int i = 0; i < 3; i++)
            {
                var pr = new Rect(x + w - 112 + i * 32, y + 9, 26, 22);
                UiKit.Tex(pr, UiKit.Parchment, g.Data.fragments[i] ? Color.white : new Color(1f, 1f, 1f, 0.2f));
            }
        }

        void DrawSeaHud(Game g)
        {
            var ship = g.PlayerShip;
            if (ship == null) return;
            var r = new Rect(20, 20, 440, 132);
            UiKit.PanelBox(r);
            UiKit.Shadow(new Rect(36, 28, 300, 34), "«Ласточка»", UiKit.H2, UiColors.Gold);
            UiKit.Bar(new Rect(36, 66, 404, 24), ship.Health01, Color.Lerp(UiColors.Red, UiColors.Green, ship.Health01),
                "Корпус " + Mathf.CeilToInt(ship.health) + " / " + Mathf.RoundToInt(ship.stats.maxHealth));
            DrawGold(g, 300, 26);
            // паруса и скорость
            UiKit.Shadow(new Rect(36, 100, 120, 40), "Паруса", UiKit.Small);
            for (int i = 0; i < 3; i++)
                UiKit.Rect(new Rect(110 + i * 30, 108, 24, 18), i < ship.sailLevel ? UiColors.Text : new Color(1f, 1f, 1f, 0.15f));
            UiKit.Shadow(new Rect(220, 100, 230, 40), (ship.speed * 1.94f).ToString("0.0") + " узл.", UiKit.Small, UiColors.Text);

            DrawObjective(g, 20, 166, 440);
            DrawCompass(g);
            DrawMinimap(g);
            DrawEnemyBars(g);
            DrawBoss(g);

            // перезарядка
            var pc = g.PlayerCtrl;
            float bw = 220, by = H - 70;
            for (int side = -1; side <= 1; side += 2)
            {
                float bx = W / 2 + (side < 0 ? -bw - 14 : 14);
                bool aimed = pc != null && pc.AimSide == side;
                var rr = new Rect(bx, by, bw, 30);
                if (aimed) UiKit.Rect(new Rect(rr.x - 3, rr.y - 3, rr.width + 6, rr.height + 6), new Color(1f, 0.8f, 0.3f, 0.7f));
                float rl = ship.Reload01(side);
                UiKit.Bar(rr, rl, rl >= 1f ? UiColors.Gold : new Color(0.6f, 0.5f, 0.35f),
                    (side < 0 ? "Левый борт (Q)" : "Правый борт (E)") + (rl >= 1f ? "" : "…"));
            }
            if (pc != null && pc.Aiming)
                UiKit.Shadow(new Rect(W / 2 - 300, by - 40, 600, 30), "Дальность: " + Mathf.RoundToInt(EstimateRange(pc.Elevation)) + " м  (наклоните камеру)", UiKit.CenterSmall, UiColors.Gold);
            DrawPrompt(g, H - 140);
            if (Time.time - g.HitMarkerTime < 0.8f)
                UiKit.Shadow(new Rect(W / 2 - 200, H / 2 + 60, 400, 40), "Попадание!", UiKit.Center, new Color(1f, 0.85f, 0.4f, 1f - (Time.time - g.HitMarkerTime) / 0.8f));
            if (!g.Data.tutorialSailing && !showHelp)
                UiKit.Shadow(new Rect(W - 520, H - 50, 500, 30), "F1 — управление", UiKit.Small);
        }

        static float EstimateRange(float elev)
        {
            float v = Ship.CannonSpeed, gr = Ship.CannonGravity, a = elev * Mathf.Deg2Rad, h = 1.5f;
            float vy = v * Mathf.Sin(a), vx = v * Mathf.Cos(a);
            float t = (vy + Mathf.Sqrt(vy * vy + 2f * gr * h)) / gr;
            return vx * t;
        }

        void DrawFootHud(Game g)
        {
            var cap = g.Captain;
            if (cap == null) return;
            var r = new Rect(20, 20, 440, 100);
            UiKit.PanelBox(r);
            UiKit.Shadow(new Rect(36, 28, 300, 34), "Капитан", UiKit.H2, UiColors.Gold);
            UiKit.Bar(new Rect(36, 66, 404, 24), cap.Health01, Color.Lerp(UiColors.Red, UiColors.Green, cap.Health01), "Здоровье " + Mathf.CeilToInt(cap.health));
            DrawGold(g, 300, 26);
            DrawObjective(g, 20, 134, 440);
            if (g.CurrentIsland != null)
            {
                UiKit.Shadow(new Rect(W / 2 - 400, 20, 800, 40), g.CurrentIsland.Def.title, UiKit.Center, UiColors.Gold);
                int n = g.CurrentIsland.EnemiesAlive;
                UiKit.Shadow(new Rect(W / 2 - 400, 56, 800, 30), n > 0 ? "Врагов на острове: " + n : "Остров зачищен", UiKit.CenterSmall);
            }
            DrawMinimap(g);
            if (cap.DigProgress >= 0f)
                UiKit.Bar(new Rect(W / 2 - 200, H / 2 + 80, 400, 30), cap.DigProgress, UiColors.Gold, "Копаем…");
            DrawPrompt(g, H - 110);
            // прицел
            UiKit.Tex(new Rect(W / 2 - 4, H / 2 - 4, 8, 8), UiKit.Circle, new Color(1f, 1f, 1f, 0.5f));
        }

        void DrawPrompt(Game g, float y)
        {
            if (!g.HasPrompt) return;
            var c = new GUIContent(g.Prompt);
            float w = Mathf.Min(1000f, UiKit.Center.CalcSize(c).x + 60f);
            var r = new Rect(W / 2 - w / 2, y, w, 50);
            UiKit.PanelBox(r);
            UiKit.Shadow(r, g.Prompt, UiKit.Center, UiColors.Text);
        }

        void DrawToasts(Game g)
        {
            float y = 310;
            for (int i = g.Toasts.Count - 1; i >= 0; i--)
            {
                var t = g.Toasts[i];
                float age = Time.unscaledTime - t.time;
                float a = Mathf.Clamp01(Mathf.Min(age * 5f, (t.duration - age) * 2f));
                var r = new Rect(W - 520, y, 500, 40);
                UiKit.Rect(r, new Color(0f, 0f, 0f, 0.45f * a));
                UiKit.Shadow(new Rect(r.x + 14, r.y + 7, r.width - 28, 30), t.text, UiKit.Body, t.color, a);
                y += 46;
                if (y > H - 200) break;
            }
        }

        void DrawBanner(Game g)
        {
            float age = Time.unscaledTime - g.BannerTime;
            if (age > 4.5f || string.IsNullOrEmpty(g.BannerTitle)) return;
            float a = Mathf.Clamp01(Mathf.Min(age * 3f, (4.5f - age) * 1.5f));
            UiKit.Shadow(new Rect(0, 230, W, 80), g.BannerTitle, UiKit.Title, UiColors.Gold, a);
            UiKit.Shadow(new Rect(0, 315, W, 40), g.BannerSubtitle, UiKit.Center, UiColors.Text, a);
        }

        void DrawCompass(Game g)
        {
            if (g.Cam == null) return;
            float cx = W / 2, w = 560;
            var r = new Rect(cx - w / 2, 14, w, 34);
            UiKit.Rect(r, new Color(0f, 0f, 0f, 0.35f));
            string[] names = { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };
            for (int i = 0; i < 8; i++)
            {
                float d = Mathf.DeltaAngle(g.Cam.Yaw, i * 45f);
                if (Mathf.Abs(d) > 70f) continue;
                float x = cx + d / 70f * (w / 2 - 20);
                UiKit.Shadow(new Rect(x - 30, r.y + 2, 60, 30), names[i], UiKit.Center, i == 0 ? UiColors.Red : UiColors.Text, 1f - Mathf.Abs(d) / 80f);
            }
            // направление на цель
            if (g.ObjectivePoint.HasValue)
            {
                var dir = g.ObjectivePoint.Value - g.Cam.transform.position;
                float d = Mathf.DeltaAngle(g.Cam.Yaw, Util.YawOf(dir));
                float x = cx + Mathf.Clamp(d, -70f, 70f) / 70f * (w / 2 - 20);
                UiKit.Rotated(new Rect(x - 10, r.yMax + 2, 20, 20), UiKit.Arrow, 180f, UiColors.Gold);
            }
            UiKit.Rect(new Rect(cx - 1, r.y, 2, r.height), new Color(1f, 1f, 1f, 0.6f));
        }

        // ---------------------------------------------------------------- миникарта
        void DrawMinimap(Game g)
        {
            const float size = 270f;
            var r = new Rect(W - size - 20, 20, size, size);
            UiKit.ParchmentBox(r);
            Vector3 center = g.PlayerFocus;
            const float range = 420f;
            float k = (size / 2 - 14) / range;
            var inner = new Rect(r.x + 8, r.y + 8, r.width - 16, r.height - 16);
            foreach (var isl in g.World.Islands)
            {
                var p = isl.transform.position - center;
                float rad = isl.Def.radius * 1.15f * k;
                var c = new Vector2(r.center.x + p.x * k, r.center.y - p.z * k);
                if (!Inside(inner, c, -rad)) continue;
                UiKit.Tex(new Rect(c.x - rad, c.y - rad, rad * 2, rad * 2), UiKit.Circle, isl.Def.mapColor);
                if (MapX(g, isl)) UiKit.Shadow(new Rect(c.x - 15, c.y - 18, 30, 30), "X", UiKit.Center, UiColors.Red);
            }
            if (g.Fleet != null)
                foreach (var s in g.Fleet.Ships)
                {
                    var p = s.transform.position - center;
                    var c = new Vector2(r.center.x + p.x * k, r.center.y - p.z * k);
                    if (Inside(inner, c, 4)) UiKit.Rotated(new Rect(c.x - 7, c.y - 7, 14, 14), UiKit.Arrow, s.transform.eulerAngles.y, UiColors.Red);
                }
            if (g.Serpent != null && g.Serpent.IsAlive && g.Serpent.Engaged)
            {
                var p = g.Serpent.HeadPosition - center;
                var c = new Vector2(r.center.x + p.x * k, r.center.y - p.z * k);
                if (Inside(inner, c, 4)) UiKit.Tex(new Rect(c.x - 6, c.y - 6, 12, 12), UiKit.Circle, UiColors.Purple);
            }
            foreach (var l in Loot.All)
            {
                if (l == null || l.kind != Loot.Kind.Barrel) continue;
                var p = l.transform.position - center;
                var c = new Vector2(r.center.x + p.x * k, r.center.y - p.z * k);
                if (Inside(inner, c, 3)) UiKit.Tex(new Rect(c.x - 4, c.y - 4, 8, 8), UiKit.Circle, UiColors.Gold);
            }
            float yaw = g.State == GameState.OnFoot && g.Captain != null ? g.Captain.transform.eulerAngles.y : g.PlayerShip != null ? g.PlayerShip.transform.eulerAngles.y : 0f;
            UiKit.Rotated(new Rect(r.center.x - 10, r.center.y - 10, 20, 20), UiKit.Arrow, yaw, Color.white);
            // ветер
            UiKit.Shadow(new Rect(r.x + 10, r.yMax + 4, 120, 26), "Ветер", UiKit.Small, UiColors.Text);
            UiKit.Rotated(new Rect(r.x + 74, r.yMax + 6, 22, 22), UiKit.Arrow, Wind.Angle, UiColors.Blue);
            UiKit.Shadow(new Rect(r.x + 120, r.yMax + 4, 160, 26), "M — карта", UiKit.Small);
        }

        static bool Inside(Rect r, Vector2 p, float pad) => p.x > r.x + pad && p.x < r.xMax - pad && p.y > r.y + pad && p.y < r.yMax - pad;

        static bool MapX(Game g, Island isl)
        {
            if (isl.Def.fragment >= 0) return !g.Data.fragments[isl.Def.fragment];
            if (isl.Def.isSkull) return g.Data.HasAllFragments && !g.Data.legendFound;
            return false;
        }

        void DrawEnemyBars(Game g)
        {
            if (g.Fleet == null || g.Cam == null) return;
            foreach (var s in g.Fleet.Ships)
            {
                var wp = s.transform.position + Vector3.up * 15f;
                if (Vector3.Distance(wp, g.Cam.transform.position) > 260f) continue;
                var sp = g.Cam.Cam.WorldToScreenPoint(wp);
                if (sp.z <= 0f) continue;
                float sc = Screen.height / 1080f;
                var p = new Vector2(sp.x / sc, (Screen.height - sp.y) / sc);
                UiKit.Shadow(new Rect(p.x - 120, p.y - 30, 240, 26), s.displayName, UiKit.CenterSmall, UiColors.Red);
                UiKit.Bar(new Rect(p.x - 70, p.y, 140, 12), s.Health01, UiColors.Red);
            }
        }

        void DrawBoss(Game g)
        {
            string name = null;
            float v = 0f;
            string extra = "";
            if (g.Kraken != null && g.Kraken.Fighting)
            {
                name = "КРАКЕН";
                v = g.Kraken.Health01;
                extra = g.Kraken.HeadVulnerable ? "Голова над водой — огонь!" : "Щупалец: " + g.Kraken.TentaclesAlive;
            }
            else if (g.Serpent != null && g.Serpent.IsAlive && g.Serpent.Engaged)
            {
                name = "МОРСКОЙ ЗМЕЙ";
                v = g.Serpent.Health01;
            }
            if (name == null) return;
            var r = new Rect(W / 2 - 330, 64, 660, 26);
            UiKit.Shadow(new Rect(r.x, r.y - 4, r.width, 30), "", UiKit.CenterSmall);
            UiKit.Bar(r, v, UiColors.Purple, name);
            if (extra.Length > 0) UiKit.Shadow(new Rect(r.x, r.yMax + 2, r.width, 28), extra, UiKit.CenterSmall, UiColors.Gold);
        }

        // ================================================================== порт

        void DrawPort(Game g)
        {
            UiKit.Rect(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.35f));
            var r = new Rect(W / 2 - 520, 90, 1040, 860);
            UiKit.ParchmentBox(r);
            GUI.Label(new Rect(r.x, r.y + 26, r.width, 60), "Порт Санта-Роза", Styled(UiKit.Big, UiColors.Ink));
            GUI.Label(new Rect(r.x, r.y + 86, r.width, 30), "Игра сохранена. Пираты в гавань не заходят.", Styled(UiKit.CenterSmall, UiColors.Ink));
            UiKit.Tex(new Rect(r.xMax - 210, r.y + 40, 34, 34), UiKit.Coin, Color.white);
            GUI.Label(new Rect(r.xMax - 168, r.y + 38, 160, 40), g.Data.gold.ToString(), Styled(UiKit.H2, UiColors.Ink));

            var d = g.Data;
            var ship = g.PlayerShip;
            float y = r.y + 150;
            int repairCost = g.RepairCost;
            Row(r, ref y, "Починить корпус", "Прочность: " + Mathf.CeilToInt(ship.health) + " / " + Mathf.RoundToInt(ship.stats.maxHealth),
                repairCost, repairCost > 0 ? "Починить" : "Цел", repairCost > 0 && d.gold >= repairCost, () => g.Repair());
            UpgradeRow(r, ref y, "Укрепить корпус", "+40 к прочности", d.hullLevel, SaveData.HullCost, () => g.BuyUpgrade(0));
            UpgradeRow(r, ref y, "Новые паруса", "+1.5 м/с к скорости и манёвренность", d.sailLevel, SaveData.SailCost, () => g.BuyUpgrade(1));
            UpgradeRow(r, ref y, "Пушки", "+урон, быстрее перезарядка, до 5 пушек на борт", d.cannonLevel, SaveData.CannonCost, () => g.BuyUpgrade(2));

            y += 10;
            UiKit.Rect(new Rect(r.x + 60, y, r.width - 120, 2), new Color(0.4f, 0.28f, 0.15f, 0.5f));
            y += 20;
            GUI.Label(new Rect(r.x + 60, y, 400, 30), "Слухи в таверне:", Styled(UiKit.H2, UiColors.Ink));
            y += 40;
            if (rumors != null)
            {
                GUI.Label(new Rect(r.x + 60, y, r.width - 280, 90), rumors[rumorIndex % rumors.Length], UiKit.Ink);
                if (UiKit.Btn(new Rect(r.xMax - 200, y, 140, 50), "Ещё")) rumorIndex++;
            }
            if (UiKit.Btn(new Rect(W / 2 - 220, r.yMax - 100, 440, 64), "Выйти в море") || GameInput.KeyDown(GKey.Escape) || GameInput.KeyDown(GKey.F))
                g.LeavePort();
        }

        void UpgradeRow(Rect r, ref float y, string title, string desc, int level, int[] costs, System.Action buy)
        {
            bool max = level >= costs.Length;
            int cost = max ? 0 : costs[level];
            Row(r, ref y, title + "  (ур. " + level + "/" + costs.Length + ")", desc, cost, max ? "Максимум" : "Купить",
                !max && Game.I.Data.gold >= cost, buy);
        }

        void Row(Rect r, ref float y, string title, string desc, int cost, string btn, bool enabled, System.Action onClick)
        {
            var row = new Rect(r.x + 50, y, r.width - 100, 92);
            UiKit.Rect(row, new Color(0.45f, 0.32f, 0.18f, 0.12f));
            GUI.Label(new Rect(row.x + 20, row.y + 10, 600, 34), title, Styled(UiKit.H2, UiColors.Ink));
            GUI.Label(new Rect(row.x + 20, row.y + 50, 600, 30), desc, UiKit.InkSmall);
            if (cost > 0)
            {
                UiKit.Tex(new Rect(row.xMax - 330, row.y + 30, 28, 28), UiKit.Coin, Color.white);
                GUI.Label(new Rect(row.xMax - 294, row.y + 27, 100, 36), cost.ToString(), Styled(UiKit.H2, UiColors.Ink));
            }
            if (UiKit.Btn(new Rect(row.xMax - 190, row.y + 18, 170, 56), btn, enabled)) onClick();
            y += 104;
        }

        // ================================================================== карта

        void DrawMap(Game g)
        {
            UiKit.Rect(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.5f));
            float size = 960f;
            var r = new Rect(W / 2 - size / 2 - 180, 60, size, size);
            UiKit.ParchmentBox(r);
            float k = (size / 2 - 40) / WorldBuilder.WorldRadius;
            var origin = g.World.Center;
            Vector2 ToMap(Vector3 p) => new Vector2(r.center.x + (p.x - origin.x) * k, r.center.y - (p.z - origin.z) * k);
            // сетка
            for (int i = -4; i <= 4; i++)
            {
                UiKit.Rect(new Rect(r.x + 30, r.center.y + i * size / 9f, r.width - 60, 1), new Color(0.4f, 0.3f, 0.15f, 0.15f));
                UiKit.Rect(new Rect(r.center.x + i * size / 9f, r.y + 30, 1, r.height - 60), new Color(0.4f, 0.3f, 0.15f, 0.15f));
            }
            // роза ветров
            UiKit.Rotated(new Rect(r.xMax - 92, r.y + 34, 26, 26), UiKit.Arrow, 0f, UiColors.Red);
            GUI.Label(new Rect(r.xMax - 64, r.y + 30, 50, 40), "С", Styled(UiKit.H2, UiColors.Ink));
            foreach (var isl in g.World.Islands)
            {
                var c = ToMap(isl.transform.position);
                float rad = Mathf.Max(10f, isl.Def.radius * 1.2f * k);
                UiKit.Tex(new Rect(c.x - rad - 3, c.y - rad - 3, rad * 2 + 6, rad * 2 + 6), UiKit.Circle, new Color(0.85f, 0.75f, 0.5f));
                UiKit.Tex(new Rect(c.x - rad, c.y - rad, rad * 2, rad * 2), UiKit.Circle, isl.Def.mapColor);
                if (MapX(g, isl)) GUI.Label(new Rect(c.x - 20, c.y - 24, 40, 40), "X", Styled(UiKit.Big, UiColors.Red));
                if (isl.Def.isSkull && !g.Data.krakenDefeated) GUI.Label(new Rect(c.x - 100, c.y + rad - 4, 200, 26), "логово Кракена", Styled(UiKit.CenterSmall, UiColors.Red));
                GUI.Label(new Rect(c.x - 140, c.y + rad + (isl.Def.isSkull && !g.Data.krakenDefeated ? 18 : 2), 280, 30), isl.Def.title, Styled(UiKit.InkCenter, UiColors.Ink));
            }
            // безопасная зона порта
            if (g.World.PortIsland != null)
            {
                var c = ToMap(g.World.PortIsland.transform.position);
                float rr = WorldBuilder.PortSafeRadius * k;
                UiKit.Tex(new Rect(c.x - rr, c.y - rr, rr * 2, rr * 2), UiKit.Ring, new Color(0.2f, 0.5f, 0.2f, 0.4f));
            }
            if (g.Fleet != null)
                foreach (var s in g.Fleet.Ships)
                {
                    if (Util.FlatDistance(s.transform.position, g.PlayerFocus) > 450f) continue;
                    var c = ToMap(s.transform.position);
                    UiKit.Rotated(new Rect(c.x - 8, c.y - 8, 16, 16), UiKit.Arrow, s.transform.eulerAngles.y, UiColors.Red);
                }
            var pp = ToMap(g.PlayerFocus);
            float yaw = g.PlayerShip != null ? g.PlayerShip.transform.eulerAngles.y : 0f;
            UiKit.Rotated(new Rect(pp.x - 13, pp.y - 13, 26, 26), UiKit.Arrow, yaw, new Color(0.1f, 0.25f, 0.6f));

            // легенда
            var lg = new Rect(r.xMax + 20, r.y, 400, size);
            UiKit.ParchmentBox(lg);
            float y = lg.y + 30;
            GUI.Label(new Rect(lg.x + 30, y, lg.width - 60, 40), "Карта Кровавой Бороды", Styled(UiKit.H2, UiColors.Ink));
            y += 50;
            for (int i = 0; i < 3; i++)
            {
                var fr = new Rect(lg.x + 40 + i * 110, y, 96, 76);
                UiKit.Tex(fr, UiKit.Parchment, g.Data.fragments[i] ? new Color(0.85f, 0.7f, 0.45f) : new Color(1f, 1f, 1f, 0.25f));
                GUI.Label(fr, g.Data.fragments[i] ? "Есть" : "?", Styled(UiKit.InkCenter, g.Data.fragments[i] ? new Color(0.2f, 0.45f, 0.15f) : UiColors.Ink));
            }
            y += 100;
            GUI.Label(new Rect(lg.x + 30, y, lg.width - 60, 140), g.Objective, UiKit.Ink);
            y += 150;
            GUI.Label(new Rect(lg.x + 30, y, 40, 30), "X", Styled(UiKit.H2, UiColors.Red));
            GUI.Label(new Rect(lg.x + 80, y + 4, lg.width - 110, 30), "часть карты или клад", UiKit.InkSmall);
            UiKit.Rotated(new Rect(lg.x + 34, y + 44, 18, 18), UiKit.Arrow, 0f, UiColors.Red);
            GUI.Label(new Rect(lg.x + 80, y + 40, lg.width - 110, 30), "пиратский корабль", UiKit.InkSmall);
            UiKit.Rotated(new Rect(lg.x + 34, y + 80, 18, 18), UiKit.Arrow, 0f, new Color(0.1f, 0.25f, 0.6f));
            GUI.Label(new Rect(lg.x + 80, y + 76, lg.width - 110, 30), "ваш корабль", UiKit.InkSmall);
            UiKit.Tex(new Rect(lg.x + 30, y + 112, 26, 26), UiKit.Ring, new Color(0.2f, 0.5f, 0.2f, 0.8f));
            GUI.Label(new Rect(lg.x + 80, y + 112, lg.width - 110, 30), "гавань — безопасно", UiKit.InkSmall);
            y += 210;
            GUI.Label(new Rect(lg.x + 30, y, lg.width - 60, 120),
                "Потоплено кораблей: " + g.Data.shipsSunk + "\nПобеждено врагов: " + g.Data.enemiesDefeated + "\nНайдено кладов: " + g.Data.treasuresFound,
                UiKit.InkSmall);
            if (UiKit.Btn(new Rect(lg.x + 60, lg.yMax - 90, lg.width - 120, 60), "Закрыть (M)")) g.ToggleMap();
        }

        // ================================================================== итоги

        void DrawShipLost(Game g)
        {
            UiKit.Rect(new Rect(0, 0, W, H), new Color(0f, 0f, 0.05f, 0.55f));
            UiKit.Shadow(new Rect(0, 260, W, 90), "Корабль пошёл ко дну!", UiKit.Title, UiColors.Red);
            UiKit.Shadow(new Rect(W / 2 - 500, 380, 1000, 80),
                "Рыбаки выловили команду и доставили в порт Санта-Роза.\nПотеряно золота: " + g.LastGoldLost, UiKit.Center);
            if (UiKit.Btn(new Rect(W / 2 - 220, 520, 440, 64), "Продолжить")) g.RespawnAtPort();
        }

        void DrawCaptainDown(Game g)
        {
            UiKit.Shadow(new Rect(0, 300, W, 90), "Капитан ранен!", UiKit.Title, UiColors.Red);
            UiKit.Shadow(new Rect(W / 2 - 500, 400, 1000, 60), "Матросы оттащили вас к шлюпке… Потеряно золота: " + g.LastGoldLost, UiKit.Center);
        }

        void DrawVictory(Game g)
        {
            UiKit.Tex(new Rect(0, 0, W, H), UiKit.Vignette, new Color(0f, 0f, 0f, 0.7f));
            float pulse = 1f + Mathf.Sin(uiTime * 2f) * 0.03f;
            UiKit.Shadow(new Rect(0, 140, W, 100), "ЛЕГЕНДАРНЫЙ КЛАД НАЙДЕН!", UiKit.Title, UiColors.Gold * pulse);
            UiKit.Shadow(new Rect(W / 2 - 520, 250, 1040, 80),
                "Золото Кровавой Бороды теперь ваше, капитан! О вас будут слагать песни во всех тавернах Карибского моря.", UiKit.Center);
            var r = new Rect(W / 2 - 300, 360, 600, 320);
            UiKit.PanelBox(r);
            var d = g.Data;
            string stats =
                "Золото: " + d.gold + "\n" +
                "Найдено кладов: " + d.treasuresFound + "\n" +
                "Потоплено кораблей: " + d.shipsSunk + "\n" +
                "Повержено монстров: " + d.monstersSlain + "\n" +
                "Побеждено врагов на суше: " + d.enemiesDefeated + "\n" +
                "Время в пути: " + Mathf.FloorToInt(d.playTime / 60f) + " мин";
            UiKit.Shadow(new Rect(r.x + 40, r.y + 30, r.width - 80, r.height - 60), stats, UiKit.Body);
            if (UiKit.Btn(new Rect(W / 2 - 460, 720, 440, 64), "Продолжить плавание")) g.ContinueAfterVictory();
            if (UiKit.Btn(new Rect(W / 2 + 20, 720, 440, 64), "Главное меню")) g.QuitToMenu();
        }
    }
}
