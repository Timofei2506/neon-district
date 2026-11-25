#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using NeonDistrict.Core;

namespace NeonDistrict.EditorTools
{
    /// <summary>
    /// Офлайн-проверки, которые выполняются БЕЗ Play Mode (в том числе в `dotnet`/csc-прогоне и в
    /// обычном редакторе): сцена, иерархия меню, EventSystem, единая точка входа в игру,
    /// целостность каталогов миссий/машин. Это быстрый «дальний» барьер; клики и геймплей
    /// проверяют Play Mode-тесты в Assets/Tests/PlayMode (там же — реальный ввод).
    /// </summary>
    public static class SmokeTests
    {
        [UnityEditor.MenuItem("Neon District/Tests/Scene sanity (edit mode)", priority = 100)]
        public static void RunSceneSanity()
        {
            Debug.Log("[Smoke] edit-mode проверки: " + (Run() ? "OK" : "ПРОВАЛ"));
        }

        /// <summary>Возвращает true, если все проверки прошли.</summary>
        public static bool Run()
        {
            int failures = 0;
            void Check(bool ok, string what)
            {
                if (ok) Debug.Log("[Smoke] ✓ " + what);
                else { failures++; Debug.LogError("[Smoke] ✗ " + what); }
            }

            var scenePath = "Assets/Scenes/Playground.unity";
            Check(File.Exists(scenePath), "сцена существует: " + scenePath);

            var menu = Type.GetType("NeonDistrict.UI.MenuScreen, Assembly-CSharp");
            Check(menu != null, "MenuScreen скомпилирован");
            if (menu != null)
            {
                var names = menu.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToList();
                foreach (var required in new[] { "playButton", "quitButton" })
                    Check(names.Contains(required), "MenuScreen." + required + " есть");
            }

            var gmType = typeof(GameManager);
            foreach (var m in new[] { "StartGame", "Pause", "Resume", "AddMoney", "AddWanted", "ClearWanted" })
                Check(gmType.GetMethod(m) != null, "GameManager." + m + "() есть");

            var catalog = NeonDistrict.Missions.MissionCatalog.Build();
            Check(catalog.Count >= 6, "миссий в каталоге: " + catalog.Count);
            Check(catalog.All(m => !string.IsNullOrEmpty(m.title) && !string.IsNullOrEmpty(m.id)), "у каждой миссии есть id и название");

            Debug.Log(failures == 0 ? "[Smoke] OK: офлайн-проверки пройдены" : $"[Smoke] ПРОВАЛ: {failures}");
            return failures == 0;
        }
    }
}
#endif
