using UnityEngine;
using NeonDistrict.Core;

namespace NeonDistrict.Police
{
    /// <summary>
    /// Спавн полицейских патрулей на уровне розыска.
    /// Уровень 1 → 1 машина, 3 → 2, 5 → 3. Машины появляются вне поля зрения игрока
    /// (за спиной) и сразу с включённой сиреной.
    /// </summary>
    public class PoliceSpawner : MonoBehaviour
    {
        [Header("Префабы патрулей")]
        public GameObject policeCarPrefab;
        [Tooltip("Дополнительные варианты (SUV / седан / электрокар). Пустые игнорируются.")]
        public GameObject[] extraPolicePrefabs;

        [Header("Настройки")]
        public int maxCars = 3;
        public float spawnDistance = 120f;
        public float spawnInterval = 6f;
        public bool spawnOnWanted = true;

        private float _timer;
        private readonly System.Collections.Generic.List<GameObject> _cars = new System.Collections.Generic.List<GameObject>();

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying || !spawnOnWanted) return;

            int desired = gm.Wanted <= 0 ? 0 : Mathf.Clamp((gm.Wanted + 1) / 2, 1, maxCars);
            _cars.RemoveAll(c => c == null);
            if (_cars.Count >= desired) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = spawnInterval;
            SpawnOne(gm);
        }

        private void SpawnOne(GameManager gm)
        {
            var prefab = PickPrefab();
            if (prefab == null) return;
            if (gm.player == null) return;

            // точка за спиной игрока, чтобы не появлялись в кадре
            var player = gm.player.transform;
            var behind = player.position - player.forward * spawnDistance;
            float angle = Random.Range(-70f, 70f);
            behind = Quaternion.Euler(0f, angle, 0f) * (behind - player.position) + player.position;
            behind.y = 0.05f;

            var car = Instantiate(prefab, behind, Quaternion.Euler(0f, player.eulerAngles.y + 180f + Random.Range(-30f, 30f), 0f), transform);
            car.name = "PoliceCar";
            var ai = car.GetComponent<PoliceAI>();
            if (ai != null) ai.target = player;
            _cars.Add(car);
            Debug.Log("[Police] Патруль выехал. Розыск: " + gm.Wanted);
        }

        private GameObject PickPrefab()
        {
            var pool = new System.Collections.Generic.List<GameObject>();
            if (policeCarPrefab != null) pool.Add(policeCarPrefab);
            if (extraPolicePrefabs != null)
                foreach (var p in extraPolicePrefabs) if (p != null) pool.Add(p);
            if (pool.Count == 0)
            {
                var fallback = Resources.Load<GameObject>("NeonDistrict/PoliceCar");
                if (fallback != null) pool.Add(fallback);
            }
            if (pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }

        /// <summary>Очистить всех (при загрузке/смерти игрока).</summary>
        public void ClearAll()
        {
            foreach (var c in _cars) if (c != null) Destroy(c);
            _cars.Clear();
        }
    }
}
