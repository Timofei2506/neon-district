using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NeonDistrict.Core;
using NeonDistrict.Economy;

namespace NeonDistrict.UI
{
    /// <summary>
    /// Экран гаража: список машин слева, характеристики и цена справа,
    /// кнопки «Купить / Выбрать / Продать / Вызвать». Открывается клавишей G
    /// (или из меню), ставит игру на паузу, но продолжает рендерить кадр.
    ///
    /// Строится кодом, как и остальной UI: ни одного ассета.
    /// </summary>
    [DefaultExecutionOrder(-110)]
    public class GarageUI : MonoBehaviour
    {
        [Header("Ссылки")]
        public GarageSystem garage;

        [Header("Горячие клавиши")]
        public KeyCode toggleKey = KeyCode.G;

        private GameObject _panel;
        private Text _moneyText;
        private Text _titleText;
        private Text _specsText;
        private Text _blurbText;
        private Text _statusText;
        private Button _buyButton, _selectButton, _sellButton, _deliverButton;
        private Text _buyLabel, _selectLabel, _sellLabel;
        private readonly List<Button> _rows = new List<Button>();
        private int _cursor;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Start()
        {
            if (garage == null) garage = GarageSystem.Instance != null ? GarageSystem.Instance : FindFirstObjectByType<GarageSystem>();
            Build();
            if (garage != null)
            {
                garage.OnGarageChanged += Refresh;
                garage.OnMessage += msg => { if (_statusText != null) _statusText.text = msg; };
            }
            Refresh();
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (garage != null) garage.OnGarageChanged -= Refresh;
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(toggleKey)) Toggle();
#endif
            if (!IsOpen) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) { SetOpen(false); return; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { _cursor = Mathf.Min(_cursor + 1, _rows.Count - 1); Refresh(); }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { _cursor = Mathf.Max(_cursor - 1, 0); Refresh(); }
#endif
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (_panel == null) return;
            _panel.SetActive(open);
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (open)
            {
                if (gm.CurrentState == GameManager.State.Playing) gm.SetState(GameManager.State.Paused);
                Refresh();
            }
            else if (gm.CurrentState == GameManager.State.Paused) gm.SetState(GameManager.State.Playing);
        }

        // ---------------------------------------------------------------- данные
        private void Refresh()
        {
            if (garage == null || _panel == null) return;

            if (_moneyText != null && GameManager.Instance != null)
                _moneyText.text = "$" + Mathf.RoundToInt(GameManager.Instance.Money).ToString("N0");

            var offers = garage.Offers;
            for (int i = 0; i < _rows.Count && i < offers.Count; i++)
            {
                var o = offers[i];
                bool isOwned = garage.IsOwned(o.id);
                bool selected = garage.SelectedId == o.id;
                var label = _rows[i].GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = (selected ? "► " : "   ") + o.title + (isOwned ? "" : "   $" + o.price.ToString("N0"));
                    label.color = selected ? RuntimeUI.Cyan : (isOwned ? Color.white : new Color(0.72f, 0.76f, 0.85f));
                }
                var img = _rows[i].GetComponent<Image>();
                if (img != null) img.color = i == _cursor ? new Color(0.1f, 0.14f, 0.2f, 0.95f) : new Color(0.05f, 0.06f, 0.09f, 0.85f);
            }

            var offer = (_cursor >= 0 && _cursor < offers.Count) ? offers[_cursor] : null;
            if (offer == null) return;

            if (_titleText != null) _titleText.text = offer.title.ToUpperInvariant();
            if (_specsText != null)
                _specsText.text = $"Максимум: {offer.topSpeedKmh:0} км/ч\nМасса: {offer.mass:0} кг\nСтоимость: {(offer.price == 0 ? "подарок" : "$" + offer.price.ToString("N0"))}";
            if (_blurbText != null) _blurbText.text = offer.blurb;

