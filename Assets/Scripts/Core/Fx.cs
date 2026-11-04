using System;
using UnityEngine;

namespace Caribbean
{
    /// <summary>Визуальные эффекты на системах частиц, созданных кодом.</summary>
    public static class Fx
    {
        static Gradient Fade(Color a, Color b, float alphaMid = 1f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alphaMid, 0.1f), new GradientAlphaKey(alphaMid * 0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// <summary>Создать систему частиц. configure вызывается до запуска.</summary>
        public static ParticleSystem Make(string name, Vector3 pos, bool additive, Action<ParticleSystem> configure, Transform parent = null, bool loop = false)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = additive ? Palette.ParticleAdditive : Palette.ParticleAlpha;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var main = ps.main;
            main.loop = loop;
            main.playOnAwake = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = loop ? ParticleSystemStopAction.None : ParticleSystemStopAction.Destroy;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var sh = ps.shape;
            sh.enabled = false;
            configure(ps);
            go.SetActive(true);
            return ps;
        }

        static void Burst(ParticleSystem ps, int count)
        {
            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        static void Colors(ParticleSystem ps, Gradient g)
        {
            var c = ps.colorOverLifetime;
            c.enabled = true;
            c.color = new ParticleSystem.MinMaxGradient(g);
        }

        static void Grow(ParticleSystem ps, float from, float to)
        {
            var s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
        }

        // ------------------------------------------------------------------

        public static void MuzzleSmoke(Vector3 pos, Vector3 dir)
        {
            Make("MuzzleSmoke", pos, false, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
                m.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
                m.startColor = new Color(0.9f, 0.9f, 0.88f, 0.8f);
                m.gravityModifier = -0.05f;
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 18f;
                sh.radius = 0.2f;
                ps.transform.rotation = Quaternion.LookRotation(dir);
                Burst(ps, 14);
                Colors(ps, Fade(Color.white, new Color(0.7f, 0.7f, 0.7f), 0.75f));
                Grow(ps, 0.6f, 2.6f);
                var ns = ps.limitVelocityOverLifetime;
                ns.enabled = true;
                ns.limit = 1.5f;
                ns.dampen = 0.15f;
            });
            Make("MuzzleFlash", pos, true, ps =>
            {
                var m = ps.main;
                m.startLifetime = 0.12f;
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
                m.startColor = new Color(1f, 0.75f, 0.3f, 1f);
                Burst(ps, 3);
            });
        }

