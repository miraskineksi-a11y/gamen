using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    /// <summary>
    /// Кракен — главный босс у Острова Черепа.
    /// Фаза щупалец: вокруг корабля поднимаются щупальца, замахиваются (на воде
    /// появляется кольцо пены — уходите!) и бьют. Когда все щупальца уничтожены,
    /// всплывает голова — только тогда по ней можно нанести урон.
    /// </summary>
    public class Kraken : MonoBehaviour, IDamageable
    {
        enum St { Dormant, Fight, HeadUp, Dying, Dead }

        public Vector3 lair;
        public float arenaRadius = 230f;
        public float maxHealth = 340f;
        public float health;

        public Team Team => Team.Monsters;
        public bool IsAlive => st != St.Dying && st != St.Dead;
        public bool Fighting => st == St.Fight || st == St.HeadUp;
        public bool Defeated => st == St.Dead || st == St.Dying;
        public float Health01 => Mathf.Clamp01(health / maxHealth);
        public int TentaclesAlive { get { int n = 0; foreach (var t in tentacles) if (t != null && t.IsAlive) n++; return n; } }
        public bool HeadVulnerable => st == St.HeadUp;

        Transform headRoot, segTemplate, tipTemplate;
        readonly List<Tentacle> tentacles = new List<Tentacle>();
        St st;
        float stT, headY = -16f, nextSlam, repositionT;
        Vector3 headPos;
        int wave;

        public static Kraken Spawn(Vector3 lair)
        {
            var go = new GameObject("Kraken");
            go.transform.position = Vector3.zero;
            var k = go.AddComponent<Kraken>();
            k.lair = lair;
            k.health = k.maxHealth;
            k.Build();
            return k;
        }

        void Build()
        {
            var src = Models.Spawn("Kraken", lair, Quaternion.identity, transform, 12f).transform;
            headRoot = Util.FindDeep(src, "Head");
            segTemplate = Util.FindDeep(src, "TentacleSeg");
            tipTemplate = Util.FindDeep(src, "TentacleTip");
            if (headRoot == null) headRoot = src;
            headRoot.SetParent(transform, true);
            var sc = headRoot.gameObject.AddComponent<SphereCollider>();
            sc.center = new Vector3(0f, 3.5f, 0f);
            sc.radius = 4.2f;
            if (segTemplate != null) { segTemplate.SetParent(transform, true); segTemplate.gameObject.SetActive(false); }
            if (tipTemplate != null) { tipTemplate.SetParent(transform, true); tipTemplate.gameObject.SetActive(false); }
            if (src != headRoot) Destroy(src.gameObject);
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            headPos = lair;
            headRoot.position = new Vector3(lair.x, headY, lair.z);
            headRoot.gameObject.SetActive(false);
        }

        /// <summary>Сделать Кракена побеждённым (при загрузке сохранения).</summary>
        public void SetDefeated()
        {
            st = St.Dead;
            foreach (var t in tentacles) if (t != null) Destroy(t.gameObject);
            tentacles.Clear();
            headRoot.gameObject.SetActive(false);
        }

        public void ResetBoss()
        {
            foreach (var t in tentacles) if (t != null) Destroy(t.gameObject);
            tentacles.Clear();
            st = St.Dormant;
            health = maxHealth;
            wave = 0;
            headY = -16f;
            headRoot.gameObject.SetActive(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            stT += dt;
            var g = Game.I;
            if (g == null) return;
            var player = g.PlayerShip;
            bool present = player != null && player.IsAlive && g.State == GameState.Sailing;
            float pd = present ? Util.FlatDistance(player.transform.position, lair) : 9999f;
            tentacles.RemoveAll(t => t == null);

            switch (st)
            {
                case St.Dormant:
                    if (present && pd < arenaRadius)
                    {
                        st = St.Fight;
                        stT = 0f;
                        wave = 0;
                        g.Banner("КРАКЕН!", "Уничтожьте щупальца, затем бейте по голове");
                        AudioManager.Play(Sfx.Roar, player.transform.position + player.transform.forward * 40f, 1f, 0.6f);
                        if (g.Cam != null) g.Cam.Shake(0.6f);
                        SpawnWave(player);
                    }
                    break;

                case St.Fight:
                    if (!present || pd > arenaRadius + 140f) { Retreat(); break; }
                    UpdateTentacleAttacks(player, dt);
                    if (TentaclesAlive == 0 && stT > 2f) SurfaceHead(player);
                    break;

                case St.HeadUp:
                    if (!present || pd > arenaRadius + 140f) { Retreat(); break; }
                    headY = Mathf.MoveTowards(headY, 0.5f, 6f * dt);
                    var face = Util.Flat(player.transform.position - headPos);
                    if (face.sqrMagnitude > 1f)
                        headRoot.rotation = Quaternion.Slerp(headRoot.rotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-2f * dt));
                    UpdateTentacleAttacks(player, dt);
                    if (stT > 12f)
                    {
                        st = St.Fight;
                        stT = 0f;
                        SpawnWave(player);
                    }
                    break;

                case St.Dying:
                    headY -= dt * 1.6f;
                    headRoot.rotation *= Quaternion.Euler(0f, 0f, dt * 6f);
                    if (stT > 7f) { st = St.Dead; headRoot.gameObject.SetActive(false); }
                    break;
            }
            if (st == St.Fight) headY = Mathf.MoveTowards(headY, -16f, 4f * dt);
            if (headRoot.gameObject.activeSelf)
                headRoot.position = new Vector3(headPos.x, headY + WaveField.Height(headPos) * 0.5f + Mathf.Sin(Time.time * 0.8f) * 0.4f, headPos.z);
            if (st == St.Fight && headY <= -15.9f) headRoot.gameObject.SetActive(false);
        }

        void UpdateTentacleAttacks(Ship player, float dt)
        {
            nextSlam -= dt;
            float health01 = Health01;
            if (nextSlam <= 0f)
            {
                Tentacle best = null;
                float bestD = 999f;
                foreach (var t in tentacles)
                {
                    if (t == null || !t.CanAttack) continue;
                    float d = Util.FlatDistance(t.transform.position, player.transform.position);
                    if (d < bestD) { bestD = d; best = t; }
                }
                if (best != null && bestD < 17f)
                {
                    best.StartSlam(player.transform.position + player.Velocity * 0.6f);
                    nextSlam = Mathf.Lerp(1.6f, 3.2f, health01) + Random.Range(0f, 0.8f);
                }
                else nextSlam = 0.5f;
            }
            // щупальца, от которых корабль уплыл, ныряют и всплывают рядом снова
            repositionT -= dt;
            if (repositionT <= 0f)
            {
                repositionT = 3.5f;
                foreach (var t in tentacles)
                {
                    if (t == null || !t.IsAlive || !t.CanAttack) continue;
                    if (Util.FlatDistance(t.transform.position, player.transform.position) > 30f)
                    {
                        t.Relocate(PointNear(player, 13f));
                        break;
                    }
                }
            }
        }

        Vector3 PointNear(Ship player, float r)
        {
            var g = Game.I;
            for (int i = 0; i < 16; i++)
            {
                float a = Random.Range(0f, 360f);
                var p = player.transform.position + player.Velocity * 1.5f + Quaternion.Euler(0f, a, 0f) * Vector3.forward * Random.Range(r, r + 4f);
                p.y = 0f;
                if (g == null || g.World == null || g.World.IsOpenWater(p, 6f)) return p;
            }
            return player.transform.position + player.transform.right * r;
        }

        void SpawnWave(Ship player)
        {
            wave++;
            int count = Health01 > 0.5f ? 4 : 6;
            float hp = Health01 > 0.5f ? 55f : 65f;
            for (int i = 0; i < count; i++)
            {
                float a = 360f / count * i + Random.Range(-15f, 15f);
                var p = player.transform.position + Quaternion.Euler(0f, a, 0f) * Vector3.forward * Random.Range(12f, 16f);
                p.y = 0f;
                var t = Tentacle.Create(this, segTemplate, tipTemplate, p, hp, 0.3f * i);
                tentacles.Add(t);
            }
            nextSlam = 2.5f;
        }

        void SurfaceHead(Ship player)
        {
            st = St.HeadUp;
            stT = 0f;
            var side = Random.value < 0.5f ? -1f : 1f;
            headPos = player.transform.position + player.transform.right * side * 34f + player.transform.forward * 12f;
            headPos.y = 0f;
            headY = -12f;
            headRoot.gameObject.SetActive(true);
            headRoot.position = new Vector3(headPos.x, headY, headPos.z);
            headRoot.rotation = Quaternion.LookRotation(Util.Flat(player.transform.position - headPos));
            Fx.Splash(headPos, 3f);
            AudioManager.Play(Sfx.Roar, headPos, 1f, 0.7f);
            Game.I.Banner("Голова Кракена!", "Стреляйте по голове, пока она над водой!");
            // одно щупальце для давления
            if (Health01 < 0.6f)
                tentacles.Add(Tentacle.Create(this, segTemplate, tipTemplate, PointNear(player, 13f), 55f, 0.5f));
        }

        void Retreat()
        {
            foreach (var t in tentacles) if (t != null) t.Submerge();
            st = St.Dormant;
            stT = 0f;
            headY = -16f;
            headRoot.gameObject.SetActive(false);
            if (Game.I != null) Game.I.Toast("Кракен скрылся в глубине…", UiColors.Muted);
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 direction, Team source)
        {
            if (!IsAlive || source == Team.Monsters) return;
            if (st != St.HeadUp)
            {
                Fx.Sparks(point, 6, Color.white);
                return;
            }
            health -= amount;
            Fx.Poof(point, new Color(0.5f, 0.2f, 0.5f, 0.7f), 1.2f);
            AudioManager.Play(Sfx.Thud, point, 1f, 0.6f);
            if (health <= 0f)
            {
                health = 0f;
                st = St.Dying;
                stT = 0f;
                foreach (var t in tentacles) if (t != null) t.Kill();
                AudioManager.Play(Sfx.Roar, point, 1f, 0.5f);
                Fx.Explosion(point, 2f);
                Fx.Bubbles(headRoot, Vector3.up, 2f);
                if (Game.I != null) Game.I.OnKrakenDefeated(this);
            }
        }
    }

    /// <summary>Щупальце Кракена: цепочка сегментов с процедурной анимацией.</summary>
    public class Tentacle : MonoBehaviour, IDamageable
    {
        enum St { Rising, Idle, Windup, Slam, Recover, Sinking, Dead }

        Kraken owner;
        readonly List<Transform> joints = new List<Transform>();
        St st;
        float stT, hp, baseY = -14f, t0;
        Vector3 slamTarget, relocateTo;
        bool relocate, struck;
        ParticleSystem warning;
        float[] bend;

        public Team Team => Team.Monsters;
        public bool IsAlive => st != St.Dead && st != St.Sinking && hp > 0f;
        public bool CanAttack => st == St.Idle;

        public static Tentacle Create(Kraken owner, Transform seg, Transform tip, Vector3 pos, float hp, float delay)
        {
            var go = new GameObject("Tentacle");
            go.transform.SetParent(owner.transform, false);
            go.transform.position = pos;
            var t = go.AddComponent<Tentacle>();
            t.owner = owner;
            t.hp = hp;
            t.t0 = Random.value * 10f;
            t.stT = -delay;
            t.Build(seg, tip);
            return t;
        }

        void Build(Transform seg, Transform tip)
        {
            const int n = 8;
            const float segLen = 1.8f;
            bend = new float[n + 1];
            Transform parent = transform;
            float len = 0f;
            for (int i = 0; i < n; i++)
            {
                float s = Mathf.Lerp(1.05f, 0.55f, i / (float)(n - 1));
                var j = new GameObject("J" + i).transform;
                j.SetParent(parent, false);
                j.localPosition = i == 0 ? Vector3.zero : Vector3.up * len;
                joints.Add(j);
                if (seg != null)
                {
                    var m = Models.ClonePart(seg, j);
                    m.transform.localScale = Vector3.one * s;
                    var c = m.AddComponent<CapsuleCollider>();
                    c.center = new Vector3(0f, 0.9f, 0f);
                    c.radius = 0.65f;
                    c.height = 2.1f;
                }
                len = segLen * s;
                parent = j;
            }
            var tj = new GameObject("JTip").transform;
            tj.SetParent(parent, false);
            tj.localPosition = Vector3.up * len;
            joints.Add(tj);
            if (tip != null)
            {
                var m = Models.ClonePart(tip, tj);
                m.transform.localScale = Vector3.one * 0.55f;
            }
            transform.position = new Vector3(transform.position.x, baseY, transform.position.z);
        }

        public void StartSlam(Vector3 target)
        {
            if (st != St.Idle) return;
            slamTarget = new Vector3(target.x, 0f, target.z);
            st = St.Windup;
            stT = 0f;
            struck = false;
            warning = Fx.Make("SlamWarning", slamTarget + Vector3.up * 0.3f, false, ps =>
            {
                var m = ps.main;
                m.startLifetime = 0.6f;
                m.startSpeed = 0.3f;
                m.startSize = 1.6f;
                m.startColor = new Color(1f, 0.95f, 0.9f, 0.8f);
                m.maxParticles = 200;
                var em = ps.emission;
                em.rateOverTime = 110f;
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = 6.5f;
                sh.radiusThickness = 0.05f;
                ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            }, null, true);
        }

        public void Relocate(Vector3 p)
        {
            if (st != St.Idle) return;
            relocate = true;
            relocateTo = p;
            st = St.Sinking;
            stT = 0f;
        }

        public void Submerge()
        {
            relocate = false;
            if (st != St.Dead) { st = St.Sinking; stT = 0f; }
            ClearWarning();
        }

        public void Kill()
        {
            hp = 0f;
            st = St.Dead;
            stT = 0f;
            ClearWarning();
        }

        void ClearWarning()
        {
            if (warning != null) Destroy(warning.gameObject);
            warning = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            stT += dt;
            if (stT < 0f) return;
            float time = Time.time + t0;
            var g = Game.I;
            var player = g != null ? g.PlayerShip : null;
            float targetBase = -1.2f;
            float curl = 7f, swayAmp = 6f;

            // поворачиваемся к кораблю
            var look = st == St.Windup || st == St.Slam ? slamTarget : (player != null ? player.transform.position : transform.position + transform.forward);
            var dir = Util.Flat(look - transform.position);
            if (dir.sqrMagnitude > 1f && st != St.Dead)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-3f * dt));

            switch (st)
            {
                case St.Rising:
                    if (stT < dt * 1.5f) { Fx.Splash(new Vector3(transform.position.x, 0f, transform.position.z), 2f); AudioManager.Play(Sfx.Splash, transform.position, 1f, 0.6f); }
                    targetBase = -1.2f;
                    if (stT > 1.6f) { st = St.Idle; stT = 0f; }
                    break;
                case St.Idle:
                    break;
                case St.Windup:
                    targetBase = 0.4f;
                    curl = -13f;
                    swayAmp = 2f;
                    if (stT > 1.25f) { st = St.Slam; stT = 0f; AudioManager.Play(Sfx.Swoosh, transform.position + Vector3.up * 8f, 1f, 0.5f); }
                    break;
                case St.Slam:
                    targetBase = -0.5f;
                    curl = 18.5f;
                    swayAmp = 0f;
                    if (!struck && stT > 0.26f)
                    {
                        struck = true;
                        var tipPos = joints[joints.Count - 1].position;
                        var hit = new Vector3(tipPos.x, 0f, tipPos.z);
                        Fx.Splash(hit, 2.4f);
                        AudioManager.Play(Sfx.Splash, hit, 1f, 0.5f);
                        if (g != null && g.Cam != null) g.Cam.Shake(0.35f);
                        if (player != null && player.IsAlive && Util.FlatDistance(player.transform.position, slamTarget) < player.HalfBeam + 5.5f)
                            player.TakeDamage(20f, player.transform.position + Vector3.up * 2f, Util.Flat(slamTarget - transform.position).normalized, Team.Monsters);
                        ClearWarning();
                    }
                    if (stT > 0.9f) { st = St.Recover; stT = 0f; }
                    break;
                case St.Recover:
                    curl = Mathf.Lerp(18f, 7f, stT / 1.2f);
                    if (stT > 1.2f) { st = St.Idle; stT = 0f; }
                    break;
                case St.Sinking:
                    targetBase = -15f;
                    if (stT > 2.2f)
                    {
                        if (relocate)
                        {
                            transform.position = new Vector3(relocateTo.x, -14f, relocateTo.z);
                            baseY = -14f;
                            relocate = false;
                            st = St.Rising;
                            stT = 0f;
                        }
                        else { Destroy(gameObject); return; }
                    }
                    break;
                case St.Dead:
                    targetBase = -15f;
                    curl = 12f + Mathf.Sin(time * 14f) * 10f * Mathf.Clamp01(1.5f - stT);
                    if (stT > 3f) { Destroy(gameObject); return; }
                    break;
            }

            float riseSpeed = st == St.Slam ? 8f : st == St.Rising ? 9f : 5f;
            baseY = Mathf.MoveTowards(baseY, targetBase, riseSpeed * dt);
            var p = transform.position;
            transform.position = new Vector3(p.x, baseY, p.z);

            float bendSpeed = st == St.Slam ? 260f : st == St.Windup ? 50f : 40f;
            for (int i = 0; i < joints.Count; i++)
            {
                float target = curl * (0.55f + 0.09f * i) + Mathf.Sin(time * 1.4f + i * 0.7f) * swayAmp * 0.6f;
                bend[i] = Mathf.MoveTowards(bend[i], target, bendSpeed * dt);
                float side = Mathf.Sin(time * 0.9f + i * 0.5f) * swayAmp;
                joints[i].localRotation = Quaternion.Euler(i == 0 ? bend[i] * 0.4f : bend[i], 0f, side);
            }
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 direction, Team source)
        {
            if (!IsAlive || source == Team.Monsters) return;
            hp -= amount;
            Fx.Poof(point, new Color(0.55f, 0.25f, 0.55f, 0.7f), 0.8f);
            AudioManager.Play(Sfx.Thud, point, 0.9f, 0.8f);
            if (hp <= 0f)
            {
                Kill();
                AudioManager.Play(Sfx.RoarHigh, point, 0.8f, 0.7f);
            }
        }
    }
}
