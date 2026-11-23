using UnityEngine;
using UnityEngine.UI;
using NeonDistrict.UI;

namespace NeonDistrict.Missions
{
    /// <summary>
    /// Связывает системы миссий, субтитров и миникарты с HUD.
    /// Один компонент на сцене: сам находит всё, что нужно, и раздаёт события,
    /// поэтому ни HUDController, ни MissionSystem не знают друг о друге.
    ///
    /// Дополнительно:
    ///  • подсказка «J — взять заказ» рядом с точкой старта;
    ///  • метка цели на миникарте;
    ///  • горячая клавиша J запускает следующую миссию из каталога.
    /// </summary>
    [DefaultExecutionOrder(-120)]
    public class MissionHudBinder : MonoBehaviour
    {
        public MissionSystem missions;
        public CinematicDialogue dialogue;
        public Minimap minimap;
        public HudMissionPanel missionPanel;
        public HUDController hud;

        private float _hintCooldown;

        private void Start()
        {
            if (missions == null) missions = FindFirstObjectByType<MissionSystem>();
            if (dialogue == null) dialogue = FindFirstObjectByType<CinematicDialogue>();
            if (minimap == null) minimap = FindFirstObjectByType<Minimap>();
            if (missionPanel == null) missionPanel = FindFirstObjectByType<HudMissionPanel>();
            if (hud == null) hud = FindFirstObjectByType<HUDController>();

            if (missions != null)
            {
                missions.OnRadio += OnRadio;
                missions.OnMissionStarted += OnMissionStarted;
                missions.OnMissionCompleted += OnCompleted;
                missions.OnMissionFailed += OnFailed;
            }
        }

        private void OnDestroy()
        {
            if (missions == null) return;
            missions.OnRadio -= OnRadio;
            missions.OnMissionStarted -= OnMissionStarted;
            missions.OnMissionCompleted -= OnCompleted;
            missions.OnMissionFailed -= OnFailed;
        }

        private void OnRadio(string speaker, string text)
        {
            if (dialogue != null) dialogue.Say(speaker, text);
        }

        private void OnMissionStarted(MissionDefinition m)
        {
            if (missionPanel != null) missionPanel.Toast("МИССИЯ: " + m.title, 4f);
        }

        private void OnCompleted(MissionDefinition m)
        {
            if (hud != null) hud.Popup("МИССИЯ ВЫПОЛНЕНА  +$" + m.reward, 5f);
        }

        private void OnFailed(MissionDefinition m)
        {
            if (hud != null) hud.Popup("МИССИЯ ПРОВАЛЕНА", 4f);
        }

        private void Update()
        {
            if (missions == null) return;

            // метка цели на миникарте
            if (minimap != null)
                minimap.SetObjectiveMarker(missions.TargetPoint, missions.IsRunning && missions.HasTarget);

            // подсказка о новой миссии
            _hintCooldown -= Time.deltaTime;
            if (!missions.IsRunning && hud != null && _hintCooldown <= 0f)
            {
                _hintCooldown = 6f;
                hud.Hint("J — взять заказ: " + NextMissionTitle(), 4.5f);
            }

            if (!missions.IsRunning && global::UnityEngine.InputSystemBridge.WasPressed(global::UnityEngine.InputSystemBridge.Key.J))
            {
                var gm = Core.GameManager.Instance;
                if (gm != null && gm.IsPlaying) missions.StartMission(missions.CompletedCount % Mathf.Max(1, missions.Catalog.Count));
            }
        }

        private string NextMissionTitle()
        {
            if (missions.Catalog == null || missions.Catalog.Count == 0) return "нет заказов";
            int idx = missions.CompletedCount % missions.Catalog.Count;
            return missions.Catalog[idx].title;
        }
    }
}
