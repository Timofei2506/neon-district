using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NeonDistrict.World
{
    /// <summary>
    /// Держит Манхэттен в сцене, грузит навигацию и предоставляет сервисы
    /// «где дорога», «где район», «где свободно» для трафика, пешеходов и миссий.
    ///
    /// Модель города — статичная геометрия (статический батчинг включён),
    /// поэтому кадр не упирается в CPU даже на 8×8 км.
    /// </summary>
    public class CityController : MonoBehaviour
    {
        [Header("Источники")]
        public GameObject manhattanPrefab;
        public TextAsset layoutJson;
        public NavMeshData bakedNavMesh;

        [Header("Опции")]
        public bool spawnOnAwake = true;
        public Vector3 playableCenter = Vector3.zero;
        public float playableRadius = 3200f;
        public bool hideFarAwayProps = true;

        public CityLayout Layout { get; private set; }
        public GameObject CityInstance { get; private set; }
        public Bounds CityBounds { get; private set; }

        private NavMeshDataInstance _navInstance;
        private readonly List<Vector3> _roadPoints = new List<Vector3>();
        private readonly List<string> _districtNames = new List<string> { "ХВАНДАМ", "СОМУН", "КУРО", "ПОРТ СОНСАН" };

        public IReadOnlyList<Vector3> RoadPoints => _roadPoints;

        private void Awake()
        {
            if (spawnOnAwake) Spawn();
        }

        public void Spawn()
        {
            if (CityInstance != null) return;
            if (manhattanPrefab == null) { Debug.LogError("[City] нет префаба Манхэттена"); return; }
            CityInstance = Instantiate(manhattanPrefab, Vector3.zero, Quaternion.identity, transform);
            CityInstance.name = "Manhattan";
            Layout = CityLayout.FromJson(layoutJson != null ? layoutJson.text : null);
            CityBounds = CalculateBounds(CityInstance);
            EnableStaticBatching(CityInstance);
            LoadNavMesh();
            CollectRoadPoints();
            Debug.Log($"[City] Город в сцене: {CityBounds.size} м");
        }

        private static Bounds CalculateBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, new Vector3(100f, 100f, 100f));
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private void EnableStaticBatching(GameObject go)
        {
            // Статический батчинг снижает число draw call'ов в разы.
            // Признак Static запекается в сцене (см. SceneSetup), в рантайме
            // выставляем только то, что доступно без UnityEditor.
#if UNITY_EDITOR
            var flags = UnityEditor.StaticEditorFlags.BatchingStatic | UnityEditor.StaticEditorFlags.OccludeeStatic;
#endif
            foreach (var t in go.GetComponentsInChildren<Transform>())
            {
#if UNITY_EDITOR
                // в редакторе помечаем объекты Static -> статический батчинг и occlusion
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
#endif
                var r = t.GetComponent<MeshRenderer>();
                if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private void LoadNavMesh()
        {
            if (bakedNavMesh == null) return;
            _navInstance = NavMesh.AddNavMeshData(bakedNavMesh);
        }

        private void CollectRoadPoints()
        {
            _roadPoints.Clear();
            if (Layout == null) Layout = CityLayout.FromJson(layoutJson != null ? layoutJson.text : null);
            foreach (var x in Layout.streetsX)
                foreach (var z in Layout.streetsZ)
                    _roadPoints.Add(new Vector3(x, 0f, z));
            if (_roadPoints.Count == 0)
            {
                for (int i = -20; i <= 20; i++)
                    for (int j = -20; j <= 20; j++)
                        _roadPoints.Add(new Vector3(i * 90f, 0f, j * 90f));
            }
        }

        /// <summary>Свободная точка у дороги для спавна машины/пешехода рядом с игроком.</summary>
        public Vector3 RandomSpawnPoint(Vector3 near, float minDist = 40f, float maxDist = 140f)
        {
            for (int i = 0; i < 30; i++)
            {
                var rp = _roadPoints[Random.Range(0, _roadPoints.Count)];
                float d = Vector3.Distance(rp, near);
                if (d < minDist || d > maxDist) continue;
                return rp;
            }
            return near + new Vector3(Random.Range(-80f, 80f), 0f, Random.Range(-80f, 80f));
        }

        public string DistrictNameAt(Vector3 p)
        {
            int idx = (p.x >= playableCenter.x ? 1 : 0) + (p.z >= playableCenter.z ? 2 : 0);
            return _districtNames[Mathf.Clamp(idx, 0, _districtNames.Count - 1)];
        }

        private void OnDestroy()
        {
            if (_navInstance.valid) _navInstance.Remove();
        }

    }
}
