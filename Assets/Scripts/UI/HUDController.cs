using UnityEngine;
using UnityEngine.UI;
using NeonDistrict.Core;
using NeonDistrict.Player;

namespace NeonDistrict.UI
{
    /// <summary>
    /// HUD в стиле городского боевика: часы, район, деньги, розыск, здоровье,
    /// скорость, патроны, подсказки взаимодействия.
    ///
    /// Все ссылки — обычные UGUI-элементы, сцена Bootstrap собирается генератором UI,
    /// поэтому в репозитории нет огромных .unity-файлов с ручной разметкой.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Текст")]
        public Text timeText;
        public Text districtText;
        public Text moneyText;
        public Text speedText;
        public Text gearText;
        public Text ammoText;
        public Text interactionText;
        public Text hintText;
        public Text popupText;

        [Header("Полосы/иконки")]
        public Image healthFill;
        public Image armorFill;
        public Image wantedStars;
        public GameObject speedPanel;
        public RectTransform minimapAnchor;

        [Header("Мир")]
        public CityController city;
        public Transform player;

        private float _popupTimer;
        private float _hintTimer;

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnMoneyChanged += _ => RefreshMoney(gm.Money);
                gm.OnWantedChanged += SetWanted;
                RefreshMoney(gm.Money);
            }
            if (city == null) city = FindFirstObjectByType<CityController>();
            if (player == null && gm != null && gm.player != null) player = gm.player.transform;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (timeText != null) timeText.text = gm.TimeString;
            if (player != null && districtText != null && city != null)
                districtText.text = city.DistrictNameAt(player.position);

            if (popupTimer > 0f)
            {
                popupTimer -= Time.deltaTime;
                if (popupText != null) popupText.color = new Color(1f, 1f, 1f, Mathf.Clamp01(popupTimer));
            }
            if (_hintTimer > 0f)
            {
                _hintTimer -= Time.deltaTime;
                if (hintText != null) hintText.gameObject.SetActive(_hintTimer > 0f);
            }

            var pc = player != null ? player.GetComponent<PlayerController>() : null;
            if (pc != null)
            {
                if (healthFill != null) healthFill.fillAmount = Mathf.Clamp01(pc.Health / 100f);
                if (ammoText != null) ammoText.text = $"{pc.magazine} / {pc.AmmoPistol}";
                bool driving = pc.IsInVehicle;
                if (speedPanel != null) speedPanel.SetActive(driving);
                if (driving && pc.currentVehicle != null)
                {
                    if (speedText != null) speedText.text = Mathf.RoundToInt(pc.currentVehicle.SpeedKmh).ToString();
                    if (gearText != null) gearText.text = pc.currentVehicle.Gear.ToString();
                }
            }
        }

        public void RefreshMoney(float money)
        {
            if (moneyText != null) moneyText.text = "$" + Mathf.RoundToInt(money).ToString("N0");
        }

        public void SetWanted(int level)
        {
            if (wantedStars == null) return;
            wantedStars.fillAmount = level / 5f;
        }

        public void SetHealth(float normalized)
        {
            if (healthFill != null) healthFill.fillAmount = Mathf.Clamp01(normalized);
        }

        public void ShowInteraction(string text)
        {
            if (interactionText == null) return;
            interactionText.text = text;
            var c = interactionText.color;
            interactionText.color = new Color(c.r, c.g, c.b, string.IsNullOrEmpty(text) ? 0f : 1f);
        }

        public void Popup(string text, float seconds = 3f)
        {
            if (popupText == null) return;
            popupText.text = text;
            popupTimer = seconds;
        }

        public void Hint(string text, float seconds = 4f)
        {
            if (hintText == null) return;
            hintText.text = text;
            hintText.gameObject.SetActive(true);
            _hintTimer = seconds;
        }
    }
}
