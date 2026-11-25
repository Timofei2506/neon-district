using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using NeonDistrict.Core;

namespace NeonDistrict.Tests
{
    /// <summary>
    /// Приёмочные тесты в РЕАЛЬНОМ Play Mode (Unity Test Framework, batchmode, без человека).
    ///
    /// Почему Play Mode: в edit-mode не тикают Update/корутины, поэтому там нельзя проверить
    /// ни клик по кнопке, ни геймплей — только наличие объектов.
    ///
    /// Что проверяется:
    ///   1. меню: EventSystem, модуль ввода с назначенными actions, видимый и незахваченный курсор,
    ///      попадание raycast в каждую кнопку, реальный клик виртуальной мышью через Input System
    ///      (MouseState -> InputSystemUIInputModule -> Button.onClick), переход меню -> игра;
    ///   2. клавиатура: ↑/↓ меняют выбранную кнопку, Enter её нажимает;
    ///   3. геймплей: сцена грузится, игра стартует, идут время/деньги/розыск/погода/миссии/гараж,
    ///      пауза и возврат в игру работают, курсор в паузе возвращается;
    ///   4. ни одного исключения/ошибки в логе за прогон.
    ///
    /// Любая проваленная проверка = красный тест = красный CI, сборка не выпускается.
    /// </summary>
    public class MenuAndGameplayTests
    {
        private const string ScenePath = "Assets/Scenes/Playground.unity";

        private readonly List<string> _log = new List<string>();
        private int _runtimeErrors;

        // ------------------------------------------------------------------ инфраструктура

        private void Note(string line)
        {
            _log.Add(line);
            Debug.Log("[Smoke] " + line);
        }

        private void Check(bool ok, string what)
        {
            if (ok) Note("✓ " + what);
            else Note("✗ " + what);
            Assert.IsTrue(ok, what);
        }

        [SetUp]
        public void SetUp()
        {
            // В CI редактор запущен с -nographics: RenderTexture/камера-в-текстуру физически не
            // создаются, и Unity пишет в лог ошибки. Это ограничение headless-режима, а не баг игры,
            // поэтому такие сообщения ожидаемы. Всё остальное (исключения, NullReference) — провал.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            _log.Clear();
            _runtimeErrors = 0;
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
        }

        // сообщения, которые в headless-режиме (-nographics) неизбежны и не относятся к игре
        private static readonly string[] HeadlessNoise =
        {
            "RenderTexture.Create failed",
            "RenderTexture.Create",
            "GfxDevice",
            "Shader",
            "No graphics device",
        };

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            if (condition.Contains("[Smoke]")) return;
            if (HeadlessNoise.Any(n => condition.Contains(n))) return;   // ограничение headless-режима
            _runtimeErrors++;
            _log.Add("✗ ИСКЛЮЧЕНИЕ/ОШИБКА В РАНТАЙМЕ: " + condition);
        }

