#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NeonDistrict.Core;

namespace NeonDistrict.EditorTools
{
    /// <summary>
    /// Автотесты в РЕАЛЬНОМ Play Mode (batchmode, без человека). Именно Play Mode важен:
    /// в edit-mode не тикают Update/корутины, поэтому там нельзя проверить ни клики, ни геймплей.
    ///
    /// Что проверяется:
    ///   1. меню: EventSystem, модуль ввода с назначенными actions, видимый и незахваченный курсор,
    ///      попадание raycast в каждую кнопку, реальный клик виртуальной мышью через Input System
    ///      (PointerState -> InputSystemUIInputModule -> Button.onClick);
    ///   2. переход меню -> игра;
    ///   3. геймплей: время идёт, деньги/розыск/погода/миссии/гараж работают, пауза и возврат;
    ///   4. ни одного исключения за прогон.
    ///
    /// В CI тесты стартует <see cref="SmokeTestRunner"/> (срабатывает по -runSmokeTests после
    /// компиляции), потому что -executeMethod не может сам войти в Play Mode.
    /// Локально: пункт меню Neon District/Tests/Full (Play Mode).
    /// </summary>
    public static class SmokeTests
    {
        private const string ScenePath = "Assets/Scenes/Playground.unity";

        [UnityEditor.MenuItem("Neon District/Tests/Full (Play Mode)", priority = 101)]
        public static void RunFromMenu()
        {
            Session.Begin(withGameplay: true);
        }

        /// <summary>CI: подготовить проект и сразу запустить тесты в Play Mode.</summary>
        public static void RunAllPrepared()
        {
            CIBuild.PrepareProject();
            RunAll();
        }

        /// <summary>Тесты по уже подготовленному проекту: вход в Play Mode + прогон.</summary>
        public static void RunAll()
        {
            Session.Begin(withGameplay: true);
        }

        // ================================================================== session
        internal sealed class Session : MonoBehaviour
        {
            private static Session _instance;
            private bool _withGameplay;
            private readonly StringBuilder _log = new StringBuilder();
            private int _failures;

            public static void Begin(bool withGameplay)
            {
                if (_instance != null) return;
                UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
                _pendingGameplay = withGameplay;

                if (UnityEditor.EditorApplication.isPlaying)
                {
                    OnPlayModeChanged(UnityEditor.PlayModeStateChange.EnteredPlayMode);
                }
                else
                {
                    // Просим редактор войти в Play Mode. Объекты здесь НЕ создаём: в edit-mode
                    // DontDestroyOnLoad бросает исключение — всё создаётся в OnPlayModeChanged.
                    Debug.Log("[Smoke] входим в Play Mode для запуска тестов");
                    UnityEditor.EditorApplication.isPlaying = true;
                }
            }

            private static bool _pendingGameplay;
            private static bool _started;

            private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
            {
                if (state != UnityEditor.PlayModeStateChange.EnteredPlayMode) return;
                if (_started) return;
                _started = true;
                UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;

                var go = new GameObject("~SmokeTestSession");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<Session>();
                _instance._withGameplay = _pendingGameplay;
                _instance.StartCoroutine(_instance.Run());
            }

            private void Awake()
            {
                Application.logMessageReceived += OnLog;
                if (EventSystem.current == null)
                {
                    var es = new GameObject("EventSystem");
                    es.AddComponent<EventSystem>();
                    es.AddComponent<NeonDistrict.UI.UIInputBootstrap>();
                    DontDestroyOnLoad(es);
                }
            }

            private void OnDestroy() => Application.logMessageReceived -= OnLog;

            private void OnLog(string condition, string stackTrace, LogType type)
            {
                if (type != LogType.Exception && type != LogType.Error) return;
                // собственные сообщения теста не считаем
                if (condition.Contains("[Smoke]") || condition.Contains("[UISmoke]") || condition.Contains("[Gameplay]")) return;
                _failures++;
                Log($"✗ ИСКЛЮЧЕНИЕ/ОШИБКА В РАНТАЙМЕ: {condition}");
            }

            private IEnumerator Run()
            {
                Log("=== SmokeTests: Play Mode ===");

                // ---------------------------------------------------------- загрузка сцены
                if (SceneManager.GetActiveScene().path != ScenePath)
                {
                    var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
                    while (load != null && !load.isDone) yield return null;
                }
                yield return null;
                yield return null;
                Check(SceneManager.GetActiveScene().path == ScenePath, $"сцена {ScenePath} загружена");

                yield return TestMenu();
                if (_withGameplay) yield return TestGameplay();

                Finish();
            }

