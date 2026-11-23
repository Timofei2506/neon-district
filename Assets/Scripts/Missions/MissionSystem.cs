using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NeonDistrict.Core;
using NeonDistrict.Player;
using NeonDistrict.Vehicles;
using NeonDistrict.World;

namespace NeonDistrict.Missions
{
    /// <summary>
    /// Менеджер миссий: запускает последовательность шагов, следит за условиями,
    /// спавнит маркер в мире, проговаривает реплики по радио и платит гонорар.
    ///
    /// Полностью рантаймовый: ни одного ассета не требуется, всё собирается кодом
    /// (маркер — процедурная геометрия, звук — синтез, текст — UGUI).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class MissionSystem : MonoBehaviour
    {
        [Header("Ссылки")]
        public GameObject player;
        public CityController city;
        public Transform markerParent;

        [Header("Темп")]
        [Tooltip("Пауза между шагами, с")] public float stepPause = 1.2f;
        [Tooltip("Можно ли запускать миссии автоматически по цепочке")] public bool autoChain = false;

        public event Action<MissionDefinition> OnMissionStarted;
        public event Action<MissionDefinition> OnMissionCompleted;
        public event Action<MissionDefinition> OnMissionFailed;
        public event Action<MissionStep, int> OnStepStarted;
        public event Action<string, string> OnRadio;          // (speaker, text)
        public event Action<string> OnObjectiveChanged;       // текст цели для HUD

        public MissionDefinition Current { get; private set; }
        public MissionStep CurrentStep => Current != null && _stepIndex >= 0 && _stepIndex < Current.steps.Length
            ? Current.steps[_stepIndex] : null;
        public int StepIndex => _stepIndex;
        public int StepCount => Current != null ? Current.steps.Length : 0;
        public float Remaining => _timeLeft;
        public Vector3 TargetPoint => _target;
        public bool HasTarget => Current != null && CurrentStep != null && CurrentStep.kind != StepKind.Survive;
        public bool IsRunning { get; private set; }
        public int CompletedCount { get; private set; }

        private readonly List<MissionDefinition> _catalog = new List<MissionDefinition>();
        private int _stepIndex = -1;
        private float _timeLeft;
        private float _stepTimer;
        private float _elapsed;
        private Vector3 _target;
        private GameObject _marker;
        private AudioSource _radioVoice;
        private RadioSynth _synth;

        private void Awake()
        {
            _catalog.AddRange(MissionCatalog.Build());
            _synth = new RadioSynth();
        }

        private void Start()
        {
            if (player == null && GameManager.Instance != null && GameManager.Instance.player != null)
                player = GameManager.Instance.player.gameObject;
            if (city == null) city = FindFirstObjectByType<CityController>();
            BuildMarker();
        }

        public IReadOnlyList<MissionDefinition> Catalog => _catalog;

        // ---------------------------------------------------------------- запуск
        /// <summary>Старт миссии по индексу в каталоге (0 — первая).</summary>
        public void StartMission(int index)
        {
            if (IsRunning) return;
            if (index < 0 || index >= _catalog.Count) return;
            StartMission(_catalog[index]);
        }

        public void StartMission(MissionDefinition mission)
        {
            if (mission == null || mission.steps == null || mission.steps.Length == 0) return;
            if (IsRunning) return;

            Current = mission;
            IsRunning = true;
            _stepIndex = -1;
            _elapsed = 0f;
            OnMissionStarted?.Invoke(mission);
            Radio(mission.contact, mission.brief);
            NextStep();
        }

        /// <summary>Прервать миссию (например, игрок проиграл или загрузил сейв).</summary>
        public void Abort(string reason = null)
        {
            if (!IsRunning) return;
            IsRunning = false;
            _stepIndex = -1;
            HideMarker();
            if (!string.IsNullOrEmpty(reason)) Radio("СИСТЕМА", reason);
            var failed = Current;
            Current = null;
            OnObjectiveChanged?.Invoke("");
            OnMissionFailed?.Invoke(failed);
        }

        // ---------------------------------------------------------------- шаги
        private void NextStep()
        {
            _stepIndex++;
            if (Current == null) return;

            if (_stepIndex >= Current.steps.Length)
            {
                Complete();
                return;
            }

            var step = Current.steps[_stepIndex];
            _stepTimer = 0f;
            _timeLeft = step.timeLimit > 0f ? step.timeLimit : 0f;

            // точка цели: либо задана, либо ищем на дороге около игрока
            if (step.kind == StepKind.Survive)
            {
                _target = player != null ? player.transform.position : Vector3.zero;
            }
            else if (step.resolveOnRoad)
            {
                Vector3 near = player != null ? player.transform.position : Vector3.zero;
                _target = city != null
                    ? city.RandomSpawnPoint(near, step.spawnDistance * 0.55f, step.spawnDistance)
                    : near + new Vector3(step.spawnDistance, 0f, 0f);
                _target.y = 0.1f;
            }
            else
            {
                _target = step.point;
            }

            ShowMarker(_target, step.kind == StepKind.Survive ? 0f : Mathf.Max(8f, step.radius));
            OnStepStarted?.Invoke(step, _stepIndex);
            OnObjectiveChanged?.Invoke(step.objective);
            if (!string.IsNullOrEmpty(step.radioLine)) Radio(step.speaker, step.radioLine);
        }

        private void Update()
        {
            if (!IsRunning || Current == null) return;
            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return;

            var step = CurrentStep;
            if (step == null) return;

            float dt = Time.deltaTime;
            _stepTimer += dt;
            _elapsed += dt;
            if (_timeLeft > 0f)
            {
                _timeLeft -= dt;
                if (_timeLeft <= 0f) { Fail("Время вышло."); return; }
            }

            switch (step.kind)
            {
                case StepKind.GoTo: CheckReached(step, false); break;
                case StepKind.TakeVehicle: CheckTakeVehicle(step); break;
                case StepKind.Deliver: CheckReached(step, true); break;
                case StepKind.Checkpoint: CheckReached(step, true); break;
                case StepKind.Survive: CheckSurvive(step); break;
            }
        }

        private bool PlayerNear(float radius)
        {
            if (player == null) return false;
            var flat = player.transform.position; flat.y = 0f;
            var t = _target; t.y = 0f;
            return Vector3.Distance(flat, t) <= radius;
        }

        private void CheckReached(MissionStep step, bool requireVehicle)
        {
            if (!PlayerNear(Mathf.Max(6f, step.radius))) return;

            var pc = player.GetComponent<PlayerController>();
            if (requireVehicle)
            {
                if (pc == null || !pc.IsInVehicle) return;
                if (pc.currentVehicle != null && pc.currentVehicle.SpeedKmh > step.deliverSpeedKmh)
                {
                    OnObjectiveChanged?.Invoke(step.objective + "  ·  остановись!");
                    return;
                }
            }
            CompleteStep();
        }

        private void CheckTakeVehicle(MissionStep step)
        {
            var pc = player != null ? player.GetComponent<PlayerController>() : null;
            if (pc == null || !pc.IsInVehicle) return;
            // машина должна быть рядом с точкой (не обязательно ровно в ней)
            if (!PlayerNear(step.radius * 3f)) return;
            CompleteStep();
        }

        private void CheckSurvive(MissionStep step)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // нужен розыск не ниже заданного — иначе полиция «потерялась»
            if (gm.Wanted < step.wanted) gm.AddWanted(step.wanted - gm.Wanted);

            _target = player != null ? player.transform.position : _target;
            if (_stepTimer >= step.duration) CompleteStep();
        }

