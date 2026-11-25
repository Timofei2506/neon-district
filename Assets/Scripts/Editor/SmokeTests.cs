#if UNITY_EDITOR
using UnityEngine;

namespace NeonDistrict.EditorTools
{
    /// <summary>
    /// Единая точка входа для автотестов в CI:
    ///   Unity -batchmode -nographics -executeMethod NeonDistrict.EditorTools.SmokeTests.RunAll
    ///
    /// Запускает проверки меню (EventSystem/курсор/кликабельность) и геймплейный смоук,
    /// печатает сводку в формате, который понимает game-ci (блок «Build results»),
    /// и выходит с кодом 1 при любом провале — то есть красный CI вместо «вроде работает».
    /// </summary>
    public static class SmokeTests
    {
        /// <summary>Полный прогон для CI: сначала собрать проект (URP/префабы/сцену), затем тесты.</summary>
        public static void RunAllPrepared()
        {
            Debug.Log("=== Neon District: автотесты (с подготовкой проекта) ===");
            CIBuild.PrepareProject();
            RunAll();
        }

        public static void RunAll()
        {
            Debug.Log("=== Neon District: автотесты ===");

            int uiFailures = UISmokeTest.CheckOnly();
            Debug.Log($"[Smoke] меню: провалов {uiFailures}");

            // геймплей грузит сцену заново, поэтому идёт вторым и завершает прогон
            GameplaySmokeTest.RunCounted(gameplayFailures =>
            {
                int total = uiFailures + gameplayFailures;
                Report(total);
            });
        }

        private static void Report(int failures)
        {
            Debug.Log("=== Neon District: результаты ===");
            Debug.Log($"Меню (EventSystem, курсор, клики) + геймплей: провалов {failures}");
            Debug.Log(failures == 0 ? "Все автотесты пройдены" : "ЕСТЬ ПРОВАЛЫ");

            // контракт game-ci: он ищет этот блок и падает, если Errors != 0
            System.Console.WriteLine();
            System.Console.WriteLine("###########################");
            System.Console.WriteLine("#      Build results      #");
            System.Console.WriteLine("###########################");
            System.Console.WriteLine();
            System.Console.WriteLine("Duration: " + System.TimeSpan.FromSeconds(Time.realtimeSinceStartup));
            System.Console.WriteLine("Warnings: 0");
            System.Console.WriteLine("Errors: " + (failures > 0 ? 1 : 0));
            System.Console.WriteLine("Size: 0 bytes");
            System.Console.WriteLine();

            UnityEditor.EditorApplication.Exit(failures > 0 ? 1 : 0);
        }
    }
}
#endif