            // ---------------------------------------------------------------- меню
            private IEnumerator TestMenu()
            {
                Log("--- меню ---");
                var menu = Find<NeonDistrict.UI.MenuScreen>();
                var gm = Find<GameManager>();
                var canvas = menu != null ? menu.canvas : null;

                Check(menu != null, "MenuScreen есть в сцене");
                Check(gm != null, "GameManager есть в сцене");
                Check(canvas != null && canvas.isActiveAndEnabled, "канвас меню активен");
                if (menu == null || canvas == null) yield break;

                Check(gm.CurrentState == GameManager.State.Menu, $"старт в меню (сейчас {gm.CurrentState})");
                yield return null;

                // EventSystem + модуль + actions
                var es = EventSystem.current;
                Check(es != null, "EventSystem.current != null");
                var module = es != null ? es.GetComponent<BaseInputModule>() : null;
                Check(module != null, "у EventSystem есть модуль ввода");
                if (module != null)
                {
                    Log($"   модуль: {module.GetType().FullName}");
                    var actionsProp = module.GetType().GetProperty("actionsAsset");
                    if (actionsProp != null)
                        Check(actionsProp.GetValue(module) != null, "модуль ввода получил actions-ассет");
                    var pointProp = module.GetType().GetProperty("point");
                    if (pointProp != null)
                    {
                        var pointRef = pointProp.GetValue(module);
                        var action = pointRef != null ? pointRef.GetType().GetProperty("action")?.GetValue(pointRef) : null;
                        Check(action != null, "модуль ввода: действие Point назначено");
                    }
                }

                // курсор
                Check(Cursor.visible, "Cursor.visible == true в меню");
                Check(Cursor.lockState == CursorLockMode.None, $"Cursor.lockState == None (сейчас {Cursor.lockState})");

                // кнопки: активность, рейкаст, верхний элемент
                var buttons = new List<Button>();
                if (menu.playButton != null) buttons.Add(menu.playButton);
                if (menu.quitButton != null) buttons.Add(menu.quitButton);
                if (menu.continueButton != null) buttons.Add(menu.continueButton);
                Check(buttons.Count >= 2, $"кнопок найдено: {buttons.Count}");

                foreach (var b in buttons)
                {
                    Check(b.gameObject.activeInHierarchy, $"«{Label(b)}»: активна");
                    Check(b.IsInteractable() || !b.interactable, $"«{Label(b)}»: interactable={b.interactable}");
                    var g = b.targetGraphic as Graphic;
                    Check(g != null && g.raycastTarget, $"«{Label(b)}»: targetGraphic ловит рейкасты");
                    CheckTopmost(b);
                }

                // РЕАЛЬНЫЙ клик: виртуальная мышь -> Input System -> модуль -> Button.onClick
                var play = menu.playButton;
                if (play != null)
                {
                    bool clicked = false;
                    var probe = play.gameObject.AddComponent<ClickProbe>();
                    probe.callback = () => clicked = true;

                    var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam,
                        ((RectTransform)play.transform).TransformPoint(((RectTransform)play.transform).rect.center));

                    yield return ClickAt(screen);
                    Object.Destroy(probe);

                    Check(clicked, "клик мышью по «ИГРАТЬ» доходит до Button.onClick");

                    // после клика должно начаться игровое состояние
                    yield return null;
                    if (gm != null)
                        Check(gm.CurrentState == GameManager.State.Playing, $"после клика состояние Playing (сейчас {gm.CurrentState})");
                }
            }

            /// <summary>Двигаем виртуальную мышь и «нажимаем» ЛКМ через Input System.</summary>
            private IEnumerator ClickAt(Vector2 screenPosition)
            {
#if !ND_COMPILE_CHECK
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse == null)
                {
                    Log("   ! Mouse.current == null — виртуальную мышь создать не удалось");
                    yield break;
                }

                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = screenPosition });
                yield return null;
                yield return null;

                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = screenPosition, buttons = 1 });
                yield return null;
                yield return null;

                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = screenPosition });
                yield return null;
                yield return null;
#else
                yield return null;
