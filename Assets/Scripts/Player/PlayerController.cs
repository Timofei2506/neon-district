using UnityEngine;
using NeonDistrict.Vehicles;

namespace NeonDistrict.Player
{
    /// <summary>
    /// Контроллер персонажа (CharacterController + New Input System).
    /// Ходьба/бег/прыжок/присед, привязка камеры к орбите, передача скорости в Animator.
    ///
    /// Модель персонажа — Renderpeople (ходьба), поэтому:
    ///  - анимация проигрывается с timeScale, привязанным к скорости;
    ///  - при остановке включается idle-поза (короткий клип / weight 0).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Движение")]
        public float walkSpeed = 2.0f;
        public float runSpeed = 5.2f;
        public float sprintSpeed = 6.8f;
        public float crouchSpeed = 1.2f;
        public float jumpHeight = 1.1f;
        public float gravity = -24f;
        public float rotationSharpness = 14f;
        public float acceleration = 18f;

        [Header("Камера")]
        public Transform cameraPivot;
        public float cameraSensitivity = 2.0f;
        public float minPitch = -35f;
        public float maxPitch = 70f;

        [Header("Бой")]
        public int ammoPistol = 36;
        public int magazineSize = 12;
        public int magazine;
        public float pistolFireRate = 3.2f;
        public float pistolRange = 70f;
        public float pistolDamage = 26f;
        public Transform muzzle;
        public LayerMask hitMask = ~0;

        [Header("Ссылки")]
        public Animator animator;
        public LocomotionAnimator locomotion;
        public VehicleController currentVehicle;

        public float Health { get; set; } = 100f;
        public int AmmoPistol { get => ammoPistol; set => ammoPistol = value; }
        public bool IsInVehicle => currentVehicle != null;
        public bool IsSprinting { get; private set; }
        public float Speed { get; private set; }

        private CharacterController _cc;
        private Vector3 _velocity;
        private float _camYaw;
        private float _camPitch = 12f;
        private float _nextShotTime;
        private bool _grounded;
        private Vector3 _lastMoveDir;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        // ---------------- ввод (без жёсткой зависимости от Asset References)
#if ND_COMPILE_CHECK
        private object _kb, _mouse, _pad;   // офлайн-проверка: Input System недоступен
#else
        private UnityEngine.InputSystem.Keyboard _kb;
        private UnityEngine.InputSystem.Mouse _mouse;
        private UnityEngine.InputSystem.Gamepad _pad;
#endif

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (locomotion == null) locomotion = GetComponentInChildren<LocomotionAnimator>();
            magazine = Mathf.Min(magazineSize, ammoPistol);
            if (cameraPivot == null && Camera.main != null) cameraPivot = Camera.main.transform;
            _camYaw = transform.eulerAngles.y;
        }

        private void Start()
        {
#if !ND_COMPILE_CHECK
            _kb = UnityEngine.InputSystem.Keyboard.current;
            _mouse = UnityEngine.InputSystem.Mouse.current;
            _pad = UnityEngine.InputSystem.Gamepad.current;
#endif
            LockCursor(true);
        }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Update()
        {
            if (Core.GameManager.Instance != null && !Core.GameManager.Instance.IsPlaying) return;
            if (IsInVehicle)
            {
                // в машине игрок только передаёт ввод в VehicleController
                currentVehicle.Drive(ReadMoveInput(), IsSprinting);
                if (PressedJump()) ExitVehicle();
                return;
            }

            HandleLook();
            HandleMovement();
            HandleCombat();
            HandleInteraction();
        }

        // ---------------------------------------------------------------- input helpers
        private Vector2 ReadMoveInput()
        {
            Vector2 v = Vector2.zero;
#if !ND_COMPILE_CHECK
            if (_kb != null)
            {
                if (_kb.wKey.isPressed) v.y += 1f;
                if (_kb.sKey.isPressed) v.y -= 1f;
                if (_kb.dKey.isPressed) v.x += 1f;
                if (_kb.aKey.isPressed) v.x -= 1f;
                if (_kb.upArrowKey.isPressed) v.y += 1f;
                if (_kb.downArrowKey.isPressed) v.y -= 1f;
                if (_kb.leftArrowKey.isPressed) v.x -= 1f;
                if (_kb.rightArrowKey.isPressed) v.x += 1f;
            }
            if (_pad != null) { var s = _pad.leftStick.ReadValue(); if (s.magnitude > v.magnitude) v = s; }
#endif
            return Vector2.ClampMagnitude(v, 1f);
        }

