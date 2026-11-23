using UnityEngine;
using UnityEngine.UI;

namespace NeonDistrict.UI
{
    /// <summary>
    /// Собирает HUD и главное меню в рантайме (без ручной вёрстки в префабах).
    /// Стиль — неоновый городской боевик: тонкий шрифт, акценты пинк/циан.
    /// </summary>
    public static class RuntimeUI
    {
        public static readonly Color Pink = new Color(1f, 0.18f, 0.47f);
        public static readonly Color Cyan = new Color(0.13f, 0.9f, 1f);
        public static readonly Color Green = new Color(0.27f, 0.88f, 0.48f);

        private static Font _font;
        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Segoe UI", 24);
                }
                return _font;
            }
        }

        public static void Build(HUDController hud, Transform parent)
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            hud.timeText = Text(canvasGo.transform, "Time", "08:30", new Vector2(0f, 1f), new Vector2(190f, 64f), new Vector2(48f, -52f), 40, TextAnchor.MiddleLeft);
            hud.districtText = Text(canvasGo.transform, "District", "ХВАНДАМ", new Vector2(0f, 1f), new Vector2(420f, 40f), new Vector2(48f, -104f), 22, TextAnchor.MiddleLeft);
            hud.moneyText = Text(canvasGo.transform, "Money", "$500", new Vector2(1f, 1f), new Vector2(320f, 64f), new Vector2(-48f, -52f), 42, TextAnchor.MiddleRight);
            hud.moneyText.color = Green;

            hud.healthFill = Bar(canvasGo.transform, "Health", new Vector2(0f, 0f), new Vector2(320f, 18f), new Vector2(48f, 132f), Green);
            hud.armorFill = Bar(canvasGo.transform, "Armor", new Vector2(0f, 0f), new Vector2(320f, 13f), new Vector2(48f, 106f), Cyan);

            hud.ammoText = Text(canvasGo.transform, "Ammo", "12 / 36", new Vector2(1f, 0f), new Vector2(260f, 54f), new Vector2(-48f, 100f), 30, TextAnchor.MiddleRight);
            hud.speedText = Text(canvasGo.transform, "Speed", "0", new Vector2(0f, 0f), new Vector2(260f, 80f), new Vector2(48f, 48f), 56, TextAnchor.MiddleLeft);
            hud.gearText = Text(canvasGo.transform, "Gear", "D", new Vector2(0f, 0f), new Vector2(80f, 54f), new Vector2(270f, 54f), 32, TextAnchor.MiddleCenter);
            hud.speedPanel = hud.speedText.transform.parent != null ? hud.speedText.gameObject : hud.speedText.gameObject;
            hud.speedText.gameObject.SetActive(false);
            hud.gearText.gameObject.SetActive(false);

            hud.interactionText = Text(canvasGo.transform, "Interact", "", new Vector2(0.5f, 0.32f), new Vector2(900f, 44f), Vector2.zero, 24, TextAnchor.MiddleCenter);
            hud.popupText = Text(canvasGo.transform, "Popup", "", new Vector2(0.5f, 0.78f), new Vector2(1100f, 48f), Vector2.zero, 28, TextAnchor.MiddleCenter);
            hud.hintText = Text(canvasGo.transform, "Hint", "", new Vector2(0.5f, 0.1f), new Vector2(1100f, 36f), Vector2.zero, 20, TextAnchor.MiddleCenter);
            hud.hintText.gameObject.SetActive(false);
        }

        public static void BuildMenu(MainMenuUI menu, Transform parent)
        {
            var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("BG", typeof(RectTransform)).AddComponent<Image>();
            bg.transform.SetParent(canvasGo.transform, false);
            var rt = bg.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            bg.color = new Color(0.02f, 0.02f, 0.05f, 0.92f);

            var title = Text(canvasGo.transform, "Title", "NEON\nDISTRICT", new Vector2(0f, 0.5f), new Vector2(900f, 320f), new Vector2(140f, 180f), 92, TextAnchor.UpperLeft);
            title.color = Pink;
            var sub = Text(canvasGo.transform, "Sub", "открытый мир · Unity 6.3 LTS", new Vector2(0f, 0.5f), new Vector2(900f, 40f), new Vector2(146f, 40f), 20, TextAnchor.UpperLeft);
            sub.color = new Color(0.7f, 0.75f, 0.85f);

            menu.playButton = Button(canvasGo.transform, "ИГРАТЬ", new Vector2(0f, 0.5f), new Vector2(360f, 62f), new Vector2(146f, -80f));
            menu.continueButton = Button(canvasGo.transform, "ПРОДОЛЖИТЬ", new Vector2(0f, 0.5f), new Vector2(360f, 62f), new Vector2(146f, -154f));
            menu.quitButton = Button(canvasGo.transform, "ВЫХОД", new Vector2(0f, 0.5f), new Vector2(360f, 62f), new Vector2(146f, -228f));
            menu.footer = Text(canvasGo.transform, "Footer", "v0.1 · ассеты: Manhattan (Sketchfab), Renderpeople, Seoul City Builder Kit, Polestar 4, Mercedes GLS/AMG", new Vector2(0f, 0f), new Vector2(1600f, 30f), new Vector2(48f, 32f), 16, TextAnchor.MiddleLeft);
            menu.footer.color = new Color(0.45f, 0.5f, 0.6f);
            menu.panel = canvasGo;
        }

        // ------------------------------------------------------------ primitives
        public static Text Text(Transform parent, string name, string value, Vector2 anchor, Vector2 size, Vector2 pos, int fontSize, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var t = go.AddComponent<Text>();
            t.font = Font;
            t.text = value;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        public static Image Bar(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 pos, Color fill)
        {
            var bgGo = new GameObject(name + "BG", typeof(RectTransform));
            bgGo.transform.SetParent(parent, false);
            var rt = (RectTransform)bgGo.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var bg = bgGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(bgGo.transform, false);
            var frt = (RectTransform)fillGo.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(3f, 3f); frt.offsetMax = new Vector2(-3f, -3f);
            var img = fillGo.AddComponent<Image>();
            img.color = fill;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = 1f;
            return img;
        }

        public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.08f, 0.08f, 0.12f, 0.85f);
            colors.highlightedColor = new Color(1f, 0.18f, 0.47f, 0.9f);
            colors.pressedColor = new Color(0.7f, 0.1f, 0.3f, 1f);
            btn.colors = colors;

            var text = Text(go.transform, "Label", label, new Vector2(0.5f, 0.5f), size, Vector2.zero, 26, TextAnchor.MiddleCenter);
            text.rectTransform.anchoredPosition = Vector2.zero;
            return btn;
        }
    }
}
