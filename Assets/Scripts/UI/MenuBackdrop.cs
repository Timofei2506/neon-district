using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NeonDistrict.Core;

namespace NeonDistrict.UI
{
    /// <summary>
    /// Живой фон главного меню вместо чёрной заливки: за панелью видно город, а поверх него —
    /// неоновые пятна, сетка горизонта, пыль, виньетка и скан-линии. Плюс медленный дрейф камеры,
    /// чтобы кадр был «киношным», а не статичной картинкой.
    ///
    /// Всё строится кодом через <see cref="NeonGfx"/>; анимация идёт на unscaled time
    /// (в меню Time.timeScale = 0). Камера и её поворот восстанавливаются при старте игры.
    /// </summary>
    public class MenuBackdrop : MonoBehaviour
    {
        [Header("Палитра")]
        public Color pink = new Color(1f, 0.18f, 0.47f);
        public Color cyan = new Color(0.13f, 0.9f, 1f);
        public Color violet = new Color(0.45f, 0.25f, 1f);

        [Header("Камера (дрейф в меню)")]
        [Tooltip("Амплитуда покачивания по горизонтали, градусы")]
        public float yawAmplitude = 5f;
        [Tooltip("Амплитуда покачивания по вертикали, градусы")]
        public float pitchAmplitude = 2.2f;
        public float driftSpeed = 0.12f;

        private Camera _camera;
        private Quaternion _cameraHome;
        private bool _cameraCaptured;

        private Canvas _canvas;
        private readonly List<GlowSpot> _spots = new List<GlowSpot>();
        private readonly List<Dust> _dust = new List<Dust>();
        private readonly List<RectTransform> _horizonLines = new List<RectTransform>();
        private RectTransform _horizon;
        private RawImage _scanlines;
        private Image _flash;

        private float _time;
        private float _flashTimer;

        private class GlowSpot
        {
            public RectTransform rect;
            public Image image;
            public Color color;
            public Vector2 anchorPx;      // базовое положение в пикселях от центра
            public Vector2 orbit;         // амплитуда движения
            public float speed;
            public float phase;
            public float baseAlpha;
            public float pulseSpeed;
        }

        private class Dust
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float alpha;
        }

