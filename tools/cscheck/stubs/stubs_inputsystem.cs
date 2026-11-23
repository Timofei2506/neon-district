// Минимальные заглушки Input System для офлайн-проверки компиляции.
// В Unity-проект не входят: настоящий com.unity.inputsystem всегда на месте.
#if ND_COMPILE_CHECK
namespace UnityEngine.InputSystem
{
    public class ButtonControl
    {
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
        public float ReadValue() => 0f;
    }

    public class KeyControl : ButtonControl { }

    public class Vector2Control
    {
        public UnityEngine.Vector2 ReadValue() => UnityEngine.Vector2.zero;
    }

    public class StickControl : Vector2Control { }

    public class Keyboard
    {
        public static Keyboard current => null;
        public KeyControl wKey => null;
        public KeyControl aKey => null;
        public KeyControl sKey => null;
        public KeyControl dKey => null;
        public KeyControl upArrowKey => null;
        public KeyControl downArrowKey => null;
        public KeyControl leftArrowKey => null;
        public KeyControl rightArrowKey => null;
        public KeyControl leftShiftKey => null;
        public KeyControl spaceKey => null;
        public KeyControl cKey => null;
        public KeyControl eKey => null;
        public KeyControl fKey => null;
    }

    public class Mouse
    {
        public static Mouse current => null;
        public StickControl delta => null;
        public ButtonControl leftButton => null;
        public ButtonControl rightButton => null;
    }

    public class Gamepad
    {
        public static Gamepad current => null;
        public StickControl leftStick => null;
        public StickControl rightStick => null;
        public ButtonControl leftStickButton => null;
        public ButtonControl buttonSouth => null;
        public ButtonControl buttonEast => null;
        public ButtonControl buttonNorth => null;
        public ButtonControl buttonWest => null;
        public ButtonControl rightTrigger => null;
    }
}
#endif
