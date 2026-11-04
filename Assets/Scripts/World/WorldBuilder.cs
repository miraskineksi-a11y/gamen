using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    /// <summary>
    /// Карта архипелага: где стоят острова, как называются, где какие части карты сокровищ.
    /// Модели островов — Resources/Models/Island_*.fbx (их делает Blender/build_models.py).
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
        public const float WorldRadius = 1150f;
        public const float PortSafeRadius = 300f;
        public Vector3 Center => new Vector3(0f, 0f, 60f);

        public readonly List<Island> Islands = new List<Island>();
        public Island PortIsland { get; private set; }
        public Island SkullIsland { get; private set; }

        public static readonly IslandDef[] Defs =
        {
            new IslandDef { id = "port", model = "Island_Port", title = "Порт Санта-Роза", radius = 85f, isPort = true,
                position = new Vector3(0f, 0f, -560f), mapColor = new Color(0.55f, 0.66f, 0.38f),
                description = "Родная гавань: ремонт, улучшения корабля, сохранение. Пираты сюда не суются." },
            new IslandDef { id = "parrot", model = "Island_Parrot", title = "Остров Попугаев", radius = 66f, fragment = 0,
                position = new Vector3(-430f, 0f, -230f), mapColor = new Color(0.35f, 0.62f, 0.3f),
                description = "Джунгли и древние руины. Где-то здесь первая часть карты." },
            new IslandDef { id = "turtle", model = "Island_Turtle", title = "Черепаший остров", radius = 58f, fragment = 1,
                position = new Vector3(420f, 0f, -180f), mapColor = new Color(0.55f, 0.65f, 0.35f),
                description = "Лагерь пиратов у скалы-черепахи. Вторая часть карты." },
            new IslandDef { id = "sandbar", model = "Island_Sandbar", title = "Песчаная коса", radius = 30f,
                position = new Vector3(30f, 0f, -170f), mapColor = new Color(0.85f, 0.78f, 0.5f),
                description = "Крошечный островок. Говорят, тут закопал золото один бедолага." },
            new IslandDef { id = "mist", model = "Island_Mist", title = "Остров Туманов", radius = 60f,
                position = new Vector3(-400f, 0f, 280f), mapColor = new Color(0.5f, 0.52f, 0.42f),
                description = "Старое кладбище кораблей. Скелеты охраняют два клада с золотом." },
            new IslandDef { id = "hangman", model = "Island_Hangman", title = "Остров Висельника", radius = 70f, fragment = 2,
                position = new Vector3(430f, 0f, 330f), mapColor = new Color(0.5f, 0.55f, 0.32f),
                description = "Пиратский форт на скалах. Третья часть карты за частоколом." },
            new IslandDef { id = "skull", model = "Island_Skull", title = "Остров Черепа", radius = 80f, isSkull = true,
                position = new Vector3(0f, 0f, 680f), mapColor = new Color(0.4f, 0.4f, 0.38f),
                description = "Логово Кракена. Здесь Кровавая Борода зарыл свой легендарный клад." },
        };

        public void Build()
        {
            foreach (var def in Defs)
            {
                var isl = Island.Create(def);
                Islands.Add(isl);
                if (def.isPort) PortIsland = isl;
                if (def.isSkull) SkullIsland = isl;
            }
            Physics.SyncTransforms();
        }

        public IslandDef Def(string id)
        {
            foreach (var d in Defs) if (d.id == id) return d;
            return null;
        }

        public float DistanceFromCenter(Vector3 p) => Util.FlatDistance(p, Center);

        public bool InPortSafeZone(Vector3 p) => PortIsland != null && Util.FlatDistance(p, PortIsland.transform.position) < PortSafeRadius;

        /// <summary>Точка далеко от берегов (для патрулей и появления кораблей).</summary>
        public bool IsOpenWater(Vector3 p, float margin)
        {
            foreach (var isl in Islands)
                if (Util.FlatDistance(p, isl.transform.position) < isl.Def.radius * 1.35f + margin) return false;
            return DistanceFromCenter(p) < WorldRadius - 50f;
        }

        /// <summary>Ближайший остров по расстоянию до его места высадки.</summary>
        public Island NearestLanding(Vector3 p, out float dist)
        {
            Island best = null;
            dist = float.MaxValue;
            foreach (var isl in Islands)
            {
                if (isl.Landing == null) continue;
                float d = Util.FlatDistance(p, isl.Anchor != null ? isl.Anchor.position : isl.Landing.position);
                d = Mathf.Min(d, Util.FlatDistance(p, isl.Landing.position));
                if (d < dist) { dist = d; best = isl; }
            }
            return best;
        }

        public Island NearestIsland(Vector3 p)
        {
            Island best = null;
            float bd = float.MaxValue;
            foreach (var isl in Islands)
            {
                float d = Util.FlatDistance(p, isl.transform.position) - isl.Def.radius;
                if (d < bd) { bd = d; best = isl; }
            }
            return best;
        }

        /// <summary>Где корабль ждёт у порта (у конца причала, носом в море).</summary>
        public void PortSpawn(out Vector3 pos, out float yaw)
        {
            if (PortIsland == null) { pos = Vector3.zero; yaw = 0f; return; }
            var c = PortIsland.transform.position;
            var dock = PortIsland.PortDock != null ? PortIsland.PortDock.position : c + Vector3.forward * 120f;
            var outward = Util.Flat(dock - c).normalized;
            if (outward.sqrMagnitude < 0.1f) outward = Vector3.back;
            pos = dock + outward * 6f + Vector3.Cross(Vector3.up, outward) * 10f;
            yaw = Util.YawOf(outward);
        }
    }
}
