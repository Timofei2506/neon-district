using UnityEngine;
using UnityEngine.UI;
using NeonDistrict.Core;
using NeonDistrict.Economy;

namespace NeonDistrict.UI
{
    /// <summary>
    /// Пауза в стиле ААА: затемнение, «ПРОДОЛЖИТЬ / ГАРАЖ / СОХРАНИТЬ / ЗАГРУЗИТЬ / В МЕНЮ / ВЫХОД»,
    /// подсказки управления и текущий прогресс (деньги, розыск, время).
    ///
    /// Открывается Esc, закрывается Esc. Игра ставится на паузу через GameManager.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PauseMenuUI : MonoBehaviour
    {
        public GameObject panel;
        public Text progressText;
        public Text statusText;
        public GarageUI garage;

        private void Start()
        {
            Build();
            SetOpen(false);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (global::UnityEngine.InputSystemBridge.WasPressed(global::UnityEngine.InputSystemBridge.Key.Escape))
            {
                if (garage != null && garage.IsOpen) return;          // гараж сам закроется
                if (panel != null && panel.activeSelf) SetOpen(false);
                else if (gm.CurrentState == GameManager.State.Playing) SetOpen(true);
            }

            if (panel != null && panel.activeSelf && progressText != null)
            {
                progressText.text = $"День {gm.Day}  ·  {gm.TimeString}  ·  " +
                                    $"${Mathf.RoundToInt(gm.Money):N0}  ·  Розыск: {gm.Wanted}/5";
            }
        }

        public void SetOpen(bool open)
        {
            if (panel == null) return;
            panel.SetActive(open);
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (open)
            {
                if (gm.CurrentState == GameManager.State.Playing) gm.SetState(GameManager.State.Paused);
                Player.PlayerController.LockCursor(false);
                if (statusText != null) statusText.text = "";
            }
            else if (gm.CurrentState == GameManager.State.Paused)
            {
                gm.SetState(GameManager.State.Playing);
                Player.PlayerController.LockCursor(true);
            }
        }

        // ---------------------------------------------------------------- вёрстка
        private void Build()
        {
            var canvasGo = new GameObject("PauseCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 35;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.01f, 0.012f, 0.02f, 0.88f);

            var title = RuntimeUI.Text(panel.transform, "Title", "ПАУЗА", new Vector2(0.5f, 0.5f),
                new Vector2(900f, 90f), new Vector2(0f, 300f), 64, TextAnchor.MiddleCenter);
            title.color = RuntimeUI.Pink;
            title.fontStyle = FontStyle.Bold;

            progressText = RuntimeUI.Text(panel.transform, "Progress", "", new Vector2(0.5f, 0.5f),
                new Vector2(1400f, 40f), new Vector2(0f, 230f), 24, TextAnchor.MiddleCenter);
            progressText.color = new Color(0.7f, 0.78f, 0.9f);

            float y = 110f;
            MenuButton(panel.transform, "ПРОДОЛЖИТЬ", y, () => SetOpen(false)); y -= 82f;
            MenuButton(panel.transform, "ГАРАЖ", y, () =>
            {
                if (garage != null) { SetOpen(false); garage.SetOpen(true); }
            }); y -= 82f;
            MenuButton(panel.transform, "СОХРАНИТЬ", y, () =>
            {
                var gm = GameManager.Instance;
                if (gm != null) SaveSystem.Save(gm, gm.player);
                if (statusText != null) statusText.text = SaveSystem.HasSave ? "Прогресс сохранён" : "Не удалось сохранить";
            }); y -= 82f;
            MenuButton(panel.transform, "ЗАГРУЗИТЬ", y, () =>
            {
                var gm = GameManager.Instance;
                if (gm != null) SaveSystem.LoadInto(gm, gm.player);
                if (statusText != null) statusText.text = SaveSystem.HasSave ? "Прогресс загружен" : "Сейвов нет";
            }); y -= 82f;
            MenuButton(panel.transform, "В ГЛАВНОЕ МЕНЮ", y, () =>
            {
                var gm = GameManager.Instance;
                if (gm != null) gm.SetState(GameManager.State.Menu);
                SetOpen(false);
            }); y -= 82f;
            MenuButton(panel.transform, "ВЫХОД", y, Quit);

            statusText = RuntimeUI.Text(panel.transform, "Status", "", new Vector2(0.5f, 0.5f),
                new Vector2(1200f, 36f), new Vector2(0f, -300f), 22, TextAnchor.MiddleCenter);
            statusText.color = RuntimeUI.Green;

            var help = RuntimeUI.Text(panel.transform, "Help",
                "WASD — движение · Shift — бег · Space — прыжок · C — присед\n" +
                "E — сесть в машину / выйти · ЛКМ — огонь · R — перезарядка\n" +
                "J — взять заказ · G — гараж · M — миникарта · Esc — пауза",
                new Vector2(0.5f, 0f), new Vector2(1500f, 110f), new Vector2(0f, 60f), 20, TextAnchor.MiddleCenter);
            help.color = new Color(0.55f, 0.62f, 0.72f);
            help.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private void MenuButton(Transform parent, string label, float y, UnityEngine.Events.UnityAction onClick)
        {
            var btn = RuntimeUI.Button(parent, label, new Vector2(0.5f, 0.5f), new Vector2(460f, 64f), new Vector2(0f, y));
            btn.onClick.AddListener(onClick);
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
