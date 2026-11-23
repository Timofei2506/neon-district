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

        /// <summary>
        /// URP 17 (Unity 6) перевёл конструктор <see cref="ClampedFloatParameter"/> на
        /// (value, min, max, overrideState) — вариант (value, overrideState) больше не компилируется.
        /// Диапазоны ниже — те же, что URP показывает в инспекторе по умолчанию.
        /// </summary>
        private static ClampedFloatParameter Ranged(float value, float min, float max)
        {
            return new ClampedFloatParameter(value, min, max, true);
        }

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
                bloom.threshold = Ranged(0.92f, 0f, 10f);
                bloom.intensity = Ranged(0.85f, 0f, 10f);
                bloom.scatter = Ranged(0.72f, 0f, 1f);
                bloom.tint = new ColorParameter(new Color(1f, 0.92f, 0.98f), true);
                bloom.highQualityFiltering = new BoolParameter(true, true);
            }

            // ---- виньетка: плотнее по краям, как в трейлерах
            var vig = profile.Add<Vignette>(true);
            if (vig != null)
            {
                vig.intensity = Ranged(0.26f, 0f, 1f);
                vig.smoothness = Ranged(0.42f, 0.01f, 1f);
                vig.color = new ColorParameter(new Color(0.01f, 0.02f, 0.04f), true);
            }

            // ---- грейдинг: чуть контрастнее, чуть теплее в светах
            var grade = profile.Add<ColorAdjustments>(true);
            if (grade != null)
            {
                grade.postExposure = Ranged(0.15f, -5f, 5f);
                grade.contrast = Ranged(12f, -100f, 100f);
                grade.saturation = Ranged(8f, -100f, 100f);
                grade.hueShift = Ranged(-2f, -180f, 180f);
            }

            var wb = profile.Add<WhiteBalance>(true);
            if (wb != null)
            {
                wb.temperature = Ranged(6f, -100f, 100f);
                wb.tint = Ranged(2f, -100f, 100f);
            }

            // ---- плёночное зерно и лёгкая аберрация: «дорогая» картинка
            var grain = profile.Add<FilmGrain>(true);
            if (grain != null)
            {
                grain.type = new FilmGrainLookupParameter(FilmGrainLookup.Thin2, true);
                grain.intensity = Ranged(0.18f, 0f, 1f);
                grain.response = Ranged(0.8f, 0f, 1f);
            }

            var ca = profile.Add<ChromaticAberration>(true);
            if (ca != null) ca.intensity = Ranged(0.09f, 0f, 1f);

            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Neon District] Пост-обработка создана: " + ProfilePath);
            return profile;
        }
    }
}
#endif
