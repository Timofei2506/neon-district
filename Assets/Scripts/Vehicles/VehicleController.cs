using UnityEngine;
using NeonDistrict.Player;
using NeonDistrict.CameraRig;

namespace NeonDistrict.Vehicles
{
    /// <summary>
    /// Аркадная, но «тяжёлая» модель машины: газ/тормоз, руль, ручник, занос,
    /// прижимная сила, подвеска-визуал (крен и клевок), звук двигателя по RPM.
    ///
    /// Настроено под Mercedes GLS 580 / Polestar 4: масса ~2.4 т, до 200 км/ч.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : MonoBehaviour, IDamageable
    {
        [Header("Двигатель")]
        public float enginePower = 32f;       // ускорение, м/с^2
        public float brakePower = 42f;
        public float maxSpeedKmh = 200f;
        public float maxReverseKmh = 45f;
        public float drag = 0.22f;
        public float downforce = 6f;

        [Header("Руление")]
        public float maxSteerAngle = 34f;
        public float steerSpeed = 6f;
        public float grip = 1.0f;
        public float handbrakeGrip = 0.35f;

        [Header("Колёса (визуал)")]
        public Transform[] frontWheels;
        public Transform[] rearWheels;
        public WheelCollider[] colliders;

        [Header("Кузов")]
        public Transform bodyVisual;
        public float rollAmount = 3.5f;
        public float pitchAmount = 2.0f;

        [Header("Свет")]
        public Light[] headlights;
        public Light[] brakeLights;

        [Header("Точки")]
        public Transform driverSeat;
        public Transform exitPoint;
        public Transform driverSeatPoint => driverSeat != null ? driverSeat : transform;
        public Vector3 ExitPoint => exitPoint != null ? exitPoint.position : transform.position + transform.right * -1.8f;

        [Header("Прочее")]
        public float health = 1000f;
        public bool engineOn = true;

        public PlayerController Driver { get; set; }
        public ThirdPersonOrbit CameraRig { get; set; }
        public bool EngineOn => engineOn && (Driver != null || Mathf.Abs(ForwardSpeed) > 0.1f);
        public Vector3 DriverSeatWorld => driverSeat != null ? driverSeat.position : transform.position;
        public float MaxSpeedKmh => maxSpeedKmh;
        public float SpeedKmh => Velocity.magnitude * 3.6f;
        public float ForwardSpeed => Vector3.Dot(Velocity, transform.forward);
        public int Gear { get; private set; } = 1;
        public float Rpm { get; private set; }

        private Rigidbody _rb;

        /// <summary>
        /// Velocity Rigidbody с фолбэком. В Unity 6 свойство называется linearVelocity,
        /// в 2022 и раньше — velocity. Сборка проекта идёт на Unity 6.3, но код
        /// остаётся совместимым (и проверяется офлайн-компилятором на обе ветки).
        /// </summary>
        private Vector3 Velocity
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return _rb.linearVelocity;
#else
                return _rb.velocity;
#endif
            }
            set
            {
#if UNITY_6000_0_OR_NEWER
                _rb.linearVelocity = value;
#else
                _rb.velocity = value;
#endif
            }
        }

        private float _throttle;
        private float _steer;
        private float _handbrake;
        private float _wheelSpin;
        private bool _braking;

        /// <summary>Автопилот (полиция/трафик): не сбрасывать ввод в FixedUpdate.</summary>
        public bool autopilot;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 2400f;
            _rb.centerOfMass = new Vector3(0f, -0.35f, 0.05f);
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        /// <summary>Автопилот для полиции и трафика: рулит к точке и держит газ.</summary>
        public void DriveTo(Vector3 worldTarget, float targetSpeedKmh, float steerGain = 2.4f)
        {
            var toTarget = worldTarget - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return;

            float desiredYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float deltaYaw = Mathf.DeltaAngle(transform.eulerAngles.y, desiredYaw);
            _steer = Mathf.Clamp(deltaYaw * steerGain, -maxSteerAngle, maxSteerAngle);
            float speedError = targetSpeedKmh - SpeedKmh;
            _throttle = Mathf.Clamp(speedError / 25f, -1f, 1f);
            _handbrake = Mathf.Abs(deltaYaw) > 65f && SpeedKmh > 45f ? 1f : 0f;
        }

        /// <summary>Вызывается из PlayerController каждый кадр, когда игрок за рулём.</summary>
        public void Drive(Vector2 input, bool boost)
        {
            autopilot = false;
            _throttle = input.y;
            var targetSteer = input.x * maxSteerAngle * (1f - 0.45f * Mathf.Clamp01(SpeedKmh / maxSpeedKmh));
            _steer = Mathf.Lerp(_steer, targetSteer, steerSpeed * Time.deltaTime);
            _handbrake = Input.GetKey(KeyCode.Space) ? 1f : 0f;
        }

        private void FixedUpdate()
        {
            if (Driver == null && !autopilot)
            {
                _throttle = 0f;
                _steer = Mathf.Lerp(_steer, 0f, 2f * Time.deltaTime);
                _handbrake = 1f; // припаркована
            }

            float speedMs = ForwardSpeed;

            // продольная тяга
            if (_throttle > 0.05f)
                _rb.AddForce(transform.forward * (_throttle * enginePower * 1000f), ForceMode.Force);
            else if (_throttle < -0.05f)
            {
                if (speedMs > 1f)
                {
                    _rb.AddForce(-transform.forward * (brakePower * 1000f), ForceMode.Force);
                    _braking = true;
                }
                else
                    _rb.AddForce(-transform.forward * (enginePower * 400f), ForceMode.Force);
            }
            else _braking = false;

            // ограничение скорости
            float maxMs = maxSpeedKmh / 3.6f;
            if (Velocity.magnitude > maxMs)
                Velocity = Velocity.normalized * maxMs;

            // сопротивление и прижимная сила
            _rb.AddForce(-Velocity * drag * 60f, ForceMode.Force);
            _rb.AddForce(-transform.up * Velocity.magnitude * downforce, ForceMode.Force);

            // боковое сцепление (аркадный занос)
            var localVel = transform.InverseTransformDirection(Velocity);
            float gripNow = Mathf.Lerp(grip, handbrakeGrip, _handbrake);
            localVel.x *= Mathf.Clamp01(1f - gripNow * Time.fixedDeltaTime * 6f);
            var v = transform.TransformDirection(localVel);
            Velocity = new Vector3(v.x, Velocity.y, v.z);

            // руление через крутящий момент (стабильнее, чем телепорт rotation)
            float steerTorque = _steer * Mathf.Clamp(speedMs, -1f, 1f) * 260f * (1f - _handbrake * 0.4f);
            _rb.AddTorque(transform.up * steerTorque, ForceMode.Force);

            // визуал колёс
            _wheelSpin += speedMs * Time.fixedDeltaTime * 40f;
            foreach (var w in frontWheels)
                if (w != null) w.localRotation = Quaternion.Euler(_wheelSpin, _steer, 0f);
            foreach (var w in rearWheels)
                if (w != null) w.localRotation = Quaternion.Euler(_wheelSpin, 0f, 0f);

            // крен и клевок кузова
            if (bodyVisual != null)
            {
                float lateral = Vector3.Dot(Velocity, transform.right);
                var target = Quaternion.Euler(_braking ? pitchAmount : -_throttle * pitchAmount * 0.5f, 0f, -lateral * rollAmount * 0.1f);
                bodyVisual.localRotation = Quaternion.Slerp(bodyVisual.localRotation, target, 6f * Time.fixedDeltaTime);
            }

            // свет
            foreach (var l in brakeLights) if (l != null) l.intensity = _braking ? 4f : 0.3f;

            UpdateEngineSound(speedMs);
        }

        private void UpdateEngineSound(float speedMs)
        {
            Gear = Mathf.Clamp(Mathf.FloorToInt(Mathf.Abs(speedMs) / 9f) + 1, 1, 6);
            float gearFraction = Mathf.Repeat(Mathf.Abs(speedMs) / 9f, 1f);
            Rpm = Mathf.Lerp(900f, 6800f, Mathf.Clamp01(gearFraction + Mathf.Abs(_throttle) * 0.3f));
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 direction)
        {
            health -= amount;
            if (health <= 0f) Debug.Log("[Vehicle] destroyed: " + name);
        }

        /// <summary>Урон от столкновения, вызывается из OnCollisionEnter.</summary>
        private void OnCollisionEnter(Collision collision)
        {
            float impact = collision.relativeVelocity.magnitude;
            if (impact > 6f)
            {
                health -= impact * 4f;
                if (Driver != null && impact > 14f)
                    Driver.TakeDamage((impact - 14f) * 1.5f, collision.GetContact(0).point, Vector3.up);
                if (Core.GameManager.Instance != null && impact > 22f)
                    Core.GameManager.Instance.AddWanted(1);
            }
        }
    }
}