        public static void Splash(Vector3 pos, float scale = 1f)
        {
            Make("Splash", pos, false, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(4f * scale, 9f * scale);
                m.startSize = new ParticleSystem.MinMaxCurve(0.5f * scale, 1.3f * scale);
                m.startColor = new Color(0.92f, 0.97f, 1f, 0.9f);
                m.gravityModifier = 1.4f;
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 14f;
                sh.radius = 0.4f * scale;
                ps.transform.rotation = Quaternion.LookRotation(Vector3.up);
                Burst(ps, (int)(22 * Mathf.Clamp(scale, 0.5f, 2.5f)));
                Colors(ps, Fade(Color.white, new Color(0.8f, 0.9f, 1f)));
            });
            Make("SplashRing", pos + Vector3.up * 0.1f, false, ps =>
            {
                var m = ps.main;
                m.startLifetime = 1.4f;
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * scale, 3f * scale);
                m.startSize = new ParticleSystem.MinMaxCurve(0.8f * scale, 1.6f * scale);
                m.startColor = new Color(1f, 1f, 1f, 0.7f);
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = 0.5f * scale;
                ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                Burst(ps, 16);
                Colors(ps, Fade(Color.white, Color.white, 0.6f));
                Grow(ps, 0.8f, 2f);
            });
        }

        public static void WoodHit(Vector3 pos)
        {
            Make("Splinters", pos, false, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 11f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.28f, 0.15f), new Color(0.75f, 0.55f, 0.3f));
                m.gravityModifier = 1.8f;
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.3f;
                Burst(ps, 24);
            });
            Make("HitSmoke", pos, false, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.8f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
                m.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
                m.startColor = new Color(0.35f, 0.33f, 0.3f, 0.7f);
                m.gravityModifier = -0.08f;
                Burst(ps, 8);
                Colors(ps, Fade(new Color(0.4f, 0.38f, 0.35f), new Color(0.6f, 0.6f, 0.6f), 0.6f));
                Grow(ps, 0.6f, 2.2f);
            });
            Sparks(pos, 10, new Color(1f, 0.7f, 0.3f));
        }

        public static void Sparks(Vector3 pos, int count, Color color)
        {
            Make("Sparks", pos, true, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
                m.startColor = color;
                m.gravityModifier = 0.8f;
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.1f;
                Burst(ps, count);
            });
        }

        public static void Explosion(Vector3 pos, float scale = 1f)
        {
            Make("Fireball", pos, true, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2f * scale, 6f * scale);
                m.startSize = new ParticleSystem.MinMaxCurve(1.5f * scale, 3.5f * scale);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.85f, 0.4f));
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.6f * scale;
                Burst(ps, 18);
                Colors(ps, Fade(new Color(1f, 0.9f, 0.5f), new Color(0.8f, 0.2f, 0.05f)));
                Grow(ps, 0.6f, 1.6f);
            });
            Make("ExplosionSmoke", pos, false, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1f * scale, 4f * scale);
                m.startSize = new ParticleSystem.MinMaxCurve(2.5f * scale, 4.5f * scale);
                m.startColor = new Color(0.2f, 0.2f, 0.2f, 0.75f);
                m.gravityModifier = -0.1f;
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.8f * scale;
                Burst(ps, 16);
                Colors(ps, Fade(new Color(0.25f, 0.23f, 0.2f), new Color(0.5f, 0.5f, 0.5f), 0.7f));
                Grow(ps, 0.5f, 2.5f);
            });
        }

        public static void Dirt(Vector3 pos, float scale = 1f)
        {
            Make("Dirt", pos, false, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2.5f * scale, 5.5f * scale);
                m.startSize = new ParticleSystem.MinMaxCurve(0.15f * scale, 0.4f * scale);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.38f, 0.25f), new Color(0.85f, 0.75f, 0.5f));
                m.gravityModifier = 1.6f;
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 30f;
                sh.radius = 0.3f;
                ps.transform.rotation = Quaternion.LookRotation(Vector3.up);
                Burst(ps, (int)(16 * scale));
            });
        }

        public static void GoldBurst(Vector3 pos)
        {
            Make("GoldSparkle", pos, true, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 1f, 0.7f));
                m.gravityModifier = 0.6f;
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 35f;
                ps.transform.rotation = Quaternion.LookRotation(Vector3.up);
                Burst(ps, 40);
            });
        }

        public static void Poof(Vector3 pos, Color color, float scale = 1f)
        {
            Make("Poof", pos, false, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1f * scale, 3f * scale);
                m.startSize = new ParticleSystem.MinMaxCurve(0.5f * scale, 1.2f * scale);
                m.startColor = color;
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.5f * scale;
                Burst(ps, 18);
                Colors(ps, Fade(color, color, 0.8f));
                Grow(ps, 0.5f, 1.8f);
            });
        }

        public static void Bones(Vector3 pos)
        {
            Make("BoneChunks", pos, false, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
                m.startColor = new Color(0.92f, 0.9f, 0.8f);
                m.gravityModifier = 2f;
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.4f;
                Burst(ps, 22);
            });
            Poof(pos, new Color(0.4f, 1f, 0.7f, 0.6f), 0.8f);
        }

        // ---------------------------------------------------------------- loops

        /// <summary>Пламя (факел, костёр, горящий корабль). Дым — дочерний объект огня.</summary>
        public static ParticleSystem Fire(Transform parent, Vector3 localPos, float size = 1f, bool smoke = true)
        {
            var fire = Make("Fire", parent.TransformPoint(localPos), true, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * size, 1.8f * size);
                m.startSize = new ParticleSystem.MinMaxCurve(0.35f * size, 0.8f * size);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.8f, 0.3f));
                m.maxParticles = 60;
                var em = ps.emission;
                em.rateOverTime = 24f;
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.15f * size;
                Colors(ps, Fade(new Color(1f, 0.9f, 0.5f), new Color(0.9f, 0.2f, 0.05f)));
                Grow(ps, 1f, 0.2f);
            }, parent, true);
            fire.transform.localPosition = localPos;
            if (smoke)
            {
                var sm = Make("Smoke", fire.transform.position + Vector3.up * 0.5f * size, false, ps =>
                {
                    var m = ps.main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
                    m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * size, 1.6f * size);
                    m.startSize = new ParticleSystem.MinMaxCurve(0.6f * size, 1.2f * size);
                    m.startColor = new Color(0.2f, 0.2f, 0.2f, 0.45f);
                    m.maxParticles = 40;
                    var em = ps.emission;
                    em.rateOverTime = 6f;
                    var sh = ps.shape;
                    sh.enabled = true;
                    sh.shapeType = ParticleSystemShapeType.Sphere;
                    sh.radius = 0.2f * size;
                    Colors(ps, Fade(new Color(0.25f, 0.25f, 0.25f), new Color(0.6f, 0.6f, 0.6f), 0.5f));
                    Grow(ps, 0.6f, 2.4f);
                }, fire.transform, true);
                sm.transform.localPosition = Vector3.up * 0.5f * size;
            }
            return fire;
        }

        /// <summary>Пена за кормой. Интенсивность регулирует корабль через emission.rateOverTime.</summary>
        public static ParticleSystem Wake(Transform parent, Vector3 localPos)
        {
            var ps = Make("Wake", parent.TransformPoint(localPos), false, p =>
            {
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1f);
                m.startSize = new ParticleSystem.MinMaxCurve(1.0f, 2.2f);
                m.startColor = new Color(1f, 1f, 1f, 0.55f);
                m.maxParticles = 300;
                var sh = p.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 1.2f;
                Colors(p, Fade(Color.white, new Color(0.85f, 0.95f, 1f), 0.6f));
                Grow(p, 0.6f, 2.4f);
            }, parent, true);
            ps.transform.localPosition = localPos;
            return ps;
        }

        public static ParticleSystem Bubbles(Transform parent, Vector3 localPos, float size = 1f)
        {
            var ps = Make("Bubbles", parent.TransformPoint(localPos), false, p =>
            {
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.4f * size, 1.2f * size);
                m.startColor = new Color(0.9f, 0.97f, 1f, 0.7f);
                m.gravityModifier = -0.2f;
                var em = p.emission;
                em.rateOverTime = 30f * size;
                var sh = p.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 3f * size;
                Colors(p, Fade(Color.white, Color.white, 0.7f));
            }, parent, true);
            ps.transform.localPosition = localPos;
            return ps;
        }

        /// <summary>Мерцающий точечный свет для огня.</summary>
        public static Light FireLight(Transform parent, Vector3 localPos, float range = 8f, float intensity = 1.6f)
        {
            var go = new GameObject("FireLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.62f, 0.28f);
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            go.AddComponent<Flicker>().baseIntensity = intensity;
            return l;
        }
    }

    /// <summary>Мерцание света огня.</summary>
    public class Flicker : MonoBehaviour
    {
        public float baseIntensity = 1.5f;
        Light l;
        float seed;
        void Awake() { l = GetComponent<Light>(); seed = UnityEngine.Random.value * 100f; }
        void Update()
        {
            if (l == null) return;
            float t = Time.time * 7f + seed;
            l.intensity = baseIntensity * (0.8f + 0.2f * Mathf.PerlinNoise(t, seed));
        }
    }
}
