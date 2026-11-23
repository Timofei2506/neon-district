using UnityEngine;

namespace NeonDistrict.Police
{
    /// <summary>
    /// Сирена: двухтональный вой (источник звука может быть пустым — тогда
    /// звук синтезируется в рантайме, чтобы проект работал без аудиоассетов).
    ///
    /// Плюс «стингер» задержания — короткий нисходящий аккорд.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PoliceSiren : MonoBehaviour
    {
        [Header("Звук")]
        public AudioClip sirenClip;
        public float volume = 0.45f;
        public bool useSynthesizedSiren = true;

        private AudioSource _source;
        private double _phase;
        private float _freq;
        private int _sampleRate = 48000;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = 8f;
            _source.maxDistance = 220f;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = volume;

            if (sirenClip != null)
            {
                _source.clip = sirenClip;
                _source.Play();
            }
            else if (useSynthesizedSiren)
            {
                _sampleRate = AudioSettings.outputSampleRate;
                _source.Play(); // источник без клипа — звук отдаём в OnAudioFilterRead
            }
        }

        /// <summary>
        /// Синтез «wail»-сирены: плавно качаем частоту 620–1180 Гц.
        /// Работает на любом билде без аудиофайлов.
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!useSynthesizedSiren || sirenClip != null) return;
            double inc = _freq / _sampleRate * 2.0 * Mathf.PI;
            for (int i = 0; i < data.Length; i += channels)
            {
                _phase += inc;
                if (_phase > Mathf.PI * 2) _phase -= Mathf.PI * 2;
                float sample = Mathf.Sin((float)_phase) * 0.16f;
                // лёгкая «гнусавость» — вторая гармоника
                sample += Mathf.Sin((float)_phase * 2f) * 0.05f;
                for (int c = 0; c < channels; c++) data[i + c] += sample;
            }
            // качание частоты между двумя тонами
            _freq = Mathf.Lerp(_freq, Mathf.Sin(Time.time * 1.6f) > 0 ? 1180f : 640f, 0.002f);
            if (_freq < 1f) _freq = 900f;
        }

        public void PlayArrestSting()
        {
            if (_source == null) return;
            // короткий «пик» вниз, чтобы игрок понял: задержали
            _freq = 1400f;
            Invoke(nameof(StopSiren), 0.35f);
        }

        private void StopSiren()
        {
            if (_source != null) _source.Stop();
            enabled = false;
        }
    }
}
