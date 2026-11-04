using System;
using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    [Serializable]
    public class IslandDef
    {
        public string id, model, title, description;
        public Vector3 position;
        public float radius;
        public int fragment = -1;       // какая часть карты зарыта здесь (0..2), -1 — нет
        public bool isPort, isSkull;
        public Color mapColor = new Color(0.45f, 0.62f, 0.3f);
    }

    /// <summary>
    /// Остров: модель из Blender + маркеры (пустышки) из неё. Здесь создаются коллайдеры,
    /// огонь факелов, места кладов и враги (при приближении игрока).
    /// </summary>
    public class Island : MonoBehaviour
    {
        public static readonly List<Island> All = new List<Island>();

        public IslandDef Def;
        public Transform Landing, Anchor, PortDock, KrakenLair;
        public bool IsPort => Def != null && Def.isPort;
        public readonly List<TreasureSpot> Treasures = new List<TreasureSpot>();
        public readonly List<LandEnemy> Enemies = new List<LandEnemy>();
        readonly List<Transform> treasureMarkers = new List<Transform>();
        readonly List<Transform> spawnMarkers = new List<Transform>();
        bool populated;

        static readonly string[] MeshColliderNames =
        {
            "Terrain", "Rock", "House", "Church", "Market", "Fort", "Lighthouse", "Dock", "Tent", "Supplies",
            "FlagPole", "TurtleRock", "Palisade", "Watchtower", "Gallows", "Ruins", "Wreck", "SkullRock", "Castaway"
        };

        public static Island Create(IslandDef def)
        {
            var go = Models.Spawn(def.model, def.position, Quaternion.identity, null, def.radius * 3f);
            go.name = def.model;
            var isl = go.AddComponent<Island>();
            isl.Def = def;
            isl.Init();
            return isl;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Init()
        {
            var children = new List<Transform>();
            foreach (Transform c in transform) children.Add(c);
            foreach (var c in children)
            {
                string n = c.name;
                if (n == "Landing") Landing = c;
                else if (n == "Anchor") Anchor = c;
                else if (n == "PortDock") PortDock = c;
                else if (n == "KrakenLair") KrakenLair = c;
                else if (n == "LighthouseLamp")
                {
                    var l = new GameObject("LighthouseLight").AddComponent<Light>();
                    l.transform.SetParent(c, false);
                    l.type = LightType.Point;
                    l.range = 40f;
                    l.intensity = 2.2f;
                    l.color = new Color(1f, 0.85f, 0.5f);
                }
                else if (n.StartsWith("Treasure_")) treasureMarkers.Add(c);
                else if (n.StartsWith("Spawn_")) spawnMarkers.Add(c);
                else if (n.StartsWith("Torch"))
                {
                    Fx.Fire(c, new Vector3(0f, 2.15f, 0f), 0.45f, false);
                    Fx.FireLight(c, new Vector3(0f, 2.4f, 0f), 7f, 1.4f);
                }
                else if (n.StartsWith("Campfire"))
                {
                    Fx.Fire(c, new Vector3(0f, 0.35f, 0f), 1.1f);
                    Fx.FireLight(c, new Vector3(0f, 1.2f, 0f), 11f, 1.8f);
                }
                AddCollider(c);
            }
        }

        static void AddCollider(Transform c)
        {
            var mf = c.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            string n = c.name;
            if (n.StartsWith("Palm"))
            {
                var cap = c.gameObject.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0f, 2f, 0f);
                cap.radius = 0.35f;
                cap.height = 4f;
                return;
            }
            if (n.StartsWith("DeadTree"))
            {
                var cap = c.gameObject.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0f, 1.5f, 0f);
                cap.radius = 0.3f;
                cap.height = 3f;
                return;
            }
            foreach (var prefix in MeshColliderNames)
            {
                if (!n.StartsWith(prefix)) continue;
                var mc = c.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                return;
            }
        }

        public Transform FindMarker(string name)
        {
            foreach (var m in treasureMarkers) if (m.name == name) return m;
            foreach (var m in spawnMarkers) if (m.name == name) return m;
            return null;
        }

        /// <summary>Создать клады и врагов (один раз, когда игрок рядом).</summary>
        public void Populate()
        {
            if (populated) return;
            populated = true;
            var data = Game.I != null ? Game.I.Data : null;
            foreach (var m in treasureMarkers)
            {
                string id = Def.id + "/" + m.name;
                if (data != null && data.dug.Contains(id)) continue;
                TreasureKind kind = m.name == "Treasure_Main" ? TreasureKind.Fragment : m.name == "Treasure_Legend" ? TreasureKind.Legend : TreasureKind.Gold;
                if (kind == TreasureKind.Fragment && (Def.fragment < 0 || (data != null && data.fragments[Def.fragment]))) kind = TreasureKind.Gold;
                int gold = kind == TreasureKind.Legend ? 2500 : kind == TreasureKind.Fragment ? 220 : UnityEngine.Random.Range(140, 280);
                Treasures.Add(TreasureSpot.Create(this, m, id, kind, gold, Def.fragment));
            }
            int k = 0;
            foreach (var s in spawnMarkers)
            {
                EnemyKind kind = EnemyKind.Pirate;
                if (s.name.StartsWith("Spawn_Crab")) kind = EnemyKind.Crab;
                else if (s.name.StartsWith("Spawn_Skeleton")) kind = EnemyKind.Skeleton;
                else if (k++ % 3 == 2) kind = EnemyKind.Brute;
                Enemies.Add(LandEnemy.Spawn(kind, s.position + Vector3.up * 0.3f, this));
            }
        }

        /// <summary>Сбросить клады и врагов (новая игра).</summary>
        public void ResetIsland()
        {
            foreach (var t in Treasures) if (t != null) Destroy(t.gameObject);
            foreach (var e in Enemies) if (e != null) Destroy(e.gameObject);
            Treasures.Clear();
            Enemies.Clear();
            populated = false;
        }

        public void ResetEnemiesToHome()
        {
            foreach (var e in Enemies) if (e != null) e.ResetToHome();
        }

        public int EnemiesAlive
        {
            get { int n = 0; foreach (var e in Enemies) if (e != null && e.IsAlive) n++; return n; }
        }

        public bool HasUndugFragment
        {
            get { foreach (var t in Treasures) if (t != null && t.Kind == TreasureKind.Fragment && !t.Opened) return true; return false; }
        }

        void Update()
        {
            if (populated || Game.I == null) return;
            var g = Game.I;
            if (g.State == GameState.MainMenu) return;
            var p = g.PlayerShip != null ? g.PlayerShip.transform.position : Vector3.zero;
            if (Util.FlatDistance(p, transform.position) < Def.radius + 330f) Populate();
        }
    }
}