        /// <summary>Строит фон под существующим канвасом меню (sortingOrder ниже, чем у меню).</summary>
        public void Build(Transform parent, Camera worldCamera)
        {
            _camera = worldCamera != null ? worldCamera : Camera.main;
            if (_camera != null)
            {
                _cameraHome = _camera.transform.rotation;
                _cameraCaptured = true;
            }

            var canvasGo = new GameObject("MenuBackdropCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 15;        // меню — 20, значит фон всегда под ним

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            BuildBaseWash(canvasGo.transform);
            BuildGlowSpots(canvasGo.transform);
            BuildHorizon(canvasGo.transform);
            BuildDust(canvasGo.transform);
            BuildOverlay(canvasGo.transform);
        }

        // ------------------------------------------------------------------ слои
        private void BuildBaseWash(Transform root)
        {
            // 1) общая «подложка»: вертикальный градиент — сверху глубже, снизу светлее к городу
            var washGo = MakeImage("Wash", root, NeonGfx.White, Color.white);
            Stretch(washGo.rectTransform);
            washGo.type = Image.Type.Simple;

            var topGo = MakeImage("WashTop", root, NeonGfx.GradientVertical, new Color(0.01f, 0.01f, 0.03f, 0.92f));
            Stretch(topGo.rectTransform);
            topGo.rectTransform.localScale = new Vector3(1f, 1f, 1f);
            topGo.type = Image.Type.Simple;

            var bottomGo = MakeImage("WashBottom", root, NeonGfx.GradientVertical, new Color(0.02f, 0.03f, 0.06f, 0.85f));
            Stretch(bottomGo.rectTransform);
            bottomGo.rectTransform.localScale = new Vector3(1f, -1f, 1f);   // переворот: плотнее снизу
            bottomGo.type = Image.Type.Simple;
        }

        private void BuildGlowSpots(Transform root)
        {
            AddSpot(root, "Glow_Pink", pink, new Vector2(-520f, -180f), new Vector2(140f, 90f), 0.18f, 2200f, 0.31f, 0.16f);
            AddSpot(root, "Glow_Cyan", cyan, new Vector2(520f, -60f), new Vector2(180f, 120f), 0.16f, 2400f, 0.23f, 0.20f);
            AddSpot(root, "Glow_Violet", violet, new Vector2(60f, 300f), new Vector2(220f, 70f), 0.14f, 1900f, 0.17f, 0.13f);
            AddSpot(root, "Glow_Amber", new Color(1f, 0.62f, 0.2f), new Vector2(-180f, -420f), new Vector2(120f, 60f), 0.10f, 1500f, 0.29f, 0.09f);
        }

        private void AddSpot(Transform root, string name, Color color, Vector2 anchorPx, Vector2 orbit, float alpha, float size, float speed, float pulseSpeed)
        {
            var img = MakeImage(name, root, NeonGfx.RadialGlow, new Color(color.r, color.g, color.b, alpha));
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = anchorPx;

            _spots.Add(new GlowSpot
            {
                rect = rt,
                image = img,
                color = color,
                anchorPx = anchorPx,
                orbit = orbit,
                speed = speed,
                phase = Random.value * 10f,
                baseAlpha = alpha,
                pulseSpeed = pulseSpeed,
            });
        }

        private void BuildHorizon(Transform root)
        {
            // «сетка горизонта» — намёк на перспективу улиц; живёт в нижней половине экрана
            var horizonGo = new GameObject("Horizon", typeof(RectTransform));
            horizonGo.transform.SetParent(root, false);
            _horizon = (RectTransform)horizonGo.transform;
            _horizon.anchorMin = new Vector2(0f, 0f);
            _horizon.anchorMax = new Vector2(1f, 0.52f);
            _horizon.offsetMin = _horizon.offsetMax = Vector2.zero;

            const int lines = 11;
            for (int i = 0; i < lines; i++)
            {
                float t = i / (float)(lines - 1);
                float y = Mathf.Pow(t, 2.1f);       // линии сгущаются к горизонту
                var img = MakeImage("HLine" + i, _horizon, NeonGfx.SoftLine, new Color(cyan.r, cyan.g, cyan.b, Mathf.Lerp(0.05f, 0.22f, 1f - t)));
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0f, y);
                rt.anchorMax = new Vector2(1f, y);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(0f, 2f);
                rt.anchoredPosition = Vector2.zero;
                _horizonLines.Add(rt);
            }

            // центральная «осевая» линия — чуть ярче
            var axis = MakeImage("Axis", _horizon, NeonGfx.White, new Color(cyan.r, cyan.g, cyan.b, 0.18f));
            var art = axis.rectTransform;
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(1f, 0f);
            art.pivot = new Vector2(0.5f, 0f);
            art.sizeDelta = new Vector2(0f, 1.5f);
            art.anchoredPosition = Vector2.zero;
        }

