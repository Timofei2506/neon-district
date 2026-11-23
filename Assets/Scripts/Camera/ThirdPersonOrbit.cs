using UnityEngine;

namespace NeonDistrict.CameraRig
{
    /// <summary>
    /// Орбитальная камера от третьего лица с коллизией (не проваливается в стены),
    /// плавным «пружинным» следованием и отдельным режимом для машины.
    ///
    /// Camera rig: Empty (этот компонент) -> Camera (Cinemachine НЕ обязателен,
    /// но если на камере висит Cinemachine Brain, скрипт отдаёт управление ему).
    /// </summary>
    public class ThirdPersonOrbit : MonoBehaviour
    {
        [Header("Цель")]
        public Transform target;         // игрок или машина
        public Vector3 targetOffset = new Vector3(0f, 1.55f, 0f);

        [Header("Орбита")]
        public float distance = 4.6f;
        public float vehicleDistance = 8.5f;
        public float minDistance = 1.2f;
        public float heightMin = -0.2f;
        public float heightMax = 1.4f;
        public float mouseSensitivity = 2.4f;
        public float colliderRadius = 0.32f;

        [Header("Сглаживание")]
        public float positionSharpness = 14f;
        public float rotationSharpness = 20f;

        public LayerMask collisionMask = ~0;

        public float Yaw => _yaw;
        private float _yaw;
        private float _pitch = 14f;
        private Vector3 _currentPos;
        private Quaternion _currentRot;
#if ND_COMPILE_CHECK
        private object _mouse, _pad;        // офлайн-проверка: Input System недоступен
#else
        private UnityEngine.InputSystem.Mouse _mouse;
        private UnityEngine.InputSystem.Gamepad _pad;
#endif
        private float _currentDistance;

        private void Start()
        {
#if !ND_COMPILE_CHECK
            _mouse = UnityEngine.InputSystem.Mouse.current;
            _pad = UnityEngine.InputSystem.Gamepad.current;
#endif
            _currentDistance = distance;
            if (target != null) _yaw = target.eulerAngles.y;
        }

        public bool VehicleMode { get; private set; }

        public void SetTarget(Transform t, bool vehicleMode)
        {
            target = t;
            VehicleMode = vehicleMode;
            distance = vehicleMode ? vehicleDistance : 4.6f;
            targetOffset = vehicleMode ? new Vector3(0f, 1.6f, -0.5f) : new Vector3(0f, 1.55f, 0f);
            _currentDistance = distance;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (Core.GameManager.Instance != null && !Core.GameManager.Instance.IsPlaying) return;

            Vector2 look = Vector2.zero;
#if !ND_COMPILE_CHECK
            if (_mouse != null) look += _mouse.delta.ReadValue() * 0.09f;
            if (_pad != null) look += _pad.rightStick.ReadValue() * 2.4f;
#endif

            _yaw += look.x * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * mouseSensitivity, -20f, 68f);

            // в машине камера сама подтягивается за кормой
            if (VehicleMode)
            {
                float targetYaw = target.eulerAngles.y;
                float delta = Mathf.DeltaAngle(_yaw, targetYaw);
                _yaw += delta * (1f - Mathf.Exp(-2.2f * Time.deltaTime));
                _pitch = Mathf.Lerp(_pitch, 12f, 1f - Mathf.Exp(-1.5f * Time.deltaTime));
            }

            var pivot = target.position + targetOffset;
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            var desiredDir = rot * Vector3.back;
            float wantedDist = distance;

            var castDist = VehicleMode ? distance * 0.85f : distance;
            if (Physics.SphereCast(pivot, colliderRadius, desiredDir, out var hit, castDist, collisionMask, QueryTriggerInteraction.Ignore))
                wantedDist = Mathf.Max(minDistance, hit.distance - 0.1f);

            _currentDistance = Mathf.Lerp(_currentDistance, wantedDist, 1f - Mathf.Exp(-12f * Time.deltaTime));
            var desiredPos = pivot + desiredDir * _currentDistance;

            float k = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
            _currentPos = Vector3.Lerp(_currentPos == default ? transform.position : _currentPos, desiredPos, k);
            _currentRot = Quaternion.Slerp(_currentRot == default ? transform.rotation : _currentRot, rot, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));

            transform.SetPositionAndRotation(_currentPos, _currentRot);
        }

        /// <summary>Мгновенно поставить камеру за целью (после спавна, телепорта).</summary>
        public void SnapBehind()
        {
            if (target == null) return;
            _yaw = target.eulerAngles.y;
            var pivot = target.position + targetOffset;
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            _currentPos = pivot + rot * Vector3.back * distance;
            _currentRot = rot;
            transform.SetPositionAndRotation(_currentPos, _currentRot);
        }
    }
}
