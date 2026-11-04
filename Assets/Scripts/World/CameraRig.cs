using UnityEngine;

namespace Caribbean
{
    /// <summary>
    /// Камера от третьего лица: вращение мышью, приближение колесом, не уходит под воду
    /// и в скалы, умеет трястись. В главном меню медленно облетает порт.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public float Yaw, Pitch = 16f;
        public float sensitivity = 0.12f;

        Transform target;
        Vector3 offset;
        float distance = 26f, minDist = 14f, maxDist = 48f, minPitch = -12f, maxPitch = 65f, zoomStep = 2.5f;
        Vector3 pivot;
        float shake, shakeSeed;
        bool menu;
        Vector3 menuCenter;
        float menuRadius, menuHeight, menuAngle;
        static readonly RaycastHit[] hits = new RaycastHit[16];

        public static CameraRig Create()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            else if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            var rig = cam.gameObject.AddComponent<CameraRig>();
            rig.Cam = cam;
            cam.fieldOfView = 60f;
            return rig;
        }

        public void FollowShip(Ship ship, bool snap = false)
        {
            menu = false;
            target = ship.transform;
            offset = new Vector3(0f, 5.5f, 0f);
            minDist = 13f; maxDist = 52f; zoomStep = 2.5f;
            minPitch = -12f; maxPitch = 65f;
            distance = Mathf.Clamp(distance < minDist ? 26f : distance, minDist, maxDist);
            if (snap) Snap();
        }

        public void FollowCaptain(Captain c, bool snap = false)
        {
            menu = false;
            target = c.transform;
            offset = new Vector3(0f, 1.65f, 0f);
            minDist = 2.8f; maxDist = 11f; zoomStep = 0.8f;
            minPitch = -30f; maxPitch = 70f;
            distance = 6f;
            if (snap) Snap();
        }

        public void MenuOrbit(Vector3 center, float radius, float height)
        {
            menu = true;
            menuCenter = center;
            menuRadius = radius;
            menuHeight = height;
        }

        public void Snap()
        {
            if (target == null) return;
            Yaw = target.eulerAngles.y;
            pivot = target.position + offset;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (menu)
            {
                menuAngle += dt * 3f;
                var p = menuCenter + Quaternion.Euler(0f, menuAngle, 0f) * new Vector3(0f, menuHeight, -menuRadius);
                transform.position = p;
                transform.rotation = Quaternion.LookRotation(menuCenter + Vector3.up * 6f - p);
                Yaw = transform.eulerAngles.y;
                return;
            }
            if (target == null) return;
            bool paused = Time.timeScale < 0.01f;
            if (!paused && Cursor.lockState == CursorLockMode.Locked)
            {
                var md = GameInput.MouseDelta * sensitivity * GameInput.MouseSensitivity;
                Yaw += md.x;
                Pitch = Mathf.Clamp(Pitch - md.y, minPitch, maxPitch);
                float sc = GameInput.Scroll;
                if (Mathf.Abs(sc) > 0.01f) distance = Mathf.Clamp(distance - sc * zoomStep, minDist, maxDist);
            }
            Pitch = Mathf.Clamp(Pitch, minPitch, maxPitch);
            var want = target.position + offset;
            pivot = paused ? pivot : Util.Damp(pivot, want, 12f, dt);
            if ((pivot - want).sqrMagnitude > 400f) pivot = want;
            var rot = Quaternion.Euler(Pitch, Yaw, 0f);
            var dir = rot * Vector3.back;
            float d = distance;
            int n = Physics.SphereCastNonAlloc(pivot, 0.35f, dir, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.collider.transform.IsChildOf(target)) continue;
                if (h.distance > 0f && h.distance < d) d = Mathf.Max(1.2f, h.distance - 0.2f);
            }
            var pos = pivot + dir * d;
            float water = WaveField.Height(pos.x, pos.z) + 0.8f;
            if (pos.y < water) pos.y = water;
            if (shake > 0f && !paused)
            {
                shakeSeed += dt * 25f;
                var j = new Vector3(Mathf.PerlinNoise(shakeSeed, 0f) - 0.5f, Mathf.PerlinNoise(0f, shakeSeed) - 0.5f, 0f) * shake * 1.2f;
                pos += rot * j;
                shake = Mathf.MoveTowards(shake, 0f, dt * 1.6f);
            }
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pivot - pos + Vector3.up * 0.001f));
        }
    }
}
