using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    /// <summary>Добыча: бочки с золотом на воде (после боя) и кучки монет на земле.</summary>
    public class Loot : MonoBehaviour
    {
        public enum Kind { Barrel, Gold }

        public static readonly List<Loot> All = new List<Loot>();

        public Kind kind;
        public int gold;
        float life, phase;
        Vector3 drift;
        Transform visual;

        public static void SpawnBarrels(Vector3 center, int totalGold, int count)
        {
            count = Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                var off = Random.insideUnitCircle * 9f;
                var p = center + new Vector3(off.x, 0f, off.y);
                var l = Create(Kind.Barrel, p, totalGold / count + (i == 0 ? totalGold % count : 0));
                l.drift = new Vector3(off.x, 0f, off.y).normalized * 0.4f;
            }
        }

        public static void SpawnGold(Vector3 pos, int gold) => Create(Kind.Gold, pos, gold);

        static Loot Create(Kind kind, Vector3 pos, int gold)
        {
            var go = new GameObject(kind == Kind.Barrel ? "LootBarrel" : "LootGold");
            go.transform.position = pos;
            var l = go.AddComponent<Loot>();
            l.kind = kind;
            l.gold = gold;
            l.phase = Random.value * 10f;
            l.visual = Models.Spawn(kind == Kind.Barrel ? "Barrel" : "GoldPile", pos, Quaternion.Euler(0, Random.Range(0, 360f), 0), go.transform, kind == Kind.Barrel ? 1.2f : 0.8f).transform;
            return l;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            float dt = Time.deltaTime;
            life += dt;
            var g = Game.I;
            if (life > 160f) { Destroy(gameObject); return; }
            var p = transform.position;
            if (kind == Kind.Barrel)
            {
                p += drift * dt;
                p.y = WaveField.Height(p.x, p.z) + 0.05f;
                transform.position = p;
                visual.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.3f + phase) * 12f, Time.time * 10f + phase * 30f, Mathf.Cos(Time.time * 1.1f + phase) * 12f);
                if (g != null && g.State == GameState.Sailing && g.PlayerShip != null && g.PlayerShip.IsAlive &&
                    Util.FlatDistance(p, g.PlayerShip.transform.position) < g.PlayerShip.HalfLength + 3f)
                    Collect();
            }
            else
            {
                visual.localPosition = new Vector3(0f, 0.25f + Mathf.Sin(Time.time * 2.5f + phase) * 0.08f, 0f);
                visual.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
                if (g != null && g.State == GameState.OnFoot && g.Captain != null && g.Captain.IsAlive)
                {
                    var cp = g.Captain.transform.position;
                    float d = Vector3.Distance(p, cp);
                    if (d < 4f && life > 0.6f) transform.position = Vector3.MoveTowards(p, cp + Vector3.up * 0.5f, dt * 9f);
                    if (d < 1.3f && life > 0.6f) Collect();
                }
            }
        }

        void Collect()
        {
            if (Game.I != null) Game.I.AddGold(gold, transform.position);
            Fx.GoldBurst(transform.position + Vector3.up * 0.5f);
            Destroy(gameObject);
        }
    }
}
