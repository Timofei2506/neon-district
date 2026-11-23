using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NeonDistrict.Core;

namespace NeonDistrict.World
{
    /// <summary>
    /// Сутки в URP: солнце/луна, цвет и плотность тумана, окружение, включение
    /// уличных огней и неона, лёгкая коррекция пост-обработки (bloom/vignette).
    /// </summary>
    [ExecuteAlways]
    public class DayNightCycle : MonoBehaviour
    {
        [Header("Ссылки")]
        public Light sun;
        public Light moon;
        public Volume postProcessVolume;
        public Light[] streetLights;
        public Renderer[] neonRenderers;

        [Header("Ключевые кадры")]
        public Gradient sunColor = DefaultSun();
        public Gradient ambientColor = DefaultAmbient();
        public Gradient fogColor = DefaultFog();
        public AnimationCurve fogDensity = AnimationCurve.EaseInOut(0f, 0.0045f, 12f, 0.0012f);

        [Header("Настройки")]
        public bool useGameTime = true;
        [Range(0f, 24f)] public float previewHour = 8.5f;
        [ColorUsage(false, true)] public Color neonNight = new Color(2.4f, 0.7f, 1.8f, 1f);

        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissionColor");
        private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

        private float Hour => useGameTime && GameManager.Instance != null ? GameManager.Instance.TimeOfDay : previewHour;

        private void Update() => Apply(Hour);

        public void Apply(float hour)
        {
            float t = Mathf.Repeat(hour / 24f, 1f);
            float sunAngle = (hour - 6f) / 24f * 360f;      // 6:00 — восход
            bool day = hour > 5.6f && hour < 19.4f;
            float dayFactor = Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01((hour - 6f) / 12f) * Mathf.PI));

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(sunAngle, 28f, 0f);
                sun.color = sunColor.Evaluate(t);
                sun.intensity = Mathf.Lerp(0.02f, 2.2f, dayFactor);
                sun.enabled = day;
            }
            if (moon != null)
            {
                moon.transform.rotation = Quaternion.Euler(sunAngle + 180f, -28f, 0f);
                moon.intensity = day ? 0f : 0.3f;
                moon.enabled = !day;
            }

            RenderSettings.ambientLight = ambientColor.Evaluate(t);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor.Evaluate(t);
            RenderSettings.fogDensity = fogDensity.Evaluate(hour);

            float night = day ? 0f : Mathf.Clamp01(hour < 6f ? (6f - hour) / 2.5f : (hour - 19.4f) / 2.5f);

            if (streetLights != null)
                foreach (var l in streetLights)
                    if (l != null) l.intensity = Mathf.Lerp(0.02f, 6f, night);

            if (neonRenderers != null && neonRenderers.Length > 0)
            {
                foreach (var r in neonRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(EmissiveColorId, neonNight * Mathf.Lerp(0.1f, 1f, night));
                    r.SetPropertyBlock(_mpb);
                }
            }

            if (postProcessVolume != null && postProcessVolume.profile != null)
            {
                if (postProcessVolume.profile.TryGet(out Bloom bloom))
                    bloom.intensity.value = Mathf.Lerp(0.5f, 1.25f, night);
                if (postProcessVolume.profile.TryGet(out Vignette vig))
                    vig.intensity.value = Mathf.Lerp(0.2f, 0.33f, night);
                if (postProcessVolume.profile.TryGet(out ColorAdjustments grade))
                    grade.postExposure.value = Mathf.Lerp(0.05f, 0.5f, night);
            }
        }

        private static Gradient DefaultSun()
        {
            var g = new Gradient();
            g.SetKeys(new[]
            {
                new GradientColorKey(new Color(1f, 0.55f, 0.3f), 0.22f),
                new GradientColorKey(new Color(1f, 0.93f, 0.84f), 0.36f),
                new GradientColorKey(new Color(1f, 1f, 0.98f), 0.52f),
                new GradientColorKey(new Color(1f, 0.76f, 0.5f), 0.75f),
                new GradientColorKey(new Color(0.5f, 0.6f, 1f), 0.85f),
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        private static Gradient DefaultAmbient()
        {
            var g = new Gradient();
            g.SetKeys(new[]
            {
                new GradientColorKey(new Color(0.09f, 0.12f, 0.2f), 0.0f),
                new GradientColorKey(new Color(0.4f, 0.38f, 0.45f), 0.25f),
                new GradientColorKey(new Color(0.55f, 0.6f, 0.7f), 0.5f),
                new GradientColorKey(new Color(0.5f, 0.42f, 0.4f), 0.75f),
                new GradientColorKey(new Color(0.09f, 0.11f, 0.2f), 1f),
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        private static Gradient DefaultFog()
        {
            var g = new Gradient();
            g.SetKeys(new[]
            {
                new GradientColorKey(new Color(0.05f, 0.07f, 0.12f), 0.0f),
                new GradientColorKey(new Color(0.5f, 0.42f, 0.45f), 0.25f),
                new GradientColorKey(new Color(0.6f, 0.68f, 0.78f), 0.5f),
                new GradientColorKey(new Color(0.62f, 0.5f, 0.42f), 0.75f),
                new GradientColorKey(new Color(0.05f, 0.07f, 0.13f), 1f),
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
