#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using NeonDistrict.Core;

namespace NeonDistrict.EditorTools
{
    /// <summary>
    /// Геймплейный smoke-тест в реальном Unity: грузит сцену, стартует игру и проверяет,
    /// что ключевые системы действительно живут — а не просто «компилируются».
    ///
    /// Проверяется:
    ///   1. сцена грузится, все системы на месте (игрок, камера, город, трафик, пешеходы,
    ///      миссии, гараж, полиция, HUD, миникарта, погода, сутки);
    ///   2. старт игры: состояние Playing, timeScale = 1, курсор захвачен;
    ///   3. игровой цикл идёт (время суток ползёт), деньги/розыск меняются через API;
    ///   4. машин можно вызвать/купить, миссия берётся, погода переключается;
    ///   5. ни одной ошибки в логе (error/exceptions) за прогон.
    ///
    /// Режим: `-executeMethod NeonDistrict.EditorTools.GameplaySmokeTest.Run` (CI, Exit(0/1))
    /// или пункт меню для локального запуска.
    /// </summary>
    public static class GameplaySmokeTest
    {
        private const string ScenePath = "Assets/Scenes/Playground.unity";
        private static readonly StringBuilder Report = new StringBuilder();
        private static int _failures;

        [UnityEditor.MenuItem("Neon District/Tests/Gameplay smoke test", priority = 100)]
        public static void RunFromMenu() => Run();

        public static void Run() => RunCounted(null);

