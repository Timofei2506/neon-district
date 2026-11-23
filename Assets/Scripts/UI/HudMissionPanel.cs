using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using NeonDistrict.Core;
using NeonDistrict.Missions;

namespace NeonDistrict.UI
{
    /// <summary>
    /// Правая панель HUD для миссий: заголовок, цель, таймер, счётчик шагов.
    /// Плюс всплывающие карточки «МИССИЯ НАЧАТА / ВЫПОЛНЕНА» и подсказка
    /// «нажми J, чтобы взять заказ» рядом с игроком.
    ///
    /// Строится кодом, как и остальной UI проекта.
    /// </summary>
    [DefaultExecutionOrder(-130)]
    public class HudMissionPanel : MonoBehaviour
    {
        public MissionSystem missions;
        public Text titleText;
        public Text objectiveText;
        public Text timerText;
        public Text stepText;
        public Text toastText;
        public GameObject panel;

        private float _toastTimer;

        private void Awake()
        {
            Build(transform);
        }

        /// <summary>Вызывается сборщиком сцены: панель — в HUD, тост — поверх него.</summary>
        public void BuildAll(Transform hudParent, Transform toastParent)
        {
            if (panel != null) panel.transform.SetParent(hudParent, false);
            if (toastText == null) BuildToast(toastParent != null ? toastParent : hudParent);
        }

        private void Start()
        {
            if (missions == null) missions = FindFirstObjectByType<MissionSystem>();
            if (missions == null) return;

            missions.OnObjectiveChanged += SetObjective;
            missions.OnMissionStarted += OnStarted;
            missions.OnMissionCompleted += OnCompleted;
            missions.OnMissionFailed += OnFailed;
            if (panel != null) panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (missions == null) return;
            missions.OnObjectiveChanged -= SetObjective;
            missions.OnMissionStarted -= OnStarted;
            missions.OnMissionCompleted -= OnCompleted;
            missions.OnMissionFailed -= OnFailed;
        }

        private void Update()
        {
            if (missions != null && panel != null && panel.activeSelf)
            {
                if (timerText != null)
                {
                    float left = missions.Remaining;
                    timerText.gameObject.SetActive(left > 0f);
                    if (left > 0f)
                    {
                        timerText.text = Mathf.CeilToInt(left).ToString() + " с";
                        timerText.color = left < 10f ? RuntimeUI.Pink : Color.white;
                    }
                }
                if (stepText != null && missions.IsRunning)
                    stepText.text = (missions.StepIndex + 1) + " / " + missions.StepCount;
            }

            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                if (toastText != null)
                {
                    var c = toastText.color;
                    toastText.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(_toastTimer));
                }
                if (_toastTimer <= 0f && toastText != null) toastText.gameObject.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- API
        private void SetObjective(string text)
        {
            if (panel != null) panel.SetActive(!string.IsNullOrEmpty(text));
            if (objectiveText != null) objectiveText.text = text;
        }

        private void OnStarted(MissionDefinition m)
        {
            if (titleText != null) titleText.text = m.title.ToUpperInvariant();
            Toast("МИССИЯ: " + m.title + "  ·  " + m.contact, 4f);
        }

        private void OnCompleted(MissionDefinition m)
        {
            Toast("МИССИЯ ВЫПОЛНЕНА  ·  +$" + m.reward, 5f);
        }

        private void OnFailed(MissionDefinition m)
        {
            Toast("МИССИЯ ПРОВАЛЕНА", 4f);
        }

        public void Toast(string text, float seconds = 3f)
        {
            if (toastText == null) return;
            toastText.text = text;
            toastText.color = new Color(1f, 1f, 1f, 1f);
            toastText.gameObject.SetActive(true);
            _toastTimer = seconds;
        }

        // ---------------------------------------------------------------- вёрстка
        private void Build(Transform parent)
        {
            panel = new GameObject("MissionPanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 190f);
            rt.anchoredPosition = new Vector2(-48f, 60f);

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.05f, 0.55f);
            bg.raycastTarget = false;

            var accent = new GameObject("Accent", typeof(RectTransform));
            accent.transform.SetParent(panel.transform, false);
            var art = (RectTransform)accent.transform;
            art.anchorMin = new Vector2(0f, 0f); art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0f, 0.5f);
            art.sizeDelta = new Vector2(4f, 0f);
            art.anchoredPosition = Vector2.zero;
            var aimg = accent.AddComponent<Image>();
            aimg.color = RuntimeUI.Pink;
            aimg.raycastTarget = false;

            titleText = RuntimeUI.Text(panel.transform, "Title", "", new Vector2(0f, 1f),
                new Vector2(360f, 30f), new Vector2(22f, -18f), 22, TextAnchor.UpperLeft);
            titleText.color = RuntimeUI.Pink;
            titleText.fontStyle = FontStyle.Bold;

            stepText = RuntimeUI.Text(panel.transform, "Step", "", new Vector2(1f, 1f),
                new Vector2(80f, 26f), new Vector2(-18f, -18f), 18, TextAnchor.UpperRight);
            stepText.color = new Color(0.6f, 0.68f, 0.8f);

            objectiveText = RuntimeUI.Text(panel.transform, "Objective", "", new Vector2(0f, 1f),
                new Vector2(370f, 70f), new Vector2(22f, -52f), 20, TextAnchor.UpperLeft);
            objectiveText.horizontalOverflow = HorizontalWrapMode.Wrap;

            timerText = RuntimeUI.Text(panel.transform, "Timer", "", new Vector2(0f, 0f),
                new Vector2(200f, 30f), new Vector2(22f, 14f), 24, TextAnchor.LowerLeft);
            timerText.fontStyle = FontStyle.Bold;

            panel.SetActive(false);
        }

        /// <summary>Всплывающая карточка по центру — строится в отдельном слое родителя.</summary>
        public void BuildToast(Transform parent)
        {
            // всплывающая карточка по центру сверху
            toastText = RuntimeUI.Text(parent, "MissionToast", "", new Vector2(0.5f, 0.72f),
                new Vector2(1200f, 46f), Vector2.zero, 30, TextAnchor.MiddleCenter);
            toastText.color = RuntimeUI.Cyan;
            toastText.fontStyle = FontStyle.Bold;
            toastText.gameObject.SetActive(false);

        }
    }
}
