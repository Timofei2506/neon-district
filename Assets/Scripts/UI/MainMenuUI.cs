using UnityEngine;
using UnityEngine.UI;
using NeonDistrict.Core;

namespace NeonDistrict.UI
{
    /// <summary>Главное меню: играть / продолжить / выход.</summary>
    public class MainMenuUI : MonoBehaviour
    {
        public Button playButton;
        public Button continueButton;
        public Button quitButton;
        public Text footer;
        public GameObject panel;

        private void Start()
        {
            if (playButton != null) playButton.onClick.AddListener(() => StartGame(false));
            if (continueButton != null) continueButton.onClick.AddListener(() => StartGame(true));
            if (quitButton != null) quitButton.onClick.AddListener(Quit);

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnStateChanged += OnState;
                OnState(gm.CurrentState);
            }
            if (continueButton != null) continueButton.interactable = SaveSystem.HasSave;
        }

        private void OnState(GameManager.State s)
        {
            if (panel != null) panel.SetActive(s == GameManager.State.Menu);
            if (s != GameManager.State.Menu) return;
            Player.PlayerController.LockCursor(false);
        }

        private void StartGame(bool fromSave)
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.StartGame(fromSave);
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
    }
}
