using UnityEngine;

namespace Caribbean
{
    /// <summary>
    /// ИИ пиратского корабля: патрулирует свой район, замечает игрока, заходит бортом
    /// и даёт залп с упреждением, при сильных повреждениях убегает. Обходит острова.
    /// </summary>
    public class PirateShip : MonoBehaviour
    {
        enum Mode { Patrol, Engage, Flee }

        public Vector3 zoneCenter;
        public float zoneRadius = 250f;
        public bool isBrig;

        Ship ship;
        Mode mode;
        Vector3 patrolTarget;
        float fireDelay;
        float thinkT;
        Vector3 desiredDir;
        public bool Engaged => mode == Mode.Engage;
        public Ship Ship => ship;

        void Start()
        {
            ship = GetComponent<Ship>();
            ship.sailLevel = 2;
            PickPatrolTarget();
            desiredDir = transform.forward;
        }

        void PickPatrolTarget()
        {
            var w = Game.I != null ? Game.I.World : null;
            for (int i = 0; i < 20; i++)
            {
                var c = Random.insideUnitCircle * zoneRadius;
                var p = zoneCenter + new Vector3(c.x, 0f, c.y);
                // в логово Кракена пираты не суются
                var k = Game.I != null ? Game.I.Kraken : null;
                if (k != null && !k.Defeated && Util.FlatDistance(p, k.lair) < k.arenaRadius + 40f) continue;
                if (w == null || w.IsOpenWater(p, 60f)) { patrolTarget = p; return; }
            }
            patrolTarget = zoneCenter;
        }

        void Update()
        {
            if (ship == null || !ship.IsAlive) return;
            var g = Game.I;
            if (g == null) return;
            float dt = Time.deltaTime;
            var player = g.PlayerShip;
            bool targetable = player != null && player.IsAlive && g.State == GameState.Sailing && !g.World.InPortSafeZone(player.transform.position);
            float dist = targetable ? Util.FlatDistance(transform.position, player.transform.position) : 9999f;

            thinkT -= dt;
            if (thinkT <= 0f)
            {
                thinkT = 0.4f;
                if (ship.Health01 < 0.22f && dist < 230f) mode = Mode.Flee;
                else if (targetable && (dist < 175f || (mode == Mode.Engage && dist < 280f) || Time.time - ship.LastHitTime < 6f)) mode = Mode.Engage;
                else mode = Mode.Patrol;
            }

            Vector3 want;
            switch (mode)
            {
                case Mode.Engage:
                    want = EngageDirection(player, dist);
                    ship.sailLevel = dist > 70f ? 3 : 2;
                    TryFire(player, dist, dt);
                    break;
                case Mode.Flee:
                    want = Util.Flat(transform.position - (player != null ? player.transform.position : zoneCenter)).normalized;
                    ship.sailLevel = 3;
                    break;
                default:
                    if (Util.FlatDistance(transform.position, patrolTarget) < 45f) PickPatrolTarget();
                    want = Util.Flat(patrolTarget - transform.position).normalized;
                    ship.sailLevel = 2;
                    break;
            }
            want = Avoid(want);
            desiredDir = Vector3.Slerp(desiredDir, want, 1f - Mathf.Exp(-3f * dt));
            float ang = Vector3.SignedAngle(Util.Flat(transform.forward), desiredDir, Vector3.up);
            ship.rudder = Mathf.Clamp(ang / 35f, -1f, 1f);
        }

        Vector3 EngageDirection(Ship player, float dist)
        {
            var pp = player.transform.position + player.Velocity * 1.5f;
            var to = Util.Flat(pp - transform.position);
            if (to.sqrMagnitude < 1f) return transform.forward;
            var toN = to.normalized;
            if (dist > 95f) return toN;
            // встать бортом: игрок должен оказаться на траверзе (±90°)
            float bearing = Vector3.SignedAngle(Util.Flat(transform.forward), toN, Vector3.up);
            int side = bearing >= 0f ? 1 : -1;
            var perp = Quaternion.Euler(0f, -side * 90f, 0f) * toN;
            if (dist < 38f) perp = (perp - toN * 0.8f).normalized;   // слишком близко — отходим
            else if (dist > 70f) perp = (perp + toN * 0.5f).normalized; // далековато — сближаемся
            return perp;
        }

        void TryFire(Ship player, float dist, float dt)
        {
            if (dist > 125f) return;
            fireDelay -= dt;
            if (fireDelay > 0f) return;
            float flight = dist / (Ship.CannonSpeed * 0.97f);
            var predicted = player.transform.position + player.Velocity * flight;
            float bearing = Util.SignedAngleTo(transform, predicted);
            for (int side = -1; side <= 1; side += 2)
            {
                if (Mathf.Abs(bearing - side * 90f) < 24f && ship.CanFire(side))
                {
                    float d = Util.FlatDistance(transform.position, predicted);
                    if (ship.FireBroadside(side, Ship.ElevationFor(d) + Random.Range(-0.8f, 1.2f), predicted))
                    {
                        fireDelay = Random.Range(0.4f, 1.2f);
                        return;
                    }
                }
            }
        }

        Vector3 Avoid(Vector3 want)
        {
            float look = 36f + ship.speed * 2.5f;
            var origin = transform.position + Vector3.up * 0.8f + transform.forward * (ship.HalfLength + 1f);
            bool fwd = Blocked(origin, transform.forward, look);
            bool left = Blocked(origin, Quaternion.Euler(0, -35f, 0) * transform.forward, look * 0.75f);
            bool right = Blocked(origin, Quaternion.Euler(0, 35f, 0) * transform.forward, look * 0.75f);
            if (!fwd && !left && !right) return want;
            if (fwd || (left && right))
            {
                var side = (left && !right) ? 1f : (right && !left) ? -1f : (Vector3.SignedAngle(transform.forward, want, Vector3.up) >= 0 ? 1f : -1f);
                return Quaternion.Euler(0f, side * 80f, 0f) * Util.Flat(transform.forward).normalized;
            }
            if (left) return Quaternion.Euler(0f, 40f, 0f) * want;
            return Quaternion.Euler(0f, -40f, 0f) * want;
        }

        bool Blocked(Vector3 origin, Vector3 dir, float len)
        {
            if (Physics.SphereCast(origin, 2f, dir, out var hit, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) return false;
                var other = hit.collider.GetComponentInParent<Ship>();
                if (other != null && other.isPlayer && mode == Mode.Engage) return hit.distance < 22f;
                return true;
            }
            return false;
        }
    }
}
