using UnityEngine;

namespace UnityEngine
{
    /// <summary>
    /// Единый тонкий мост ввода для кода, которому нужен только «факт нажатия» клавиши или позиция
    /// курсора (меню, гараж, подсказки HUD, ручник).
    ///
    /// Важно: в настройках проекта активен ТОЛЬКО новый Input System (activeInputHandler = 1).
    /// При таком режиме любой вызов старого UnityEngine.Input бросает
    /// InvalidOperationException и ломает тот кадр, в котором был вызван — поэтому legacy-ветки
    /// здесь полностью убраны, а при отсутствии пакета мост просто возвращает false.
    /// </summary>
    public static class InputBridge
    {
        public static bool HasInputSystem
        {
            get
            {
#if ND_COMPILE_CHECK
                return false;
#else
                return true;
#endif
            }
        }

        // ------------------------------------------------------------------ клавиатура

        /// <summary>Нажата ли клавиша в этом кадре.</summary>
        public static bool KeyDown(KeyCode key)
        {
#if ND_COMPILE_CHECK
            return false;
#else
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return false;
            switch (key)
            {
                case KeyCode.W: return kb.wKey.wasPressedThisFrame;
                case KeyCode.A: return kb.aKey.wasPressedThisFrame;
                case KeyCode.S: return kb.sKey.wasPressedThisFrame;
                case KeyCode.D: return kb.dKey.wasPressedThisFrame;
                case KeyCode.G: return kb.gKey.wasPressedThisFrame;
                case KeyCode.M: return kb.mKey.wasPressedThisFrame;
                case KeyCode.J: return kb.jKey.wasPressedThisFrame;
                case KeyCode.R: return kb.rKey.wasPressedThisFrame;
                case KeyCode.C: return kb.cKey.wasPressedThisFrame;
                case KeyCode.E: return kb.eKey.wasPressedThisFrame;
                case KeyCode.F: return kb.fKey.wasPressedThisFrame;
                case KeyCode.F5: return kb.f5Key.wasPressedThisFrame;
                case KeyCode.Space: return kb.spaceKey.wasPressedThisFrame;
                case KeyCode.Return: return kb.enterKey.wasPressedThisFrame;
                case KeyCode.KeypadEnter: return kb.numpadEnterKey.wasPressedThisFrame;
                case KeyCode.Escape: return kb.escapeKey.wasPressedThisFrame;
                case KeyCode.Tab: return kb.tabKey.wasPressedThisFrame;
                case KeyCode.UpArrow: return kb.upArrowKey.wasPressedThisFrame;
                case KeyCode.DownArrow: return kb.downArrowKey.wasPressedThisFrame;
                case KeyCode.LeftArrow: return kb.leftArrowKey.wasPressedThisFrame;
                case KeyCode.RightArrow: return kb.rightArrowKey.wasPressedThisFrame;
                default: return false;
            }
#endif
        }

        /// <summary>Клавиша удерживается (например, ручник).</summary>
        public static bool KeyHeld(KeyCode key)
        {
#if ND_COMPILE_CHECK
            return false;
#else
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return false;
            switch (key)
            {
                case KeyCode.Space: return kb.spaceKey.isPressed;
                case KeyCode.LeftShift: return kb.leftShiftKey.isPressed;
                case KeyCode.LeftControl: return kb.leftCtrlKey.isPressed;
                default: return false;
            }
#endif
        }

        /// <summary>Любая клавиша нажата в этом кадре (для «пропустить заставку»).</summary>
        public static bool AnyKeyDown
        {
            get
            {
#if ND_COMPILE_CHECK
                return false;
#else
                var kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && kb.anyKey.wasPressedThisFrame;
#endif
            }
        }

        // ------------------------------------------------------------------ мышь

        public static Vector2 MousePosition
        {
            get
            {
#if ND_COMPILE_CHECK
                return Vector2.zero;
#else
                var mouse = UnityEngine.InputSystem.Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#endif
            }
        }

        public static bool MouseLeftPressed
        {
            get
            {
#if ND_COMPILE_CHECK
                return false;
#else
                var mouse = UnityEngine.InputSystem.Mouse.current;
                return mouse != null && mouse.leftButton.wasPressedThisFrame;
#endif
            }
        }
    }

    /// <summary>Старое имя моста — оставлено, чтобы не ломать существующий код HUD.</summary>
    public static class InputSystemBridge
    {
        public enum Key { Space, Enter, Escape, M, Tab, J }

        public static bool WasPressed(Key key)
        {
            switch (key)
            {
                case Key.Space: return InputBridge.KeyDown(KeyCode.Space);
                case Key.Enter: return InputBridge.KeyDown(KeyCode.Return);
                case Key.Escape: return InputBridge.KeyDown(KeyCode.Escape);
                case Key.M: return InputBridge.KeyDown(KeyCode.M);
                case Key.Tab: return InputBridge.KeyDown(KeyCode.Tab);
                case Key.J: return InputBridge.KeyDown(KeyCode.J);
                default: return false;
            }
        }
    }
}
