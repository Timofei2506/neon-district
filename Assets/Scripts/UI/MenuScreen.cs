using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NeonDistrict.Core;

namespace NeonDistrict.UI
{
    /// <summary>
    /// Главное меню: неоновый заголовок, вертикальное меню с подсветкой и своим курсором.
    ///
    /// Своя вёрстка (вместо прежней плоской) решает два раздражающих момента:
    /// 1) курсор. В Unity 6 системный курсор в standalone-билде может «исчезать», если
    ///    Cursor.visible выставлен не в тот кадр или если мышь уходит за пределы окна.
    ///    Поэтому рисуем собственный курсор на канвасе и дополнительно каждый кадр
    ///    подтверждаем системному: видим, не заблокирован, обычная стрелка.
    /// 2) плоский тёмный фон — за панелью теперь город + живой backdrop (<see cref="MenuBackdrop"/>).
    /// </summary>
    public class MenuScreen : MonoBehaviour
    {
        [Header("Ссылки (заполняются автоматически)")]
        public Canvas canvas;
        public RectTransform panel;
        public GameObject cursor;

        private Button[] _buttons;
        private Text[] _labels;
        private RectTransform[] _buttonsRt;
        private int _selected;
        private float _time;
        private float _panelTargetAlpha;
        private CanvasGroup _panelGroup;
        private float _navCooldown;

        public Button playButton;
        public Button continueButton;
        public Button quitButton;
        public Text footer;

