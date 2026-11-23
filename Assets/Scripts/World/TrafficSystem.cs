using System.Collections.Generic;
using UnityEngine;
using NeonDistrict.Vehicles;

namespace NeonDistrict.World
{
    /// <summary>
    /// Поток машин по регулярной сетке улиц.
    ///
    /// Каждая машина получает маршрут «до следующего перекрёстка», на перекрёстке
    /// случайно выбирает поворот, держит дистанцию до впереди идущей машины и
    /// тормозит перед игроком. Никакого NavMesh — на сетке он не нужен и в разы дороже.
    /// </summary>
    public class TrafficSystem : MonoBehaviour
    {
        [Header("Настройки")]
        public int maxCars = 16;
        public float spawnRadius = 140f;
        public float despawnRadius = 240f;
        public float laneOffset = 3.4f;
        public float cruiseSpeed = 11f;
        public GameObject[] carPrefabs;

        [Header("Ссылки")]
        public CityController city;
        public Transform player;

        private readonly List<CarAI> _cars = new List<CarAI>();
        private float _spawnCooldown;

        private class CarAI
        {
            public Transform t;
            public VehicleController vehicle;
            public Vector3 target;
            public float speed;
            public bool alongX;
            public int dir;
        }

        private void Start()
        {
            if (city == null) city = FindFirstObjectByType<CityController>();
            if (player == null && Core.GameManager.Instance != null && Core.GameManager.Instance.player != null)
                player = Core.GameManager.Instance.player.transform;
            for (int i = 0; i < maxCars / 2; i++) SpawnCar(true);
        }

        private void Update()
        {
            _spawnCooldown -= Time.deltaTime;
            if (_spawnCooldown <= 0f && _cars.Count < maxCars)
            {
                _spawnCooldown = 1.6f;
                SpawnCar(false);
                CullFarCars();
            }

            var p = player != null ? player.position : Vector3.zero;
            for (int i = 0; i < _cars.Count; i++) UpdateCar(_cars[i], p);
        }

        private void SpawnCar(bool silent)
        {
            if (city == null || carPrefabs == null || carPrefabs.Length == 0) return;
            // берём точку на сетке улиц Манхэттена
            bool alongX = Random.value > 0.5f;
            int dir = Random.value > 0.5f ? 1 : -1;
            Vector3 pos = city.RandomSpawnPoint(player != null ? player.position : Vector3.zero,
                                                 silent ? 30f : 60f, spawnRadius);
            pos.y = 0.05f;
            if (!silent && player != null && Vector3.Distance(pos, player.position) < 45f) return;

            var prefab = carPrefabs[Random.Range(0, carPrefabs.Length)];
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, alongX ? (dir > 0 ? 0f : 180f) : (dir > 0 ? 90f : -90f), 0f), transform);
            go.name = "TrafficCar";
            var car = new CarAI
            {
                t = go.transform,
                vehicle = go.GetComponent<VehicleController>(),
                alongX = alongX,
                dir = dir,
                speed = cruiseSpeed * Random.Range(0.75f, 1.15f),
            };
            if (car.vehicle != null) car.vehicle.engineOn = true;
            PickNextTarget(car);
            _cars.Add(car);
        }

        private void PickNextTarget(CarAI car)
        {
            const float step = 90f;   // средний шаг кварталов Манхэттена
            var p = car.t.position;
            // на перекрёстке меняем направление
            bool turn = Random.value > 0.62f;
            if (turn) { car.alongX = !car.alongX; car.dir = Random.value > 0.5f ? 1 : -1; }
            car.target = car.alongX
                ? new Vector3(p.x + step * car.dir, p.y, Mathf.Round(p.z / 25f) * 25f + laneOffset * car.dir)
                : new Vector3(Mathf.Round(p.x / 25f) * 25f + laneOffset * car.dir, p.y, p.z + step * car.dir);
        }

        private void UpdateCar(CarAI car, Vector3 playerPos)
        {
            if (car.t == null) return;
            var pos = car.t.position;
            var flatTarget = new Vector3(car.target.x, pos.y, car.target.z);
            var toTarget = flatTarget - pos;
            if (toTarget.sqrMagnitude < 4f) PickNextTarget(car);

            var fwd = toTarget.normalized;
            var rot = Quaternion.LookRotation(new Vector3(fwd.x, 0f, fwd.z));
            car.t.rotation = Quaternion.Slerp(car.t.rotation, rot, 3.2f * Time.deltaTime);

            // дистанция до машины впереди
            float speed = car.speed;
            foreach (var other in _cars)
            {
                if (other == car || other.t == null) continue;
                var d = other.t.position - pos;
                if (d.sqrMagnitude > 140f) continue;
                if (Vector3.Dot(d.normalized, fwd) < 0.7f) continue;
                if (d.magnitude < 9f) speed = Mathf.Min(speed, 2f);
                else if (d.magnitude < 16f) speed = Mathf.Min(speed, 6f);
            }
            // тормозим перед игроком
            var toPlayer = playerPos - pos;
            if (Vector3.Dot(toPlayer.normalized, fwd) > 0.6f && toPlayer.magnitude < 11f) speed = 0f;

            car.t.position += car.t.forward * (speed * Time.deltaTime);

            if (car.vehicle != null)
            {
                // держим визуальные колёса/двигатель в тонусе
                var dummyInput = new Vector2(0f, speed > 0.2f ? 1f : 0f);
                car.vehicle.Drive(dummyInput, false);
            }
        }

        private void CullFarCars()
        {
            if (player == null) return;
            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                if (_cars[i].t == null) { _cars.RemoveAt(i); continue; }
                if (Vector3.Distance(_cars[i].t.position, player.position) > despawnRadius)
                {
                    Destroy(_cars[i].t.gameObject);
                    _cars.RemoveAt(i);
                }
            }
        }
    }
}
