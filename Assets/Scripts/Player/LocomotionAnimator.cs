using UnityEngine;

namespace NeonDistrict.Player
{
    /// <summary>
    /// Управляет смешиванием анимаций персонажа поверх стандартного Animator:
    ///  • дыхание/стойка в покое (idle), ходьба, бег — через Speed;
    ///  • процедурный наклон корпуса в поворотах и при беге;
    ///  • «отдача» головы/корпуса при выстреле;
    ///  • приземление после прыжка (короткая просадка).
    ///
    /// Так мы получаем «топовые» ощущения без покупки Motion Matching-паков:
    /// клипы остаются Renderpeople, но подача — как в AAA.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class LocomotionAnimator : MonoBehaviour
    {
        [Header("Кости")]
        public Transform spine;
        public Transform head;
        public Transform hips;

        [Header("Настройки")]
        public float maxLean = 12f;
        public float leanSharpness = 6f;
        public float recoilAmount = 6f;
        public float recoilRecovery = 9f;
        public float landDip = 0.12f;

        private Animator _anim;
        private CharacterController _cc;
        private PlayerController _player;
        private float _recoil;
        private float _landDipCurrent;
        private Vector3 _spineBase;

        private void Awake()
        {
            _anim = GetComponent<Animator>();
            _cc = GetComponentInParent<CharacterController>();
            _player = GetComponentInParent<PlayerController>();
            if (spine == null) spine = _anim.GetBoneTransform(HumanBodyBones.Spine);
            if (head == null) head = _anim.GetBoneTransform(HumanBodyBones.Head);
            if (hips == null) hips = _anim.GetBoneTransform(HumanBodyBones.Hips);
            if (spine != null) _spineBase = spine.localEulerAngles;
        }

        /// <summary>Толчок при выстреле — вызывается из PlayerController.</summary>
        public void PunchRecoil() => _recoil = recoilAmount;

        /// <summary>Просадка при приземлении.</summary>
        public void Land(float fallSpeed)
        {
            _landDipCurrent = Mathf.Clamp(Mathf.Abs(fallSpeed) * 0.02f, 0.02f, landDip);
        }

        private void LateUpdate()
        {
            if (_player == null || _anim == null) return;

            // наклон в поворотах: сравниваем локальную боковую скорость с направлением взгляда
            float lateral = 0f;
            if (_cc != null && _cc.velocity.sqrMagnitude > 0.1f)
            {
                var local = transform.InverseTransformDirection(_cc.velocity);
                lateral = Mathf.Clamp(local.x / 4f, -1f, 1f);
            }
            float targetLean = -lateral * maxLean + Mathf.Clamp(_player.Speed / 6f, 0f, 1f) * -3f;
            if (spine != null)
            {
                var e = spine.localEulerAngles;
                float z = Mathf.LerpAngle(0f, targetLean, 1f - Mathf.Exp(-leanSharpness * Time.deltaTime));
                spine.localEulerAngles = new Vector3(e.x, e.y, z);
            }

            // отдача
            if (_recoil > 0.001f)
            {
                _recoil = Mathf.Lerp(_recoil, 0f, 1f - Mathf.Exp(-recoilRecovery * Time.deltaTime));
                if (head != null) head.localEulerAngles += new Vector3(_recoil * Time.deltaTime * 40f, 0f, 0f);
            }

            // просадка при приземлении
            if (_landDipCurrent > 0.001f)
            {
                _landDipCurrent = Mathf.Lerp(_landDipCurrent, 0f, 1f - Mathf.Exp(-8f * Time.deltaTime));
                if (hips != null)
                {
                    var p = hips.localPosition;
                    hips.localPosition = new Vector3(p.x, p.y - _landDipCurrent, p.z);
                }
            }
        }
    }
}
