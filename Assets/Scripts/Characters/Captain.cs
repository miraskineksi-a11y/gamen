using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    /// <summary>
    /// Капитан (игрок) на суше:
    ///   WASD — ходьба, Shift — бег, Пробел — прыжок, ЛКМ — удар саблей, ПКМ — блок,
    ///   E (удерживать) — копать клад, F — вернуться на корабль (у шлюпки).
    /// На корабле стоит у штурвала.
    /// </summary>
    public class Captain : MonoBehaviour, IDamageable
    {
        public float maxHealth = 100f;
        public float health = 100f;
        public float walkSpeed = 4.2f, runSpeed = 7.4f, jumpSpeed = 6.8f, gravity = 22f;
        public float swordDamage = 26f;

        public bool OnShip { get; private set; }
        public bool IsAlive => !dead;
        public Team Team => Team.Player;
        public float Health01 => Mathf.Clamp01(health / maxHealth);
        public float DigProgress { get; private set; } = -1f;

        CharacterController cc;
        HumanoidRig rig;
        Transform shovel;
        float yaw, vy;
        Vector3 knock;
        float attackT = -1f, attackCooldown;
        bool hitApplied, blocking, dead;
        float hurt, deadT, lastDamage = -100f, digSoundT;
        readonly HashSet<IDamageable> hitSet = new HashSet<IDamageable>();
        static readonly Collider[] overlap = new Collider[24];

        const float AttackDuration = 0.55f;

        public static Captain Create(Vector3 pos)
        {
            var go = new GameObject("Captain");
            go.transform.position = pos;
            go.layer = Util.IgnoreRaycastLayer;
            var c = go.AddComponent<Captain>();
            c.cc = go.AddComponent<CharacterController>();
            c.cc.height = 1.8f;
            c.cc.radius = 0.33f;
            c.cc.center = new Vector3(0f, 0.92f, 0f);
            c.cc.slopeLimit = 50f;
            c.cc.stepOffset = 0.45f;
            c.cc.skinWidth = 0.05f;
            var model = Models.Spawn("Captain", pos, Quaternion.identity, go.transform, 1.9f);
            Util.SetLayerRecursively(model, Util.IgnoreRaycastLayer);
            c.rig = new HumanoidRig(model.transform);
            c.rig.runSpeed = c.runSpeed;
            if (c.rig.Hand != null)
            {
                c.shovel = Models.Spawn("Shovel", c.rig.Hand.position, c.rig.Hand.rotation, c.rig.Hand, 1.6f).transform;
                Util.SetLayerRecursively(c.shovel.gameObject, Util.IgnoreRaycastLayer);
                c.shovel.gameObject.SetActive(false);
            }
            return c;
        }

        // ------------------------------------------------------------------ корабль / берег

        public void AttachToShip(Ship ship)
        {
            OnShip = true;
            dead = false;
            if (cc != null) cc.enabled = false;
            transform.SetParent(ship.Model, false);
            var helm = ship.Helm;
            transform.position = helm != null ? helm.position : ship.transform.position + Vector3.up * 1.5f;
            transform.rotation = ship.Model.rotation;
            rig.ResetPose();
            rig.steer = true;
            SetSword(true);
            if (shovel != null) shovel.gameObject.SetActive(false);
        }

        public void PlaceOnIsland(Vector3 pos, float facingYaw)
        {
            OnShip = false;
            transform.SetParent(null, true);
            cc.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, facingYaw, 0f));
            cc.enabled = true;
            yaw = facingYaw;
            vy = 0f;
            knock = Vector3.zero;
            dead = false;
            deadT = 0f;
            health = maxHealth;
            attackT = -1f;
            rig.ResetPose();
            rig.steer = false;
            SetSword(true);
        }

        void SetSword(bool on)
        {
            if (rig.Sword != null) rig.Sword.gameObject.SetActive(on);
            if (shovel != null) shovel.gameObject.SetActive(!on);
        }

        // ------------------------------------------------------------------ обновление

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var g = Game.I;
            if (OnShip)
            {
                rig.moveSpeed = 0f;
                rig.grounded = true;
                rig.Tick(dt);
                return;
            }
            if (dead)
            {
                deadT += dt;
                rig.dead = true;
                rig.Tick(dt);
                return;
            }

            bool control = g != null && g.State == GameState.OnFoot;
            Vector2 mv = control ? GameInput.Move : Vector2.zero;
            bool run = control && GameInput.Key(GKey.Shift);
            float camYaw = g != null && g.Cam != null ? g.Cam.Yaw : transform.eulerAngles.y;
            Vector3 wish = Quaternion.Euler(0f, camYaw, 0f) * new Vector3(mv.x, 0f, mv.y);
            bool attacking = attackT >= 0f;
            bool digging = DigProgress >= 0f;

            float spd = (run ? runSpeed : walkSpeed) * (blocking ? 0.45f : 1f) * (attacking ? 0.55f : 1f) * (digging ? 0f : 1f);
            if (transform.position.y < 0.25f) spd *= 0.6f; // по колено в воде
            Vector3 h = wish * spd;
            if (h.sqrMagnitude > 0.01f)
            {
                var next = transform.position + wish.normalized * 0.9f;
                if (Util.GroundHeight(next, out float gh) && gh < -0.9f) h = Vector3.zero;   // дальше глубоко
                else if (!Util.GroundHeight(next, out gh)) h = Vector3.zero;
            }

            if (attacking || blocking) yaw = Mathf.MoveTowardsAngle(yaw, camYaw, 540f * dt);
            else if (wish.sqrMagnitude > 0.01f) yaw = Mathf.MoveTowardsAngle(yaw, Util.YawOf(wish), 720f * dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (cc.isGrounded)
            {
                vy = -2f;
                if (control && GameInput.KeyDown(GKey.Space) && !digging) vy = jumpSpeed;
            }
            else vy -= gravity * dt;
            knock = Vector3.MoveTowards(knock, Vector3.zero, 12f * dt);
            cc.Move((h + knock + Vector3.up * vy) * dt);

            // бой
            attackCooldown -= dt;
            if (control && GameInput.MouseDown(0) && attackCooldown <= 0f && !digging) StartAttack();
            if (attackT >= 0f) UpdateAttack(dt);
            blocking = control && GameInput.Mouse(1) && attackT < 0f && !digging;

            Interact(g, control, dt);

            if (Time.time - lastDamage > 6f) health = Mathf.Min(maxHealth, health + 7f * dt);
            hurt = Mathf.MoveTowards(hurt, 0f, dt * 4f);
            if (transform.position.y < -15f && g != null) g.OnCaptainFell();

            rig.moveSpeed = Util.Flat(cc.velocity).magnitude;
            rig.grounded = cc.isGrounded || vy < 0f && vy > -3f;
            rig.attack = attackT >= 0f ? attackT / AttackDuration : -1f;
            rig.block = blocking;
            rig.dig = DigProgress >= 0f ? Time.time : -1f;
            rig.hurt = hurt;
            rig.steer = false;
            rig.dead = false;
            rig.Tick(dt);
        }

        void StartAttack()
        {
            attackT = 0f;
            hitApplied = false;
            attackCooldown = AttackDuration + 0.05f;
            hitSet.Clear();
            AudioManager.Play(Sfx.Swoosh, transform.position + Vector3.up, 0.7f, 1.1f);
        }

        void UpdateAttack(float dt)
        {
            attackT += dt;
            if (!hitApplied && attackT >= AttackDuration * 0.45f)
            {
                hitApplied = true;
                var center = transform.position + transform.forward * 1.25f + Vector3.up * 1f;
                int n = Physics.OverlapSphereNonAlloc(center, 1.45f, overlap, ~0, QueryTriggerInteraction.Ignore);
                bool any = false;
                for (int i = 0; i < n; i++)
                {
                    var d = overlap[i].GetComponentInParent<IDamageable>();
                    if (d == null || ReferenceEquals(d, this) || !d.IsAlive || d.Team == Team.Player || hitSet.Contains(d)) continue;
                    var mb = d as Component;
                    if (mb == null) continue;
                    var to = Util.Flat(mb.transform.position - transform.position);
                    if (to.sqrMagnitude > 0.01f && Vector3.Dot(to.normalized, transform.forward) < 0.15f) continue;
                    hitSet.Add(d);
                    d.TakeDamage(swordDamage * Random.Range(0.9f, 1.15f), center, transform.forward, Team.Player);
                    any = true;
                }
                if (any) AudioManager.Play(Sfx.Clang, center, 0.8f);
            }
            if (attackT >= AttackDuration) attackT = -1f;
        }

        void Interact(Game g, bool control, float dt)
        {
            if (g == null) return;
            var isl = g.CurrentIsland;
            if (isl != null && isl.Landing != null && Util.FlatDistance(transform.position, isl.Landing.position) < 7f)
            {
                g.SetPrompt("[F] Вернуться на корабль");
                if (control && GameInput.KeyDown(GKey.F)) { g.BoardShip(); DigProgress = -1f; return; }
            }
            var spot = TreasureSpot.Nearest(transform.position, 2.4f);
            if (spot != null && spot.CanDig)
            {
                if (control && GameInput.Key(GKey.E))
                {
                    if (DigProgress < 0f) SetSword(false);
                    DigProgress = spot.Dig(dt);
                    yaw = Mathf.MoveTowardsAngle(yaw, Util.YawOf(spot.transform.position - transform.position), 360f * dt);
                    digSoundT -= dt;
                    if (digSoundT <= 0f)
                    {
                        digSoundT = 0.45f;
                        AudioManager.Play(Sfx.Dig, spot.transform.position, 0.8f);
                        Fx.Dirt(spot.transform.position + Vector3.up * 0.2f, 0.7f);
                    }
                    g.SetPrompt("Копаем… " + Mathf.RoundToInt(spot.Progress01 * 100f) + "%");
                    if (!spot.CanDig) StopDig();
                    return;
                }
                g.SetPrompt("[E] Удерживайте, чтобы выкопать клад");
            }
            else if (spot != null && spot.Locked)
            {
                g.SetPrompt("Здесь что-то зарыто… Без полной карты (3 части) не найти.");
            }
            if (DigProgress >= 0f) StopDig();
        }

        void StopDig()
        {
            DigProgress = -1f;
            SetSword(true);
        }

        // ------------------------------------------------------------------ урон

        public void TakeDamage(float amount, Vector3 point, Vector3 direction, Team source)
        {
            if (dead || OnShip || source == Team.Player) return;
            var dirFlat = Util.Flat(direction).normalized;
            if (blocking && Vector3.Dot(transform.forward, -dirFlat) > 0.3f)
            {
                amount *= 0.25f;
                AudioManager.Play(Sfx.Clang, point, 1f, 0.9f);
                Fx.Sparks(point, 12, new Color(1f, 0.9f, 0.6f));
            }
            else AudioManager.Play(Sfx.Thud, point, 1f);
            health -= amount;
            lastDamage = Time.time;
            hurt = 1f;
            knock = dirFlat * 4.5f;
            if (DigProgress >= 0f) StopDig();
            var g = Game.I;
            if (g != null)
            {
                g.FlashDamage();
                if (g.Cam != null) g.Cam.Shake(0.25f);
            }
            if (health <= 0f)
            {
                health = 0f;
                dead = true;
                deadT = 0f;
                attackT = -1f;
                if (g != null) g.OnCaptainDied();
            }
        }
    }
}
