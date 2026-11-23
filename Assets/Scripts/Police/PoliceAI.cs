using System.Collections.Generic;
using UnityEngine;
using NeonDistrict.Core;
using NeonDistrict.Vehicles;

namespace NeonDistrict.Police
{
    /// <summary>
    /// Полицейский ИИ: при розыске выезжает с «базы», догоняет игрока по прямой
    /// (улицы Манхэттена регулярные, этого достаточно), таранит и держит дистанцию.
    ///
    /// Уровень розыска (1–5) задаёт число патрулей: 1 → один, 3 → два, 5 → три.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class PoliceAI : MonoBehaviour
    {
        [Header("Настройки")]
        public float maxChaseSpeedKmh = 165f;
        public float ramDistance = 3.2f;
        public float arrestDistance = 2.2f;
        public float sirenRange = 260f;
        public float despawnDistance = 420f;
        public float ramCooldown = 1.1f;

        [Header("Ссылки (заполняются автоматически)")]
        public Transform target;
        public PoliceLivery livery;

        private VehicleController _vehicle;
        private float _nextRam;
        private float _stuckTimer;
        private Vector3 _lastPos;
        private PoliceSiren _siren;

        private void Awake()
        {
            _vehicle = GetComponent<VehicleController>();
            _vehicle.autopilot = true;
            if (livery == null) livery = GetComponent<PoliceLivery>();
            _siren = GetComponent<PoliceSiren>();
        }

        private void Start()
        {
            if (target == null && GameManager.Instance != null && GameManager.Instance.player != null)
                target = GameManager.Instance.player.transform;
            if (livery != null) livery.SetSirens(true);
        }

        private void Update()
        {
            if (target == null) return;
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            // розыск снят — патруль уезжает и исчезает
            if (gm.Wanted <= 0)
            {
                var away = transform.position + transform.forward * 80f;
                _vehicle.DriveTo(away, 40f);
                if (Vector3.Distance(transform.position, target.position) > despawnDistance) Destroy(gameObject);
                return;
            }

            var toTarget = target.position - transform.position;
            float dist = toTarget.magnitude;

            // целимся «наперерез»: точка чуть впереди игрока
            var playerVehicle = FindPlayerVehicle();
            Vector3 aimPoint = target.position;
            if (playerVehicle != null)
            {
                var pv = playerVehicle.GetComponent<Rigidbody>();
#if UNITY_6000_0_OR_NEWER
                if (pv != null) aimPoint += pv.linearVelocity * 0.6f;
#else
                if (pv != null) aimPoint += pv.velocity * 0.6f;
#endif
                else aimPoint += playerVehicle.transform.forward * 6f;
            }

            float speed = Mathf.Lerp(70f, maxChaseSpeedKmh, Mathf.Clamp01(gm.Wanted / 5f));
            _vehicle.DriveTo(aimPoint, speed, 2.6f);

            // антизастревание: если стоим на месте дольше 2 секунд — включаем задний ход/разворот
            if (Vector3.Distance(transform.position, _lastPos) < 0.15f) _stuckTimer += Time.deltaTime;
            else _stuckTimer = 0f;
            _lastPos = transform.position;
            if (_stuckTimer > 2f)
            {
                _vehicle.DriveTo(transform.position - transform.forward * 12f, 25f, 3.2f);
                if (_stuckTimer > 4f) _stuckTimer = 0f;
            }

            // таран
            if (dist < ramDistance && Time.time > _nextRam)
            {
                _nextRam = Time.time + ramCooldown;
                var rb = GetComponent<Rigidbody>();
                if (rb != null) rb.AddForce(transform.forward * 9000f, ForceMode.Impulse);
                gm.AddWanted(1);
                if (livery != null) livery.sirensOn = true;
            }

            // «арест»: игрок совсем рядом и почти стоит — начисляем штраф и сбрасываем розыск
            if (dist < arrestDistance && playerVehicle != null && playerVehicle.SpeedKmh < 6f && Time.time > _nextRam + 2f)
            {
                ArrestPlayer(gm);
            }

            if (dist > despawnDistance) Destroy(gameObject);
        }

        private void ArrestPlayer(GameManager gm)
        {
            float fine = 250f * gm.Wanted;
            gm.AddMoney(-Mathf.Min(fine, gm.Money));
            gm.ClearWanted();
            Debug.Log($"[Police] Задержание. Штраф ${fine}");
            if (_siren != null) _siren.PlayArrestSting();
            Destroy(gameObject, 1.2f);
        }

        private VehicleController FindPlayerVehicle()
        {
            if (target == null) return null;
            var pc = target.GetComponent<Player.PlayerController>();
            return pc != null ? pc.currentVehicle : null;
        }
    }
}