            bool owned = garage.IsOwned(offer.id);
            if (_buyButton != null) _buyButton.interactable = !owned && GameManager.Instance != null && GameManager.Instance.Money >= offer.price;
            if (_selectButton != null) _selectButton.interactable = owned && garage.SelectedId != offer.id;
            if (_sellButton != null) _sellButton.interactable = owned && offers.Count > 1;
            if (_deliverButton != null) _deliverButton.interactable = owned;
            if (_buyLabel != null) _buyLabel.text = owned ? "УЖЕ В ГАРАЖЕ" : "КУПИТЬ";
            if (_selectLabel != null) _selectLabel.text = garage.SelectedId == offer.id ? "АКТИВНА" : "ВЫБРАТЬ";
            if (_sellLabel != null) _sellLabel.text = "ПРОДАТЬ (−40%)";
        }

        private VehicleOffer Current => (_cursor >= 0 && _cursor < garage.Offers.Count) ? garage.Offers[_cursor] : null;

        // ---------------------------------------------------------------- вёрстка
        private void Build()
        {
            var canvasGo = new GameObject("GarageCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("Panel", typeof(RectTransform));
            _panel.transform.SetParent(canvasGo.transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.01f, 0.012f, 0.02f, 0.94f);

            var title = RuntimeUI.Text(_panel.transform, "Header", "ГАРАЖ", new Vector2(0f, 1f),
                new Vector2(600f, 60f), new Vector2(80f, -60f), 46, TextAnchor.UpperLeft);
            title.color = RuntimeUI.Pink;
            title.fontStyle = FontStyle.Bold;

            _moneyText = RuntimeUI.Text(_panel.transform, "Money", "$0", new Vector2(1f, 1f),
                new Vector2(400f, 60f), new Vector2(-80f, -60f), 40, TextAnchor.UpperRight);
            _moneyText.color = RuntimeUI.Green;

            // список
            float y = -150f;
            if (garage != null)
            {
                for (int i = 0; i < garage.Offers.Count; i++)
                {
                    int idx = i;
                    var btn = RuntimeUI.Button(_panel.transform, garage.Offers[i].title, new Vector2(0f, 1f),
                        new Vector2(620f, 62f), new Vector2(80f, y));
                    btn.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                    btn.GetComponentInChildren<Text>().fontSize = 24;
                    btn.onClick.AddListener(() => { _cursor = idx; Refresh(); });
                    _rows.Add(btn);
                    y -= 74f;
                }
            }

            // карточка справа
            var card = new GameObject("Card", typeof(RectTransform));
            card.transform.SetParent(_panel.transform, false);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.sizeDelta = new Vector2(700f, 620f);
            crt.anchoredPosition = new Vector2(760f, -150f);
            var cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0.04f, 0.05f, 0.08f, 0.9f);

            _titleText = RuntimeUI.Text(card.transform, "Title", "", new Vector2(0f, 1f),
                new Vector2(640f, 50f), new Vector2(30f, -26f), 34, TextAnchor.UpperLeft);
            _titleText.color = RuntimeUI.Cyan;
            _titleText.fontStyle = FontStyle.Bold;

            _specsText = RuntimeUI.Text(card.transform, "Specs", "", new Vector2(0f, 1f),
                new Vector2(640f, 120f), new Vector2(30f, -86f), 22, TextAnchor.UpperLeft);
            _specsText.horizontalOverflow = HorizontalWrapMode.Wrap;

            _blurbText = RuntimeUI.Text(card.transform, "Blurb", "", new Vector2(0f, 1f),
                new Vector2(640f, 120f), new Vector2(30f, -226f), 21, TextAnchor.UpperLeft);
            _blurbText.color = new Color(0.78f, 0.82f, 0.9f);
            _blurbText.horizontalOverflow = HorizontalWrapMode.Wrap;

            _buyButton = RuntimeUI.Button(card.transform, "КУПИТЬ", new Vector2(0f, 1f), new Vector2(300f, 58f), new Vector2(30f, -370f));
            _buyLabel = _buyButton.GetComponentInChildren<Text>();
            _buyButton.onClick.AddListener(() => { if (Current != null) garage.Buy(Current.id); });

            _selectButton = RuntimeUI.Button(card.transform, "ВЫБРАТЬ", new Vector2(0f, 1f), new Vector2(300f, 58f), new Vector2(350f, -370f));
            _selectLabel = _selectButton.GetComponentInChildren<Text>();
            _selectButton.onClick.AddListener(() => { if (Current != null) garage.Select(Current.id); });

            _sellButton = RuntimeUI.Button(card.transform, "ПРОДАТЬ", new Vector2(0f, 1f), new Vector2(300f, 58f), new Vector2(30f, -440f));
            _sellLabel = _sellButton.GetComponentInChildren<Text>();
            _sellButton.onClick.AddListener(() => { if (Current != null) garage.Sell(Current.id); });

            _deliverButton = RuntimeUI.Button(card.transform, "ВЫЗВАТЬ К СЕБЕ", new Vector2(0f, 1f), new Vector2(300f, 58f), new Vector2(350f, -440f));
            _deliverButton.onClick.AddListener(() =>
            {
                garage.DeliverSelected();
                SetOpen(false);
            });

            _statusText = RuntimeUI.Text(_panel.transform, "Status", "", new Vector2(0f, 0f),
                new Vector2(1300f, 40f), new Vector2(80f, 60f), 22, TextAnchor.LowerLeft);
            _statusText.color = RuntimeUI.Green;

            var hint = RuntimeUI.Text(_panel.transform, "Hint", "G — закрыть · ↑↓ — выбор", new Vector2(0.5f, 0f),
                new Vector2(900f, 30f), new Vector2(0f, 24f), 18, TextAnchor.MiddleCenter);
            hint.color = new Color(0.5f, 0.56f, 0.66f);
        }
    }
}
