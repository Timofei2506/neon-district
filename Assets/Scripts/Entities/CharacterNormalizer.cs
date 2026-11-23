using System;
using UnityEngine;

namespace NeonDistrict.Player
{
    /// <summary>
    /// Приводит любой FBX персонажа к единому «человеческому» виду:
    /// рост 1.8 м, ступни на уровне пивота, лицом +Z.
    /// Renderpeople приходит в масштабе 0.01 и с обёртками — этот компонент
    /// вычищает такие сюрпризы в рантайме, чтобы анимации и IK не «плясали».
    /// </summary>
    public class CharacterNormalizer : MonoBehaviour
    {
        [Tooltip("Желаемый рост, м")]
        public float targetHeight = 1.8f;
        [Tooltip("Развернуть на 180°, если модель смотрит в -Z")]
        public bool flipForward = false;

        public float AppliedScale { get; private set; } = 1f;

        private void Awake()
        {
            Normalize();
        }

        [ContextMenu("Нормализовать")]
        public void Normalize()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float h = bounds.size.y;
            if (h > 0.01f && Mathf.Abs(h - targetHeight) > 0.02f)
            {
                AppliedScale = targetHeight / h;
                // масштабируем корень, но не трогаем пивот по Y
                var local = transform.localScale;
                transform.localScale = new Vector3(local.x * AppliedScale, local.y * AppliedScale, local.z * AppliedScale);
            }

            if (flipForward) transform.rotation *= Quaternion.Euler(0f, 180f, 0f);

            // ступни на землю
            renderers = GetComponentsInChildren<Renderer>();
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float offset = bounds.min.y - transform.position.y;
            if (Mathf.Abs(offset) > 0.005f)
                transform.position -= new Vector3(0f, offset, 0f);
        }
    }
}
