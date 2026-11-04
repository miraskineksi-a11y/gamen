using UnityEngine;

namespace Caribbean
{
    /// <summary>Пушечное ядро: баллистика, попадания (sphere cast), всплеск при падении в воду.</summary>
    public class Cannonball : MonoBehaviour
    {
        Vector3 velocity;
        float damage;
        Team team;
        Ship owner;
        float life;
        static readonly RaycastHit[] hits = new RaycastHit[16];

        public static Cannonball Spawn(Vector3 pos, Vector3 velocity, float damage, Team team, Ship owner)
        {
            var go = Models.Spawn("Cannonball", pos, Quaternion.identity, null, 0.4f);
            go.name = "Cannonball";
            var cb = go.AddComponent<Cannonball>();
            cb.velocity = velocity;
            cb.damage = damage;
            cb.team = team;
            cb.owner = owner;
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = Palette.ParticleAlpha;
            tr.time = 0.35f;
            tr.startWidth = 0.35f;
            tr.endWidth = 0.05f;
            tr.startColor = new Color(0.9f, 0.9f, 0.9f, 0.5f);
            tr.endColor = new Color(0.9f, 0.9f, 0.9f, 0f);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return cb;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            life += dt;
            if (life > 7f) { Destroy(gameObject); return; }
            var p0 = transform.position;
            velocity += Vector3.down * Ship.CannonGravity * dt;
            var step = velocity * dt;
            var p1 = p0 + step;
            float dist = step.magnitude;

            int n = Physics.SphereCastNonAlloc(p0, 0.35f, step / Mathf.Max(dist, 1e-5f), hits, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;
                if (owner != null && h.collider.transform.IsChildOf(owner.transform)) continue;
                if (h.distance < bestD) { bestD = h.distance; best = i; }
            }
            if (best >= 0)
            {
                var h = hits[best];
                var point = h.distance > 0f ? h.point : p0;
                var target = h.collider.GetComponentInParent<IDamageable>();
                if (target != null)
                {
                    if (target.IsAlive && target.Team != team)
                    {
                        target.TakeDamage(damage, point, velocity.normalized, team);
                        if (team == Team.Player && Game.I != null) Game.I.ShowHitMarker();
                    }
                    else Fx.Sparks(point, 6, new Color(1f, 0.8f, 0.4f));
                }
                else
                {
                    Fx.Dirt(point, 1.3f);
                    Fx.Poof(point, new Color(0.6f, 0.55f, 0.45f, 0.6f), 1f);
                    AudioManager.Play(Sfx.Thud, point, 0.8f, 0.7f);
                }
                Destroy(gameObject);
                return;
            }

            float water = WaveField.Height(p1.x, p1.z);
            if (p1.y < water)
            {
                p1.y = water;
                Fx.Splash(p1, 1f);
                AudioManager.Play(Sfx.Splash, p1, 0.7f);
                Destroy(gameObject);
                return;
            }
            transform.position = p1;
        }
    }
}