#if ND_COMPILE_CHECK
        private bool PressedSprint() => false;
        private bool PressedJump() => false;
        private bool PressedCrouch() => false;
#else
        private bool PressedSprint() => (_kb != null && _kb.leftShiftKey.isPressed) || (_pad != null && _pad.leftStickButton.isPressed);
        private bool PressedJump() => (_kb != null && _kb.spaceKey.wasPressedThisFrame) || (_pad != null && _pad.buttonSouth.wasPressedThisFrame);
        private bool PressedCrouch() => _kb != null && _kb.cKey.isPressed;
#endif

        // ---------------------------------------------------------------- look
        /// <summary>Орбитальная камера (ThirdPersonOrbit) — источник направления «куда смотрит игрок».</summary>
        public NeonDistrict.CameraRig.ThirdPersonOrbit cameraRig;

        private void HandleLook()
        {
            // Если камерой управляет ThirdPersonOrbit — просто синхронизируем yaw,
            // чтобы персонаж двигался относительно взгляда камеры.
            if (cameraRig != null && cameraRig.enabled)
            {
                _camYaw = cameraRig.Yaw;
                return;
            }
            Vector2 look = Vector2.zero;
#if !ND_COMPILE_CHECK
            if (_mouse != null) look += _mouse.delta.ReadValue() * 0.08f;
            if (_pad != null) look += _pad.rightStick.ReadValue() * 2.2f;
#endif
            _camYaw += look.x * cameraSensitivity;
            _camPitch = Mathf.Clamp(_camPitch - look.y * cameraSensitivity, minPitch, maxPitch);
        }

        // ---------------------------------------------------------------- movement
        private void HandleMovement()
        {
            var input = ReadMoveInput();
            IsSprinting = PressedSprint() && !PressedCrouch();
            bool crouch = PressedCrouch();
            float targetSpeed = crouch ? crouchSpeed : (IsSprinting ? sprintSpeed : runSpeed);

            var camFwd = Quaternion.Euler(0f, _camYaw, 0f) * Vector3.forward;
            var camRight = Quaternion.Euler(0f, _camYaw, 0f) * Vector3.right;
            var wish = (camFwd * input.y + camRight * input.x);
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            var targetVel = wish * (targetSpeed * Mathf.Clamp01(input.magnitude));
            var planar = new Vector3(_velocity.x, 0f, _velocity.z);
            planar = Vector3.MoveTowards(planar, targetVel, acceleration * Time.deltaTime);
            _velocity.x = planar.x;
            _velocity.z = planar.z;
            Speed = planar.magnitude;

            if (wish.sqrMagnitude > 0.001f)
            {
                _lastMoveDir = wish.normalized;
                var targetRot = Quaternion.LookRotation(_lastMoveDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
            }

            bool wasGrounded = _grounded;
            _grounded = _cc.isGrounded;
            if (_grounded && !wasGrounded && locomotion != null) locomotion.Land(_velocity.y);
            if (_grounded && _velocity.y < 0f) _velocity.y = -2f;
            if (PressedJump() && _grounded)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                if (animator != null) animator.SetTrigger(JumpHash);
            }
            _velocity.y += gravity * Time.deltaTime;

            var crouchScale = crouch ? 0.65f : 1f;
            _cc.height = Mathf.Lerp(_cc.height, 1.8f * crouchScale, 8f * Time.deltaTime);
            _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);

            _cc.Move(_velocity * Time.deltaTime);

            // animator
            if (animator != null)
            {
                animator.SetFloat(SpeedHash, Speed / runSpeed, 0.12f, Time.deltaTime);
                animator.SetBool(GroundedHash, _grounded);
                // клип ходьбы подстраиваем под скорость
                animator.speed = Mathf.Clamp(Speed / walkSpeed, 0.55f, 2.2f);
            }
        }

        // ---------------------------------------------------------------- combat
        private void HandleCombat()
        {
#if ND_COMPILE_CHECK
            bool attackHeld = false;
#else
            bool attackHeld = (_mouse != null && _mouse.leftButton.isPressed) || (_pad != null && _pad.rightTrigger.isPressed > 0.4f);
#endif
            if (!attackHeld || Time.time < _nextShotTime) return;
            _nextShotTime = Time.time + 1f / pistolFireRate;
            if (magazine <= 0) { Reload(); return; }

            magazine--;
            if (locomotion != null) locomotion.PunchRecoil();
            var cam = Camera.main;
            var origin = cam != null ? cam.transform.position : transform.position + Vector3.up * 1.5f;
            var dir = cam != null ? cam.transform.forward : transform.forward;
            if (Physics.Raycast(origin, dir, out var hit, pistolRange, hitMask, QueryTriggerInteraction.Ignore))
            {
                var dmg = hit.collider.GetComponentInParent<IDamageable>();
                dmg?.TakeDamage(pistolDamage, hit.point, dir);
                if (dmg == null)
                {
                    var ped = hit.collider.GetComponentInParent<Pedestrian>();
                    if (ped != null) ped.Flee(origin);
                }
                if (hit.rigidbody != null) hit.rigidbody.AddForceAtPosition(dir * 60f, hit.point, ForceMode.Impulse);
            }
            if (Core.GameManager.Instance != null) Core.GameManager.Instance.AddWanted(1);
        }

        private void HandleInteraction()
        {
#if ND_COMPILE_CHECK
            bool pressed = false;
#else
            bool pressed = (_kb != null && _kb.eKey.wasPressedThisFrame) || (_kb != null && _kb.fKey.wasPressedThisFrame) || (_pad != null && _pad.buttonEast.wasPressedThisFrame);
#endif
            if (!pressed) return;
            var v = FindNearbyVehicle(3.6f);
            if (v != null) EnterVehicle(v);
        }

        private void Reload()
        {
            int need = magazineSize - magazine;
            int take = Mathf.Min(need, ammoPistol);
            magazine += take;
            ammoPistol -= take;
        }

        // ---------------------------------------------------------------- vehicles
        public void EnterVehicle(VehicleController v)
        {
            if (v == null || IsInVehicle) return;
            currentVehicle = v;
            v.Driver = this;
            _cc.enabled = false;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            transform.position = v.DriverSeatWorld;
            transform.SetParent(v.transform, true);
            if (animator != null) animator.SetFloat(SpeedHash, 0f);
            if (cameraRig != null) cameraRig.SetTarget(v.transform, true);
            if (v.engineOn) { foreach (var h in v.headlights) if (h != null) h.intensity = 6f; }
        }

        public void ExitVehicle()
        {
            if (!IsInVehicle) return;
            var v = currentVehicle;
            currentVehicle = null;
            transform.SetParent(null, true);
            transform.position = v.ExitPoint + Vector3.up * 0.15f;
            _cc.enabled = true;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = true;
            v.Driver = null;
            if (cameraRig != null) cameraRig.SetTarget(transform, false);
            foreach (var h in v.headlights) if (h != null) h.intensity = 0f;
        }

        /// <summary>Ближайшая машина в радиусе (для подсказки «нажми E»).</summary>
        public VehicleController FindNearbyVehicle(float radius = 3.5f)
        {
            VehicleController best = null;
            float bd = radius;
            foreach (var v in Object.FindObjectsByType<VehicleController>(FindObjectsSortMode.None))
            {
                if (v == null || v.Driver != null) continue;
                float d = Vector3.Distance(transform.position, v.transform.position);
                if (d < bd) { bd = d; best = v; }
            }
            return best;
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 normal)
        {
            Health = Mathf.Max(0f, Health - amount);
            if (Health <= 0f) Debug.Log("[Player] down");
        }
    }

    public interface IDamageable
    {
        void TakeDamage(float amount, Vector3 point, Vector3 direction);
    }
}