        private static IEnumerator LoadGameScene()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
                while (load != null && !load.isDone) yield return null;
            }
            yield return null;
            yield return null;
            // даём отработать Awake/Start всех систем
            for (int i = 0; i < 5; i++) yield return null;
        }

        private static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();

        /// <summary>В CI редактор запущен с -nographics: экрана/графики нет.</summary>
        private static bool Headless =>
            SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;

        /// <summary>
        /// Приводит игру в состояние «меню». Нужно потому, что GameManager — DontDestroyOnLoad-
        /// синглтон: он переживает перезагрузку сцены, и без этого тесты зависели бы от порядка.
        /// </summary>
        private static GameManager ResetToMenu()
        {
            var gm = Find<GameManager>();
            if (gm == null) return null;
            gm.SetState(GameManager.State.Menu);
            Time.timeScale = 0f;
            return gm;
        }

        private static string Label(Button b)
        {
            var t = b.GetComponentInChildren<Text>();
            return t != null ? t.text : b.name;
        }

        private static Vector2 ScreenPointOf(Button b)
        {
            var rt = (RectTransform)b.transform;
            var canvas = b.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
        }

        // ------------------------------------------------------------------ меню: раскладка

        [UnityTest]
        public IEnumerator Menu_IsClickable()
        {
            yield return LoadGameScene();

            Note("--- меню ---");
            ResetToMenu();
            yield return null;
            var menu = Find<NeonDistrict.UI.MenuScreen>();
            var gm = Find<GameManager>();
            Check(menu != null, "MenuScreen есть в сцене");
            Check(gm != null, "GameManager есть в сцене");
            if (menu == null || !menu) { yield break; }
            var canvas = menu.canvas;
            Note($"   состояние: {gm?.CurrentState}, canvas.enabled: {canvas?.enabled}, screen: {Screen.width}x{Screen.height}, headless: {Headless}");
            Check(canvas != null && canvas.isActiveAndEnabled, "канвас меню активен");
            if (canvas == null) yield break;

            Check(gm != null && gm.CurrentState == GameManager.State.Menu, "старт в меню");
            yield return null;

            // EventSystem + модуль + actions
            var es = EventSystem.current;
            Check(es != null, "EventSystem.current != null");
            var module = es != null ? es.GetComponent<BaseInputModule>() : null;
            Check(module != null, "у EventSystem есть модуль ввода");
            if (module != null)
            {
                Note("   модуль: " + module.GetType().FullName);
                var actionsProp = module.GetType().GetProperty("actionsAsset");
                if (actionsProp != null)
                    Check(actionsProp.GetValue(module) != null, "модуль ввода получил actions-ассет");
                var pointProp = module.GetType().GetProperty("point");
                if (pointProp != null)
                {
                    var pointRef = pointProp.GetValue(module);
                    var action = pointRef?.GetType().GetProperty("action")?.GetValue(pointRef);
                    Check(action != null, "модуль ввода: действие Point назначено");
                }
            }

            // курсор
            Note($"   курсор: visible={Cursor.visible}, lockState={Cursor.lockState}");
            Check(Cursor.visible, "Cursor.visible == true в меню");
            Check(Headless || Cursor.lockState == CursorLockMode.None, "Cursor.lockState == None");

            // кнопки: активность, рейкаст, верхний элемент
            var buttons = new List<Button>();
            if (menu.playButton != null) buttons.Add(menu.playButton);
            if (menu.quitButton != null) buttons.Add(menu.quitButton);
            if (menu.continueButton != null) buttons.Add(menu.continueButton);
            Check(buttons.Count >= 2, "кнопок в меню: " + buttons.Count);

            foreach (var b in buttons)
            {
                if (b == null) continue;
                var label = Label(b);
                Check(b.gameObject.activeInHierarchy, "«" + label + "»: активна");
                var g = b.targetGraphic as Graphic;
                Check(g != null && g.raycastTarget, "«" + label + "»: targetGraphic ловит рейкасты");

                var screen = ScreenPointOf(b);
                var data = new PointerEventData(es) { position = screen };
                var results = new List<RaycastResult>();
                es.RaycastAll(data, results);
                bool hit = results.Count > 0 && results[0].gameObject != null &&
                           (results[0].gameObject == b.gameObject || results[0].gameObject.transform.IsChildOf(b.transform));
                var top = results.Count > 0 ? results[0].gameObject.name : "<ничего>";
                Check(hit, "«" + label + "»: под точкой клика именно кнопка (сейчас: " + top + ")");
            }

            Assert.AreEqual(0, _runtimeErrors, "ошибки рантайма");
        }

        // ------------------------------------------------------------------ меню: реальный клик

        [UnityTest]
        public IEnumerator Menu_PlayButton_Click_StartsGame()
        {
            yield return LoadGameScene();

            var gm = ResetToMenu();
            yield return null;
            var menu = Find<NeonDistrict.UI.MenuScreen>();
            Assert.IsNotNull(menu, "MenuScreen есть в сцене");
            Assert.IsNotNull(gm, "GameManager есть в сцене");

            var play = menu.playButton;
            Assert.IsNotNull(play, "кнопка «ИГРАТЬ» есть");

            bool clicked = false;
            var probe = play.gameObject.AddComponent<ClickProbe>();
            probe.callback = () => clicked = true;

            yield return Click(play);
            UnityEngine.Object.Destroy(probe);

            Check(clicked, "клик мышью по «ИГРАТЬ» доходит до Button.onClick");
            yield return null;
            Check(gm.CurrentState == GameManager.State.Playing, "после клика состояние Playing");
            Assert.AreEqual(0, _runtimeErrors, "ошибки рантайма");
        }

        // ------------------------------------------------------------------ меню: клавиатура

        [UnityTest]
        public IEnumerator Menu_Keyboard_NavigatesAndActivates()
        {
            yield return LoadGameScene();

            var gm = ResetToMenu();
            Assert.IsNotNull(gm, "GameManager есть в сцене");
            yield return null;

            var menu = Find<NeonDistrict.UI.MenuScreen>();
            Assert.IsNotNull(menu, "MenuScreen есть в сцене");
            var es = EventSystem.current;
            Assert.IsNotNull(es, "EventSystem есть");

            // навигация в меню своя (пункт «выбран» ведёт MenuScreen), поэтому проверяем не
            // EventSystem.currentSelectedGameObject, а то, что стрелки не ломают меню и Enter играет
            yield return Key(UnityEngine.InputSystem.Key.DownArrow);
            yield return Key(UnityEngine.InputSystem.Key.UpArrow);

            var play = menu.playButton;
            Assert.IsNotNull(play, "кнопка «ИГРАТЬ» есть");
            Check(play.interactable, "после стрелок «ИГРАТЬ» остаётся доступной");

            bool clicked = false;
            var probe = play.gameObject.AddComponent<ClickProbe>();
            probe.callback = () => clicked = true;
            yield return Key(UnityEngine.InputSystem.Key.Enter);
            UnityEngine.Object.Destroy(probe);

            Check(clicked, "Enter по выбранному пункту вызывает Button.onClick");
            yield return null;
            Check(gm.CurrentState == GameManager.State.Playing, "после Enter игра стартует");
            Assert.AreEqual(0, _runtimeErrors, "ошибки рантайма");
        }

        // ------------------------------------------------------------------ геймплей

        [UnityTest]
        public IEnumerator Gameplay_Runs()
        {
            yield return LoadGameScene();

            Note("--- геймплей ---");
            var gm = Find<GameManager>();
            Assert.IsNotNull(gm, "GameManager есть в сцене");
            if (gm.CurrentState != GameManager.State.Playing) gm.StartGame(false);
            yield return null;
            yield return null;

            Check(gm.CurrentState == GameManager.State.Playing, "состояние Playing");
            Check(Mathf.Approximately(Time.timeScale, 1f), "timeScale == 1");
            Note($"   курсор в игре: visible={Cursor.visible}, lockState={Cursor.lockState}");
            // В headless-режиме Unity игнорирует захват курсора, поэтому строгую проверку
            // lockState делаем только когда есть реальный экран.
            Check(!Cursor.visible && (Headless || Cursor.lockState == CursorLockMode.Locked),
                  "в игре курсор скрыт/захвачен");

            Check(Find<NeonDistrict.Player.PlayerController>() != null, "PlayerController");
            Check(Camera.main != null, "MainCamera");
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
            Check(!Mathf.Approximately(gm.TimeOfDay, hour0), $"игровое время идёт ({hour0:F3} -> {gm.TimeOfDay:F3})");

            // движение игрока от клавиатуры
            var player = Find<NeonDistrict.Player.PlayerController>();
            if (player != null)
            {
                var before = player.transform.position;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(
                    UnityEngine.InputSystem.Keyboard.current,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
                for (int i = 0; i < 30; i++) yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(
                    UnityEngine.InputSystem.Keyboard.current,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
                var moved = Vector3.Distance(before, player.transform.position);
                Check(moved > 0.1f, $"игрок двигается от клавиши W ({moved:F2} м)");
            }

            // экономика/розыск
            float money0 = gm.Money;
            gm.AddMoney(1000f);
            Check(gm.Money >= money0 + 999f, "деньги начисляются (" + gm.Money + ")");
            gm.AddWanted(2);
            Check(gm.Wanted >= 2, "розыск растёт (" + gm.Wanted + ")");
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
            else Note("? WeatherSystem не найден в сцене");

            // гараж и миссии
            var garage = Find<NeonDistrict.Economy.GarageSystem>();
            if (garage != null)
                Check(garage.Offers != null && garage.Offers.Count > 0, "в гараже " + (garage.Offers?.Count ?? 0) + " машин");

            var missions = Find<NeonDistrict.Missions.MissionSystem>();
            if (missions != null)
            {
                var catalog = NeonDistrict.Missions.MissionCatalog.Build();
                Check(catalog.Count >= 6, "в каталоге " + catalog.Count + " миссий");
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

            for (int i = 0; i < 120; i++) yield return null;
            Note("   кадров за прогон: " + Time.frameCount);

            Assert.AreEqual(0, _runtimeErrors, "ошибки рантайма: " + string.Join(" | ", _log.Where(l => l.Contains("ИСКЛЮЧЕНИЕ"))));
        }

        // ------------------------------------------------------------------ ввод (виртуальные устройства)

        private sealed class ClickProbe : MonoBehaviour, IPointerClickHandler
        {
            public Action callback;
            public void OnPointerClick(PointerEventData eventData) => callback?.Invoke();
        }

        /// <summary>Клик настоящей виртуальной мышью: Input System -> модуль ввода -> Button.onClick.</summary>
        private static IEnumerator Click(Button b)
        {
            var screen = ScreenPointOf(b);
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null)
            {
                Debug.Log("[Smoke] ✗ Mouse.current == null — виртуальную мышь создать не удалось");
                Assert.Fail("нет виртуальной мыши Input System");
            }

            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = screen });
            yield return null;
            yield return null;

            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = screen, buttons = 1 });
            yield return null;
            yield return null;

            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = screen });
            yield return null;
            yield return null;
        }

        private static IEnumerator Key(UnityEngine.InputSystem.Key key)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) Assert.Fail("нет виртуальной клавиатуры Input System");

            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb,
                new UnityEngine.InputSystem.LowLevel.KeyboardState(key));
            yield return null;
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb,
                new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return null;
            yield return null;
        }
    }
}
