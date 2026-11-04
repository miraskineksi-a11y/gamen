using UnityEngine;
#if ENABLE_INPUT_SYSTEM && CARIB_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Caribbean
{
    /// <summary>Клавиши, которые использует игра.</summary>
    public enum GKey
    {
        W, A, S, D, Q, E, F, M, R, H, Space, Shift, Escape, Tab, Enter,
        Up, Down, Left, Right, F1, Alpha1, Alpha2, Alpha3, Alpha4
    }

    /// <summary>
    /// Единая точка чтения ввода. Работает и со старым Input Manager, и с новым
    /// Input System (пакет com.unity.inputsystem). Если включены оба — используется старый.
    /// </summary>
    public static class GameInput
    {
        /// <summary>Чувствительность мыши (меняется в меню паузы).</summary>
        public static float MouseSensitivity = 1f;

        /// <summary>true, если ввод недоступен (нужно включить Active Input Handling = Both).</summary>
        public static bool Unavailable
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER || (ENABLE_INPUT_SYSTEM && CARIB_INPUT_SYSTEM)
                return false;
#else
                return true;
#endif
            }
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        static KeyCode Map(GKey k)
        {
            switch (k)
            {
                case GKey.W: return KeyCode.W;
                case GKey.A: return KeyCode.A;
                case GKey.S: return KeyCode.S;
                case GKey.D: return KeyCode.D;
                case GKey.Q: return KeyCode.Q;
                case GKey.E: return KeyCode.E;
                case GKey.F: return KeyCode.F;
                case GKey.M: return KeyCode.M;
                case GKey.R: return KeyCode.R;
                case GKey.H: return KeyCode.H;
                case GKey.Space: return KeyCode.Space;
                case GKey.Shift: return KeyCode.LeftShift;
                case GKey.Escape: return KeyCode.Escape;
                case GKey.Tab: return KeyCode.Tab;
                case GKey.Enter: return KeyCode.Return;
                case GKey.Up: return KeyCode.UpArrow;
                case GKey.Down: return KeyCode.DownArrow;
                case GKey.Left: return KeyCode.LeftArrow;
                case GKey.Right: return KeyCode.RightArrow;
                case GKey.F1: return KeyCode.F1;
                case GKey.Alpha1: return KeyCode.Alpha1;
                case GKey.Alpha2: return KeyCode.Alpha2;
                case GKey.Alpha3: return KeyCode.Alpha3;
                case GKey.Alpha4: return KeyCode.Alpha4;
            }
            return KeyCode.None;
        }

        public static bool Key(GKey k) => Input.GetKey(Map(k)) || (k == GKey.Shift && Input.GetKey(KeyCode.RightShift));
        public static bool KeyDown(GKey k) => Input.GetKeyDown(Map(k));
        public static bool KeyUp(GKey k) => Input.GetKeyUp(Map(k));
        public static bool Mouse(int b) => Input.GetMouseButton(b);
        public static bool MouseDown(int b) => Input.GetMouseButtonDown(b);
        public static bool MouseUp(int b) => Input.GetMouseButtonUp(b);

        /// <summary>Сдвиг мыши за кадр (примерно в пикселях).</summary>
        public static Vector2 MouseDelta
        {
            get
            {
                try { return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f; }
                catch (System.ArgumentException) { return Vector2.zero; }
            }
        }

        /// <summary>Прокрутка колеса: +1 / -1 за щелчок.</summary>
        public static float Scroll => Input.mouseScrollDelta.y;

#elif ENABLE_INPUT_SYSTEM && CARIB_INPUT_SYSTEM
        static UnityEngine.InputSystem.Key Map(GKey k)
        {
            switch (k)
            {
                case GKey.W: return UnityEngine.InputSystem.Key.W;
                case GKey.A: return UnityEngine.InputSystem.Key.A;
                case GKey.S: return UnityEngine.InputSystem.Key.S;
                case GKey.D: return UnityEngine.InputSystem.Key.D;
                case GKey.Q: return UnityEngine.InputSystem.Key.Q;
                case GKey.E: return UnityEngine.InputSystem.Key.E;
                case GKey.F: return UnityEngine.InputSystem.Key.F;
                case GKey.M: return UnityEngine.InputSystem.Key.M;
                case GKey.R: return UnityEngine.InputSystem.Key.R;
                case GKey.H: return UnityEngine.InputSystem.Key.H;
                case GKey.Space: return UnityEngine.InputSystem.Key.Space;
                case GKey.Shift: return UnityEngine.InputSystem.Key.LeftShift;
                case GKey.Escape: return UnityEngine.InputSystem.Key.Escape;
                case GKey.Tab: return UnityEngine.InputSystem.Key.Tab;
                case GKey.Enter: return UnityEngine.InputSystem.Key.Enter;
                case GKey.Up: return UnityEngine.InputSystem.Key.UpArrow;
                case GKey.Down: return UnityEngine.InputSystem.Key.DownArrow;
                case GKey.Left: return UnityEngine.InputSystem.Key.LeftArrow;
                case GKey.Right: return UnityEngine.InputSystem.Key.RightArrow;
                case GKey.F1: return UnityEngine.InputSystem.Key.F1;
                case GKey.Alpha1: return UnityEngine.InputSystem.Key.Digit1;
                case GKey.Alpha2: return UnityEngine.InputSystem.Key.Digit2;
                case GKey.Alpha3: return UnityEngine.InputSystem.Key.Digit3;
                case GKey.Alpha4: return UnityEngine.InputSystem.Key.Digit4;
            }
            return UnityEngine.InputSystem.Key.None;
        }

        static UnityEngine.InputSystem.Controls.KeyControl Ctl(GKey k)
        {
            var kb = Keyboard.current;
            return kb == null ? null : kb[Map(k)];
        }

        public static bool Key(GKey k)
        {
            var c = Ctl(k);
            if (c == null) return false;
            if (k == GKey.Shift && Keyboard.current.rightShiftKey.isPressed) return true;
            return c.isPressed;
        }
        public static bool KeyDown(GKey k) { var c = Ctl(k); return c != null && c.wasPressedThisFrame; }
        public static bool KeyUp(GKey k) { var c = Ctl(k); return c != null && c.wasReleasedThisFrame; }

        static UnityEngine.InputSystem.Controls.ButtonControl Btn(int b)
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m == null) return null;
            return b == 0 ? m.leftButton : b == 1 ? m.rightButton : m.middleButton;
        }
        public static bool Mouse(int b) { var c = Btn(b); return c != null && c.isPressed; }
        public static bool MouseDown(int b) { var c = Btn(b); return c != null && c.wasPressedThisFrame; }
        public static bool MouseUp(int b) { var c = Btn(b); return c != null && c.wasReleasedThisFrame; }

        public static Vector2 MouseDelta
        {
            get
            {
                var m = UnityEngine.InputSystem.Mouse.current;
                return m == null ? Vector2.zero : m.delta.ReadValue();
            }
        }

        public static float Scroll
        {
            get
            {
                var m = UnityEngine.InputSystem.Mouse.current;
                if (m == null) return 0f;
                float y = m.scroll.ReadValue().y;
                if (Mathf.Abs(y) > 10f) y /= 120f; // на Windows один щелчок = 120
                return Mathf.Clamp(y, -3f, 3f);
            }
        }
#else
        // Ввод недоступен: в Project Settings > Player > Active Input Handling
        // выберите "Both" (или установите пакет Input System).
        public static bool Key(GKey k) => false;
        public static bool KeyDown(GKey k) => false;
        public static bool KeyUp(GKey k) => false;
        public static bool Mouse(int b) => false;
        public static bool MouseDown(int b) => false;
        public static bool MouseUp(int b) => false;
        public static Vector2 MouseDelta => Vector2.zero;
        public static float Scroll => 0f;
#endif

        /// <summary>WASD/стрелки как вектор (x — вправо, y — вперёд).</summary>
        public static Vector2 Move
        {
            get
            {
                float x = (Key(GKey.D) || Key(GKey.Right) ? 1f : 0f) - (Key(GKey.A) || Key(GKey.Left) ? 1f : 0f);
                float y = (Key(GKey.W) || Key(GKey.Up) ? 1f : 0f) - (Key(GKey.S) || Key(GKey.Down) ? 1f : 0f);
                var v = new Vector2(x, y);
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }
    }
}