        private void CompleteStep()
        {
            HideMarker();
            StartCoroutine(NextStepDelayed());
        }

        private IEnumerator NextStepDelayed()
        {
            yield return new WaitForSeconds(stepPause);
            if (IsRunning) NextStep();
        }

        private void Complete()
        {
            var mission = Current;
            IsRunning = false;
            Current = null;
            HideMarker();
            OnObjectiveChanged?.Invoke("");

            if (mission != null)
            {
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    gm.AddMoney(mission.reward);
                    if (mission.clearWantedOnComplete) gm.ClearWanted();
                }
                CompletedCount++;
                Radio(mission.contact, "Готово. " + mission.reward + " на твоём счёте.");
                OnMissionCompleted?.Invoke(mission);
            }
        }

        private void Fail(string reason)
        {
            var mission = Current;
            IsRunning = false;
            Current = null;
            HideMarker();
            OnObjectiveChanged?.Invoke("");
            Radio("СИСТЕМА", reason);
            OnMissionFailed?.Invoke(mission);
        }

        // ---------------------------------------------------------------- радио
        /// <summary>Реплика «по рации»: субтитры + короткий синтезированный писк.</summary>
        public void Radio(string speaker, string text)
        {
            OnRadio?.Invoke(speaker, text);
            if (_radioVoice != null && _synth != null)
                _radioVoice.PlayOneShot(_synth.Squelch());
        }

        // ---------------------------------------------------------------- маркер
        private void BuildMarker()
        {
            if (_marker != null) return;
            _marker = new GameObject("MissionMarker");
            if (markerParent != null) _marker.transform.SetParent(markerParent, false);

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(1f, 0.18f, 0.47f, 0.55f);

            // столб света
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar";
            pillar.transform.SetParent(_marker.transform, false);
            pillar.transform.localScale = new Vector3(2.2f, 14f, 2.2f);
            pillar.transform.localPosition = new Vector3(0f, 14f, 0f);
            var pr = pillar.GetComponent<Renderer>();
            pr.sharedMaterial = mat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(pillar.GetComponent<Collider>());

            // круг на земле
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            disc.transform.SetParent(_marker.transform, false);
            disc.transform.localScale = new Vector3(1f, 0.02f, 1f);
            disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var dr = disc.GetComponent<Renderer>();
            dr.sharedMaterial = mat;
            dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(disc.GetComponent<Collider>());

            _marker.AddComponent<MissionMarkerSpin>();
            _marker.SetActive(false);

            _radioVoice = gameObject.AddComponent<AudioSource>();
            _radioVoice.playOnAwake = false;
            _radioVoice.spatialBlend = 0f;
            _radioVoice.volume = 0.35f;
        }

        private void ShowMarker(Vector3 pos, float radius)
        {
            if (_marker == null) BuildMarker();
            if (_marker == null) return;
            _marker.transform.position = new Vector3(pos.x, 0.02f, pos.z);
            // радиус рисуем масштабом диска (единичный цилиндр Unity = 1 м диаметра)
            var disc = _marker.transform.Find("Disc");
            if (disc != null)
            {
                float d = radius > 0f ? radius * 2f : 16f;
                disc.localScale = new Vector3(d, 0.02f, d);
            }
            _marker.SetActive(true);
        }

        private void HideMarker() { if (_marker != null) _marker.SetActive(false); }

        // ---------------------------------------------------------------- сохранение
        public string SaveJson()
        {
            var s = new MissionSave
            {
                missionId = Current != null ? Current.id : "",
                step = _stepIndex,
                completed = CompletedCount,
                timeLeft = _timeLeft,
            };
            return JsonUtility.ToJson(s);
        }

        [Serializable]
        private class MissionSave
        {
            public string missionId;
            public int step;
            public int completed;
            public float timeLeft;
        }
    }

    /// <summary>Вращение и пульсация маркера: дёшево и читаемо в кадре.</summary>
    public class MissionMarkerSpin : MonoBehaviour
    {
        private float _t;
        private Vector3 _baseScale = Vector3.one;

        private void Start()
        {
            var disc = transform.Find("Disc");
            if (disc != null) _baseScale = disc.localScale;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            transform.Rotate(0f, 30f * Time.deltaTime, 0f, Space.World);
            var disc = transform.Find("Disc");
            if (disc != null)
            {
                float pulse = 1f + Mathf.Sin(_t * 3f) * 0.04f;
                disc.localScale = new Vector3(_baseScale.x * pulse, _baseScale.y, _baseScale.z * pulse);
            }
            var pillar = transform.Find("Pillar");
            if (pillar != null)
            {
                var r = pillar.GetComponent<Renderer>();
                if (r != null && r.sharedMaterial != null)
                {
                    var c = r.sharedMaterial.color;
                    c.a = 0.35f + Mathf.Sin(_t * 2.4f) * 0.12f;
                    r.sharedMaterial.color = c;
                }
            }
        }
    }

    /// <summary>Крошечный синтезатор служебных звуков рации (без аудиоассетов).</summary>
    public class RadioSynth
    {
        private const int SampleRate = 44100;
        private AudioClip _squelch;

        public AudioClip Squelch()
        {
            if (_squelch != null) return _squelch;
            int n = Mathf.RoundToInt(SampleRate * 0.16f);
            var data = new float[n];
            var rnd = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 26f);
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0) * 0.35f;
                float tone = Mathf.Sin(2f * Mathf.PI * 1180f * t) * 0.5f;
                data[i] = (noise + tone * Mathf.Exp(-t * 40f)) * env;
            }
            _squelch = AudioClip.Create("radio_squelch", n, 1, SampleRate, false);
            _squelch.SetData(data, 0);
            return _squelch;
        }
    }
}
