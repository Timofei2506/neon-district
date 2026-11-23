using UnityEngine;
using UnityEngine.UI;

namespace NeonDistrict.UI
{
    /// <summary>
    /// Вёрстка миникарты: круглая маска, рамка, север-индикатор и слой меток.
    /// Отдельный класс, чтобы Minimap занимался только логикой, а сцена —
    /// одной строкой собирала внешний вид.
    /// </summary>
    public static class MinimapView
    {
        public static void Build(Minimap minimap, Transform parent, float size = 232f)
        {
            if (minimap == null) return;

            var root = new GameObject("Minimap", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(-40f, 150f);

            // подложка-круг
            var bg = new GameObject("Backdrop", typeof(RectTransform));
            bg.transform.SetParent(root.transform, false);
            var brt = (RectTransform)bg.transform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = RadialSprite(256, 1f);
            bgImg.color = new Color(0.02f, 0.03f, 0.06f, 0.86f);
            bgImg.raycastTarget = false;

            // сама карта (RawImage с текстурой ортокамеры), обрезана маской
            var mapGo = new GameObject("Map", typeof(RectTransform));
            mapGo.transform.SetParent(root.transform, false);
            var mrt = (RectTransform)mapGo.transform;
            mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
            mrt.offsetMin = new Vector2(6f, 6f); mrt.offsetMax = new Vector2(-6f, -6f);
            var raw = mapGo.AddComponent<RawImage>();
            raw.color = new Color(1f, 1f, 1f, 0.92f);
            raw.raycastTarget = false;
            mapGo.AddComponent<Mask>().showMaskGraphic = true;

            // слой меток (масштабируется вместе с картой)
            var blips = new GameObject("Blips", typeof(RectTransform));
            blips.transform.SetParent(mapGo.transform, false);
            var blrt = (RectTransform)blips.transform;
            blrt.anchorMin = Vector2.zero; blrt.anchorMax = Vector2.one;
            blrt.offsetMin = blrt.offsetMax = Vector2.zero;

            // рамка
            var frame = new GameObject("Frame", typeof(RectTransform));
            frame.transform.SetParent(root.transform, false);
            var frt = (RectTransform)frame.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            var frameImg = frame.AddComponent<Image>();
            frameImg.sprite = RingSprite(256, 0.94f, 1f);
            frameImg.color = new Color(0.13f, 0.9f, 1f, 0.75f);
            frameImg.raycastTarget = false;

            // север
            var north = RuntimeUI.Text(root.transform, "North", "N", new Vector2(0.5f, 1f),
                new Vector2(40f, 24f), new Vector2(0f, -2f), 16, TextAnchor.MiddleCenter);
            north.color = RuntimeUI.Cyan;

            minimap.blipRoot = blrt;
            minimap.output = raw;
        }

        /// <summary>Сплошной круг с мягким краем — для маски карты.</summary>
        private static Sprite RadialSprite(int size, float radiusNorm)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = size * 0.5f, r = c * radiusNorm;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01((r - d) / 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Кольцо (только контур) для рамки.</summary>
        private static Sprite RingSprite(int size, float innerNorm, float outerNorm)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = size * 0.5f, ro = c * outerNorm, ri = c * innerNorm;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01((ro - d) / 1.5f) * Mathf.Clamp01((d - ri) / 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
