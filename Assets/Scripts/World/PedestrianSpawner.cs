using System.Collections.Generic;
using UnityEngine;

namespace NeonDistrict.World
{
    /// <summary>
    /// Держит вокруг игрока «живую толпу»: спавнит и убирает прохожих,
    /// следит, чтобы их не было слишком много в одном месте.
    /// </summary>
    public class PedestrianSpawner : MonoBehaviour
    {
        public Transform player;
        public GameObject pedestrianPrefab;
        public int maxPedestrians = 26;
        public float spawnRadius = 90f;
        public float despawnRadius = 160f;
        public int spawnBatch = 2;
        public float spawnInterval = 0.6f;

        private readonly List<GameObject> _peds = new List<GameObject>();
        private float _timer;

        private void Start()
        {
            if (pedestrianPrefab == null)
                pedestrianPrefab = Resources.Load<GameObject>("NeonDistrict/Pedestrian");
            if (player == null && Core.GameManager.Instance != null && Core.GameManager.Instance.player != null)
                player = Core.GameManager.Instance.player.transform;
            for (int i = 0; i < maxPedestrians / 2; i++) SpawnOne();
        }

        private void Update()
        {
            if (player == null) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = spawnInterval;

            if (_peds.Count < maxPedestrians)
                for (int i = 0; i < spawnBatch; i++) SpawnOne();

            for (int i = _peds.Count - 1; i >= 0; i--)
            {
                if (_peds[i] == null) { _peds.RemoveAt(i); continue; }
                if (Vector3.Distance(_peds[i].transform.position, player.position) > despawnRadius)
                {
                    Destroy(_peds[i]);
                    _peds.RemoveAt(i);
                }
            }
        }

        private void SpawnOne()
        {
            if (pedestrianPrefab == null || player == null) return;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var dist = Random.Range(spawnRadius * 0.4f, spawnRadius);
                var pos = player.position + new Vector3(Mathf.Cos(angle) * dist, 0.2f, Mathf.Sin(angle) * dist);
                var city = FindFirstObjectByType<CityController>();
                if (city != null) pos = city.RandomSpawnPoint(player.position, spawnRadius * 0.3f, spawnRadius);
                pos.y = 0.2f;
                if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 12f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    var go = Instantiate(pedestrianPrefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
                    go.name = "Ped";
                    _peds.Add(go);
                    return;
                }
            }
        }
    }
}