        public void Build(Transform parent)
        {
            var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            // тычок в EventSystem: если его нет, создаём (в старых сценах его не было вовсе)
            if (EventSystem.current == null) UIInputBootstrap.Instance?.Setup();
            _panelGroup = canvasGo.GetComponent<CanvasGroup>();
            if (_panelGroup == null) _panelGroup = canvasGo.AddComponent<CanvasGroup>();
            _panelGroup.interactable = true;
            _panelGroup.blocksRaycasts = true;

            // ---------------------------------------------------------- затемнение под текстом
            var shade = MakeImage("Shade", canvasGo.transform, NeonGfx.White, new Color(0f, 0f, 0f, 0.0f));
            Stretch(shade.rectTransform);

            // слева-снизу мягкая подложка, чтобы текст читался на любом кадре города
            var shadeLeft = MakeImage("ShadeLeft", canvasGo.transform, NeonGfx.GradientVertical, new Color(0.01f, 0.01f, 0.025f, 0.75f));
            var slRt = shadeLeft.rectTransform;
            slRt.anchorMin = new Vector2(0f, 0f);
            slRt.anchorMax = new Vector2(0.62f, 1f);
            slRt.offsetMin = slRt.offsetMax = Vector2.zero;
            shadeLeft.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // затемнение к левому краю

            // ---------------------------------------------------------- колонка меню
            panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = new Vector2(0f, 0.5f);
            panel.anchorMax = new Vector2(0f, 0.5f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.sizeDelta = new Vector2(820f, 760f);
            panel.anchoredPosition = new Vector2(120f, 0f);

            BuildTitle(panel);
            BuildButtons(panel);
            BuildFooter(panel);

            // ---------------------------------------------------------- свой курсор
            cursor = BuildCursor(canvasGo.transform).gameObject;

            SetCursorVisible(true);
        }

        // ------------------------------------------------------------------ вёрстка
        private void BuildTitle(RectTransform root)
        {
            const string title = "NEON DISTRICT";
            // «DISTRICT» отдельной строкой и с другим цветом — фирменный вид
            var line1 = Label("Title1", root, "NEON", 132, TextAnchor.UpperLeft);
            var rt1 = line1.rectTransform;
            rt1.anchorMin = rt1.anchorMax = new Vector2(0f, 1f);
            rt1.pivot = new Vector2(0f, 1f);
            rt1.anchoredPosition = new Vector2(0f, -20f);
            rt1.sizeDelta = new Vector2(820f, 150f);
            line1.color = new Color(1f, 0.97f, 1f, 0.98f);

            var glow1 = Label("Title1Glow", rt1, "NEON", 132, TextAnchor.UpperLeft);
            glow1.color = new Color(NeonDistrict.UI.RuntimeUI.Pink.r, 0.5f, 0.75f, 0.5f);
            glow1.rectTransform.anchoredPosition = new Vector2(-2f, 2f);
            glow1.rectTransform.sizeDelta = rt1.sizeDelta;
            glow1.transform.SetAsFirstSibling();

            var line2 = Label("Title2", root, "DISTRICT", 132, TextAnchor.UpperLeft);
            var rt2 = line2.rectTransform;
            rt2.anchorMin = rt2.anchorMax = new Vector2(0f, 1f);
            rt2.pivot = new Vector2(0f, 1f);
            rt2.anchoredPosition = new Vector2(0f, -152f);
            rt2.sizeDelta = new Vector2(820f, 150f);
            line2.color = NeonDistrict.UI.RuntimeUI.Pink;

            // неоновое свечение под заголовком
            var glow = MakeImage("TitleGlow", root, NeonGfx.RadialGlow, new Color(0.85f, 0.2f, 0.55f, 0.32f));
            var gRt = glow.rectTransform;
            gRt.anchorMin = gRt.anchorMax = new Vector2(0f, 1f);
            gRt.pivot = new Vector2(0f, 1f);
            gRt.anchoredPosition = new Vector2(150f, 30f);
            gRt.sizeDelta = new Vector2(1300f, 700f);
            glow.transform.SetAsFirstSibling();

            var rule = MakeImage("Rule", root, NeonGfx.SoftLine, new Color(NeonDistrict.UI.RuntimeUI.Cyan.r, NeonDistrict.UI.RuntimeUI.Cyan.g, NeonDistrict.UI.RuntimeUI.Cyan.b, 0.55f));
            var rRt = rule.rectTransform;
            rRt.anchorMin = rRt.anchorMax = new Vector2(0f, 1f);
            rRt.pivot = new Vector2(0f, 1f);
            rRt.anchoredPosition = new Vector2(6f, -312f);
            rRt.sizeDelta = new Vector2(620f, 3f);

            var sub = Label("Sub", root, "ОТКРЫТЫЙ ГОРОД · UNITY 6.3 LTS · URP", 24, TextAnchor.UpperLeft);
            var sRt = sub.rectTransform;
            sRt.anchorMin = sRt.anchorMax = new Vector2(0f, 1f);
            sRt.pivot = new Vector2(0f, 1f);
            sRt.anchoredPosition = new Vector2(6f, -332f);
            sRt.sizeDelta = new Vector2(760f, 40f);
            sub.color = new Color(0.55f, 0.66f, 0.78f);

            // лёгкая неоновая полоска слева от меню
            var accent = MakeImage("Accent", root, NeonGfx.White, new Color(NeonDistrict.UI.RuntimeUI.Pink.r, NeonDistrict.UI.RuntimeUI.Pink.g, NeonDistrict.UI.RuntimeUI.Pink.b, 0.85f));
            var aRt = accent.rectTransform;
            aRt.anchorMin = aRt.anchorMax = new Vector2(0f, 1f);
            aRt.pivot = new Vector2(0f, 1f);
            aRt.anchoredPosition = new Vector2(-28f, -470f);
            aRt.sizeDelta = new Vector2(3f, 250f);
        }

        private void BuildButtons(RectTransform root)
        {
            playButton = MakeMenuButton(root, "ИГРАТЬ", -430f, 0);
            continueButton = MakeMenuButton(root, "ПРОДОЛЖИТЬ", -500f, 1);
            quitButton = MakeMenuButton(root, "ВЫХОД", -570f, 2);
            CacheButtons();
        }

        /// <summary>
        /// Кнопки нужны и в собранном плеере: там сцену никто не «строит» через Build(), и без
        /// этого массива навигация с клавиатуры падала с NullReferenceException ещё в Start().
        /// </summary>
        private void CacheButtons()
        {
            _buttons = new[] { playButton, continueButton, quitButton };
            _buttonsRt = new RectTransform[_buttons.Length];
            for (int i = 0; i < _buttons.Length; i++)
                _buttonsRt[i] = _buttons[i] != null ? (RectTransform)_buttons[i].transform : null;
        }

        private Button MakeMenuButton(RectTransform root, string label, float y, int index)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(400f, 58f);

            var bg = go.AddComponent<Image>();
            bg.sprite = NeonGfx.RoundRect;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.55f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = new Color(0.05f, 0.05f, 0.08f, 0.55f);
            colors.highlightedColor = new Color(0.16f, 0.06f, 0.14f, 0.92f);
            colors.pressedColor = new Color(0.45f, 0.08f, 0.22f, 1f);
#if UNITY_2019_3_OR_NEWER
            colors.selectedColor = colors.highlightedColor;
#endif
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;      // навигацию ведём сами (см. Update)
            btn.navigation = nav;

            // рамка-подсветка
            var outline = new GameObject("Outline", typeof(RectTransform)).AddComponent<Image>();
            outline.transform.SetParent(go.transform, false);
            Stretch(outline.rectTransform, -1f);
            outline.sprite = NeonGfx.RoundRectOutline;
            outline.type = Image.Type.Sliced;
            outline.color = new Color(NeonDistrict.UI.RuntimeUI.Pink.r, NeonDistrict.UI.RuntimeUI.Pink.g, NeonDistrict.UI.RuntimeUI.Pink.b, 0f);
            outline.raycastTarget = false;

            // неоновая полоска-акцент слева
            var bar = new GameObject("Bar", typeof(RectTransform)).AddComponent<Image>();
            bar.transform.SetParent(go.transform, false);
            var barRt = bar.rectTransform;
            barRt.anchorMin = new Vector2(0f, 0f);
            barRt.anchorMax = new Vector2(0f, 1f);
            barRt.pivot = new Vector2(0f, 0.5f);
            barRt.offsetMin = new Vector2(0f, 6f);
            barRt.offsetMax = new Vector2(4f, -6f);
            bar.sprite = NeonGfx.RoundRect;
            bar.type = Image.Type.Sliced;
            bar.color = new Color(NeonDistrict.UI.RuntimeUI.Cyan.r, NeonDistrict.UI.RuntimeUI.Cyan.g, NeonDistrict.UI.RuntimeUI.Cyan.b, 0.35f);
            bar.raycastTarget = false;

            var text = Label("Label", go.transform, label, 30, TextAnchor.MiddleLeft);
            var tr = text.rectTransform;
            Stretch(tr, 0f);
            tr.offsetMin = new Vector2(28f, 0f);
            tr.offsetMax = new Vector2(-16f, 0f);
            text.color = new Color(0.92f, 0.94f, 0.98f);

            var arrow = new GameObject("Arrow", typeof(RectTransform)).AddComponent<Image>();
            arrow.transform.SetParent(go.transform, false);
            var aRt = arrow.rectTransform;
            aRt.anchorMin = aRt.anchorMax = new Vector2(1f, 0.5f);
            aRt.pivot = new Vector2(1f, 0.5f);
            aRt.anchoredPosition = new Vector2(-18f, 0f);
            aRt.sizeDelta = new Vector2(18f, 18f);
            arrow.sprite = NeonGfx.Arrow;
            arrow.color = new Color(NeonDistrict.UI.RuntimeUI.Cyan.r, NeonDistrict.UI.RuntimeUI.Cyan.g, NeonDistrict.UI.RuntimeUI.Cyan.b, 0.25f);
            arrow.raycastTarget = false;

            // подкрашиваем строку при наведении
            var hover = go.AddComponent<MenuButtonHover>();
            hover.bar = bar;
            hover.outline = outline;
            hover.arrow = arrow;
            hover.label = text;

            return btn;
        }

