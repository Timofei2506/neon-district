#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NeonDistrict.EditorTools
{
    /// <summary>
    /// Точка входа автотестов в CI.
    ///
    /// Почему так, а не -executeMethod: метод, вызванный через -executeMethod, работает в
    /// edit-mode скрипте, а там нельзя ни войти в Play Mode (DontDestroyOnLoad бросает
    /// исключение), ни дождаться тиков Update/корутин. Поэтому подготовка проекта идёт
    /// через -executeMethod, а тесты запускает этот статический конструктор: он срабатывает
    /// после того, как Unity закончил компиляцию и загрузил сцену первого запуска.
    ///
    /// Порядок в CI:
    ///   1. -executeMethod ...CIBuild.PrepareProject   (URP, префабы, сцена)
    ///   2. -executeMethod ...CIBuild.BuildWindows     НЕ вызывается
    ///   3. маркер-файл .smoke-tests в корне проекта   → здесь: Play Mode → тесты → Exit(0/1)
    ///
    /// Почему файл, а не аргумент командной строки: game-ci прокидывает customParameters через
    /// yargs, и флаг вида -runSmokeTests разбирается как набор коротких опций
    /// («Unknown arguments: r, n, S, k, e, T»). Файл в workspace виден контейнеру всегда.
    /// </summary>
    [InitializeOnLoad]
    public static class SmokeTestRunner
    {
        private const string Flag = "-runSmokeTests";
        private const string MarkerFile = ".smoke-tests";

        static SmokeTestRunner()
        {
            if (!HasFlag()) return;
            // ждём, пока редактор догрузит сцену и ассеты
            EditorApplication.delayCall += Start;
        }

        private static bool HasFlag()
        {
            foreach (var arg in System.Environment.GetCommandLineArgs())
                if (arg == Flag) return true;

            // маркер-файл в корне проекта (кладётся CI-шагом перед запуском Unity)
            var projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
            return System.IO.File.Exists(System.IO.Path.Combine(projectRoot, MarkerFile));
        }

        private static void Start()
        {
            Debug.Log("[Smoke] запуск автотестов (Play Mode)");
            SmokeTests.RunAll();
        }
    }
}
#endif
