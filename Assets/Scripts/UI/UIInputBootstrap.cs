using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace NeonDistrict.UI
{
    /// <summary>
    /// Гарантирует, что UI-клики работают: в сцене должен быть ровно один EventSystem
    /// с модулем ввода, у которого назначены actions.
    ///
    /// Почему это отдельный компонент: <see cref="InputSystemUIInputModule"/>, созданный
    /// через AddComponent, приходит с ПУСТЫМИ действиями (нет UI-ассета) — событий мыши он
    /// не получает, и кнопки меню не нажимаются, хотя Cursor.visible = true и GraphicRaycaster
    /// на месте. Поэтому мы создаём ассет действий в рантайме и подсовываем его модулю.
    ///
    /// Работает и без нового Input System: если пакета нет/модуль недоступен, ставится
    /// StandaloneInputModule (старый менеджер ввода) — клики остаются живыми.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public class UIInputBootstrap : MonoBehaviour
    {
        public static UIInputBootstrap Instance { get; private set; }

        [Tooltip("Печатать в лог, какой модуль ввода в итоге выбран")]
        public bool logSetup = true;

        private void Awake()
        {
            Instance = this;
            Setup();
        }

        private void OnEnable()
        {
            // сцена может перезагрузиться — проверяем ещё раз
            Setup();
        }

        public void Setup()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem");
                eventSystem = go.AddComponent<EventSystem>();
                go.AddComponent<UIInputBootstrap>();
                if (go != this.gameObject) Destroy(this);   // один экземпляр на сцену
            }

            var module = eventSystem.GetComponent<BaseInputModule>();
            if (module == null)
            {
#if !ND_COMPILE_CHECK
                var inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                AssignUiActions(inputModule);
                if (logSetup) Debug.Log("[Neon District] UI: InputSystemUIInputModule с назначенными действиями.");
#else
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
                if (logSetup) Debug.Log("[Neon District] UI: StandaloneInputModule.");
#endif
            }
#if !ND_COMPILE_CHECK
            else if (module is InputSystemUIInputModule existing)
            {
                // модуль есть, но мог остаться без действий (сцена собрана раньше) — лечим
                if (existing.actionsAsset == null) AssignUiActions(existing);
            }
#endif
        }

#if !ND_COMPILE_CHECK
        /// <summary>
        /// Создаёт UI-ассет действий (point / leftClick / rightClick / middleClick / scroll /
        /// move / submit / cancel / trackedDevice*) и назначает его модулю.
        /// </summary>
        private static void AssignUiActions(InputSystemUIInputModule module)
        {
            if (module == null) return;

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "NeonDistrict_UI_Actions";

            var map = asset.AddActionMap("UI");

            var point = map.AddAction("Point", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            point.AddBinding("<Mouse>/position");
            point.AddBinding("<Pen>/position");
            point.AddBinding("<Touchscreen>/touch*/position");

            var leftClick = map.AddAction("Click", InputActionType.PassThrough, expectedControlLayout: "Button");
            leftClick.AddBinding("<Mouse>/leftButton");
            leftClick.AddBinding("<Pen>/tip");
            leftClick.AddBinding("<Touchscreen>/touch*/press");

            var rightClick = map.AddAction("RightClick", InputActionType.PassThrough, expectedControlLayout: "Button");
            rightClick.AddBinding("<Mouse>/rightButton");

            var middleClick = map.AddAction("MiddleClick", InputActionType.PassThrough, expectedControlLayout: "Button");
            middleClick.AddBinding("<Mouse>/middleButton");

            var scroll = map.AddAction("Scroll", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            scroll.AddBinding("<Mouse>/scroll");

            var move = map.AddAction("Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            move.AddBinding("2DVector", null, "2DVector");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            var submit = map.AddAction("Submit", InputActionType.Button, expectedControlLayout: "Button");
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/numpadEnter");
            submit.AddBinding("<Keyboard>/space");
            submit.AddBinding("<Gamepad>/buttonSouth");

            var cancel = map.AddAction("Cancel", InputActionType.Button, expectedControlLayout: "Button");
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");

            // действия для указателей/устройств: нужны модулю, чтобы отслеживать мышь и тач
            var trackedDevicePosition = map.AddAction("TrackedDevicePosition", InputActionType.PassThrough, expectedControlLayout: "Vector3");
            trackedDevicePosition.AddBinding("<TrackedDevice>/devicePosition");

            var trackedDeviceOrientation = map.AddAction("TrackedDeviceOrientation", InputActionType.PassThrough, expectedControlLayout: "Quaternion");
            trackedDeviceOrientation.AddBinding("<TrackedDevice>/deviceRotation");

            module.actionsAsset = asset;
            module.point = new InputActionReference(point, asset);
            module.leftClick = new InputActionReference(leftClick, asset);
            module.rightClick = new InputActionReference(rightClick, asset);
            module.middleClick = new InputActionReference(middleClick, asset);
            module.scrollWheel = new InputActionReference(scroll, asset);
            module.move = new InputActionReference(move, asset);
            module.submit = new InputActionReference(submit, asset);
            module.cancel = new InputActionReference(cancel, asset);
            module.trackedDevicePosition = new InputActionReference(trackedDevicePosition, asset);
            module.trackedDeviceOrientation = new InputActionReference(trackedDeviceOrientation, asset);

            // клик по кнопке не должен требовать «фокуса» — снимаем требование, если поле есть
            module.deselectOnBackgroundClick = false;
        }
#endif
    }
}
