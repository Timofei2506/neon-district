using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NeonDistrict.Core;
using NeonDistrict.UI;

namespace NeonDistrict.Missions
{
    /// <summary>
    /// Субтитры и реплики «по рации» с кинематографичной подачей:
    /// затемнение полос, letterbox сверху/снизу, имя говорящего акцентным цветом,
    /// посимвольный вывод текста, индикатор «эфир».
    ///
    /// Всё строится рантаймом поверх UGUI: в репозитории нет ни одного ассета UI-текста.
    /// </summary>
    [DefaultExecutionOrder(-140)]
    public class CinematicDialogue : MonoBehaviour
    {
        [Header("Тайминги")]
        [Tooltip("Секунд на символ при печати текста")] public float charsPerSecond = 42f;
        [Tooltip("Минимум, сколько реплика висит на экране, с")] public float minHold = 2.2f;
        [Tooltip("Задержка после реплики, с")] public float tailHold = 0.6f;

        [Header("Кинематографичность")]
        [Tooltip("Показывать letterbox-полосы при разговоре")] public bool letterbox = true;
        [Tooltip("Высота полос, пикселей (при 1080p)")] public float barHeight = 74f;
        [Tooltip("Приглушать звук игры, пока говорят")] public bool duckAudio = true;

        public bool IsSpeaking => _speaking;
        public event Action<string, string> OnLineFinished;

        private CanvasGroup _group;
        private Text _speakerText;
        private Text _bodyText;
        private Text _airTag;
        private RectTransform _barTop, _barBottom;
        private Canvas _canvas;
        private readonly Queue<Line> _queue = new Queue<Line>();
        private bool _speaking;
        private float _duckTarget = 1f;
        private float _duckNow = 1f;

        private class Line
        {
            public string speaker;
            public string text;
            public float hold;
        }

        /// <summary>Собирает слой субтитров внутри уже существующего Canvas'а HUD.</summary>
        public void Build(Transform parent)
        {
            var go = new GameObject("Subtitles", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            _canvas = go.AddComponent<Canvas>();
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 50;
            go.AddComponent<GraphicRaycaster>();
            _group = go.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            if (letterbox)
            {
                _barTop = MakeBar(go.transform, "BarTop", new Vector2(0f, 1f), new Vector2(1f, 1f), barHeight);
                _barBottom = MakeBar(go.transform, "BarBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), barHeight);
            }

            _speakerText = RuntimeUI.Text(go.transform, "Speaker", "", new Vector2(0.5f, 0f),
                new Vector2(1100f, 34f), new Vector2(0f, barHeight + 92f), 22, TextAnchor.MiddleCenter);
            _speakerText.color = RuntimeUI.Cyan;
            _speakerText.fontStyle = FontStyle.Bold;

            _bodyText = RuntimeUI.Text(go.transform, "Line", "", new Vector2(0.5f, 0f),
                new Vector2(1400f, 90f), new Vector2(0f, barHeight + 52f), 28, TextAnchor.LowerCenter);
            _bodyText.color = new Color(0.95f, 0.96f, 1f);

            _airTag = RuntimeUI.Text(go.transform, "Air", "● В ЭФИРЕ", new Vector2(1f, 1f),
                new Vector2(220f, 30f), new Vector2(-40f, -barHeight - 18f), 16, TextAnchor.MiddleRight);
            _airTag.color = RuntimeUI.Pink;
            _airTag.gameObject.SetActive(false);
        }

        private static RectTransform MakeBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, anchorMin.y);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.92f);
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>Поставить реплику в очередь (безопасно вызывать из любого места).</summary>
        public void Say(string speaker, string text, float hold = 0f)
        {
            if (string.IsNullOrEmpty(text)) return;
            _queue.Enqueue(new Line { speaker = speaker, text = text, hold = hold });
            if (!_speaking) StartCoroutine(Pump());
        }

        /// <summary>Пропустить текущую реплику (по клику/пробелу).</summary>
        public void Skip()
        {
            if (_speaking) _skipRequested = true;
        }

        private bool _skipRequested;

        private void Update()
        {
            // приглушение звука во время реплик
            float target = _speaking && duckAudio ? 0.45f : 1f;
            _duckTarget = target;
            _duckNow = Mathf.MoveTowards(_duckNow, _duckTarget, Time.unscaledDeltaTime * 2.2f);
            AudioListener.volume = _duckNow;

            if (_speaking && (_skipRequested
                || (global::UnityEngine.InputSystemBridge.WasPressed(global::UnityEngine.InputSystemBridge.Key.Space))
                || (global::UnityEngine.InputSystemBridge.WasPressed(global::UnityEngine.InputSystemBridge.Key.Enter))))
            {
                _skipRequested = false;
                _holdLeft = 0f;
            }
        }

        private float _holdLeft;

        private IEnumerator Pump()
        {
            _speaking = true;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.State.Playing)
                GameManager.Instance.SetState(GameManager.State.Cinematic);

            while (_queue.Count > 0)
            {
                var line = _queue.Dequeue();
                if (_speakerText != null) _speakerText.text = line.speaker;
                if (_bodyText != null) _bodyText.text = "";
                if (_airTag != null) _airTag.gameObject.SetActive(true);

                yield return FadeTo(1f, 0.18f);

                float perChar = 1f / Mathf.Max(8f, charsPerSecond);
                _skipRequested = false;
                for (int i = 1; i <= line.text.Length; i++)
                {
                    if (_skipRequested) break;
                    if (_bodyText != null) _bodyText.text = line.text.Substring(0, i);
                    yield return new WaitForSecondsRealtime(perChar);
                }
                if (_bodyText != null) _bodyText.text = line.text;

                _holdLeft = Mathf.Max(line.hold > 0f ? line.hold : minHold,
                                      line.text.Length * 0.028f + tailHold);
                while (_holdLeft > 0f)
                {
                    _holdLeft -= Time.unscaledDeltaTime;
                    yield return null;
                }
                OnLineFinished?.Invoke(line.speaker, line.text);
            }

            yield return FadeTo(0f, 0.25f);
            if (_airTag != null) _airTag.gameObject.SetActive(false);
            _speaking = false;

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.State.Cinematic)
                GameManager.Instance.SetState(GameManager.State.Playing);
        }

        private IEnumerator FadeTo(float alpha, float duration)
        {
            if (_group == null) yield break;
            float start = _group.alpha, t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(start, alpha, t / duration);
                yield return null;
            }
            _group.alpha = alpha;
        }
    }
}
