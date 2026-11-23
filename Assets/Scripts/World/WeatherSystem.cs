using System.Collections.Generic;
using UnityEngine;
using NeonDistrict.Core;

namespace NeonDistrict.World
{
    /// <summary>
    /// Погода и время суток как единая система: ясное небо, облачность, дождь, гроза.
    /// Дождь — GPU-партиклы, следующие за камерой (не за игроком): капли всегда в кадре,
    /// но не «летят» за игроком по миру. Мокрый асфальт — за счёт усиления отражений:
    /// понижаем шероховатость у материалов земли и поднимаем экспозицию отражений.
    /// </summary>
    [DefaultExecutionOrder(-170)]
    public class WeatherSystem : MonoBehaviour
    {
        public enum Weather { Clear = 0, Overcast = 1, Rain = 2, Storm = 3 }

        [Header("Ссылки")]
        public Light sun;
        public Camera targetCamera;
        public DayNightCycle dayNight;

        [Header("Расписание (игровые часы)")]
        [Tooltip("Через сколько игровых минут может смениться погода")] public float minChangeMinutes = 25f;
        [Tooltip("Максимальная длительность одной погоды в игровых минутах")] public float maxChangeMinutes = 70f;
        public bool autoWeather = true;
        public Weather startWeather = Weather.Clear;

        [Header("Дождь")]
        public int rainParticles = 9000;
        public float rainRadius = 26f;
        public float rainHeight = 22f;
        public float rainSpeed = 26f;

        [Header("Настройки")]
        [Range(0f, 1f)] public float wetness;
        public float transitionSpeed = 0.15f;

        public Weather Current { get; private set; }
        public float Intensity { get; private set; }   // 0 — сухо, 1 — ливень
        public string Label => Current switch
        {
            Weather.Clear => "ЯСНО",
            Weather.Overcast => "ОБЛАЧНО",
            Weather.Rain => "ДОЖДЬ",
            _ => "ГРОЗА",
        };

        public event System.Action<Weather> OnWeatherChanged;

        private float _timer;
        private float _targetIntensity;
        private ParticleSystem _rain;
        private ParticleSystem _splash;
        private float _flashTimer;
        private float _baseSunIntensity = 2f;
        private readonly List<Material> _wetMaterials = new List<Material>();
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");

        private void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (dayNight == null) dayNight = FindFirstObjectByType<DayNightCycle>();
            if (sun != null) _baseSunIntensity = sun.intensity;

            BuildRain();
            SetWeather(startWeather, instant: true);
            _timer = Random.Range(minChangeMinutes, maxChangeMinutes) * 60f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (autoWeather && GameManager.Instance != null && GameManager.Instance.IsPlaying)
            {
                _timer -= dt * 1f; // 1 сек реального времени; масштаб игрового времени задаёт GameManager
                if (_timer <= 0f)
                {
                    Advance();
                    _timer = Random.Range(minChangeMinutes, maxChangeMinutes) * 60f;
                }
            }

            // плавный переход интенсивности
            Intensity = Mathf.MoveTowards(Intensity, _targetIntensity, transitionSpeed * dt);
            wetness = Mathf.MoveTowards(wetness, Current == Weather.Clear ? 0f : Mathf.Lerp(0.35f, 1f, Intensity), transitionSpeed * dt);

            ApplyRainToCamera();
            ApplyAtmosphere();
            ApplyWetLook();
            Thunder(dt);
        }

        // ---------------------------------------------------------------- управление
        public void SetWeather(Weather w, bool instant = false)
        {
            Current = w;
            _targetIntensity = w switch
            {
                Weather.Clear => 0f,
                Weather.Overcast => 0.12f,
                Weather.Rain => 0.65f,
                _ => 1f,
            };
            if (instant) Intensity = _targetIntensity;
            if (_rain != null)
            {
                var em = _rain.emission;
                em.rateOverTime = Mathf.Lerp(0f, 4200f, Intensity);
            }
            OnWeatherChanged?.Invoke(w);
        }

        private void Advance()
        {
            // простая цепочка погоды с шансами
            float r = Random.value;
            Weather next = Current switch
            {
                Weather.Clear => r < 0.55f ? Weather.Overcast : Weather.Clear,
                Weather.Overcast => r < 0.45f ? Weather.Rain : (r < 0.75f ? Weather.Clear : Weather.Overcast),
                Weather.Rain => r < 0.35f ? Weather.Storm : (r < 0.8f ? Weather.Overcast : Weather.Rain),
                _ => r < 0.6f ? Weather.Rain : Weather.Overcast,
            };
            SetWeather(next);
        }

