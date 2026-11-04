using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    /// <summary>Пиратский флот: по кораблю на каждый район моря, потопленные возвращаются через время.</summary>
    public class PirateFleet : MonoBehaviour
    {
        class Zone
        {
            public Vector3 center;
            public float radius;
            public bool brig;
            public string name;
            public PirateShip ship;
            public float respawnAt;
        }

        readonly List<Zone> zones = new List<Zone>();
        const float RespawnDelay = 90f;

        public void Init()
        {
            zones.Clear();
            zones.Add(new Zone { center = new Vector3(-330f, 0f, 30f), radius = 190f, brig = false, name = "«Чёрная Чайка»" });
            zones.Add(new Zone { center = new Vector3(330f, 0f, 80f), radius = 190f, brig = true, name = "«Морской Дьявол»" });
            zones.Add(new Zone { center = new Vector3(0f, 0f, -60f), radius = 170f, brig = false, name = "«Ржавый Якорь»" });
            zones.Add(new Zone { center = new Vector3(-260f, 0f, 470f), radius = 160f, brig = true, name = "«Беззубая Акула»" });
        }

        public bool AnyEngaged
        {
            get
            {
                foreach (var z in zones) if (z.ship != null && z.ship.Ship != null && z.ship.Ship.IsAlive && z.ship.Engaged) return true;
                return false;
            }
        }

        public IEnumerable<Ship> Ships
        {
            get
            {
                foreach (var z in zones)
                    if (z.ship != null && z.ship.Ship != null && z.ship.Ship.IsAlive) yield return z.ship.Ship;
            }
        }

        public void ResetFleet()
        {
            foreach (var z in zones)
            {
                if (z.ship != null) Destroy(z.ship.gameObject);
                z.ship = null;
                Spawn(z, true);
            }
        }

        void Update()
        {
            var g = Game.I;
            if (g == null || g.State == GameState.MainMenu) return;
            foreach (var z in zones)
            {
                if (z.ship != null && z.ship.Ship != null && !z.ship.Ship.Sinking) continue;
                if (z.ship != null && z.ship.Ship != null) continue; // ещё тонет
                if (z.respawnAt <= 0f) z.respawnAt = Time.time + RespawnDelay;
                if (Time.time >= z.respawnAt) Spawn(z, false);
            }
        }

        void Spawn(Zone z, bool initial)
        {
            var g = Game.I;
            var playerPos = g != null && g.PlayerShip != null ? g.PlayerShip.transform.position : Vector3.zero;
            Vector3 pos = z.center;
            for (int i = 0; i < 30; i++)
            {
                var c = Random.insideUnitCircle * z.radius;
                var p = z.center + new Vector3(c.x, 0f, c.y);
                if (g != null && g.World != null && !g.World.IsOpenWater(p, 70f)) continue;
                if (!initial && Util.FlatDistance(p, playerPos) < 320f) continue;
                pos = p;
                break;
            }
            var stats = z.brig ? ShipStats.PirateBrig : ShipStats.PirateSloop;
            var ship = Ship.Create(z.brig ? "Ship_PirateBrig" : "Ship_PirateSloop", pos, Random.Range(0f, 360f), Team.Pirates, stats, z.name);
            var ai = ship.gameObject.AddComponent<PirateShip>();
            ai.zoneCenter = z.center;
            ai.zoneRadius = z.radius;
            ai.isBrig = z.brig;
            bool brig = z.brig;
            ship.Sunk += s => { if (Game.I != null) Game.I.OnPirateShipSunk(s, brig); };
            z.ship = ai;
            z.respawnAt = 0f;
        }
    }
}
