using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    /// <summary>
    /// Морской змей. Тело — цепочка сегментов, которые повторяют путь головы.
    /// Плывёт под водой (тёмная тень), подкрадывается к кораблю игрока, выныривает,
    /// ревёт (это предупреждение!) и бросается кусать. Уязвим, только когда над водой.
    /// </summary>
    public class SeaSerpent : MonoBehaviour, IDamageable
    {
        enum St { Roam, Stalk, Rise, Lunge, Dive, Dying }

        public Vector3 territory;
        public float territoryRadius = 260f;
        public float maxHealth = 260f;
        public float health;

        public Team Team => Team.Monsters;
        public bool IsAlive => st != St.Dying && health > 0f;
        public bool Engaged => st == St.Stalk || st == St.Rise || st == St.Lunge;
        public float Health01 => Mathf.Clamp01(health / maxHealth);
        public Vector3 HeadPosition => head != null ? head.position : transform.position;

        Transform head, jaw;
        Quaternion jawRest;
        readonly List<Transform> parts = new List<Transform>();
        readonly List<Vector3> trail = new List<Vector3>();
        Vector3 pos, wanderTarget, lungeTarget, lungeDir;
        float yaw, y, stT, surfaceT, attackCooldown = 4f, side = 1f;
        St st;
        bool bitten, roared;
        const float Spacing = 2.15f;
        const int Segments = 13;

        public static SeaSerpent Spawn(Vector3 position, Vector3 territory)
        {
            var go = new GameObject("SeaSerpent");
            var s = go.AddComponent<SeaSerpent>();
            s.territory = territory;
            s.pos = new Vector3(position.x, 0f, position.z);
            s.health = s.maxHealth;
            s.Build();
            return s;
        }

        void Build()
        {
            var src = Models.Spawn("SeaSerpent", Vector3.zero, Quaternion.identity, transform, 6f).transform;
            head = Util.FindDeep(src, "Head");
            jaw = Util.FindDeep(src, "Jaw");
            var seg = Util.FindDeep(src, "Segment");
            var tail = Util.FindDeep(src, "Tail");
            if (head == null) { head = src; }
            head.SetParent(transform, true);
            if (jaw != null) { jaw.SetParent(head, true); jawRest = jaw.localRotation; }
            AddSphere(head.gameObject, new Vector3(0f, 0.1f, 1.3f), 1.45f);
            if (seg != null)
            {
                seg.SetParent(transform, true);
                parts.Add(seg);
                for (int i = 1; i < Segments - 1; i++)
                {
                    var c = Instantiate(seg.gameObject, transform).transform;
                    c.name = "Segment" + i;
                    parts.Add(c);
                }
            }
            if (tail != null) { tail.SetParent(transform, true); parts.Add(tail); }
            foreach (var p in parts) AddSphere(p.gameObject, new Vector3(0f, 0f, -1.15f), p.name == "Tail" ? 0.85f : 1.1f);
            if (src != head) Destroy(src.gameObject);
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            yaw = Random.Range(0f, 360f);
            y = -4f;
            var back = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            for (int i = 0; i < 80; i++) trail.Add(pos + Vector3.up * y + back * (i * 0.5f));
            wanderTarget = territory;
            UpdateBody(0f);
        }

        static void AddSphere(GameObject go, Vector3 center, float r)
        {
            var c = go.AddComponent<SphereCollider>();
            c.center = center;
            c.radius = r;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            stT += dt;
            attackCooldown -= dt;
            var g = Game.I;
            var player = g != null ? g.PlayerShip : null;
            bool hunt = player != null && player.IsAlive && g.State == GameState.Sailing && !g.World.InPortSafeZone(player.transform.position);
            float pd = hunt ? Util.FlatDistance(pos, player.transform.position) : 9999f;
            float speed = 7f, targetY = -3.2f;
            Vector3 goal = wanderTarget;

            switch (st)
            {
                case St.Roam:
                    surfaceT += dt;
                    targetY = (surfaceT % 26f) > 17f ? 0.15f : -3.4f;
                    if (Util.FlatDistance(pos, wanderTarget) < 25f || stT > 30f)
                    {
                        var c = Random.insideUnitCircle * territoryRadius;
                        wanderTarget = territory + new Vector3(c.x, 0f, c.y);
                        if (g != null && g.World != null && !g.World.IsOpenWater(wanderTarget, 50f)) wanderTarget = territory;
                        stT = 0f;
                    }
                    if (hunt && pd < 210f && attackCooldown <= 0f) { Enter(St.Stalk); side = Random.value < 0.5f ? -1f : 1f; }
                    break;
                case St.Stalk:
                    speed = 14f;
                    targetY = -3.2f;
                    if (!hunt || pd > 330f) { Enter(St.Roam); break; }
                    goal = player.transform.position + player.transform.right * side * 17f + player.transform.forward * (6f + player.speed * 1.2f);
                    if (Util.FlatDistance(pos, goal) < 9f || stT > 14f) Enter(St.Rise);
                    break;
                case St.Rise:
                    speed = 3f;
                    targetY = 3.2f;
                    if (hunt) goal = player.transform.position;
                    if (!roared) { roared = true; AudioManager.Play(Sfx.Roar, pos, 1f, 1.15f); }
                    if (stT > 1.5f)
                    {
                        if (hunt)
                        {
                            lungeTarget = player.transform.position + player.Velocity * 0.45f;
                            lungeDir = Util.Flat(lungeTarget - pos).normalized;
                        }
                        else lungeDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                        Enter(St.Lunge);
                    }
                    break;
                case St.Lunge:
                    speed = 21f;
                    targetY = 1.2f;
                    goal = pos + lungeDir * 20f;
                    if (!bitten && hunt)
                    {
                        float d = DistanceToShip(player, head.position);
                        if (d < 3.2f)
                        {
                            bitten = true;
                            player.TakeDamage(18f, head.position, lungeDir, Team.Monsters);
                            AudioManager.Play(Sfx.Bite, head.position, 1f);
                            Fx.Splash(head.position, 1.6f);
                        }
                    }
                    if (stT > 0.75f) Enter(St.Dive);
                    break;
                case St.Dive:
                    speed = 12f;
                    targetY = -5f;
                    goal = hunt ? pos + Util.Flat(pos - player.transform.position).normalized * 30f + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 20f : wanderTarget;
                    if (stT > 3.2f) { attackCooldown = Random.Range(5f, 8f); Enter(St.Roam); }
                    break;
                case St.Dying:
                    speed = Mathf.Max(0f, 4f - stT);
                    targetY = -9f;
                    goal = pos + Quaternion.Euler(0f, yaw + Mathf.Sin(stT * 4f) * 40f, 0f) * Vector3.forward * 10f;
                    if (stT > 7f) { Destroy(gameObject); return; }
                    break;
            }

            // движение головы
            var to = Util.Flat(goal - pos);
            if (to.sqrMagnitude > 1f)
            {
                float turn = st == St.Lunge ? 40f : st == St.Rise ? 120f : 70f;
                yaw = Mathf.MoveTowardsAngle(yaw, Util.YawOf(to), turn * dt);
            }
            var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            pos += fwd * speed * dt;
            pos.y = 0f;
            float prevY = y;
            float vrate = st == St.Rise ? 4f : st == St.Lunge ? 3f : 2.2f;
            y = Mathf.MoveTowards(y, targetY, vrate * dt);
            float wave = WaveField.Height(pos.x, pos.z) * Mathf.Clamp01((y + 2f) / 3f);
            var hp = new Vector3(pos.x, y + wave, pos.z);
            float pitch = -Mathf.Atan2((y - prevY) / dt, Mathf.Max(speed, 0.5f)) * Mathf.Rad2Deg;
            if (st == St.Rise) pitch = -25f;
            head.SetPositionAndRotation(hp, Quaternion.Euler(Mathf.Clamp(pitch, -40f, 40f), yaw, 0f));
            if (jaw != null)
            {
                float open = st == St.Rise ? Mathf.Clamp01(stT * 1.5f) * 32f : st == St.Lunge ? Mathf.Lerp(32f, 0f, stT / 0.5f) : 4f + Mathf.Sin(Time.time * 2f) * 3f;
                jaw.localRotation = jawRest * Quaternion.Euler(open, 0f, 0f);
            }

            if ((trail[0] - hp).sqrMagnitude > 0.25f)
            {
                trail.Insert(0, hp);
                if (trail.Count > 140) trail.RemoveAt(trail.Count - 1);
            }
            else trail[0] = hp;
            UpdateBody(dt);
        }

        void UpdateBody(float dt)
        {
            float surface = Mathf.Clamp01((y + 1.5f) / 2f);
            float acc = 0f;
            int ti = 0;
            Vector3 prev = head != null ? head.position : trail[0];
            for (int i = 0; i < parts.Count; i++)
            {
                float want = (i + 1) * Spacing;
                Vector3 p = trail[trail.Count - 1];
                while (ti < trail.Count - 1)
                {
                    float seg = Vector3.Distance(trail[ti], trail[ti + 1]);
                    if (acc + seg >= want)
                    {
                        p = Vector3.Lerp(trail[ti], trail[ti + 1], (want - acc) / Mathf.Max(seg, 1e-4f));
                        break;
                    }
                    acc += seg;
                    ti++;
                }
                // горбы над водой
                p.y += Mathf.Sin(i * 0.9f - Time.time * 2.2f) * 1.1f * surface * (i < parts.Count - 1 ? 1f : 0.5f);
                var dir = prev - p;
                var rot = dir.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(dir) : parts[i].rotation;
                if (st == St.Dying) rot *= Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 6f + i) * 35f);
                parts[i].SetPositionAndRotation(p, rot);
                prev = p;
            }
        }

        static float DistanceToShip(Ship s, Vector3 p)
        {
            var local = s.transform.InverseTransformPoint(p);
            float dz = Mathf.Max(0f, Mathf.Abs(local.z) - s.HalfLength);
            float dx = Mathf.Max(0f, Mathf.Abs(local.x) - s.HalfBeam);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        void Enter(St s)
        {
            st = s;
            stT = 0f;
            bitten = false;
            roared = false;
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 direction, Team source)
        {
            if (!IsAlive || source == Team.Monsters) return;
            health -= amount;
            Fx.Sparks(point, 10, new Color(0.6f, 1f, 0.7f));
            Fx.Splash(point, 0.6f);
            AudioManager.Play(Sfx.Thud, point, 1f, 0.7f);
            if (st == St.Roam) { Enter(St.Stalk); side = Random.value < 0.5f ? -1f : 1f; }
            if (health <= 0f)
            {
                health = 0f;
                Enter(St.Dying);
                AudioManager.Play(Sfx.Roar, point, 1f, 0.8f);
                Fx.Explosion(point, 1.2f);
                if (Game.I != null) Game.I.OnSerpentKilled(this);
            }
        }
    }
}
