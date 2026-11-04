using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    public enum EnemyKind { Pirate, Brute, Skeleton, Crab }

    /// <summary>
    /// Враг на острове (пират, громила, скелет, гигантский краб). Стоит у своей точки,
    /// замечает капитана, бежит к нему, замахивается (видно заранее) и бьёт.
    /// Далеко от дома не уходит, в глубокую воду не заходит.
    /// </summary>
    public class LandEnemy : MonoBehaviour, IDamageable
    {
        public static readonly List<LandEnemy> All = new List<LandEnemy>();

        enum St { Idle, Chase, Windup, Recover, Return, Dead }

        public EnemyKind kind;
        public Island island;
        public bool IsAlive => st != St.Dead;
        public Team Team => kind == EnemyKind.Skeleton || kind == EnemyKind.Crab ? Team.Monsters : Team.Pirates;
        public bool Aggro => st == St.Chase || st == St.Windup || st == St.Recover;

        CharacterController cc;
        HumanoidRig rig;
        CrabRig crab;
        Transform model;
        Renderer[] renderers;
        MaterialPropertyBlock mpb;
        Vector3 home;
        float hp, maxHp, walk, run, dmg, range, windup, cooldown, aggro, leash;
        int goldMin, goldMax;
        St st;
        float stT, yaw, vy, hurt, flashT, wanderT, deadT;
        Vector3 knock, wanderTarget;
        bool struck;

        public static LandEnemy Spawn(EnemyKind kind, Vector3 pos, Island island)
        {
            var go = new GameObject(kind.ToString());
            go.layer = Util.IgnoreRaycastLayer;
            go.transform.position = pos;
            var e = go.AddComponent<LandEnemy>();
            e.kind = kind;
            e.island = island;
            e.home = pos;
            e.Setup();
            return e;
        }

        void Setup()
        {
            string modelName = "Pirate_A";
            float size = 1.9f, scale = 1f;
            switch (kind)
            {
                case EnemyKind.Pirate: Stats(60, 2.2f, 4.8f, 12, 1.9f, 0.5f, 1.1f, 16, 12, 25); modelName = "Pirate_A"; break;
                case EnemyKind.Brute: Stats(110, 2.0f, 4.1f, 20, 2.1f, 0.7f, 1.5f, 15, 25, 40); modelName = "Pirate_B"; scale = 1.1f; break;
                case EnemyKind.Skeleton: Stats(70, 2.0f, 4.6f, 14, 1.9f, 0.45f, 1.0f, 18, 15, 30); modelName = "Skeleton"; break;
                case EnemyKind.Crab: Stats(40, 1.8f, 5.0f, 8, 1.9f, 0.35f, 0.9f, 11, 4, 10); modelName = "Crab"; size = 2.6f; scale = Random.Range(1.0f, 1.35f); break;
            }
            cc = gameObject.AddComponent<CharacterController>();
            bool isCrab = kind == EnemyKind.Crab;
            float radius = isCrab ? 0.6f * scale : 0.35f;
            float height = isCrab ? Mathf.Max(1.0f, radius * 2f) : 1.8f * Mathf.Max(1f, scale * 0.95f);
            cc.radius = radius;
            cc.height = height;
            cc.center = new Vector3(0f, height * 0.5f + 0.03f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;
            model = Models.Spawn(modelName, transform.position, transform.rotation, transform, size).transform;
            model.localScale *= scale;
            Util.SetLayerRecursively(gameObject, Util.IgnoreRaycastLayer);
            if (isCrab) crab = new CrabRig(model);
            else
            {
                rig = new HumanoidRig(model);
                rig.runSpeed = run;
            }
            renderers = model.GetComponentsInChildren<Renderer>();
            mpb = new MaterialPropertyBlock();
            yaw = Random.Range(0f, 360f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            wanderTarget = home;
        }

        void Stats(float hp, float walk, float run, float dmg, float range, float windup, float cooldown, float aggro, int gmin, int gmax)
        {
            this.hp = maxHp = hp;
            this.walk = walk; this.run = run; this.dmg = dmg; this.range = range;
            this.windup = windup; this.cooldown = cooldown; this.aggro = aggro;
            leash = 38f;
            goldMin = gmin; goldMax = gmax;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void ResetToHome()
        {
            if (st == St.Dead) return;
            cc.enabled = false;
            transform.position = home;
            cc.enabled = true;
            st = St.Idle;
            hp = maxHp;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var g = Game.I;
            if (st == St.Dead)
            {
                deadT += dt;
                if (rig != null) { rig.dead = true; rig.Tick(dt); }
                if (crab != null) { crab.dead = true; crab.Tick(dt); }
                if (deadT > 4f) model.localPosition += Vector3.down * dt * 0.6f;
                if (deadT > 6.5f) Destroy(gameObject);
                return;
            }
            // далеко от игрока — экономим (не думаем и не анимируем)
            var cam = g != null && g.Cam != null ? g.Cam.transform.position : transform.position;
            if ((cam - transform.position).sqrMagnitude > 200f * 200f) return;

            var cap = g != null ? g.Captain : null;
            bool canSee = g != null && g.State == GameState.OnFoot && cap != null && cap.IsAlive && !cap.OnShip && g.CurrentIsland == island;
            float d = canSee ? Vector3.Distance(transform.position, cap.transform.position) : 9999f;
            float fromHome = Util.FlatDistance(transform.position, home);
            Vector3 move = Vector3.zero;
            stT += dt;

            switch (st)
            {
                case St.Idle:
                    wanderT -= dt;
                    if (wanderT <= 0f)
                    {
                        wanderT = Random.Range(3f, 7f);
                        var c = Random.insideUnitCircle * 4f;
                        wanderTarget = home + new Vector3(c.x, 0f, c.y);
                    }
                    if (Util.FlatDistance(transform.position, wanderTarget) > 0.6f) move = Toward(wanderTarget) * walk * 0.6f;
                    if (d < aggro) Enter(St.Chase);
                    break;
                case St.Chase:
                    move = Toward(cap.transform.position) * run;
                    if (d < range) Enter(St.Windup);
                    else if (d > aggro * 2.2f || fromHome > leash) Enter(St.Return);
                    break;
                case St.Windup:
                    Face(cap != null ? cap.transform.position : transform.position + transform.forward, dt, 720f);
                    if (!struck && stT >= windup)
                    {
                        struck = true;
                        AudioManager.Play(Sfx.Swoosh, transform.position + Vector3.up, 0.6f, kind == EnemyKind.Crab ? 1.4f : 0.9f);
                        if (canSee && d < range + 0.7f)
                        {
                            var to = Util.Flat(cap.transform.position - transform.position).normalized;
                            if (Vector3.Dot(transform.forward, to) > 0.35f)
                                cap.TakeDamage(dmg * Random.Range(0.85f, 1.15f), cap.transform.position + Vector3.up, to, Team);
                        }
                    }
                    if (stT >= windup + 0.3f) Enter(St.Recover);
                    break;
                case St.Recover:
                    if (stT >= cooldown) Enter(canSee ? St.Chase : St.Return);
                    break;
                case St.Return:
                    move = Toward(home) * walk;
                    if (fromHome < 1.5f) Enter(St.Idle);
                    else if (d < aggro * 0.8f && fromHome < leash * 0.8f) Enter(St.Chase);
                    break;
            }

            // расталкиваем соседей
            foreach (var o in All)
            {
                if (o == this || o.st == St.Dead) continue;
                var off = Util.Flat(transform.position - o.transform.position);
                float dd = off.magnitude;
                if (dd > 0.01f && dd < 1.3f) move += off / dd * (1.3f - dd) * 3f;
            }
            // не лезем в глубокую воду
            if (move.sqrMagnitude > 0.01f)
            {
                var next = transform.position + move.normalized * 0.9f;
                if (!Util.GroundHeight(next, out float gh) || gh < -0.5f) move = Vector3.zero;
            }
            if (st == St.Chase || st == St.Return || (st == St.Idle && move.sqrMagnitude > 0.05f))
                Face(transform.position + move, dt, 540f);

            if (cc.isGrounded) vy = -2f; else vy -= 20f * dt;
            knock = Vector3.MoveTowards(knock, Vector3.zero, 10f * dt);
            cc.Move((move + knock + Vector3.up * vy) * dt);
            if (transform.position.y < -10f) { cc.enabled = false; transform.position = home; cc.enabled = true; }

            hurt = Mathf.MoveTowards(hurt, 0f, dt * 4f);
            UpdateFlash(dt);
            float spd = Util.Flat(cc.velocity).magnitude;
            float atk = st == St.Windup ? Mathf.Clamp01(stT / (windup + 0.3f)) : -1f;
            if (rig != null)
            {
                rig.moveSpeed = spd;
                rig.grounded = true;
                // замах растянут: фаза 0..0.32 — замах (видно заранее), удар — в момент windup
                rig.attack = st == St.Windup ? (stT < windup ? Mathf.Lerp(0f, 0.32f, stT / windup) : Mathf.Lerp(0.32f, 1f, (stT - windup) / 0.3f)) : -1f;
                rig.hurt = hurt;
                rig.Tick(dt);
            }
            if (crab != null)
            {
                crab.moveSpeed = spd;
                crab.attack = atk;
                crab.hurt = hurt;
                crab.Tick(dt);
            }
        }

        Vector3 Toward(Vector3 target)
        {
            var d = Util.Flat(target - transform.position);
            return d.sqrMagnitude < 0.01f ? Vector3.zero : d.normalized;
        }

        void Face(Vector3 point, float dt, float speed)
        {
            var d = Util.Flat(point - transform.position);
            if (d.sqrMagnitude < 0.001f) return;
            yaw = Mathf.MoveTowardsAngle(yaw, Util.YawOf(d), speed * dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void Enter(St s)
        {
            if (s == St.Chase && st != St.Chase && st != St.Recover)
                AudioManager.Play(kind == EnemyKind.Skeleton ? Sfx.RoarHigh : Sfx.Thud, transform.position, 0.4f, kind == EnemyKind.Crab ? 1.8f : 1.2f);
            st = s;
            stT = 0f;
            struck = false;
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 direction, Team source)
        {
            if (st == St.Dead || source == Team) return;
            hp -= amount;
            hurt = 1f;
            flashT = 0.12f;
            knock = Util.Flat(direction).normalized * (kind == EnemyKind.Brute ? 2.5f : 5f);
            Fx.Sparks(point, 8, kind == EnemyKind.Skeleton ? new Color(0.7f, 1f, 0.8f) : new Color(1f, 0.85f, 0.5f));
            AudioManager.Play(kind == EnemyKind.Skeleton ? Sfx.WoodHit : Sfx.Thud, point, 0.8f, kind == EnemyKind.Skeleton ? 1.5f : 1f);
            if (st == St.Idle || st == St.Return) Enter(St.Chase);
            else if (st == St.Windup && kind != EnemyKind.Brute && stT < windup * 0.6f) Enter(St.Recover); // сбили замах
            if (hp <= 0f) Die();
        }

        void Die()
        {
            st = St.Dead;
            deadT = 0f;
            cc.enabled = false;
            var p = transform.position + Vector3.up * 0.8f;
            if (kind == EnemyKind.Skeleton) Fx.Bones(p);
            else Fx.Poof(p, new Color(0.8f, 0.8f, 0.75f, 0.6f), 0.8f);
            Loot.SpawnGold(transform.position + transform.forward * 0.6f + Vector3.up * 0.1f, Random.Range(goldMin, goldMax + 1));
            if (Game.I != null) Game.I.OnEnemyKilled(this);
        }

        void UpdateFlash(float dt)
        {
            if (flashT <= 0f) return;
            flashT -= dt;
            if (flashT > 0f)
            {
                mpb.SetColor("_Color", new Color(1f, 0.55f, 0.5f));
                mpb.SetColor("_BaseColor", new Color(1f, 0.55f, 0.5f));
                foreach (var r in renderers) if (r != null) r.SetPropertyBlock(mpb);
            }
            else
            {
                mpb.Clear();
                foreach (var r in renderers) if (r != null) r.SetPropertyBlock(mpb);
            }
        }
    }
}
