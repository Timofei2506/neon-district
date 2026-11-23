using UnityEngine;

namespace UnityEngine
{
    /// <summary>
    /// Крошечный «мост» ввода для UI-кода, которому нужен только факт нажатия
    /// (пропустить субтитр, открыть карту, пауза).
    ///
    /// Работает без прямой зависимости от Input System: если пакет
    /// com.unity.inputsystem подключён (а он подключён в манифесте) — используется он,
    /// иначе — старый Input. Это упрощает офлайн-проверку и не ломает сборку.
    /// </summary>
    public static class InputSystemBridge
    {
        public enum Key { Space, Enter, Escape, M, Tab, J }

        public static bool WasPressed(Key key)
        {
#if ENABLE_INPUT_SYSTEM && !ND_COMPILE_CHECK
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return false;
            switch (key)
            {
                case Key.Space: return kb.spaceKey.wasPressedThisFrame;
                case Key.Enter: return kb.enterKey.wasPressedThisFrame;
                case Key.Escape: return kb.escapeKey.wasPressedThisFrame;
                case Key.M: return kb.mKey.wasPressedThisFrame;
                case Key.Tab: return kb.tabKey.wasPressedThisFrame;
                case Key.J: return kb.jKey.wasPressedThisFrame;
            }
            return false;
#else
#if ENABLE_LEGACY_INPUT_MANAGER
            switch (key)
            {
                case Key.Space: return Input.GetKeyDown(KeyCode.Space);
                case Key.Enter: return Input.GetKeyDown(KeyCode.Return);
                case Key.Escape: return Input.GetKeyDown(KeyCode.Escape);
                case Key.M: return Input.GetKeyDown(KeyCode.M);
                case Key.Tab: return Input.GetKeyDown(KeyCode.Tab);
                case Key.J: return Input.GetKeyDown(KeyCode.J);
            }
#endif
            return false;
#endif
        }
    }
}
