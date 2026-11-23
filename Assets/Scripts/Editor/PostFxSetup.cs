#if UNITY_EDITOR && !ND_COMPILE_CHECK
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NeonDistrict.EditorTools
{
    /// <summary>
    /// Кинематографичный пост-обработки профиль URP: ACES-тонмаппинг, bloom для неона,
    /// виньетка, цветокоррекция, зерно, хроматическая аберрация, баланс белого.
    ///
    /// Без этого в сцене не было ни одного Volume — то есть экспозиция, bloom и грейдинг
    /// оставались «из коробки». Теперь профиль создаётся кодом (идемпотентно) и
    /// назначается на глобальный Volume в сцене; DayNightCycle дальше правит
    /// bloom/vignette/экспозицию от времени суток.
    /// </summary>
    public static class PostFxSetup
    {
        public const string Folder = "Assets/Art/URP";
        public const string ProfilePath = Folder + "/NeonDistrict_PostFX.asset";

        [MenuItem("Neon District/0) Post-processing profile", priority = 0)]
        public static VolumeProfile EnsureProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Folder);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "NeonDistrict_PostFX";

            // ---- тонмаппинг: ACES даёт «киношную» кривую и мягкие блики
            var tone = profile.Add<Tonemapping>(true);
            if (tone != null) tone.mode = new TonemappingModeParameter(TonemappingMode.ACES, true);

            // ---- bloom: неоновые вывески и фары на ночной улице
            var bloom = profile.Add<Bloom>(true);
            if (bloom != null)
            {
                bloom.threshold = new ClampedFloatParameter(0.92f, true);
                bloom.intensity = new ClampedFloatParameter(0.85f, true);
                bloom.scatter = new ClampedFloatParameter(0.72f, true);
                bloom.tint = new ColorParameter(new Color(1f, 0.92f, 0.98f), true);
                bloom.highQualityFiltering = new BoolParameter(true, true);
            }

            // ---- виньетка: плотнее по краям, как в трейлерах
            var vig = profile.Add<Vignette>(true);
            if (vig != null)
            {
                vig.intensity = new ClampedFloatParameter(0.26f, true);
                vig.smoothness = new ClampedFloatParameter(0.42f, true);
                vig.color = new ColorParameter(new Color(0.01f, 0.02f, 0.04f), true);
            }

            // ---- грейдинг: чуть контрастнее, чуть теплее в светах
            var grade = profile.Add<ColorAdjustments>(true);
            if (grade != null)
            {
                grade.postExposure = new ClampedFloatParameter(0.15f, true);
                grade.contrast = new ClampedFloatParameter(12f, true);
                grade.saturation = new ClampedFloatParameter(8f, true);
                grade.hueShift = new ClampedFloatParameter(-2f, true);
            }

            var wb = profile.Add<WhiteBalance>(true);
            if (wb != null)
            {
                wb.temperature = new ClampedFloatParameter(6f, true);
                wb.tint = new ClampedFloatParameter(2f, true);
            }

            // ---- плёночное зерно и лёгкая аберрация: «дорогая» картинка
            var grain = profile.Add<FilmGrain>(true);
            if (grain != null)
            {
                grain.type = new FilmGrainLookupParameter(FilmGrainLookup.Thin2, true);
                grain.intensity = new ClampedFloatParameter(0.18f, true);
                grain.response = new ClampedFloatParameter(0.8f, true);
            }

            var ca = profile.Add<ChromaticAberration>(true);
            if (ca != null) ca.intensity = new ClampedFloatParameter(0.09f, true);

            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Neon District] Пост-обработка создана: " + ProfilePath);
            return profile;
        }
    }
}
#endif
