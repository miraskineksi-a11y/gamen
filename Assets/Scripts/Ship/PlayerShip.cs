using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    /// <summary>
    /// Управление кораблём игрока:
    ///   W/S — поднять/убрать паруса, A/D — руль,
    ///   ЛКМ — залп бортом, на который смотрит камера, Q/E — левый/правый борт,
    ///   ПКМ (удерживать) — показать траекторию, наклон камеры меняет дальность,
    ///   F — высадиться на остров / войти в порт.
    /// </summary>
    public class PlayerShip : MonoBehaviour
    {
        public Ship Ship { get; private set; }
        public float Elevation { get; private set; } = 6f;
        public int AimSide { get; private set; } = 1;
        public bool Aiming { get; private set; }

        LineRenderer aim;
        readonly List<Vector3> pts = new List<Vector3>();
        Vector3[] ptsArray = new Vector3[64];
        float reloadMsgTime;

        void Awake()
        {
            Ship = GetComponent<Ship>();
            var go = new GameObject("AimLine");
            go.transform.SetParent(transform, false);
            aim = go.AddComponent<LineRenderer>();
            aim.sharedMaterial = Palette.ParticleAdditive;
            aim.widthMultiplier = 0.45f;
            aim.startColor = new Color(1f, 0.85f, 0.4f, 0.9f);
            aim.endColor = new Color(1f, 0.4f, 0.2f, 0.5f);
            aim.useWorldSpace = true;
            aim.numCapVertices = 2;
            aim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aim.enabled = false;
        }

        void Update()
        {
            var g = Game.I;
            if (g == null || Ship == null) return;
            float dt = Time.deltaTime;
            bool control = g.State == GameState.Sailing && Ship.IsAlive;
            if (!control)
            {
                Ship.rudder = Mathf.MoveTowards(Ship.rudder, 0f, dt * 2f);
                aim.enabled = false;
                Aiming = false;
                return;
            }

            if (GameInput.KeyDown(GKey.W) || GameInput.KeyDown(GKey.Up))
            {
                Ship.anchored = false;
                if (Ship.sailLevel < 3) { Ship.sailLevel++; AudioManager.PlayUI(Sfx.Swoosh, 0.5f, 0.8f); }
            }
            if (GameInput.KeyDown(GKey.S) || GameInput.KeyDown(GKey.Down))
            {
                if (Ship.sailLevel > 0) { Ship.sailLevel--; AudioManager.PlayUI(Sfx.Swoosh, 0.5f, 0.7f); }
            }
            float steer = (GameInput.Key(GKey.D) || GameInput.Key(GKey.Right) ? 1f : 0f) - (GameInput.Key(GKey.A) || GameInput.Key(GKey.Left) ? 1f : 0f);
            Ship.rudder = Mathf.MoveTowards(Ship.rudder, steer, dt * 2.5f);

            // прицеливание: борт — тот, куда смотрит камера; дальность — наклон камеры
            var cam = g.Cam;
            if (cam != null)
            {
                AimSide = Vector3.Dot(cam.transform.forward, transform.right) >= 0f ? 1 : -1;
                Elevation = Mathf.Clamp(11f - cam.Pitch * 0.45f, 1.5f, 20f);
            }
            Aiming = GameInput.Mouse(1);
            aim.enabled = Aiming;
            if (Aiming)
            {
                Ship.PredictTrajectory(AimSide, Elevation, pts, 64);
                if (ptsArray.Length < pts.Count) ptsArray = new Vector3[pts.Count];
                pts.CopyTo(ptsArray);
                aim.positionCount = pts.Count;
                aim.SetPositions(ptsArray);
            }
            if (GameInput.MouseDown(0)) Fire(AimSide);
            if (GameInput.KeyDown(GKey.Q)) Fire(-1);
            if (GameInput.KeyDown(GKey.E)) Fire(1);

            Interactions(g);
        }

        void Fire(int side)
        {
            if (Ship.FireBroadside(side, Elevation)) return;
            if (Time.time > reloadMsgTime)
            {
                reloadMsgTime = Time.time + 1.5f;
                Game.I.Toast(side < 0 ? "Левый борт перезаряжается…" : "Правый борт перезаряжается…", UiColors.Muted);
            }
        }

        void Interactions(Game g)
        {
            var world = g.World;
            if (world == null) return;
            var pos = transform.position;

            // граница мира
            float r = world.DistanceFromCenter(pos);
            if (r > WorldBuilder.WorldRadius - 60f)
            {
                g.SetPrompt("Дальше — открытое море и шторма. Поворачивайте!");
                if (r > WorldBuilder.WorldRadius)
                    Ship.Push(-(Util.Flat(pos - world.Center)).normalized * (r - WorldBuilder.WorldRadius + 2f) * 2f);
            }

            var port = world.PortIsland;
            if (port != null && port.PortDock != null)
            {
                float d = Util.FlatDistance(pos, port.PortDock.position);
                if (d < 60f)
                {
                    if (Ship.speed < 6.5f)
                    {
                        g.SetPrompt("[F] Войти в порт Санта-Роза");
                        if (GameInput.KeyDown(GKey.F)) g.EnterPort();
                    }
                    else g.SetPrompt("Сбавьте ход (S), чтобы войти в порт");
                    return;
                }
            }

            var isl = world.NearestLanding(pos, out float dl);
            if (isl == null || isl.IsPort) return;
            if (dl < 85f)
            {
                if (g.Kraken != null && g.Kraken.Fighting && isl.Def.id == "skull")
                {
                    g.SetPrompt("Кракен не даёт подойти к берегу!");
                    return;
                }
                if (Ship.speed < 5f)
                {
                    g.SetPrompt("[F] Высадиться на берег: " + isl.Def.title);
                    if (GameInput.KeyDown(GKey.F)) g.GoAshore(isl);
                }
                else g.SetPrompt("Сбавьте ход (S), чтобы высадиться: " + isl.Def.title);
            }
        }
    }
}