        private void BuildFooter(RectTransform root)
        {
            footer = Label("Footer", root, "v0.1 · WASD — движение · J — заказ · G — гараж · Esc — пауза", 18, TextAnchor.LowerLeft);
            var rt = footer.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(0f, 24f);
            rt.sizeDelta = new Vector2(760f, 40f);
            footer.color = new Color(0.42f, 0.48f, 0.58f);
        }

        private Image BuildCursor(Transform parent)
        {
            var img = MakeImage("Cursor", parent, NeonGfx.Arrow, new Color(1f, 0.35f, 0.6f, 0.95f));
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(22f, 22f);
            img.raycastTarget = false;
            // поворот: наш спрайт смотрит вправо, курсор должен смотреть влево-вверх
            rt.localRotation = Quaternion.Euler(0f, 0f, 135f);

            var dot = MakeImage("CursorDot", rt, NeonGfx.RadialGlow, new Color(1f, 0.5f, 0.75f, 0.35f));
            Stretch(dot.rectTransform, 26f);
            dot.raycastTarget = false;
            return img;
        }

        // ------------------------------------------------------------------ логика
        private void Start()
        {
            CacheButtons();          // в плеере Build() не вызывается — массив кнопок собираем тут
            if (playButton != null) playButton.onClick.AddListener(() => StartGame(false));
            if (continueButton != null) continueButton.onClick.AddListener(() => StartGame(true));
            if (quitButton != null) quitButton.onClick.AddListener(Quit);

            if (continueButton != null)
            {
                continueButton.interactable = SaveSystem.HasSave;
                var label = continueButton.GetComponentInChildren<Text>();
                if (label != null && !SaveSystem.HasSave) label.color = new Color(0.5f, 0.52f, 0.58f);
            }

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnStateChanged += OnState;
                OnState(gm.CurrentState);
            }
        }