#endif
            }

            private void CheckTopmost(Button b)
            {
                var rt = (RectTransform)b.transform;
                var canvas = b.GetComponentInParent<Canvas>();
                var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));

                var data = new PointerEventData(EventSystem.current) { position = screen };
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(data, results);

                bool hit = results.Count > 0 && results[0].gameObject != null &&
                           (results[0].gameObject == b.gameObject || results[0].gameObject.transform.IsChildOf(b.transform));
                string top = results.Count > 0 ? results[0].gameObject.name : "<ничего>";
                Check(hit, $"«{Label(b)}»: под точкой клика именно кнопка (сейчас: {top})");
            }

            // ---------------------------------------------------------------- геймплей
            private IEnumerator TestGameplay()
            {
                Log("--- геймплей ---");
                var gm = Find<GameManager>();
                if (gm == null) { Fail("GameManager пропал"); yield break; }

                if (gm.CurrentState != GameManager.State.Playing) gm.StartGame(false);
                yield return null;
                Check(gm.CurrentState == GameManager.State.Playing, $"состояние Playing (сейчас {gm.CurrentState})");
                Check(Mathf.Approximately(Time.timeScale, 1f), $"timeScale == 1 (сейчас {Time.timeScale})");
                Check(!Cursor.visible && Cursor.lockState == CursorLockMode.Locked, "в игре курсор захвачен");

                var player = Find<NeonDistrict.Player.PlayerController>();
                var cam = Camera.main;
                Check(player != null, "PlayerController");
                Check(cam != null, "MainCamera");
                Check(Find<NeonDistrict.World.CityController>() != null, "CityController");
                Check(Find<NeonDistrict.World.TrafficSystem>() != null, "TrafficSystem");
                Check(Find<NeonDistrict.World.PedestrianSpawner>() != null, "PedestrianSpawner");
                Check(Find<NeonDistrict.Missions.MissionSystem>() != null, "MissionSystem");
                Check(Find<NeonDistrict.Economy.GarageSystem>() != null, "GarageSystem");
                Check(Find<NeonDistrict.Police.PoliceSpawner>() != null, "PoliceSpawner");
                Check(Find<NeonDistrict.UI.HUDController>() != null, "HUDController");
                Check(Find<NeonDistrict.UI.Minimap>() != null, "Minimap");
                Check(Find<NeonDistrict.World.DayNightCycle>() != null, "DayNightCycle");

                // время суток идёт
                float hour0 = gm.TimeOfDay;
                for (int i = 0; i < 120; i++) yield return null;
                Check(gm.TimeOfDay != hour0, $"игровое время идёт ({hour0:F3} -> {gm.TimeOfDay:F3})");

                // экономика/розыск
                float money0 = gm.Money;
                gm.AddMoney(1000f);
                Check(gm.Money >= money0 + 999f, $"деньги начисляются ({gm.Money})");
                gm.AddWanted(2);
                Check(gm.Wanted >= 2, $"розыск растёт ({gm.Wanted})");
                gm.ClearWanted();
                Check(gm.Wanted == 0, "розыск сбрасывается");

                // погода
                var weather = Find<NeonDistrict.World.WeatherSystem>();
                if (weather != null)
                {
                    weather.SetWeather(NeonDistrict.World.WeatherSystem.Weather.Rain, true);
                    yield return null;
                    Check(weather.Current == NeonDistrict.World.WeatherSystem.Weather.Rain, "погода переключается");
                    weather.SetWeather(NeonDistrict.World.WeatherSystem.Weather.Clear, true);
                }

                // гараж и миссии
                var garage = Find<NeonDistrict.Economy.GarageSystem>();
                if (garage != null) Check(garage.Offers != null && garage.Offers.Count > 0, $"в гараже {garage.Offers?.Count} машин");

                var missions = Find<NeonDistrict.Missions.MissionSystem>();
                if (missions != null)
                {
                    var catalog = NeonDistrict.Missions.MissionCatalog.Build();
                    Check(catalog.Count >= 6, $"в каталоге {catalog.Count} миссий");
                    if (catalog.Count > 0)
                    {
                        missions.StartMission(catalog[0]);
                        yield return null;
                        Check(missions.Current != null, "миссия стартует");
                    }
                }

                // пауза и возврат (курсор обязан вернуться)
                gm.Pause();
                yield return null;
                Check(gm.CurrentState == GameManager.State.Paused, "пауза включается");
                Check(Mathf.Approximately(Time.timeScale, 0f), "в паузе timeScale == 0");
                Check(Cursor.visible, "в паузе курсор снова виден");
                gm.Resume();
                yield return null;
                Check(gm.CurrentState == GameManager.State.Playing, "возврат из паузы в игру");

                // кадры идут без падения
                for (int i = 0; i < 120; i++) yield return null;
                Log($"   кадров за прогон: {Time.frameCount}");
            }

            // ---------------------------------------------------------------- helpers
            private sealed class ClickProbe : MonoBehaviour, IPointerClickHandler
            {
                public System.Action callback;
                public void OnPointerClick(PointerEventData eventData) => callback?.Invoke();
            }

            private static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>();

            private static string Label(Button b)
            {
                var t = b.GetComponentInChildren<Text>();
                return t != null ? t.text : b.name;
            }

            private void Check(bool ok, string what)
            {
                if (ok) Log("✓ " + what);
                else Fail(what);
            }

            private void Fail(string what)
            {
                _failures++;
                Log("✗ " + what);
            }

            private void Log(string line)
            {
                _log.AppendLine(line);
                Debug.Log("[Smoke] " + line);
            }

            private void Finish()
            {
                Debug.Log("[Smoke] ---- итог ----\n" + _log);
                Debug.Log(_failures == 0
                    ? "[Smoke] OK: меню и геймплей проверены"
                    : $"[Smoke] ПРОВАЛ: {_failures} проверок");

                // контракт game-ci: он ищет этот блок и валит job при Errors != 0
                System.Console.WriteLine();
                System.Console.WriteLine("###########################");
                System.Console.WriteLine("#      Build results      #");
                System.Console.WriteLine("###########################");
                System.Console.WriteLine();
                System.Console.WriteLine("Duration: " + System.TimeSpan.FromSeconds(Time.realtimeSinceStartup));
                System.Console.WriteLine("Warnings: 0");
                System.Console.WriteLine("Errors: " + (_failures > 0 ? 1 : 0));
                System.Console.WriteLine("Size: 0 bytes");
                System.Console.WriteLine();

                if (Application.isBatchMode)
                {
                    UnityEditor.EditorApplication.Exit(_failures > 0 ? 1 : 0);
                }
                else
                {
                    UnityEditor.EditorApplication.isPlaying = false;
                }
            }
        }
    }
}
#endif
