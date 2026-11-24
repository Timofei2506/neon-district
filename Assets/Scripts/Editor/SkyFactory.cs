#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NeonDistrict.EditorTools
{
    /// <summary>
    /// Процедурное небо вместо «чёрного ничто»: градиентный skybox (горизонт тёплый, зенит
    /// тёмно-синий) плюс общий градиент окружения. Раньше RenderSettings.skybox был null,
    /// из-за чего за домами и в кадре меню была просто заливка.
    ///
    /// Материал создаётся кодом (шейдер Skybox/Procedural из встроенных) и сохраняется в
    /// Assets/Art/URP/NeonDistrict_Sky.mat — идемпотентно, повторный запуск ничего не ломает.
    /// </summary>
    public static class SkyFactory
    {
        public const string Folder = "Assets/Art/URP";
        public const string Path = Folder + "/NeonDistrict_Sky.mat";

        [MenuItem("Neon District/0) Skybox", priority = 1)]
        public static Material EnsureSkybox()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(Path);
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return existing;

            if (existing == null)
            {
                Directory.CreateDirectory(Folder);
                existing = new Material(shader) { name = "NeonDistrict_Sky" };
                AssetDatabase.CreateAsset(existing, Path);
            }

            // Skybox/Procedural: экспозиция, толщина атмосферы, разброс и цвета зенита/горизонта
            TrySetFloat(existing, "_SunSize", 0.035f);
            TrySetFloat(existing, "_SunSizeConvergence", 3.2f);
            TrySetFloat(existing, "_AtmosphereThickness", 0.92f);
            TrySetFloat(existing, "_Exposure", 1.15f);
            TrySetColor(existing, "_SkyTint", new Color(0.28f, 0.33f, 0.52f));
            TrySetColor(existing, "_GroundColor", new Color(0.12f, 0.11f, 0.14f));

            // градиент окружения: тёплый свет сверху, холодный от земли — «городской» ambient
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }

        private static void TrySetFloat(Material m, string prop, float value)
        {
            if (m.HasProperty(prop)) m.SetFloat(prop, value);
        }

        private static void TrySetColor(Material m, string prop, Color value)
        {
            if (m.HasProperty(prop)) m.SetColor(prop, value);
        }
    }
}
#endif
