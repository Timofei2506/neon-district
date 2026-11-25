#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using NeonDistrict.Core;

namespace NeonDistrict.EditorTools
{
    /// <summary>
    /// Точка входа для CI (GitHub Actions). Вызывается так:
    ///   Unity -batchmode -nographics -executeMethod NeonDistrict.EditorTools.CIBuild.BuildWindows
    ///
    /// Сам готовит проект: URP-ассеты, префабы, сцену, настройки — затем собирает билд
    /// и складывает его в Builds/Windows. Код возврата 1 при ошибке, чтобы CI «покраснел».
    /// </summary>
    public static class CIBuild
    {
        private const string ScenePath = "Assets/Scenes/Playground.unity";
        // game-ci передаёт свой путь через -customBuildPath; локально собираем в Builds/Windows.
        private const string OutputDir = "Builds/Windows";

        [MenuItem("Neon District/Build Windows (CI)", priority = 20)]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, OutputDir, "NeonDistrict.exe");

        [MenuItem("Neon District/Build Linux (CI)", priority = 21)]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux", "NeonDistrict.x86_64");

        /// <summary>Вызов из CLI: подготовка проекта без сборки (для автотестов).</summary>
        public static void PrepareOnly()
        {
            PrepareProject();
            Console.WriteLine();
            Console.WriteLine("Build succeeded!");
            EditorApplication.Exit(0);
        }

        private static void Build(BuildTarget target, string outDir, string exeName)
        {
            try
            {
                Debug.Log("=== Neon District CI build: " + target + " ===");
                Debug.Log($"[CI] активный Backend сборки (игрок): {PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone)}");
                PrepareProject();

                // если запуск идёт через game-ci, он передаёт -customBuildPath <...>/NeonDistrict.exe —
                // уважаем его, иначе используем наш Builds/...
                string location = Path.Combine(outDir, exeName);
                var argv = Environment.GetCommandLineArgs();
                for (int i = 0; i < argv.Length - 1; i++)
                    if (argv[i] == "-customBuildPath" && !string.IsNullOrEmpty(argv[i + 1]))
                        location = argv[i + 1];

                string dir = Path.GetDirectoryName(location);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = location,
                    target = target,
                    options = BuildOptions.None,
                };

                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                Debug.Log($"[CI] result={summary.result} size={summary.totalSize / (1024 * 1024)} MB " +
                          $"errors={summary.totalErrors} warnings={summary.totalWarnings} time={summary.totalTime}");

                // Формат ниже — контракт game-ci: он парсит stdout регуляркой
                // /^#\s*Build results\s*#(.*)^Size:/ms и валит билд, если Errors != 0.
                // Без этого блока успешная сборка помечается как «There was an error building the project».
                Console.WriteLine();
                Console.WriteLine("###########################");
                Console.WriteLine("#      Build results      #");
                Console.WriteLine("###########################");
                Console.WriteLine();
                Console.WriteLine("Duration: " + summary.totalTime);
                Console.WriteLine("Warnings: " + summary.totalWarnings);
                Console.WriteLine("Errors: " + summary.totalErrors);
                Console.WriteLine("Size: " + summary.totalSize + " bytes");
                Console.WriteLine();

                if (summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError("[CI] BUILD FAILED");
                    Console.WriteLine("Build failed!");
                    EditorApplication.Exit(101);
                }

                Console.WriteLine("Build succeeded!");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[CI] EXCEPTION: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Полная автоматическая подготовка проекта «с нуля» — то же, что три пункта меню.</summary>
        public static void PrepareProject()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            // 1) URP, материалы города, Player Settings
            ProjectSetup.Run();

            // 0) небо (иначе за городом чёрная заливка) и кинематографичный пост-процесс
            var sky = SkyFactory.EnsureSkybox();
            if (sky != null) RenderSettings.skybox = sky;

            // URP требует, чтобы ambient брался из созданного skybox'а, иначе небо освещает сцену
            // неправильно (объекты выглядят плоскими)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.1f;
            DynamicGI.UpdateEnvironment();

#if !ND_COMPILE_CHECK
            PostFxSetup.EnsureProfile();
#endif

            // 2) префабы (игрок, прохожий, машины, полиция)
            AssetPipeline.BuildAll();

            // 3) сцена
            if (!File.Exists(ScenePath))
            {
                SceneSetup.Build();
            }
            else
            {
                Debug.Log("[CI] сцена уже есть: " + ScenePath);
            }

            // сцена обязательно в билд-листе
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            // 4) качество/графика под сборку
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.neondistrict.studio.game");
            QualitySettings.SetQualityLevel(Mathf.Max(0, QualitySettings.names.Length - 1), false);

            AssetDatabase.SaveAssets();
            Debug.Log("[CI] проект подготовлен: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => s.path)));
        }
    }
}
#endif
