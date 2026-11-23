using System;
using System.Collections.Generic;
using UnityEngine;
using NeonDistrict.Core;
using NeonDistrict.Player;
using NeonDistrict.Vehicles;

namespace NeonDistrict.Economy
{
    /// <summary>Карточка автомобиля в каталоге гаража.</summary>
    [Serializable]
    public class VehicleOffer
    {
        public string id;
        public string title;
        [Tooltip("Имя префаба в Assets/Resources/NeonDistrict")] public string prefabName;
        public int price;
        public float topSpeedKmh;
        public float mass;
        [TextArea] public string blurb;
        public bool ownedByDefault;
    }

    /// <summary>
    /// Гараж и экономика: каталог машин, покупка/продажа, выбор активной машины,
    /// вызов её к игроку. Деньги — в GameManager, владение — здесь (и в сейве).
    ///
    /// Никаких ассетов: префабы берутся из Resources по имени, поэтому каталог
    /// можно править строкой в коде.
    /// </summary>
    [DefaultExecutionOrder(-180)]
    public class GarageSystem : MonoBehaviour
    {
        [Serializable]
        private class GarageSave
        {
            public List<string> owned = new List<string>();
            public string selected = "";
        }

        public static GarageSystem Instance { get; private set; }

        [Header("Ссылки")]
        public Transform player;

        [Header("Каталог")]
        public List<VehicleOffer> offers = new List<VehicleOffer>();

        [Header("Сервис")]
        [Tooltip("Сколько стоит вызов машины к игроку")] public int deliveryFee = 60;

        public event Action OnGarageChanged;
        public event Action<string> OnMessage;

        private readonly HashSet<string> _owned = new HashSet<string>();
        private string _selected;
        private VehicleController _activeVehicle;

        public string SelectedId => _selected;
        public bool IsOwned(string id) => _owned.Contains(id);
        public IReadOnlyList<VehicleOffer> Offers => offers;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            BuildDefaultCatalog();
            LoadState();
        }

        private void Start()
        {
            if (player == null && GameManager.Instance != null && GameManager.Instance.player != null)
                player = GameManager.Instance.player.transform;
        }

        // ---------------------------------------------------------------- каталог
        private void BuildDefaultCatalog()
        {
            if (offers.Count > 0) return;
            offers.Add(new VehicleOffer
            {
                id = "amg", title = "Mercedes-AMG C63", prefabName = "Vehicle_AMG",
                price = 0, topSpeedKmh = 250f, mass = 1750f, ownedByDefault = true,
                blurb = "Задний привод, злой характер. Твоя первая машина.",
            });
            offers.Add(new VehicleOffer
            {
                id = "gls", title = "Mercedes GLS", prefabName = "Vehicle_GLS",
                price = 4200, topSpeedKmh = 210f, mass = 2400f,
                blurb = "Тяжёлый внедорожник: держит удары и не боится бордюров.",
            });
            offers.Add(new VehicleOffer
            {
                id = "corvette", title = "Corvette Stingray", prefabName = "Vehicle_Corvette",
                price = 12500, topSpeedKmh = 290f, mass = 1600f,
                blurb = "Спорткар для тех, кто умеет уходить от погони.",
            });
            offers.Add(new VehicleOffer
            {
                id = "polestar", title = "Polestar 4", prefabName = "Vehicle_Polestar",
                price = 18000, topSpeedKmh = 235f, mass = 2300f,
                blurb = "Электрический гран-туризмо: тихо, быстро, дорого.",
            });
        }

        // ---------------------------------------------------------------- API
        public VehicleOffer Find(string id)
        {
            foreach (var o in offers) if (o.id == id) return o;
            return null;
        }