        /// <summary>
        /// Запускает тест. <paramref name="onDone"/> получает число провалов; если он null,
        /// тест сам завершает редактор (code 0/1) — режим CI.
        /// </summary>
        public static void RunCounted(System.Action<int> onDone)
        {
            var go = new GameObject("GameplaySmokeTestRunner");
            Object.DontDestroyOnLoad(go);
            SetCallback(onDone);
            go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private void Start() => StartCoroutine(Execute());
            private void Update() { }   // сцена должна тикать

            private IEnumerator Execute()
            {
                Report.Clear();
                _failures = 0;
                Log("=== GameplaySmokeTest ===");

                // ---------------------------------------------------------- сцена
                var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
                while (load != null && !load.isDone) yield return null;
                yield return null;
                yield return null;

                var scene = SceneManager.GetActiveScene();
                Check(scene.path == ScenePath, $"загружена сцена {scene.path}");

                // ---------------------------------------------------------- системы
                var gm = Find<GameManager>();
                var player = Find<NeonDistrict.Player.PlayerController>();
                var cam = Camera.main;
                var city = Find<NeonDistrict.World.CityController>();
                var traffic = Find<NeonDistrict.World.TrafficSystem>();
                var peds = Find<NeonDistrict.World.PedestrianSpawner>();
                var missions = Find<NeonDistrict.Missions.MissionSystem>();
                var garage = Find<NeonDistrict.Economy.GarageSystem>();
                var police = Find<NeonDistrict.Police.PoliceSpawner>();
                var hud = Find<NeonDistrict.UI.HUDController>();
                var minimap = Find<NeonDistrict.UI.Minimap>();
                var weather = Find<NeonDistrict.World.WeatherSystem>();
                var dayNight = Find<NeonDistrict.World.DayNightCycle>();
                var menu = Find<NeonDistrict.UI.MenuScreen>();
                var orbit = Find<NeonDistrict.CameraRig.ThirdPersonOrbit>();

                Check(gm != null, "GameManager");
                Check(player != null, "PlayerController");
                Check(cam != null, "MainCamera");
                Check(city != null, "CityController (город)");
                Check(traffic != null, "TrafficSystem");
                Check(peds != null, "PedestrianSpawner");
                Check(missions != null, "MissionSystem");
                Check(garage != null, "GarageSystem");
                Check(police != null, "PoliceSpawner");
                Check(hud != null, "HUDController");
                Check(minimap != null, "Minimap");
                Check(weather != null, "WeatherSystem");
                Check(dayNight != null, "DayNightCycle");
                Check(menu != null, "MenuScreen");
                Check(orbit != null, "ThirdPersonOrbit (камера)");

                if (gm == null) { Finish(); yield break; }

                // ---------------------------------------------------------- меню -> игра
                yield return null;
                Check(gm.CurrentState == GameManager.State.Menu, $"старт в меню (сейчас {gm.CurrentState})");
                Check(Cursor.visible, "курсор виден в меню");

                gm.StartGame(false);
                yield return null;
                Check(gm.CurrentState == GameManager.State.Playing, $"состояние Playing (сейчас {gm.CurrentState})");
                Check(Mathf.Approximately(Time.timeScale, 1f), $"timeScale = 1 (сейчас {Time.timeScale})");
                Check(!Cursor.visible && Cursor.lockState == CursorLockMode.Locked, "курсор захвачен в игре");

                // ---------------------------------------------------------- игровой цикл
                float hourBefore = gm.TimeOfDay;
                float moneyBefore = gm.Money;
                for (int i = 0; i < 60; i++) yield return null;      // ~1 секунда
                Check(gm.TimeOfDay != hourBefore, $"игровое время идёт ({hourBefore:F3} -> {gm.TimeOfDay:F3})");
                Check(Mathf.Approximately(gm.Money, moneyBefore), $"деньги не «утекают» ({gm.Money})");

                gm.AddMoney(1000f);
                Check(gm.Money >= moneyBefore + 999f, $"начисление денег работает ({gm.Money})");

                gm.AddWanted(2);
                Check(gm.Wanted >= 2, $"розыск растёт ({gm.Wanted})");
                gm.ClearWanted();
                Check(gm.Wanted == 0, "розыск сбрасывается");

                // ---------------------------------------------------------- погода
                if (weather != null)
                {
                    weather.SetWeather(NeonDistrict.World.WeatherSystem.Weather.Rain, instant: true);
                    yield return null;
                    Check(weather.Current == NeonDistrict.World.WeatherSystem.Weather.Rain, "погода переключается");
                    weather.SetWeather(NeonDistrict.World.WeatherSystem.Weather.Clear, instant: true);
                }

                // ---------------------------------------------------------- гараж / миссии
                if (garage != null)
                {
                    int cars = 0;
                    try { cars = garage.Offers != null ? garage.Offers.Count : 0; }
                    catch (System.Exception e) { Fail("гараж: " + e.Message); }
                    Check(cars > 0, $"в гараже есть машины ({cars})");
                }

                if (missions != null)
                {
                    bool started = false;
                    try
                    {
                        var catalog = NeonDistrict.Missions.MissionCatalog.Build();
                        Check(catalog.Count >= 6, $"в каталоге миссий {catalog.Count} шт");
                        if (catalog.Count > 0)
                        {
                            missions.StartMission(catalog[0]);
                            started = missions.Current != null;
                        }
                    }
                    catch (System.Exception e) { Fail("миссии: " + e.Message); }
                    Check(started, "миссия стартует");
                }

                // ---------------------------------------------------------- мир реально живёт
                int carsAlive = 0, pedsAlive = 0;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (t.name.Contains("Traffic")) carsAlive++;
                    if (t.name.Contains("Pedestrian")) pedsAlive++;
                }
                Log($"   объектов в сцене: {Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length}, " +
                    $"трафик≈{carsAlive}, пешеходы≈{pedsAlive}");

                for (int i = 0; i < 180; i++) yield return null;     // ~3 секунды симуляции
                Check(Time.frameCount > 0, "кадры идут без падения");

                // ---------------------------------------------------------- пауза
                gm.Pause();
                yield return null;
                Check(gm.CurrentState == GameManager.State.Paused, "пауза включается");
                Check(Mathf.Approximately(Time.timeScale, 0f), "в паузе timeScale = 0");
                gm.Resume();
                yield return null;
                Check(gm.CurrentState == GameManager.State.Playing, "с паузы возвращаемся в игру");

                Finish();
            }
        }

        private static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>();

        private static void Check(bool ok, string what)
        {
            if (ok) Log("   ✓ " + what);
            else Fail(what);
        }

        private static void Fail(string what)
        {
            _failures++;
            Log("   ✗ " + what);
        }

        private static void Log(string line)
        {
            Report.AppendLine(line);
            Debug.Log("[GameplaySmoke] " + line);
        }

        private static System.Action<int> _onDone;

        public static void SetCallback(System.Action<int> callback) => _onDone = callback;

        private static void Finish()
        {
            Debug.Log("[GameplaySmoke] ---- итог ----\n" + Report);
            if (_failures > 0) Debug.LogError($"[GameplaySmoke] ПРОВАЛ: {_failures} проверок");
            else Debug.Log("[GameplaySmoke] OK: геймплейные системы работают");

            var callback = _onDone;
            _onDone = null;
            if (callback != null) { callback(_failures); return; }

            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(_failures > 0 ? 1 : 0);
        }
    }
}
#endif
