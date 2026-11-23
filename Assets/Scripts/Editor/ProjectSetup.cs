#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#if !ND_COMPILE_CHECK
using UnityEngine.Rendering.Universal;
#endif

namespace NeonDistrict.EditorTools
{
    /// <summary>
    /// Одноразовая настройка проекта (меню Neon District → 1) Setup Project):
    ///  • импорт Манхэттена с материалами из FBX-описаний;
    ///  • создание URP Asset/Renderer с включённым HDR, SSAO, SSAO-подобным AO, тенями;
    ///  • назначение URP в Graphics/Quality;
    ///  • параметры сборки под Windows x64 (IL2CPP).
    ///
    /// Всё идемпотентно: можно запускать сколько угодно раз.
    /// </summary>
    public static class ProjectSetup
    {
        private const string UrpFolder = "Assets/Art/URP";
        private const string UrpAssetPath = UrpFolder + "/NeonDistrict_URP.asset";
        private const string UrpRendererPath = UrpFolder + "/NeonDistrict_Renderer.asset";
        private const string ManhattanFbx = "Assets/Art/Manhattan/Manhattan.fbx";

        [MenuItem("Neon District/1) Setup Project", priority = 1)]
        public static void Run()
        {
            Directory.CreateDirectory(UrpFolder);
            Directory.CreateDirectory("Assets/Scenes");
            ImportManhattan();
            #if !ND_COMPILE_CHECK
            var asset = EnsureUrpAsset();
            AssignUrp(asset);
            #endif
            ConfigureQuality();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<b>[Neon District]</b> Проект настроен. Дальше: Neon District → 2) Build Prefabs, затем 3) Build Playground Scene.");
        }

        // ------------------------------------------------------------ Manhattan
        private static void ImportManhattan()
        {
            var importer = AssetImporter.GetAtPath(ManhattanFbx) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Neon District] Не найден {ManhattanFbx}");
                return;
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.isReadable = false;
            importer.generateSecondaryUV = false;
            importer.addCollider = false;
            importer.SaveAndReimport();

            // текстуры: включаем mipmaps + анизотропию, чтобы город не «мылило» вдали
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Manhattan/Textures" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                if (ti.maxTextureSize < 2048) ti.maxTextureSize = 2048;
                ti.anisoLevel = 8;
                ti.mipmapEnabled = true;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            // ------------------------------------------------------------------
            // ВАЖНО: модель Манхэттена — 260k треугольников одним куском.
            // Фрустум- и окклюжн-каллинг по ней не работают (один меш = один объект),
            // поэтому в кадре рисуется весь город целиком. Режем меш на чанки по сетке
            // 128 м: Unity получает возможность каллинга, static batching по-прежнему
            // работает, draw call'ы остаются в разумных пределах.
            // ------------------------------------------------------------------
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ManhattanFbx);
            if (model != null)
            {
                int chunks = 0;
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null || mf.sharedMesh.triangles.Length < 30000) continue;
                    var parent = mf.transform.parent != null ? mf.transform.parent : mf.transform;
                    int made = MeshChunker.Split(mf.gameObject, 1600f);
                    chunks += made;
                    Debug.Log($"[Neon District] чанки для {mf.name}: {made} шт (родитель {parent.name})");
                }
                if (chunks > 0) Debug.Log($"[Neon District] Манхэттен разбит на {chunks} чанков для каллинга.");
            }

            Debug.Log("[Neon District] Манхэттен импортирован (материалы + текстуры).");
        }

        // ------------------------------------------------------------ URP
