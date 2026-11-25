using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NeonDistrict.UI
{
    /// <summary>
    /// Гарантирует, что UI-клики работают: в сцене должен быть ровно один EventSystem
    /// с модулем ввода, у которого назначены actions.
    ///
    /// Почему это отдельный компонент: InputSystemUIInputModule, созданный
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
            // ВАЖНО: ищем только ЖИВЫЕ объекты. EventSystem.current возвращает null на этапе Awake,
            // пока сцена полностью не загружена, и раньше мы из-за этого создавали ВТОРОЙ EventSystem:
            // в сцене их оказывалось два, и ввод уходил «не в тот» — кнопки меню не нажимались.
            var eventSystem = FindLiveEventSystem();
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem");
                eventSystem = go.AddComponent<EventSystem>();
                if (go != this.gameObject) go.AddComponent<UIInputBootstrap>();
                Destroy(this);   // один экземпляр на сцену
                eventSystem = go.GetComponent<EventSystem>();
            }

            var module = eventSystem.GetComponent<BaseInputModule>();
            if (module == null)
            {
#if !ND_COMPILE_CHECK
                // тип берём по имени: офлайн reference-сборки Unity не содержат пакета Input System,
                // и прямой using сломал бы статическую проверку (а в Unity тип есть всегда)
                var moduleType = System.Type.GetType(
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (moduleType != null)
                {
                    var inputModule = eventSystem.gameObject.AddComponent(moduleType) as BaseInputModule;
                    AssignUiActions(inputModule);
                    if (logSetup) Debug.Log("[Neon District] UI: InputSystemUIInputModule с назначенными действиями.");
                }
                else
                {
                    eventSystem.gameObject.AddComponent<StandaloneInputModule>();
                    if (logSetup) Debug.LogWarning("[Neon District] UI: Input System недоступен, стоит StandaloneInputModule.");
                }
#else
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
                if (logSetup) Debug.Log("[Neon District] UI: StandaloneInputModule.");
#endif
            }
#if !ND_COMPILE_CHECK
            else if (IsInputSystemModule(module) && GetActionsAsset(module) == null)
            {
                // модуль есть, но остался без действий (сцена собрана раньше) — лечим
                AssignUiActions(module);
            }
            else if (IsInputSystemModule(module))
            {
                // ассет есть, но мог остаться выключенным — без этого модуль не видит ввод
                var asset = GetActionsAsset(module);
                if (asset is InputActionAsset actionAsset && !actionAsset.enabled) actionAsset.Enable();
                if (asset != null) EnsureEnabled(module);
            }
#endif
        }

#if !ND_COMPILE_CHECK
        /// <summary>
        /// Создаёт UI-ассет действий (point / leftClick / rightClick / middleClick / scroll /
        /// move / submit / cancel / trackedDevice*) и назначает его модулю.
        /// </summary>
        private static EventSystem FindLiveEventSystem()
        {
            var all = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all != null && all.Length > 0 ? all[0] : EventSystem.current;
        }

        private static bool IsInputSystemModule(BaseInputModule module) =>
            module != null && module.GetType().Name == "InputSystemUIInputModule";

        private static object GetActionsAsset(BaseInputModule module)
        {
            var prop = module.GetType().GetProperty("actionsAsset");
            return prop != null ? prop.GetValue(module) : null;
        }

        private static void AssignUiActions(BaseInputModule module)
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

            // Сначала активируем ассет: без этого действия не запущены и модуль ввода не получает
            // ни позицию курсора, ни клики (кнопки выглядят живыми, но не нажимаются).
            asset.Enable();

            SetProperty(module, "actionsAsset", asset);
            SetProperty(module, "point", InputActionReference.Create(point));
            SetProperty(module, "leftClick", InputActionReference.Create(leftClick));
            SetProperty(module, "rightClick", InputActionReference.Create(rightClick));
            SetProperty(module, "middleClick", InputActionReference.Create(middleClick));
            SetProperty(module, "scrollWheel", InputActionReference.Create(scroll));
            SetProperty(module, "move", InputActionReference.Create(move));
            SetProperty(module, "submit", InputActionReference.Create(submit));
            SetProperty(module, "cancel", InputActionReference.Create(cancel));
            SetProperty(module, "trackedDevicePosition", InputActionReference.Create(trackedDevicePosition));
            SetProperty(module, "trackedDeviceOrientation", InputActionReference.Create(trackedDeviceOrientation));
            SetProperty(module, "deselectOnBackgroundClick", false);

            // Модуль мог быть уже активирован — тогда его Point/Click-колбэки подписаны на СТАРЫЕ
            // действия (или на пустой ассет). Перезапуск модуля подписывает его на новые: без этого
            // курсор и клики до UI не доходят, хотя действия выглядят назначенными.
            if (module.isActiveAndEnabled)
            {
                module.enabled = false;
                module.enabled = true;
            }
        }
        /// <summary>Включат действия, назначенные модулю (на случай ассета, собранного в сцене).</summary>
        private static void EnsureEnabled(BaseInputModule module)
        {
            foreach (var name in new[] { "point", "leftClick", "rightClick", "middleClick", "scrollWheel", "move", "submit", "cancel" })
            {
                var reference = module.GetType().GetProperty(name)?.GetValue(module) as InputActionReference;
                var action = reference != null ? reference.action : null;
                if (action != null && !action.enabled) action.Enable();
            }
        }

        private static void SetProperty(object target, string name, object value)
        {
            if (target == null) return;
            var prop = target.GetType().GetProperty(name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop != null && prop.CanWrite) prop.SetValue(target, value);
        }
#endif
    }
}