        public bool Buy(string id)
        {
            var offer = Find(id);
            if (offer == null) return false;
            if (_owned.Contains(id)) { OnMessage?.Invoke("Уже в гараже"); return false; }

            var gm = GameManager.Instance;
            if (gm == null) return false;
            if (!gm.TrySpend(offer.price))
            {
                OnMessage?.Invoke("Не хватает денег: нужно $" + offer.price);
                return false;
            }

            _owned.Add(id);
            _selected = id;
            SaveState();
            OnMessage?.Invoke("Куплено: " + offer.title);
            OnGarageChanged?.Invoke();
            return true;
        }

        public bool Sell(string id)
        {
            if (!_owned.Contains(id)) return false;
            if (_owned.Count <= 1) { OnMessage?.Invoke("Нельзя продать последнюю машину"); return false; }
            var offer = Find(id);
            if (offer == null) return false;

            int refund = Mathf.RoundToInt(offer.price * 0.6f);
            _owned.Remove(id);
            if (_selected == id) foreach (var o in offers) if (_owned.Contains(o.id)) { _selected = o.id; break; }
            GameManager.Instance?.AddMoney(refund);
            SaveState();
            OnMessage?.Invoke("Продано за $" + refund);
            OnGarageChanged?.Invoke();
            return true;
        }

        public void Select(string id)
        {
            if (!_owned.Contains(id)) return;
            _selected = id;
            SaveState();
            OnMessage?.Invoke("Выбрано: " + (Find(id)?.title ?? id));
            OnGarageChanged?.Invoke();
        }

        /// <summary>Вызвать выбранную машину к игроку (как «механик» в GTA).</summary>
        public VehicleController DeliverSelected()
        {
            var offer = Find(_selected);
            if (offer == null) { OnMessage?.Invoke("Машина не выбрана"); return null; }
            if (player == null) return null;

            var prefab = Resources.Load<GameObject>("NeonDistrict/" + offer.prefabName);
            if (prefab == null) { OnMessage?.Invoke("Префаб не найден: " + offer.prefabName); return null; }

            var gm = GameManager.Instance;
            if (gm != null && !gm.TrySpend(deliveryFee)) { OnMessage?.Invoke("Вызов стоит $" + deliveryFee); return null; }

            // точка рядом с игроком, со сдвигом назад-вправо, чтобы не упасть на него
            var t = player;
            var pos = t.position - t.forward * 3f + t.right * 3.5f + Vector3.up * 0.4f;
            var rot = Quaternion.Euler(0f, t.eulerAngles.y, 0f);

            var go = Instantiate(prefab, pos, rot);
            var vc = go.GetComponent<VehicleController>();
            if (vc != null)
            {
                vc.engineOn = true;
                _activeVehicle = vc;
            }
            OnMessage?.Invoke("Машина доставлена: " + offer.title);
            return vc;
        }

        public VehicleController ActiveVehicle => _activeVehicle;

        // ---------------------------------------------------------------- сохранение
        private const string Key = "neondistrict.garage";

        private void SaveState()
        {
            var data = new GarageSave { owned = new List<string>(_owned), selected = _selected };
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private void LoadState()
        {
            foreach (var o in offers) if (o.ownedByDefault) _owned.Add(o.id);
            _selected = offers.Count > 0 ? offers[0].id : "";

            var json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var data = JsonUtility.FromJson<GarageSave>(json);
                    if (data != null && data.owned != null && data.owned.Count > 0)
                    {
                        _owned.Clear();
                        foreach (var id in data.owned) _owned.Add(id);
                        if (!string.IsNullOrEmpty(data.selected)) _selected = data.selected;
                    }
                }
                catch (Exception e) { Debug.LogWarning("[Garage] save: " + e.Message); }
            }
            if (!_owned.Contains(_selected) && _owned.Count > 0)
                foreach (var o in offers) if (_owned.Contains(o.id)) { _selected = o.id; break; }
        }

        public void ResetProgress()
        {
            _owned.Clear();
            foreach (var o in offers) if (o.ownedByDefault) _owned.Add(o.id);
            _selected = offers.Count > 0 ? offers[0].id : "";
            SaveState();
            OnGarageChanged?.Invoke();
        }
    }
}