        // ---------------------------------------------------------------- дождь
        private void BuildRain()
        {
            if (_rain != null) return;

            var go = new GameObject("RainFX");
            go.transform.SetParent(transform, false);
            _rain = go.AddComponent<ParticleSystem>();
            var main = _rain.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(rainSpeed * 0.85f, rainSpeed * 1.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new Color(0.75f, 0.82f, 0.92f, 0.55f);
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = rainParticles;

            var emission = _rain.emission;
            emission.rateOverTime = 0f;

            var shape = _rain.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = rainRadius;
            shape.radiusThickness = 1f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 9f;
            renderer.velocityScale = 0.06f;
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(0.8f, 0.86f, 0.95f, 0.5f);
            renderer.sharedMaterial = mat;

            // камера-трафарет: дождь рендерим в мировых координатах камеры
            main.prewarm = true;
            _rain.Play();

            // брызги на земле
            var splashGo = new GameObject("RainSplashes");
            splashGo.transform.SetParent(transform, false);
            _splash = splashGo.AddComponent<ParticleSystem>();
            var smain = _splash.main;
            smain.loop = true;
            smain.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            smain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            smain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            smain.startColor = new Color(0.8f, 0.86f, 0.95f, 0.35f);
            smain.gravityModifier = 0.6f;
            smain.simulationSpace = ParticleSystemSimulationSpace.World;
            var sem = _splash.emission; sem.rateOverTime = 0f;
            var sshape = _splash.shape;
            sshape.shapeType = ParticleSystemShapeType.Circle;
            sshape.radius = rainRadius * 0.9f;
            var srend = splashGo.GetComponent<ParticleSystemRenderer>();
            srend.sharedMaterial = mat;
            _splash.Play();
        }

        private void ApplyRainToCamera()
        {
            if (_rain == null) return;
            var cam = targetCamera != null ? targetCamera.transform : null;
            if (cam == null) return;

            // система висит над камерой — капли не «обгоняют» игрока в кадре
            _rain.transform.position = cam.position + Vector3.up * rainHeight * 0.5f + cam.forward * 6f;
            _splash.transform.position = cam.position + Vector3.up * 0.15f;

            var em = _rain.emission;
            em.rateOverTime = Mathf.Lerp(0f, 4200f, Intensity);
            var sem = _splash.emission;
            sem.rateOverTime = Mathf.Lerp(0f, 900f, Intensity);
        }

        private void ApplyAtmosphere()
        {
            float heavy = Mathf.Clamp01(Intensity);
            // туман: дождь заметно поднимает плотность и гасит дальность
            float baseDensity = RenderSettings.fogDensity;
            RenderSettings.fogDensity = Mathf.Lerp(baseDensity, 0.0075f, heavy);

            if (sun != null && dayNight != null)
            {
                // грозовые тучи гасят прямой свет (не перебивая сам день/ночь)
                float dim = Mathf.Lerp(1f, 0.42f, heavy);
                sun.intensity = Mathf.Min(sun.intensity, _baseSunIntensity * dim);
            }
        }

        /// <summary>Собираем материалы земли и асфальта — им поднимаем «мокрость».</summary>
        public void RegisterWetMaterial(Material m)
        {
            if (m != null && !_wetMaterials.Contains(m)) _wetMaterials.Add(m);
        }

        private void ApplyWetLook()
        {
            if (_wetMaterials.Count == 0) return;
            foreach (var m in _wetMaterials)
            {
                if (m == null) continue;
                if (m.HasProperty(SmoothnessId))
                    m.SetFloat(SmoothnessId, Mathf.Lerp(0.25f, 0.92f, wetness));
                if (m.HasProperty(GlossinessId))
                    m.SetFloat(GlossinessId, Mathf.Lerp(0.25f, 0.92f, wetness));
            }
        }

        // ---------------------------------------------------------------- гроза
        private void Thunder(float dt)
        {
            if (Current != Weather.Storm) return;
            _flashTimer -= dt;
            if (_flashTimer > 0f) return;
            _flashTimer = Random.Range(4f, 11f);

            // короткая вспышка: мгновенно поднимаем ambient и гасим за 0.25 с
            var c = RenderSettings.ambientLight;
            RenderSettings.ambientLight = new Color(0.85f, 0.88f, 1f);
            StartCoroutine(FadeAmbient(c, 0.25f));
        }

        private System.Collections.IEnumerator FadeAmbient(Color target, float duration)
        {
            float t = 0f;
            var start = RenderSettings.ambientLight;
            while (t < duration)
            {
                t += Time.deltaTime;
                RenderSettings.ambientLight = Color.Lerp(start, target, t / duration);
                yield return null;
            }
            RenderSettings.ambientLight = target;
        }
    }
}
