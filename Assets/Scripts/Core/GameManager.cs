using System;
using System.Collections;
using UnityEngine;

namespace NeonDistrict.Core
{
    /// <summary>
    /// Глобальный «дирижёр» игры: состояние (меню/игра/пауза), порядок систем,
    /// смена дня и ночи, экономика, розыск. Все остальные системы подписываются на события.
    /// Singleton, живёт в сцене Bootstrap.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum State { Boot, Menu, Playing, Paused, Cinematic }

        [Header("Ссылки (заполняются автоматически)")]
        public NeonDistrict.Player.PlayerController player;

        [Header("Правила")]
        [Tooltip("Сколько реальных секунд длится одна игровая минута")]
        public float secondsPerGameMinute = 1.2f;
        [Tooltip("Стартовый час суток")]
        [Range(0, 24)] public float startHour = 8.5f;

        public float Money { get; private set; } = 500f;
        public int Wanted { get; private set; }
        public State CurrentState { get; private set; } = State.Boot;
        public bool IsPlaying => CurrentState == State.Playing;

        /// <summary>Игровое время в часах: 13.5 == 13:30</summary>
        public float TimeOfDay { get; private set; }
        public int Day { get; private set; } = 1;

        public event Action<State> OnStateChanged;
        public event Action<float> OnMoneyChanged;
        public event Action<int> OnWantedChanged;
        public event Action<int> OnHourChanged;
        public event Action<int> OnDayChanged;

        private float _minuteAcc;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            TimeOfDay = startHour;
        }

        private void Start()
        {
            SetState(State.Menu);
        }

        private void Update()
        {
            if (CurrentState != State.Playing) return;
            TickClock(Time.deltaTime);
        }

        private void TickClock(float dt)
        {
            var before = Mathf.FloorToInt(TimeOfDay * 60f);
            TimeOfDay += dt / secondsPerGameMinute / 60f;
            if (TimeOfDay >= 24f) { TimeOfDay -= 24f; Day++; OnDayChanged?.Invoke(Day); }
            var after = Mathf.FloorToInt(TimeOfDay * 60f);
            if (after != before && before != -1)
            {
                _minuteAcc += dt;
                if (Mathf.FloorToInt(after / 60f) != Mathf.FloorToInt(before / 60f))
                    OnHourChanged?.Invoke(Mathf.FloorToInt(TimeOfDay));
            }
        }

        public void SetState(State s)
        {
            if (CurrentState == s) return;
            CurrentState = s;
            Time.timeScale = s == State.Paused || s == State.Menu ? 0f : 1f;
            AudioListener.pause = s == State.Paused;
            Cursor.visible = s == State.Menu || s == State.Paused;
            Cursor.lockState = Cursor.visible ? CursorLockMode.None : CursorLockMode.Locked;
            OnStateChanged?.Invoke(s);
        }

        public void StartGame(bool fromSave)
        {
            SetState(State.Playing);
            if (fromSave) SaveSystem.LoadInto(this, player);
        }

        public void Pause() { if (CurrentState == State.Playing) SetState(State.Paused); }
        public void Resume() { if (CurrentState == State.Paused) SetState(State.Playing); }

        // ---------------------------------------------------------------- economy
        public void AddMoney(float amount)
        {
            Money = Mathf.Max(0f, Money + amount);
            OnMoneyChanged?.Invoke(Money);
        }

        public bool TrySpend(float amount)
        {
            if (Money < amount) return false;
            Money -= amount;
            OnMoneyChanged?.Invoke(Money);
            return true;
        }

        // ---------------------------------------------------------------- wanted
        public void AddWanted(int amount)
        {
            Wanted = Mathf.Clamp(Wanted + amount, 0, 5);
            OnWantedChanged?.Invoke(Wanted);
        }

        public void ClearWanted()
        {
            Wanted = 0;
            OnWantedChanged?.Invoke(0);
            var spawner = FindFirstObjectByType<NeonDistrict.Police.PoliceSpawner>();
            if (spawner != null) spawner.ClearAll();
        }

        public void ResetWorldState()
        {
            Money = 500f;
            Wanted = 0;
            TimeOfDay = startHour;
            Day = 1;
            OnMoneyChanged?.Invoke(Money);
            OnWantedChanged?.Invoke(0);
        }

        public string TimeString => $"{Mathf.FloorToInt(TimeOfDay):00}:{Mathf.FloorToInt(TimeOfDay % 1f * 60f):00}";
    }
}
