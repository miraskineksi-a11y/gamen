using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    [Serializable]
    public struct ShipStats
    {
        public float maxHealth, maxSpeed, turnRate, cannonDamage, reload;
        public int guns;

        public static ShipStats PirateSloop => new ShipStats { maxHealth = 70, maxSpeed = 10.5f, turnRate = 32, cannonDamage = 9, reload = 3.8f, guns = 3 };
        public static ShipStats PirateBrig => new ShipStats { maxHealth = 150, maxSpeed = 9f, turnRate = 23, cannonDamage = 11, reload = 4.2f, guns = 4 };
    }

    /// <summary>
    /// Корабль. Движение кинематическое (своя простая физика): паруса задают целевую
    /// скорость, руль — поворот, ветер — множитель скорости. Корпус качается на волнах
    /// WaveField. Корневой объект всегда на уровне моря, качка применяется к модели.
    /// </summary>
    public class Ship : MonoBehaviour, IDamageable
    {
        public static readonly List<Ship> All = new List<Ship>();
        public const float CannonSpeed = 62f;
        public const float CannonGravity = 14f;

        public class Gun
        {
            public Transform muzzle, gun;
            public int side; // -1 левый борт, +1 правый
            public float z;
            public Vector3 restPos;
        }

        [Header("Состояние")]
        public Team team = Team.Pirates;
        public bool isPlayer;
        public string displayName = "Корабль";
        public ShipStats stats;
        public float health;
        [Range(0, 3)] public int sailLevel;
        [Range(-1, 1)] public float rudder;
        public bool anchored;
        public float speed;

        public bool Sinking { get; private set; }
        public bool IsAlive => !Sinking && health > 0f;
        public Team Team => team;
        public float Health01 => stats.maxHealth > 0 ? Mathf.Clamp01(health / stats.maxHealth) : 0f;
        public float HalfLength { get; private set; } = 7f;
        public float HalfBeam { get; private set; } = 2.3f;
        public Vector3 Velocity => transform.forward * speed + push;
        public float LastHitTime { get; private set; } = -100f;
        public event Action<Ship> Sunk;

        public Transform Model { get; private set; }
        public Transform Helm { get; private set; }
        public BoxCollider Box { get; private set; }

        readonly List<Gun> guns = new List<Gun>();
        readonly List<Transform> sails = new List<Transform>();
        readonly List<Vector3> sailScale = new List<Vector3>();
        Transform wheel, flag;
        Quaternion wheelRest, flagRest;
        ParticleSystem wake;
        readonly List<ParticleSystem> fires = new List<ParticleSystem>();
        float reloadL, reloadR;
        float yawRate, heave, pitch, roll, recoil, recoilVel, sailVisual, wheelAngle, flagYaw;
        Vector3 push;
        float sinkT, lastBump;
        static readonly Collider[] overlap = new Collider[32];

        // ------------------------------------------------------------------ создание

        public static Ship Create(string modelName, Vector3 pos, float yaw, Team team, ShipStats stats, string displayName)
        {
            var go = new GameObject(displayName);
            go.transform.SetPositionAndRotation(new Vector3(pos.x, 0f, pos.z), Quaternion.Euler(0f, yaw, 0f));
            var ship = go.AddComponent<Ship>();
            ship.team = team;
            ship.stats = stats;
            ship.displayName = displayName;
            ship.Build(modelName);
            return ship;
        }

        void Build(string modelName)
        {
            Model = Models.Spawn(modelName, transform.position, transform.rotation, transform, 16f).transform;
            var hull = Util.FindDeep(Model, "Hull");
            var b = Util.LocalBounds(transform, hull != null ? hull : Model);
            HalfLength = Mathf.Max(3f, b.extents.z);
            HalfBeam = Mathf.Max(1.5f, b.extents.x);
            Box = gameObject.AddComponent<BoxCollider>();
            float top = Mathf.Min(b.max.y, 3.2f);
            Box.center = new Vector3(b.center.x, (b.min.y + top) * 0.5f, b.center.z);
            Box.size = new Vector3(b.size.x * 0.92f, top - b.min.y, b.size.z * 0.9f);
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            Helm = Util.FindDeep(Model, "Helm");
            wheel = Util.FindDeep(Model, "Wheel");
            if (wheel != null) wheelRest = wheel.localRotation;
            flag = Util.FindDeep(Model, "Flag");
            if (flag != null) flagRest = flag.localRotation;
            foreach (var s in Util.FindAllPrefix(Model, "Sail_"))
            {
                sails.Add(s);
                sailScale.Add(s.localScale);
            }
            foreach (var m in Util.FindAllPrefix(Model, "CannonMuzzle_"))
            {
                string suffix = m.name.Substring("CannonMuzzle_".Length);
                var g = new Gun { muzzle = m, gun = Util.FindDeep(Model, "CannonGun_" + suffix) };
                var lp = transform.InverseTransformPoint(m.position);
                g.side = lp.x >= 0f ? 1 : -1;
                g.z = lp.z;
                if (g.gun != null)
                {
                    g.restPos = g.gun.localPosition;
                    // жерло двигаем вместе с пушкой (в FBX иерархия плоская)
                    m.SetParent(g.gun, true);
                }
                guns.Add(g);
            }
            SetGunCount(stats.guns);

            var wakeMarker = Util.FindDeep(Model, "Wake");
            wake = Fx.Wake(transform, wakeMarker != null ? transform.InverseTransformPoint(wakeMarker.position) : new Vector3(0, 0.2f, -HalfLength));
            health = stats.maxHealth;
            sailVisual = sailLevel / 3f;
        }

        /// <summary>Сколько пушек на каждом борту видно и стреляет (остальные скрыты).</summary>
        public void SetGunCount(int perSide)
        {
            stats.guns = perSide;
            for (int side = -1; side <= 1; side += 2)
            {
                var list = guns.FindAll(g => g.side == side);
                float center = 0f;
                foreach (var g in list) center += g.z;
                if (list.Count > 0) center /= list.Count;
                list.Sort((a, c) => Mathf.Abs(a.z - center).CompareTo(Mathf.Abs(c.z - center)));
                for (int i = 0; i < list.Count; i++)
                    if (list[i].gun != null) list[i].gun.gameObject.SetActive(i < perSide);
            }
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        // ------------------------------------------------------------------ обновление

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (Sinking) { UpdateSinking(dt); return; }

            float[] sailFactor = { 0f, 0.38f, 0.7f, 1f };
            float target = anchored ? 0f : stats.maxSpeed * sailFactor[Mathf.Clamp(sailLevel, 0, 3)] * Wind.SpeedFactor(transform.forward);
            float accel = target > speed ? 2.2f : 3.2f;
            speed = Mathf.MoveTowards(speed, target, accel * dt);

            float eff = 0.3f + 0.7f * Mathf.Clamp01(speed / Mathf.Max(1f, stats.maxSpeed * 0.55f));
            float targetYaw = rudder * stats.turnRate * eff;
            yawRate = Mathf.MoveTowards(yawRate, targetYaw, stats.turnRate * 2.5f * dt);
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + yawRate * dt, 0f);
            transform.position += (transform.forward * speed + push) * dt;
            push = Vector3.MoveTowards(push, Vector3.zero, 6f * dt);
            var p = transform.position;
            p.y = 0f;
            transform.position = p;

            ResolveCollisions();
            Animate(dt);

            reloadL = Mathf.Max(0f, reloadL - dt);
            reloadR = Mathf.Max(0f, reloadR - dt);
        }

        void ResolveCollisions()
        {
            if (Box == null || !Box.enabled) return;
            var center = transform.TransformPoint(Box.center);
            int n = Physics.OverlapBoxNonAlloc(center, Box.size * 0.5f, overlap, transform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = overlap[i];
                if (c == null || c == Box || c.transform.IsChildOf(transform)) continue;
                if (!Physics.ComputePenetration(Box, transform.position, transform.rotation, c, c.transform.position, c.transform.rotation, out var dir, out var dist)) continue;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) continue;
                dir.Normalize();
                var other = c.GetComponentInParent<Ship>();
                float share = other != null && other.IsAlive ? 0.5f : 1f;
                transform.position += dir * (dist * share + 0.01f);
                if (other != null && other.IsAlive) other.transform.position -= dir * dist * 0.5f;
                float into = Vector3.Dot(transform.forward * speed, -dir);
                if (into > 0.3f)
                {
                    if (into > 4.5f && Time.time > lastBump + 1.2f)
                    {
                        lastBump = Time.time;
                        AudioManager.Play(Sfx.WoodHit, transform.position + transform.forward * HalfLength, 0.9f, 0.7f);
                        if (other == null) TakeDamage(into * 1.1f, transform.position + transform.forward * HalfLength, -dir, Team.Neutral);
                        if (isPlayer && Game.I != null && Game.I.Cam != null) Game.I.Cam.Shake(0.35f);
                    }
                    speed = Mathf.Max(0f, speed - into * 0.6f);
                }
            }
        }

        void Animate(float dt)
        {
            var p = transform.position;
            var f = transform.forward * HalfLength * 0.8f;
            var r = transform.right * HalfBeam;
            float hb = WaveField.Height(p + f), hs = WaveField.Height(p - f);
            float hl = WaveField.Height(p - r), hr = WaveField.Height(p + r), hc = WaveField.Height(p);
            float tHeave = (hb + hs + hl + hr + 2f * hc) / 6f;
            float tPitch = Mathf.Atan2(hs - hb, 2f * HalfLength * 0.8f) * Mathf.Rad2Deg;
            float tRoll = Mathf.Atan2(hr - hl, 2f * HalfBeam) * Mathf.Rad2Deg;
            tRoll += Mathf.Clamp(yawRate * speed * 0.025f, -9f, 9f);
            heave = Util.Damp(heave, tHeave, 4f, dt);
            pitch = Util.Damp(pitch, tPitch, 3f, dt);
            roll = Util.Damp(roll, tRoll, 3f, dt);
            // отдача от залпа — пружина
            recoilVel += (-recoil * 40f - recoilVel * 5f) * dt;
            recoil += recoilVel * dt;
            Model.localPosition = new Vector3(0f, heave, 0f);
            Model.localRotation = Quaternion.Euler(pitch, 0f, roll + recoil);

            sailVisual = Mathf.MoveTowards(sailVisual, sailLevel / 3f, dt * 0.6f);
            float billow = 0.55f + 0.45f * Wind.SpeedFactor(transform.forward) * sailVisual;
            for (int i = 0; i < sails.Count; i++)
            {
                var s = sailScale[i];
                sails[i].localScale = new Vector3(s.x, s.y * Mathf.Lerp(0.1f, 1f, sailVisual), s.z * billow * (1f + 0.04f * Mathf.Sin(Time.time * 2.3f + i)));
            }
            if (wheel != null)
            {
                wheelAngle = Util.Damp(wheelAngle, -rudder * 150f, 4f, dt);
                wheel.localRotation = wheelRest * Quaternion.Euler(0f, 0f, wheelAngle);
            }
            if (flag != null)
            {
                var apparent = Wind.Dir * 8f - Velocity;
                var local = transform.InverseTransformDirection(apparent);
                float target = Util.YawOf(local) - 180f + Mathf.Sin(Time.time * 6f) * 6f;
                flagYaw = Mathf.LerpAngle(flagYaw, target, 1f - Mathf.Exp(-3f * dt));
                flag.localRotation = flagRest * Quaternion.Euler(0f, flagYaw, 0f);
            }
            if (wake != null)
            {
                var em = wake.emission;
                em.rateOverTime = Mathf.Max(0f, speed - 1f) * 4f;
            }
            foreach (var g in guns)
                if (g.gun != null) g.gun.localPosition = Vector3.Lerp(g.gun.localPosition, g.restPos, 1f - Mathf.Exp(-6f * dt));
        }

        // ------------------------------------------------------------------ пушки

        public bool CanFire(int side) => !Sinking && (side < 0 ? reloadL : reloadR) <= 0f;
        public float Reload01(int side) => stats.reload <= 0 ? 1f : 1f - Mathf.Clamp01((side < 0 ? reloadL : reloadR) / stats.reload);

        /// <summary>Угол возвышения ствола, чтобы ядро упало на дистанции distance.</summary>
        public static float ElevationFor(float distance)
        {
            float s = Mathf.Clamp(CannonGravity * distance / (CannonSpeed * CannonSpeed), 0f, 0.98f);
            return Mathf.Max(0.5f, 0.5f * Mathf.Asin(s) * Mathf.Rad2Deg - 0.6f);
        }

        /// <summary>Бортовой залп. side: -1 левый, +1 правый. target — точка прицеливания (для ИИ).</summary>
        public bool FireBroadside(int side, float elevation, Vector3? target = null)
        {
            if (!CanFire(side)) return false;
            if (side < 0) reloadL = stats.reload; else reloadR = stats.reload;
            StartCoroutine(FireRoutine(side, elevation, target));
            recoilVel += side * 22f;
            return true;
        }

        IEnumerator FireRoutine(int side, float elevation, Vector3? target)
        {
            var list = guns.FindAll(g => g.side == side && g.gun != null && g.gun.gameObject.activeSelf);
            if (list.Count == 0) list = guns.FindAll(g => g.side == side);
            list.Sort((a, b) => b.z.CompareTo(a.z));
            for (int i = 0; i < list.Count; i++)
            {
                if (Sinking) yield break;
                var g = list[i];
                Vector3 beam = transform.right * side;
                Vector3 dir = beam;
                if (target.HasValue)
                {
                    var to = Util.Flat(target.Value - g.muzzle.position);
                    if (to.sqrMagnitude > 1f) dir = Vector3.RotateTowards(beam, to.normalized, 25f * Mathf.Deg2Rad, 0f);
                }
                float yawJitter = UnityEngine.Random.Range(-1.6f, 1.6f);
                float el = elevation + UnityEngine.Random.Range(-0.7f, 0.7f);
                dir = Quaternion.AngleAxis(yawJitter, Vector3.up) * dir;
                dir = (dir * Mathf.Cos(el * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(el * Mathf.Deg2Rad)).normalized;
                var pos = g.muzzle.position;
                Cannonball.Spawn(pos, dir * CannonSpeed + transform.forward * speed, stats.cannonDamage, team, this);
                Fx.MuzzleSmoke(pos, dir);
                AudioManager.Play(Sfx.Cannon, pos, isPlayer ? 1f : 0.85f, UnityEngine.Random.Range(0.9f, 1.1f));
                if (g.gun != null) g.gun.localPosition = g.restPos - g.gun.parent.InverseTransformDirection(beam) * 0.35f;
                if (isPlayer && Game.I != null && Game.I.Cam != null) Game.I.Cam.Shake(0.18f);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.06f, 0.14f));
            }
        }

        /// <summary>Точки траектории ядра (для прицела игрока).</summary>
        public void PredictTrajectory(int side, float elevation, List<Vector3> pts, int maxPts = 60)
        {
            pts.Clear();
            Gun best = null;
            foreach (var g in guns)
                if (g.side == side && g.gun != null && g.gun.gameObject.activeSelf && (best == null || Mathf.Abs(g.z) < Mathf.Abs(best.z))) best = g;
            var p = best != null ? best.muzzle.position : transform.position + transform.right * side * HalfBeam + Vector3.up * 1.5f;
            var dir = transform.right * side;
            var v = (dir * Mathf.Cos(elevation * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(elevation * Mathf.Deg2Rad)) * CannonSpeed + transform.forward * speed;
            const float step = 0.06f;
            for (int i = 0; i < maxPts; i++)
            {
                pts.Add(p);
                v += Vector3.down * CannonGravity * step;
                p += v * step;
                if (p.y < -0.2f) { pts.Add(p); break; }
            }
        }

        // ------------------------------------------------------------------ урон

        public void TakeDamage(float amount, Vector3 point, Vector3 direction, Team source)
        {
            if (Sinking || amount <= 0f) return;
            if (source == team) return;
            health -= amount;
            LastHitTime = Time.time;
            Fx.WoodHit(point);
            AudioManager.Play(Sfx.WoodHit, point, 1f);
            if (isPlayer && Game.I != null)
            {
                if (Game.I.Cam != null) Game.I.Cam.Shake(0.45f);
                Game.I.FlashDamage();
            }
            UpdateFires();
            if (health <= 0f)
            {
                health = 0f;
                Sink();
            }
        }

        public void Repair(float amount)
        {
            health = Mathf.Min(stats.maxHealth, health + amount);
            UpdateFires();
        }

        void UpdateFires()
        {
            int want = Health01 < 0.25f ? 2 : Health01 < 0.5f ? 1 : 0;
            while (fires.Count < want)
            {
                var lp = new Vector3(UnityEngine.Random.Range(-HalfBeam * 0.4f, HalfBeam * 0.4f), 1.8f, UnityEngine.Random.Range(-HalfLength * 0.5f, HalfLength * 0.5f));
                fires.Add(Fx.Fire(Model, lp, 1.4f));
            }
            while (fires.Count > want)
            {
                var f = fires[fires.Count - 1];
                fires.RemoveAt(fires.Count - 1);
                if (f != null) Destroy(f.gameObject); // дым — дочерний объект огня
            }
        }

        void Sink()
        {
            if (Sinking) return;
            Sinking = true;
            sinkT = 0f;
            Fx.Explosion(transform.position + Vector3.up * 2f, 1.3f);
            AudioManager.Play(Sfx.Cannon, transform.position, 1f, 0.6f);
            Fx.Bubbles(transform, Vector3.zero, 1f);
            while (fires.Count < 3) fires.Add(Fx.Fire(Model, new Vector3(UnityEngine.Random.Range(-1f, 1f), 2f, UnityEngine.Random.Range(-HalfLength * 0.5f, HalfLength * 0.5f)), 1.6f));
            Sunk?.Invoke(this);
        }

        void UpdateSinking(float dt)
        {
            sinkT += dt;
            speed = Mathf.MoveTowards(speed, 0f, dt * 2f);
            transform.position += transform.forward * speed * dt;
            float depth = sinkT * sinkT * 0.12f + sinkT * 0.3f;
            float hw = WaveField.Height(transform.position);
            Model.localPosition = new Vector3(0f, hw - depth, 0f);
            Model.localRotation = Quaternion.Euler(Mathf.Lerp(pitch, -16f, sinkT / 8f), 0f, Mathf.Lerp(roll, 28f, sinkT / 6f));
            if (Box != null && sinkT > 1.5f) Box.enabled = false;
            if (sinkT > 2f && fires.Count > 0)
            {
                foreach (var f in fires) if (f != null) { var em = f.emission; em.rateOverTime = 0f; }
            }
            if (sinkT > 10f && !isPlayer) Destroy(gameObject);
        }

        /// <summary>Мгновенно вернуть корабль в строй (после гибели игрока).</summary>
        public void Restore(Vector3 pos, float yaw)
        {
            StopAllCoroutines();
            Sinking = false;
            sinkT = 0f;
            health = stats.maxHealth;
            speed = 0f;
            yawRate = 0f;
            push = Vector3.zero;
            sailLevel = 0;
            rudder = 0f;
            transform.SetPositionAndRotation(new Vector3(pos.x, 0f, pos.z), Quaternion.Euler(0f, yaw, 0f));
            if (Box != null) Box.enabled = true;
            foreach (var f in fires) if (f != null) Destroy(f.gameObject);
            fires.Clear();
            foreach (Transform t in transform)
                if (t.name == "Bubbles") Destroy(t.gameObject);
        }

        public void Push(Vector3 v) => push += v;
    }
}
