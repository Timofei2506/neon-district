#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using NeonDistrict.Core;
using NeonDistrict.World;
using NeonDistrict.UI;

namespace NeonDistrict.EditorTools
{
    /// <summary>
    /// Сборка игровой сцены из скриптов (Neon District → 3) Build Playground Scene).
    ///
    /// В репозитории сцена генерируется заново, поэтому в git нет километровых .unity-файлов,
    /// а результат всегда соответствует коду.
    /// </summary>
    public static class SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Playground.unity";
        private const string ManhattanFbx = "Assets/Art/Manhattan/Manhattan.fbx";
        private const string NavMeshPath = "Assets/Art/Manhattan/Manhattan_NavMesh.asset";

        [MenuItem("Neon District/3) Build Playground Scene", priority = 3)]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------------------------------------------------- свет
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 2.0f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sunGo.transform.rotation = Quaternion.Euler(42f, 28f, 0f);

            var moonGo = new GameObject("Moon");
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.68f, 1f);
            moon.intensity = 0f;
            moonGo.transform.rotation = Quaternion.Euler(222f, -28f, 0f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0022f;
            RenderSettings.fogColor = new Color(0.55f, 0.62f, 0.72f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.46f, 0.55f);
            RenderSettings.skybox = null;

            // ---------------------------------------------------------- город
            var cityGo = new GameObject("City");
            var city = cityGo.AddComponent<CityController>();
            city.manhattanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManhattanFbx);
            var layoutJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Art/Manhattan/manhattan_layout.json");
            city.layoutJson = layoutJson;
            var navmesh = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath);
            city.bakedNavMesh = navmesh;

            // ---------------------------------------------------------- игрок
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NeonDistrict/Player.prefab");
            GameObject playerGo = null;
            if (playerPrefab != null)
            {
                playerGo = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                playerGo.transform.position = new Vector3(0f, 1.2f, 0f);
                playerGo.transform.rotation = Quaternion.Euler(0f, 210f, 0f);
            }
            else Debug.LogError("[Neon District] Нет префаба Player — сначала 2) Build Prefabs");

            // ---------------------------------------------------------- менеджер
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.startHour = 8.5f;
            gm.secondsPerGameMinute = 1.2f;

            // ---------------------------------------------------------- камера
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 3000f;
            cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = playerGo != null ? playerGo.transform.position + new Vector3(0f, 3f, -6f) : new Vector3(0f, 3f, -6f);
            var orbit = camGo.AddComponent<NeonDistrict.CameraRig.ThirdPersonOrbit>();
            orbit.target = playerGo != null ? playerGo.transform : null;
            orbit.enabled = true;

            // ---------------------------------------------------------- трафик / пешеходы / дневной цикл
            var systems = new GameObject("Systems");
            var traffic = systems.AddComponent<TrafficSystem>();
            traffic.city = city;
            traffic.player = playerGo != null ? playerGo.transform : null;

            var peds = systems.AddComponent<PedestrianSpawner>();
            peds.player = playerGo != null ? playerGo.transform : null;
            peds.pedestrianPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NeonDistrict/Pedestrian.prefab");

            // стартовая машина рядом с игроком
            var playerPos = playerGo != null ? playerGo.transform.position : Vector3.zero;
            var garageCars = new[]
            {
                "Assets/Resources/NeonDistrict/Vehicle_GLS.prefab",
                "Assets/Resources/NeonDistrict/Vehicle_Polestar.prefab",
                "Assets/Resources/NeonDistrict/Vehicle_Corvette.prefab",
                "Assets/Resources/NeonDistrict/Vehicle_AMG.prefab",
            };
            for (int i = 0; i < garageCars.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(garageCars[i]);
                if (prefab == null) continue;
                var car = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                car.transform.position = playerPos + new Vector3(4f + i * 5f, 0.1f, 3f);
                car.transform.rotation = Quaternion.Euler(0f, 210f, 0f);
            }

            var police = systems.AddComponent<NeonDistrict.Police.PoliceSpawner>();
            police.policeCarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NeonDistrict/PoliceCar.prefab");
            police.extraPolicePrefabs = new[]
            {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NeonDistrict/Police_AMG.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NeonDistrict/Police_Polestar.prefab"),
            };

            var dnc = systems.AddComponent<World.DayNightCycle>();
            dnc.sun = sun;
            dnc.moon = moon;

            // ---------------------------------------------------------- UI
            var ui = new GameObject("UI");
            var hud = ui.AddComponent<NeonDistrict.UI.HUDController>();
            hud.player = playerGo != null ? playerGo.transform : null;
            NeonDistrict.UI.RuntimeUI.Build(hud, ui.transform);

            var menu = ui.AddComponent<MainMenuUI>();
            NeonDistrict.UI.RuntimeUI.BuildMenu(menu, ui.transform);

            // ---------------------------------------------------------- сохраняем
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            // сцена в билд-листе
            var buildScenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = buildScenes;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<b>[Neon District]</b> Сцена собрана: {ScenePath}. Запускайте Play.");
        }

        // ------------------------------------------------------------ navmesh
        [MenuItem("Neon District/4) Bake NavMesh (Manhattan)", priority = 4)]
        public static void BakeNavMesh()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManhattanFbx);
            if (prefab == null) { Debug.LogError("Нет " + ManhattanFbx); return; }

            // временная сцена с городом
            var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            SceneManager.MoveGameObjectToScene(inst, temp);

            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.4f;
            settings.agentHeight = 1.8f;
            settings.agentSlope = 45f;
            settings.agentClimb = 0.45f;
            settings.agentTypeID = 0;

            // источники: геометрия города + большая «земля», чтобы пешеходы не падали
            var sources = new System.Collections.Generic.List<NavMeshBuildSource>();
            var markups = new System.Collections.Generic.List<NavMeshBuildMarkup>();
            UnityEditor.AI.NavMeshBuilder.CollectSources(
                new Bounds(Vector3.zero, new Vector3(12000f, 900f, 12000f)),
                ~0, NavMeshCollectGeometry.RenderMeshes, 0, markups, sources);
            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(12000f, 0.2f, 12000f),
                transform = Matrix4x4.Translate(new Vector3(0f, -0.12f, 0f)),
                area = 0,
            });
            var bounds = new Bounds(Vector3.zero, new Vector3(12000f, 900f, 12000f));
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data != null)
            {
                if (File.Exists(NavMeshPath)) AssetDatabase.DeleteAsset(NavMeshPath);
                AssetDatabase.CreateAsset(data, NavMeshPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Neon District] NavMesh запечён: " + NavMeshPath + " (плоскость улиц — движение по земле)");
            }

            EditorSceneManager.CloseScene(temp, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
#endif
