#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NeonDistrict.Core;
using NeonDistrict.UI;

namespace NeonDistrict.EditorTools
{
    /// <summary>
    /// Автопроверка главного меню в РЕАЛЬНОМ Unity (batchmode, без человека):
    ///   • есть EventSystem и модуль ввода с назначенными действиями;
    ///   • курсор видим и не заблокирован;
    ///   • каждая кнопка ловит указатель (raycast попадает именно в неё, а не в оверлей);
    ///   • клик через EventSystem реально вызывает обработчик (проверяем на «ВЫХОД»).
    ///
    /// Намеренно не тянет com.unity.test-framework: работает как обычный редакторский метод,
    /// который вызывается из CI (`-executeMethod ...UISmokeTest.Run`) и валит сборку при провале.
    /// </summary>
    public static class UISmokeTest
    {
        private static readonly StringBuilder Report = new StringBuilder();
        private static int _failures;

        public static void Run()
        {
            int failures = CheckOnly();
            if (failures > 0) UnityEditor.EditorApplication.Exit(1);
            UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>Только проверки, без выхода из редактора. Возвращает число провалов.</summary>
        public static int CheckOnly()
        {
            Report.Clear();
            _failures = 0;
            Log("=== UISmokeTest: меню ===");

            var gm = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            var menu = UnityEngine.Object.FindFirstObjectByType<MenuScreen>();
            if (menu == null) { Fail("MenuScreen не найден в сцене"); Finish(); return _failures; }
            if (gm == null) { Fail("GameManager не найден в сцене"); Finish(); return _failures; }

            // сцена должна открываться в меню
            Check(gm.CurrentState == GameManager.State.Menu, $"стартовое состояние = Menu (сейчас {gm.CurrentState})");

            // ---------------------------------------------------------- EventSystem
            var es = EventSystem.current;
            Check(es != null, "EventSystem присутствует");
            if (es != null)
            {
                var module = es.GetComponent<BaseInputModule>();
                Check(module != null, "у EventSystem есть модуль ввода");
                if (module != null)
                {
                    Log($"   модуль ввода: {module.GetType().Name}");
#if !ND_COMPILE_CHECK
                    if (module is UnityEngine.InputSystem.UI.InputSystemUIInputModule ism)
                    {
                        Check(ism.actionsAsset != null, "InputSystemUIInputModule: actionsAsset назначен");
                        Check(ism.point != null && ism.point.action != null, "InputSystemUIInputModule: point назначен");
                        Check(ism.leftClick != null && ism.leftClick.action != null, "InputSystemUIInputModule: leftClick назначен");
                        Check(ism.submit != null && ism.submit.action != null, "InputSystemUIInputModule: submit назначен");
                    }
#endif
                }
            }

            // ---------------------------------------------------------- курсор
            Check(Cursor.visible, "Cursor.visible == true в меню");
            Check(Cursor.lockState == CursorLockMode.None, $"Cursor.lockState == None (сейчас {Cursor.lockState})");

            // ---------------------------------------------------------- кнопки
            var buttons = new List<Button>();
            if (menu.playButton != null) buttons.Add(menu.playButton);
            if (menu.continueButton != null) buttons.Add(menu.continueButton);
            if (menu.quitButton != null) buttons.Add(menu.quitButton);
            Check(buttons.Count >= 2, $"кнопок в меню: {buttons.Count}");

            foreach (var button in buttons)
            {
                Check(button.gameObject.activeInHierarchy, $"кнопка «{Name(button)}» активна");
                var canvasGroup = button.GetComponentInParent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    var cg = canvasGroup;
                    Check(cg.blocksRaycasts, $"«{Name(button)}»: CanvasGroup.blocksRaycasts == true");
                    Check(cg.interactable, $"«{Name(button)}»: CanvasGroup.interactable == true");
                }
                var canvas = button.GetComponentInParent<Canvas>();
                Check(canvas != null && canvas.isActiveAndEnabled, $"«{Name(button)}»: Canvas активен");
                Check(button.GetComponentInParent<GraphicRaycaster>() != null, $"«{Name(button)}»: GraphicRaycaster есть");
                CheckTargetGraphic(button);
                CheckTopmost(button);
            }

            // ---------------------------------------------------------- клик
            if (EventSystem.current != null && menu.quitButton != null)
                CheckClickOnQuit(menu.quitButton);

            Finish();
            return _failures;
        }

        private static void CheckTargetGraphic(Button button)
        {
            var graphic = button.targetGraphic as Graphic;
            Check(graphic != null, $"«{Name(button)}»: targetGraphic назначен");
            if (graphic != null)
            {
                Check(graphic.raycastTarget, $"«{Name(button)}»: targetGraphic.raycastTarget == true");
                var rt = graphic.rectTransform;
                Check(rt.rect.width > 4f && rt.rect.height > 4f, $"«{Name(button)}»: размер клика {rt.rect.width}×{rt.rect.height}");
            }
        }

        /// <summary>Проверяем, что в точку кнопки raycast попадает сама кнопка (а не фон/оверлей).</summary>
        private static void CheckTopmost(Button button)
        {
            var rect = (RectTransform)button.transform;
            Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
            var canvas = button.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : (canvas != null ? canvas.worldCamera : null);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);

            var data = new PointerEventData(EventSystem.current) { position = screen };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);

            bool hit = results.Count > 0 && results[0].gameObject != null &&
                       (results[0].gameObject == button.gameObject ||
                        results[0].gameObject.transform.IsChildOf(button.transform));

            string topmost = results.Count > 0 ? results[0].gameObject.name : "<ничего>";
            Check(hit, $"«{Name(button)}»: верхний элемент под курсором = сама кнопка (сейчас: {topmost})");
        }

        /// <summary>
        /// Полный клик по «ВЫХОД»: pointerDown+pointerUp через EventSystem.
        /// В batchmode выполняем через ExecuteEvents, т.к. реальная мышь недоступна;
        /// это ровно тот путь, которым идёт InputSystemUIInputModule.
        /// </summary>
        private static void CheckClickOnQuit(Button quit)
        {
            var clicked = false;
            var probe = quit.gameObject.AddComponent<ClickProbe>();
            probe.onClick = () => clicked = true;

            var rect = (RectTransform)quit.transform;
            var data = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
            };

            ExecuteEvents.Execute(quit.gameObject, data, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(quit.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(quit.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(quit.gameObject, data, ExecuteEvents.pointerClickHandler);

            UnityEngine.Object.DestroyImmediate(probe);
            Check(clicked, "клик по «ВЫХОД» доходит до Button.onClick");
        }

        private sealed class ClickProbe : MonoBehaviour, IPointerClickHandler
        {
            public Action onClick;
            public void OnPointerClick(PointerEventData eventData) => onClick?.Invoke();
        }

        private static string Name(Button b)
        {
            var text = b.GetComponentInChildren<Text>();
            return text != null ? text.text : b.name;
        }

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
            Debug.Log("[UISmokeTest] " + line);
        }

        private static void Finish()
        {
            Debug.Log("[UISmokeTest] ---- итог ----\n" + Report);
            if (_failures > 0) Debug.LogError($"[UISmokeTest] ПРОВАЛ: {_failures} проверок");
            else Debug.Log("[UISmokeTest] OK: меню кликабельно, курсор на месте");
        }
    }
}
#endif
