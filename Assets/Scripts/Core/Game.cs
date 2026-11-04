using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    public enum GameState { Boot, MainMenu, Intro, Sailing, OnFoot, Port, Map, Paused, ShipLost, CaptainDown, Victory }

    /// <summary>
    /// Главный класс игры. Создаёт весь мир кодом (сцена может быть пустой), управляет
    /// состояниями (меню, море, суша, порт, карта, пауза), прогрессом и сохранениями.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        public GameState State { get; private set; } = GameState.Boot;
        public GameState PausedFrom { get; private set; } = GameState.Sailing;
        public SaveData Data { get; private set; } = new SaveData();

        public Ship PlayerShip { get; private set; }
        public PlayerShip PlayerCtrl { get; private set; }
        public Captain Captain { get; private set; }
        public CameraRig Cam { get; private set; }
        public WorldBuilder World { get; private set; }
        public PirateFleet Fleet { get; private set; }
        public SeaSerpent Serpent { get; private set; }
        public Kraken Kraken { get; private set; }
        public EnvironmentSetup Env { get; private set; }
        public Island CurrentIsland { get; private set; }

        // ---- сообщения для интерфейса
        public struct ToastMsg { public string text; public Color color; public float time, duration; }
        public readonly List<ToastMsg> Toasts = new List<ToastMsg>();
        public string Prompt { get; private set; } = "";
        int promptFrame = -1;
        public bool HasPrompt => promptFrame == Time.frameCount && !string.IsNullOrEmpty(Prompt);
        public string BannerTitle { get; private set; }
        public string BannerSubtitle { get; private set; }
        public float BannerTime { get; private set; } = -100f;
        public float Fade { get; private set; }
        public float DamageFlash { get; private set; }
        public float HitMarkerTime { get; private set; } = -100f;
        public int LastGoldLost { get; private set; }
        public string Objective { get; private set; } = "";
        public Vector3? ObjectivePoint { get; private set; }

        public Vector3 PlayerFocus =>
            State == GameState.OnFoot && Captain != null ? Captain.transform.position :
            PlayerShip != null ? PlayerShip.transform.position : Vector3.zero;

        public int RepairCost => PlayerShip == null ? 0 : Mathf.CeilToInt(Mathf.Max(0f, PlayerShip.stats.maxHealth - PlayerShip.health));

        GameUI ui;
        Ocean ocean;
        GameState mapFrom = GameState.Sailing;
        float captainDownT, serpentRespawnAt = -1f, helpHideAt = -1f, autosaveT, objectiveT;
        bool transitioning;

        // Игра запускается в любой сцене: если объекта Game нет — создаём его сами.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (I == null) new GameObject("Caribbean Game").AddComponent<Game>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Application.targetFrameRate = 120;
            Palette.Init();

            new GameObject("Audio").AddComponent<AudioManager>().Build();
            Cam = CameraRig.Create();
            Env = new GameObject("Environment").AddComponent<EnvironmentSetup>();
            Env.Build(Cam.Cam);
            ocean = new GameObject("Ocean").AddComponent<Ocean>();
            ocean.follow = Cam.transform;
            ocean.Build();

            World = new GameObject("World").AddComponent<WorldBuilder>();
            World.Build();

            World.PortSpawn(out var sp, out var syaw);
            PlayerShip = Ship.Create("Ship_Player", sp, syaw, Team.Player, StatsFromData(), "Ласточка");
            PlayerShip.isPlayer = true;
            PlayerShip.anchored = true;
            PlayerShip.Sunk += OnPlayerShipSunk;
            PlayerCtrl = PlayerShip.gameObject.AddComponent<PlayerShip>();
            Captain = Captain.Create(PlayerShip.transform.position);
            Captain.AttachToShip(PlayerShip);

            Fleet = new GameObject("Pirate Fleet").AddComponent<PirateFleet>();
            Fleet.Init();
            var lair = World.SkullIsland != null && World.SkullIsland.KrakenLair != null
                ? World.SkullIsland.KrakenLair.position
                : new Vector3(0f, 0f, 520f);
            Kraken = Kraken.Spawn(new Vector3(lair.x, 0f, lair.z));

            ui = gameObject.AddComponent<GameUI>();
            GoToMenu();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
        }

        ShipStats StatsFromData() => new ShipStats
        {
            maxHealth = Data.ShipMaxHealth,
            maxSpeed = Data.ShipMaxSpeed,
            turnRate = Data.ShipTurnRate,
            cannonDamage = Data.CannonDamage,
            reload = Data.ReloadTime,
            guns = Data.GunsPerSide
        };

        void ApplyShipStats(bool keepHealth)
        {
            float hp = PlayerShip.health;
            PlayerShip.stats = StatsFromData();
            PlayerShip.SetGunCount(Data.GunsPerSide);
            PlayerShip.health = keepHealth ? Mathf.Min(hp, PlayerShip.stats.maxHealth) : PlayerShip.stats.maxHealth;
        }

        // ================================================================== меню и запуск

        void GoToMenu()
        {
            State = GameState.MainMenu;
            Time.timeScale = 1f;
            CurrentIsland = null;
            World.PortSpawn(out var sp, out var syaw);
            PlayerShip.Restore(sp, syaw);
            PlayerShip.anchored = true;
            Captain.AttachToShip(PlayerShip);
            var port = World.PortIsland != null ? World.PortIsland.transform.position : Vector3.zero;
            Cam.MenuOrbit(Vector3.Lerp(port, sp, 0.55f), 150f, 48f);
            AudioManager.SetBattle(false);
        }

        public void StartNewGame()
        {
            Data = new SaveData();
            ResetWorld();
            World.PortSpawn(out var sp, out var syaw);
            PlayerShip.Restore(sp, syaw);
            ApplyShipStats(false);
            State = GameState.Intro;
            Cam.FollowShip(PlayerShip, true);
            Cam.Pitch = 14f;
        }

        public void ContinueGame()
        {
            Data = SaveData.Load();
            ResetWorld();
            World.PortSpawn(out var sp, out var syaw);
            PlayerShip.Restore(sp, syaw);
            ApplyShipStats(false);
            if (Data.shipHealth > 0f) PlayerShip.health = Mathf.Min(Data.shipHealth, PlayerShip.stats.maxHealth);
            BeginVoyage();
            Banner("С возвращением, капитан!", "Золото: " + Data.gold + " • Частей карты: " + Data.FragmentCount + " из 3");
        }

        public void BeginVoyage()
        {
            State = GameState.Sailing;
            Time.timeScale = 1f;
            PlayerShip.anchored = false;
            Cam.FollowShip(PlayerShip, true);
            if (!Data.tutorialSailing)
            {
                Data.tutorialSailing = true;
                ui.showHelp = true;
                helpHideAt = Time.unscaledTime + 18f;
                Banner("Порт Санта-Роза", "W — поднять паруса, A/D — руль, M — карта");
            }
        }

        void ResetWorld()
        {
            foreach (var isl in World.Islands) isl.ResetIsland();
            foreach (var l in new List<Loot>(Loot.All)) if (l != null) Destroy(l.gameObject);
            foreach (var e in new List<LandEnemy>(LandEnemy.All)) if (e != null) Destroy(e.gameObject);
            foreach (var t in new List<TreasureSpot>(TreasureSpot.All)) if (t != null) Destroy(t.gameObject);
            Fleet.ResetFleet();
            Kraken.ResetBoss();
            if (Data.krakenDefeated) Kraken.SetDefeated();
            if (Serpent != null) Destroy(Serpent.gameObject);
            SpawnSerpent();
            Toasts.Clear();
            CurrentIsland = null;
        }

        void SpawnSerpent()
        {
            var territory = new Vector3(-160f, 0f, 230f);
            var pos = territory + new Vector3(UnityEngine.Random.Range(-120f, 120f), 0f, UnityEngine.Random.Range(-120f, 120f));
            Serpent = SeaSerpent.Spawn(pos, territory);
            serpentRespawnAt = -1f;
        }

        // ================================================================== цикл

        void Update()
        {
            float dt = Time.deltaTime;
            float udt = Time.unscaledDeltaTime;
            Wind.Tick(Time.time);
            DamageFlash = Mathf.MoveTowards(DamageFlash, 0f, udt * 1.6f);
            Toasts.RemoveAll(t => Time.unscaledTime - t.time > t.duration);
            if (helpHideAt > 0f && Time.unscaledTime > helpHideAt) { ui.showHelp = false; helpHideAt = -1f; }

            bool battle = false;
            switch (State)
            {
                case GameState.Sailing:
                    Data.playTime += dt;
                    if (!transitioning && GameInput.KeyDown(GKey.Escape)) Pause();
                    else if (!transitioning && GameInput.KeyDown(GKey.M)) ToggleMap();
                    battle = Fleet.AnyEngaged || (Serpent != null && Serpent.Engaged) || (Kraken != null && Kraken.Fighting);
                    break;
                case GameState.OnFoot:
                    Data.playTime += dt;
                    if (!transitioning && GameInput.KeyDown(GKey.Escape)) Pause();
                    else if (!transitioning && GameInput.KeyDown(GKey.M)) ToggleMap();
                    if (CurrentIsland != null)
                        foreach (var e in CurrentIsland.Enemies)
                            if (e != null && e.IsAlive && e.Aggro) { battle = true; break; }
                    break;
                case GameState.Map:
                    if (GameInput.KeyDown(GKey.M) || GameInput.KeyDown(GKey.Escape)) ToggleMap();
                    break;
                case GameState.Paused:
                    if (GameInput.KeyDown(GKey.Escape)) Resume();
                    break;
                case GameState.CaptainDown:
                    captainDownT -= dt;
                    if (captainDownT <= 0f) RespawnCaptain();
                    break;
            }
            AudioManager.SetBattle(battle);

            // мрачное небо у Острова Черепа
            if (World.SkullIsland != null && Env != null)
            {
                float d = Util.FlatDistance(PlayerFocus, World.SkullIsland.transform.position);
                float gloom = State == GameState.MainMenu ? 0f : Mathf.Clamp01(1f - (d - 260f) / 280f);
                if (Data.krakenDefeated) gloom *= 0.35f;
                Env.SetGloom(gloom, dt);
            }

            if (Serpent == null && serpentRespawnAt > 0f && Time.time > serpentRespawnAt) SpawnSerpent();

            if (State == GameState.Sailing || State == GameState.OnFoot)
            {
                autosaveT += dt;
                if (autosaveT > 90f) { autosaveT = 0f; SaveProgress(); }
            }
            objectiveT -= Time.unscaledDeltaTime;
            if (objectiveT <= 0f) { objectiveT = 0.25f; UpdateObjective(); }
            UpdateCursor();
        }

        void UpdateCursor()
        {
            bool locked = (State == GameState.Sailing || State == GameState.OnFoot) && !transitioning;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void UpdateObjective()
        {
            ObjectivePoint = null;
            var d = Data;
            if (!d.HasAllFragments)
            {
                Island best = null;
                float bd = float.MaxValue;
                foreach (var isl in World.Islands)
                {
                    if (isl.Def.fragment < 0 || d.fragments[isl.Def.fragment]) continue;
                    float dist = Util.FlatDistance(PlayerFocus, isl.transform.position);
                    if (dist < bd) { bd = dist; best = isl; }
                }
                Objective = "Найдите части карты Кровавой Бороды (" + d.FragmentCount + "/3)." +
                            (best != null ? " Ближайшая — " + best.Def.title + "." : "");
                if (best != null) ObjectivePoint = best.transform.position;
            }
            else if (!d.krakenDefeated)
            {
                Objective = "Карта собрана! Плывите на север к Острову Черепа и одолейте Кракена.";
                if (World.SkullIsland != null) ObjectivePoint = World.SkullIsland.transform.position;
            }
            else if (!d.legendFound)
            {
                Objective = "Высадитесь на Острове Черепа и выкопайте легендарный клад у скалы-черепа.";
                if (World.SkullIsland != null) ObjectivePoint = World.SkullIsland.Landing != null ? World.SkullIsland.Landing.position : World.SkullIsland.transform.position;
            }
            else
            {
                Objective = "Клад Кровавой Бороды ваш! Плавайте дальше: топите пиратов и ищите золото.";
            }
            if (State == GameState.OnFoot && CurrentIsland != null)
            {
                var spot = NearestOpenTreasure(CurrentIsland);
                if (spot != null)
                {
                    Objective += spot.Locked ? "" : "\nКрасный крест отмечает клад — копайте (E).";
                    if (!spot.Locked) ObjectivePoint = spot.transform.position;
                }
                else Objective += "\nЗдесь больше нечего искать — возвращайтесь к шлюпке (F).";
            }
            if (PlayerShip != null && PlayerShip.IsAlive && PlayerShip.Health01 < 0.35f && State == GameState.Sailing)
                Objective += "\nКорпус сильно повреждён — почините корабль в порту!";
        }

        static TreasureSpot NearestOpenTreasure(Island isl)
        {
            foreach (var t in isl.Treasures) if (t != null && !t.Opened) return t;
            return null;
        }

        // ================================================================== переходы

        IEnumerator Transition(Action mid)
        {
            transitioning = true;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f) { Fade = t; yield return null; }
            Fade = 1f;
            mid();
            yield return null;
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.45f) { Fade = t; yield return null; }
            Fade = 0f;
            transitioning = false;
        }

        IEnumerator After(float seconds, Action a)
        {
            yield return new WaitForSeconds(seconds);
            a();
        }

        public void GoAshore(Island isl)
        {
            if (transitioning || isl == null || isl.Landing == null) return;
            StartCoroutine(Transition(() =>
            {
                PlayerShip.sailLevel = 0;
                PlayerShip.anchored = true;
                PlayerShip.speed = 0f;
                CurrentIsland = isl;
                isl.Populate();
                var landing = isl.Landing.position;
                float facing = Util.YawOf(isl.transform.position - landing);
                Captain.PlaceOnIsland(landing + Vector3.up * 0.3f, facing);
                Cam.FollowCaptain(Captain, true);
                Cam.Yaw = facing;
                Cam.Pitch = 16f;
                State = GameState.OnFoot;
                Banner(isl.Def.title, isl.Def.description);
                if (!Data.tutorialFoot)
                {
                    Data.tutorialFoot = true;
                    ui.showHelp = true;
                    helpHideAt = Time.unscaledTime + 15f;
                }
            }));
        }

        public void BoardShip()
        {
            if (transitioning) return;
            StartCoroutine(Transition(() =>
            {
                Captain.AttachToShip(PlayerShip);
                if (CurrentIsland != null) CurrentIsland.ResetEnemiesToHome();
                CurrentIsland = null;
                PlayerShip.anchored = false;
                State = GameState.Sailing;
                Cam.FollowShip(PlayerShip, true);
                Cam.Pitch = 14f;
                SaveProgress();
            }));
        }

        public void EnterPort()
        {
            if (transitioning) return;
            StartCoroutine(Transition(() =>
            {
                World.PortSpawn(out var p, out var yaw);
                PlayerShip.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
                PlayerShip.speed = 0f;
                PlayerShip.sailLevel = 0;
                PlayerShip.anchored = true;
                State = GameState.Port;
                var port = World.PortIsland != null ? World.PortIsland.transform.position : p;
                Cam.MenuOrbit(Vector3.Lerp(port, p, 0.6f), 120f, 40f);
                SaveProgress();
                AudioManager.SetBattle(false);
            }));
        }

        public void LeavePort()
        {
            if (transitioning) return;
            State = GameState.Sailing;
            PlayerShip.anchored = false;
            PlayerShip.sailLevel = 1;
            Cam.FollowShip(PlayerShip, true);
            Cam.Pitch = 14f;
        }

        public void Repair()
        {
            int cost = RepairCost;
            if (cost <= 0 || Data.gold < cost) return;
            Data.gold -= cost;
            PlayerShip.Repair(PlayerShip.stats.maxHealth);
            AudioManager.PlayUI(Sfx.WoodHit, 0.6f, 1.3f);
            SaveProgress();
        }

        public void BuyUpgrade(int kind)
        {
            int[] costs = kind == 0 ? SaveData.HullCost : kind == 1 ? SaveData.SailCost : SaveData.CannonCost;
            int level = kind == 0 ? Data.hullLevel : kind == 1 ? Data.sailLevel : Data.cannonLevel;
            if (level >= costs.Length || Data.gold < costs[level]) return;
            Data.gold -= costs[level];
            if (kind == 0) Data.hullLevel++;
            else if (kind == 1) Data.sailLevel++;
            else Data.cannonLevel++;
            float oldMax = PlayerShip.stats.maxHealth;
            ApplyShipStats(true);
            if (kind == 0) PlayerShip.health += PlayerShip.stats.maxHealth - oldMax;
            AudioManager.PlayUI(Sfx.Coin, 0.8f, 0.9f);
            SaveProgress();
        }

        // ================================================================== пауза / карта

        public void Pause()
        {
            if (State != GameState.Sailing && State != GameState.OnFoot) return;
            PausedFrom = State;
            State = GameState.Paused;
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            State = PausedFrom;
            Time.timeScale = 1f;
        }

        public void ToggleMap()
        {
            if (State == GameState.Map)
            {
                State = mapFrom;
                Time.timeScale = 1f;
            }
            else if (State == GameState.Sailing || State == GameState.OnFoot)
            {
                mapFrom = State;
                State = GameState.Map;
                Time.timeScale = 0f;
            }
        }

        public void QuitToMenu()
        {
            if (State != GameState.MainMenu && State != GameState.Victory) SaveProgress();
            else if (State == GameState.Victory) SaveProgress();
            ResetWorld();
            GoToMenu();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnApplicationQuit()
        {
            if (State == GameState.Sailing || State == GameState.OnFoot || State == GameState.Paused || State == GameState.Map)
                SaveProgress();
        }

        public void SaveProgress()
        {
            if (PlayerShip != null && PlayerShip.IsAlive) Data.shipHealth = PlayerShip.health;
            Data.Save();
        }

        // ================================================================== события

        public void SetPrompt(string text)
        {
            Prompt = text;
            promptFrame = Time.frameCount;
        }

        public void Toast(string text, Color color, float duration = 4f)
        {
            Toasts.Add(new ToastMsg { text = text, color = color, time = Time.unscaledTime, duration = duration });
            if (Toasts.Count > 6) Toasts.RemoveAt(0);
        }

        public void Toast(string text) => Toast(text, UiColors.Text);

        public void Banner(string title, string subtitle)
        {
            BannerTitle = title;
            BannerSubtitle = subtitle;
            BannerTime = Time.unscaledTime;
        }

        public void FlashDamage() => DamageFlash = 1f;
        public void ShowHitMarker() => HitMarkerTime = Time.time;

        public void AddGold(int amount, Vector3 where)
        {
            if (amount <= 0) return;
            Data.gold += amount;
            Toast("+" + amount + " золота", UiColors.Gold, 3f);
            AudioManager.Play(Sfx.Coin, where, 1f);
        }

        void OnPlayerShipSunk(Ship s)
        {
            LastGoldLost = Data.gold / 4;
            Data.gold -= LastGoldLost;
            Banner("Корабль тонет!", "");
            StartCoroutine(After(4f, () =>
            {
                if (State == GameState.Sailing || State == GameState.Map || State == GameState.Paused)
                {
                    Time.timeScale = 1f;
                    State = GameState.ShipLost;
                }
            }));
        }

        public void RespawnAtPort()
        {
            StartCoroutine(Transition(() =>
            {
                World.PortSpawn(out var p, out var yaw);
                PlayerShip.Restore(p, yaw);
                ApplyShipStats(false);
                Captain.AttachToShip(PlayerShip);
                Kraken.ResetBoss();
                if (Data.krakenDefeated) Kraken.SetDefeated();
                State = GameState.Sailing;
                Cam.FollowShip(PlayerShip, true);
                SaveProgress();
            }));
        }

        public void OnCaptainDied()
        {
            LastGoldLost = Data.gold * 15 / 100;
            Data.gold -= LastGoldLost;
            State = GameState.CaptainDown;
            captainDownT = 3.2f;
        }

        public void OnCaptainFell()
        {
            if (CurrentIsland == null || CurrentIsland.Landing == null) return;
            var l = CurrentIsland.Landing.position;
            Captain.PlaceOnIsland(l + Vector3.up * 0.3f, Util.YawOf(CurrentIsland.transform.position - l));
        }

        void RespawnCaptain()
        {
            StartCoroutine(Transition(() =>
            {
                if (CurrentIsland != null && CurrentIsland.Landing != null)
                {
                    var l = CurrentIsland.Landing.position;
                    Captain.PlaceOnIsland(l + Vector3.up * 0.3f, Util.YawOf(CurrentIsland.transform.position - l));
                    CurrentIsland.ResetEnemiesToHome();
                    Cam.FollowCaptain(Captain, true);
                    State = GameState.OnFoot;
                }
                else
                {
                    Captain.AttachToShip(PlayerShip);
                    State = GameState.Sailing;
                    Cam.FollowShip(PlayerShip, true);
                }
            }));
            captainDownT = 999f;
        }

        public void OnPirateShipSunk(Ship s, bool brig)
        {
            Data.shipsSunk++;
            int gold = brig ? UnityEngine.Random.Range(130, 190) : UnityEngine.Random.Range(60, 100);
            Loot.SpawnBarrels(s.transform.position, gold, brig ? 4 : 3);
            Toast("Потоплен пиратский корабль " + s.displayName + "!", UiColors.Green);
            Toast("Соберите бочки с добычей", UiColors.Muted);
        }

        public void OnEnemyKilled(LandEnemy e)
        {
            Data.enemiesDefeated++;
            if (e.kind == EnemyKind.Skeleton || e.kind == EnemyKind.Crab) Data.monstersSlain++;
        }

        public void OnSerpentKilled(SeaSerpent s)
        {
            Data.monstersSlain++;
            AddGold(400, s.HeadPosition);
            Banner("Морской змей повержен!", "+400 золота");
            serpentRespawnAt = Time.time + 240f;
            StartCoroutine(After(8f, () => { if (Serpent == s) Serpent = null; }));
        }

        public void OnKrakenDefeated(Kraken k)
        {
            Data.krakenDefeated = true;
            Data.monstersSlain++;
            AddGold(800, PlayerFocus);
            Banner("Кракен повержен!", "Путь к Острову Черепа свободен");
            AudioManager.PlayUI(Sfx.Fanfare, 0.9f);
            SaveProgress();
        }

        public void OnTreasureFound(TreasureSpot t)
        {
            if (!Data.dug.Contains(t.Id)) Data.dug.Add(t.Id);
            Data.treasuresFound++;
            switch (t.Kind)
            {
                case TreasureKind.Gold:
                    AddGold(t.Gold, t.transform.position);
                    Banner("Клад найден!", "+" + t.Gold + " золота");
                    break;
                case TreasureKind.Fragment:
                    if (t.FragmentIndex >= 0 && t.FragmentIndex < 3) Data.fragments[t.FragmentIndex] = true;
                    AddGold(t.Gold, t.transform.position);
                    Banner("Часть карты найдена!", Data.FragmentCount + " из 3" + (Data.HasAllFragments ? " — карта собрана! Курс на Остров Черепа!" : ""));
                    AudioManager.PlayUI(Sfx.Fanfare, 0.7f);
                    break;
                case TreasureKind.Legend:
                    Data.legendFound = true;
                    AddGold(t.Gold, t.transform.position);
                    AudioManager.PlayUI(Sfx.Fanfare, 1f);
                    StartCoroutine(After(2.5f, () => { State = GameState.Victory; SaveProgress(); }));
                    break;
            }
            SaveProgress();
        }

        public void ContinueAfterVictory()
        {
            State = CurrentIsland != null ? GameState.OnFoot : GameState.Sailing;
        }
    }
}