        private void BuildDust(Transform root)
        {
            for (int i = 0; i < 64; i++)
            {
                var img = MakeImage("Dust" + i, root, NeonGfx.Dot, new Color(1f, 1f, 1f, 0.05f));
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                float size = Random.Range(2f, 7f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(Random.Range(-960f, 960f), Random.Range(-540f, 540f));

                var tint = Random.value < 0.4f ? cyan : (Random.value < 0.5f ? pink : Color.white);
                float alpha = Random.Range(0.06f, 0.22f);
                img.color = new Color(tint.r, tint.g, tint.b, alpha);

                _dust.Add(new Dust
                {
                    rect = rt,
                    image = img,
                    velocity = new Vector2(Random.Range(-9f, 9f), Random.Range(6f, 22f)),
                    alpha = alpha,
                });
            }
        }

        private void BuildOverlay(Transform root)
        {
            var vig = MakeImage("Vignette", root, NeonGfx.Vignette, new Color(0f, 0f, 0f, 0.9f));
            Stretch(vig.rectTransform);

            var scanGo = new GameObject("Scanlines", typeof(RectTransform));
            scanGo.transform.SetParent(root, false);
            Stretch((RectTransform)scanGo.transform);
            _scanlines = scanGo.AddComponent<RawImage>();
            _scanlines.texture = NeonGfx.Scanlines;
            _scanlines.color = new Color(1f, 1f, 1f, 0.5f);
            _scanlines.raycastTarget = false;
            _scanlines.uvRect = new Rect(0f, 0f, 480f, 270f);

            var flashGo = MakeImage("LightningFlash", root, NeonGfx.White, new Color(cyan.r, cyan.g, 1f, 0f));
            Stretch(flashGo.rectTransform);
            _flash = flashGo;
        }

        // ------------------------------------------------------------------ анимация
        private void LateUpdate()
        {
            bool inMenu = GameManager.Instance == null || GameManager.Instance.CurrentState == GameManager.State.Menu;
            if (!inMenu)
            {
                if (_canvas != null && _canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(false);
                return;
            }
            if (_canvas != null && !_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);

            float dt = Time.unscaledDeltaTime;
            _time += dt;

            AnimateCamera(dt);
            AnimateSpots();
            AnimateDust(dt);
            AnimateScanlines();
            AnimateFlash(dt);
        }

        private void AnimateCamera(float dt)
        {
            if (!_cameraCaptured || _camera == null) return;
            var euler = _cameraHome.eulerAngles;
            float yaw = Mathf.Sin(_time * driftSpeed * Mathf.PI * 2f) * yawAmplitude;
            float pitch = Mathf.Sin(_time * driftSpeed * Mathf.PI * 2f * 0.63f + 1.7f) * pitchAmplitude;
            _camera.transform.rotation = Quaternion.Euler(euler.x + pitch, euler.y + yaw, euler.z);
        }

        private void AnimateSpots()
        {
            for (int i = 0; i < _spots.Count; i++)
            {
                var s = _spots[i];
                float t = _time * s.speed + s.phase;
                s.rect.anchoredPosition = s.anchorPx + new Vector2(Mathf.Sin(t * Mathf.PI * 2f) * s.orbit.x,
                                                                 Mathf.Cos(t * Mathf.PI * 1.3f * 2f) * s.orbit.y);
                float pulse = 0.75f + 0.25f * Mathf.Sin(_time * s.pulseSpeed * Mathf.PI * 2f);
                s.image.color = new Color(s.color.r, s.color.g, s.color.b, s.baseAlpha * pulse);
            }
        }

        private void AnimateDust(float dt)
        {
            for (int i = 0; i < _dust.Count; i++)
            {
                var d = _dust[i];
                var p = d.rect.anchoredPosition + d.velocity * dt;
                if (p.y > 560f) { p.y = -560f; p.x = Random.Range(-960f, 960f); }
                if (p.x > 980f) p.x = -980f;
                if (p.x < -980f) p.x = 980f;
                d.rect.anchoredPosition = p;

                float twinkle = 0.7f + 0.3f * Mathf.Sin(_time * 1.7f + i);
                var c = d.image.color;
                d.image.color = new Color(c.r, c.g, c.b, d.alpha * twinkle);
            }
        }

        private void AnimateScanlines()
        {
            if (_scanlines == null) return;
            // повторяем текстуру 4×4 по экрану: так линии остаются тонкими при любом разрешении
            var uv = _scanlines.uvRect;
            uv.width = Screen.width / 2f;
            uv.height = Screen.height / 2f;
            uv.y = Mathf.Repeat(_time * 0.05f, 1f);
            _scanlines.uvRect = uv;
        }

        private void AnimateFlash(float dt)
        {
            if (_flash == null) return;
            _flashTimer -= dt;
            if (_flashTimer <= 0f)
            {
                _flashTimer = Random.Range(9f, 18f);
                StartCoroutine(FadeFlash());
            }
        }

        private System.Collections.IEnumerator FadeFlash()
        {
            float t = 0f;
            const float duration = 0.45f;
            while (t < duration && _flash != null)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI) * 0.16f;
                _flash.color = new Color(cyan.r, cyan.g, 1f, a);
                yield return null;
            }
            if (_flash != null) _flash.color = new Color(cyan.r, cyan.g, 1f, 0f);
        }

        /// <summary>Возвращает камеру в исходное положение (вызывается при старте игры).</summary>
        public void RestoreCamera()
        {
            if (!_cameraCaptured || _camera == null) return;
            _camera.transform.rotation = _cameraHome;
        }

        // ------------------------------------------------------------------ helpers
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

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