        private void OnDisable()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.OnStateChanged -= OnState;
        }

        private void OnState(GameManager.State s)
        {
            bool inMenu = s == GameManager.State.Menu;
            if (canvas != null) canvas.enabled = inMenu;
            if (_panelGroup != null) { _panelGroup.interactable = inMenu; _panelGroup.blocksRaycasts = inMenu; }
            if (cursor != null) cursor.SetActive(inMenu);
            if (inMenu)
            {
                _selected = FirstInteractable();
                SetCursorVisible(true);
                Time.timeScale = 0f;
            }
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            bool inMenu = gm == null || gm.CurrentState == GameManager.State.Menu;

            if (inMenu)
            {
                // системный курсор: в меню он должен быть виден и не заблокирован.
                // Делаем это каждый кадр — единственный надёжный способ пережить
                // переключение фокуса окна и «просадку» Cursor.visible в standalone.
                ConfirmSystemCursor();
                FollowCursor();
                HandleKeyboard();
            }

            _time += Time.unscaledDeltaTime;
            AnimateSelection();
        }

        private static void ConfirmSystemCursor()
        {
            if (!Cursor.visible) Cursor.visible = true;
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        }

        private void FollowCursor()
        {
            if (cursor == null) return;
            var rt = (RectTransform)cursor.transform;
            Vector2 screen = PointerPosition();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)canvas.transform, screen, null, out var local);
            rt.anchoredPosition = local;

            // лёгкое «дыхание» курсора
            float s = 1f + 0.06f * Mathf.Sin(_time * 6f);
            rt.localScale = new Vector3(s, s, 1f);
        }

        private void HandleKeyboard()
        {
            if (_buttons == null || _buttons.Length == 0) return;
            _navCooldown -= Time.unscaledDeltaTime;

            float v = 0f;
            if (KeyDown(KeyKind.Down) || KeyDown(KeyKind.S)) v = 1f;
            if (KeyDown(KeyKind.Up) || KeyDown(KeyKind.W)) v = -1f;

            if (v != 0f && _navCooldown <= 0f)
            {
                _navCooldown = 0.14f;
                int next = _selected;
                for (int i = 0; i < _buttons.Length; i++)
                {
                    next = (next + (v > 0f ? 1 : _buttons.Length - 1)) % _buttons.Length;
                    if (IsInteractable(next)) break;
                }
                _selected = next;
            }

            if (IsInteractable(_selected) &&
                (KeyDown(KeyKind.Enter) || KeyDown(KeyKind.NumpadEnter) || KeyDown(KeyKind.Space)))
            {
                _buttons[_selected].onClick.Invoke();
            }
        }

        private bool IsInteractable(int i) => i >= 0 && _buttons != null && i < _buttons.Length &&
                                             _buttons[i] != null && _buttons[i].interactable;

        private int FirstInteractable()
        {
            if (_buttons == null) return 0;
            for (int i = 0; i < _buttons.Length; i++) if (IsInteractable(i)) return i;
            return 0;
        }

        private void AnimateSelection()
        {
            if (_buttons == null) return;
            var menu = EventSystem.current;
            // если мышью навели на другую кнопку — синхронизируем выбор с мышью
            if (menu != null && menu.currentSelectedGameObject != null)
            {
                for (int i = 0; i < _buttons.Length; i++)
                    if (_buttons[i] != null && _buttons[i].gameObject == menu.currentSelectedGameObject) _selected = i;
            }

            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] == null) continue;
                var hover = _buttons[i].GetComponent<MenuButtonHover>();
                if (hover == null) continue;
                bool active = i == _selected && IsInteractable(i);
                float glow = 0.5f + 0.5f * Mathf.Sin(_time * 3.4f);
                hover.SetFocus(active, glow);
            }
        }

        private static void SetCursorVisible(bool visible)
        {
            Cursor.visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        }

        private void StartGame(bool fromSave)
        {
            if (GameManager.Instance == null) return;
            var backdrop = FindFirstObjectByType<MenuBackdrop>();
            if (backdrop != null) backdrop.RestoreCamera();

            GameManager.Instance.StartGame(fromSave);
            if (canvas != null) canvas.enabled = false;
            if (cursor != null) cursor.SetActive(false);
            Player.PlayerController.LockCursor(true);
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ ввод
        // Меню должно работать и на новом Input System (он в манифесте), и на старом Input
        // Manager: какой бэкенд активен, знает только Unity, поэтому выбираем через defines.

        private enum KeyKind { Up, Down, W, S, Enter, NumpadEnter, Space }

        private static bool KeyDown(KeyKind kind)
        {
#if ENABLE_INPUT_SYSTEM && !ND_COMPILE_CHECK
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return false;
            switch (kind)
            {
                case KeyKind.Up: return kb.upArrowKey.wasPressedThisFrame;
                case KeyKind.Down: return kb.downArrowKey.wasPressedThisFrame;
                case KeyKind.W: return kb.wKey.wasPressedThisFrame;
                case KeyKind.S: return kb.sKey.wasPressedThisFrame;
                case KeyKind.Enter: return kb.enterKey.wasPressedThisFrame;
                case KeyKind.NumpadEnter: return kb.numpadEnterKey.wasPressedThisFrame;
                case KeyKind.Space: return kb.spaceKey.wasPressedThisFrame;
            }
            return false;
#else
            switch (kind)
            {
                case KeyKind.Up: return Input.GetKeyDown(KeyCode.UpArrow);
                case KeyKind.Down: return Input.GetKeyDown(KeyCode.DownArrow);
                case KeyKind.W: return Input.GetKeyDown(KeyCode.W);
                case KeyKind.S: return Input.GetKeyDown(KeyCode.S);
                case KeyKind.Enter: return Input.GetKeyDown(KeyCode.Return);
                case KeyKind.NumpadEnter: return Input.GetKeyDown(KeyCode.KeypadEnter);
                case KeyKind.Space: return Input.GetKeyDown(KeyCode.Space);
            }
            return false;
#endif
        }

        private static Vector2 PointerPosition()
        {
#if ENABLE_INPUT_SYSTEM && !ND_COMPILE_CHECK
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        // ------------------------------------------------------------------ helpers
        private static Text Label(string name, Transform parent, string value, int size, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = NeonDistrict.UI.RuntimeUI.Font;
            t.text = value;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = false;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-padding, -padding);
            rt.offsetMax = new Vector2(padding, padding);
        }
    }
}
