using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    public enum TreasureKind { Gold, Fragment, Legend }

    /// <summary>
    /// Место клада: красный крест на земле. Капитан копает (удерживая E), из земли
    /// поднимается сундук, крышка открывается — золото, часть карты или легендарный клад.
    /// </summary>
    public class TreasureSpot : MonoBehaviour
    {
        public static readonly List<TreasureSpot> All = new List<TreasureSpot>();
        const float DigTime = 2.6f;

        public string Id { get; private set; }
        public Island Island { get; private set; }
        public TreasureKind Kind { get; private set; }
        public int Gold { get; private set; }
        public int FragmentIndex { get; private set; }
        public bool Opened { get; private set; }
        public float Progress01 => Mathf.Clamp01(dig / DigTime);

        public bool Locked => Kind == TreasureKind.Legend && (Game.I == null || !Game.I.Data.HasAllFragments);
        public bool CanDig => !Opened && !Locked && dig < DigTime;

        GameObject xmark;
        float dig;

        public static TreasureSpot Create(Island island, Transform marker, string id, TreasureKind kind, int gold, int fragment)
        {
            var go = new GameObject("Treasure " + id);
            var p = marker.position;
            if (Util.GroundHeight(p + Vector3.up * 2f, out float h, 30f)) p.y = h;
            go.transform.position = p;
            var t = go.AddComponent<TreasureSpot>();
            t.Id = id;
            t.Island = island;
            t.Kind = kind;
            t.Gold = gold;
            t.FragmentIndex = fragment;
            t.xmark = Models.Spawn("XMark", p + Vector3.up * 0.02f, Quaternion.Euler(0f, Random.Range(0f, 90f), 0f), go.transform, 2f);
            return t;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            if (xmark != null) xmark.SetActive(!Opened && !Locked);
        }

        public static TreasureSpot Nearest(Vector3 p, float maxDist)
        {
            TreasureSpot best = null;
            float bd = maxDist;
            foreach (var t in All)
            {
                if (t == null || t.Opened) continue;
                float d = Util.FlatDistance(p, t.transform.position);
                if (d < bd && Mathf.Abs(p.y - t.transform.position.y) < 3f) { bd = d; best = t; }
            }
            return best;
        }

        /// <summary>Покопать dt секунд. Возвращает прогресс 0..1.</summary>
        public float Dig(float dt)
        {
            if (!CanDig) return Progress01;
            dig += dt;
            if (dig >= DigTime) StartCoroutine(Reveal());
            return Progress01;
        }

        IEnumerator Reveal()
        {
            Opened = true;
            var g = Game.I;
            if (xmark != null) xmark.SetActive(false);
            var p = transform.position;
            float yaw = g != null && g.Captain != null ? Util.YawOf(g.Captain.transform.position - p) : 0f;
            var chest = Models.Spawn(Kind == TreasureKind.Legend ? "ChestLegend" : "Chest", p + Vector3.down * 1.0f, Quaternion.Euler(0f, yaw, 0f), transform, Kind == TreasureKind.Legend ? 1.5f : 1.1f);
            var lid = Util.FindDeep(chest.transform, "ChestLid");
            Fx.Dirt(p + Vector3.up * 0.3f, 2f);
            AudioManager.Play(Sfx.Dig, p, 1f, 0.8f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.9f;
                chest.transform.position = p + Vector3.down * Mathf.Lerp(1.0f, 0.05f, Util.EaseOutBack(Mathf.Clamp01(t)));
                yield return null;
            }
            AudioManager.Play(Sfx.Chest, p, 1f);
            t = 0f;
            Quaternion lidRest = lid != null ? lid.localRotation : Quaternion.identity;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.7f;
                if (lid != null) lid.localRotation = lidRest * Quaternion.Euler(-105f * Util.EaseOutBack(Mathf.Clamp01(t)), 0f, 0f);
                yield return null;
            }
            Fx.GoldBurst(p + Vector3.up * 0.8f);
            if (Kind == TreasureKind.Legend) Fx.Poof(p + Vector3.up, new Color(1f, 0.85f, 0.3f, 0.7f), 2f);
            if (g != null) g.OnTreasureFound(this);
        }
    }
}
