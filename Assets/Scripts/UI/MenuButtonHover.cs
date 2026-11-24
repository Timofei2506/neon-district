using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NeonDistrict.UI
{
    /// <summary>
    /// Подсветка строки меню: неоновая полоска слева, рамка, стрелка и цвет подписи.
    /// Отдельный компонент, чтобы <see cref="MenuScreen"/> оставался про вёрстку и логику.
    /// </summary>
    public class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image bar;
        public Image outline;
        public Image arrow;
        public Text label;

        private bool _pointerOver;
        private float _focus;          // 0..1 — плавное «включение» подсветки

        public void OnPointerEnter(PointerEventData eventData) => _pointerOver = true;
        public void OnPointerExit(PointerEventData eventData) => _pointerOver = false;

        /// <summary>Вызывается меню каждый кадр: active — строка выбрана, glow — фаза пульсации.</summary>
        public void SetFocus(bool active, float glow)
        {
            float target = active || _pointerOver ? 1f : 0f;
            _focus = Mathf.MoveTowards(_focus, target, Time.unscaledDeltaTime * 8f);

            if (bar != null)
            {
                var c = bar.color;
                bar.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.25f, 1f, _focus));
                bar.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(0f, 5f, _focus), bar.rectTransform.sizeDelta.y);
            }
            if (outline != null)
            {
                var c = outline.color;
                float a = _focus * (0.35f + 0.35f * glow);
                outline.color = new Color(c.r, c.g, c.b, a);
            }
            if (arrow != null)
            {
                var c = arrow.color;
                arrow.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.15f, 0.95f, _focus));
                arrow.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-18f, -28f, _focus), 0f);
            }
            if (label != null)
            {
                label.color = Color.Lerp(new Color(0.82f, 0.85f, 0.92f), new Color(1f, 1f, 1f), _focus);
            }
        }
    }
}