#if !ND_COMPILE_CHECK
        private static UniversalRenderPipelineAsset EnsureUrpAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (asset != null) return asset;

            // рендерер
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name = "NeonDistrict_Renderer";
            AssetDatabase.CreateAsset(rendererData, UrpRendererPath);

            asset = UniversalRenderPipelineAsset.Create(rendererData);
            asset.name = "NeonDistrict_URP";
            AssetDatabase.CreateAsset(asset, UrpAssetPath);

            // настройки качества рендера
            asset.msaaSampleCount = 4;
            asset.renderScale = 1.0f;
            asset.supportsHDR = true;
            TrySetEnum(asset, "hdrColorBufferPrecision", "FP16_precision", "_64Bits"); // имя enum менялось между версиями URP
            asset.shadowDistance = 260f;
            asset.shadowCascadeCount = 4;
            asset.mainLightShadowmapResolution = 4096;
            asset.additionalLightsShadowmapResolution = 2048;
            // supportsSoftShadows / supportsMainLightShadows / supportsAdditionalLightShadows
            // в URP 17 доступны только для чтения (переехали в UniversalRendererData) — не трогаем.
            asset.shadowDepthBias = 1.0f;
            asset.shadowNormalBias = 1.0f;
            asset.supportsCameraDepthTexture = true;
            asset.supportsCameraOpaqueTexture = true;
            asset.maxAdditionalLightsCount = 4;
            

            // AO (SSAO) — в URP 17 рендер-фича живёт на рендерере
            var aoType = System.Type.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime");
            if (aoType != null)
            {
                var feature = ScriptableObject.CreateInstance(aoType);
                feature.name = "SSAO";
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                var field = rendererData.GetType().GetField("m_RendererFeatures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var list = field.GetValue(rendererData) as List<ScriptableRendererFeature>;
                    if (list != null)
                    {
                        list.Add(feature as ScriptableRendererFeature);
                        field.SetValue(rendererData, list);
                    }
                }
                EditorUtility.SetDirty(rendererData);
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[Neon District] URP Asset создан: " + UrpAssetPath);
            return asset;
        }

#endif

        /// <summary>
        /// Ставит enum-свойство по имени значения, молча пропуская его, если в этой версии URP
        /// свойство/значение называется иначе (ассеты URP переименовывали их между 14 и 17).
        /// Нужно, чтобы редакторский сетап не ломал компиляцию на апгрейде пакета.
        /// </summary>
        private static void TrySetEnum(object target, string property, params string[] candidateValues)
        {
            if (target == null) return;
            var prop = target.GetType().GetProperty(property,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop == null || !prop.CanWrite || !prop.PropertyType.IsEnum) return;

            foreach (var candidate in candidateValues)
            {
                try
                {
                    prop.SetValue(target, System.Enum.Parse(prop.PropertyType, candidate));
                    EditorUtility.SetDirty(target as UnityEngine.Object);
                    return;
                }
                catch (System.ArgumentException) { /* нет такого значения — пробуем следующее */ }
            }
        }

#if !ND_COMPILE_CHECK
        private static void AssignUrp(UniversalRenderPipelineAsset asset)
        {
            if (asset == null) return;
            GraphicsSettings.defaultRenderPipeline = asset;
            int levels = QualitySettings.names.Length;
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            Debug.Log("[Neon District] URP назначен в Graphics и Quality.");
        }
#endif

        private static void ConfigureQuality()
        {
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, false);
            QualitySettings.shadowDistance = 260f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
            QualitySettings.antiAliasing = 4;
            QualitySettings.vSyncCount = 1;
            QualitySettings.lodBias = 2f;
            QualitySettings.skinWeights = SkinWeights.FourBones;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Neon District Studio";
            PlayerSettings.productName = "Neon District";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;
            // IL2CPP — финальный релизный бэкенд. Для CI-сборки на game-ci ставится Mono2x
            // (CIBuild.PrepareProject), т.к. в образе редактора нет IL2CPP-модуля под Windows.
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[]
            {
                UnityEngine.Rendering.GraphicsDeviceType.Direct3D12,
                UnityEngine.Rendering.GraphicsDeviceType.Direct3D11
            });
            Debug.Log("[Neon District] Player Settings: Windows x64, IL2CPP, D3D12, Linear, Input System.");
        }
    }
}
#endif
