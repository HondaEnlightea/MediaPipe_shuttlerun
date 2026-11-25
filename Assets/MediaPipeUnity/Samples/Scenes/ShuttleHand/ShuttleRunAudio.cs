using UnityEngine;

namespace Mediapipe.Unity.Sample.ShuttleHand
{
  public class ShuttleRunAudio : MonoBehaviour
  {
    private AudioSource _audioSource;
    private const int SAMPLE_RATE = 44100;
    private const float NOTE_DURATION = 0.3f;
    private const float BUZZER_DURATION = 0.4f;

    /// <summary>ドレミファソラシド の周波数テーブル (C4～C5)</summary>
    private static readonly float[] NOTE_FREQUENCIES = {
      261.63f, // ド (C4)
      293.66f, // レ (D4)
      329.63f, // ミ (E4)
      349.23f, // ファ (F4)
      392.00f, // ソ (G4)
      440.00f, // ラ (A4)
      493.88f, // シ (B4)
      523.25f  // 高ド (C5)
    };

    private AudioClip[] _noteClips;
    private AudioClip _buzzerClip;
    private AudioClip _startClip;
    private AudioClip _restClip;

    public void Initialize()
    {
      _audioSource = gameObject.AddComponent<AudioSource>();
      _audioSource.playOnAwake = false;
      _audioSource.volume = 0.6f;

      _noteClips = new AudioClip[NOTE_FREQUENCIES.Length];
      for (int i = 0; i < NOTE_FREQUENCIES.Length; i++)
      {
        _noteClips[i] = CreateToneClip($"Note_{i}", NOTE_FREQUENCIES[i], NOTE_DURATION);
      }

      _buzzerClip = CreateToneClip("Buzzer", 130f, BUZZER_DURATION, isBuzzer: true);
      _startClip = CreateToneClip("Start", 523.25f, 0.5f);

      // 休憩音: 低いド（C3）+ さらに低いド（C2）の合成
      _restClip = CreateRestClip("Rest", 0.6f);
    }

    public void PlayNote(int noteIndex)
    {
      if (noteIndex >= 0 && noteIndex < _noteClips.Length)
        _audioSource.PlayOneShot(_noteClips[noteIndex]);
    }

    public void PlayBuzzer()
    {
      _audioSource.PlayOneShot(_buzzerClip);
    }

    public void PlayStart()
    {
      _audioSource.PlayOneShot(_startClip);
    }

    /// <summary>休憩音（折り返し地点）を再生</summary>
    public void PlayRest()
    {
      _audioSource.PlayOneShot(_restClip);
    }

    private AudioClip CreateToneClip(string name, float frequency, float duration, bool isBuzzer = false)
    {
      int sampleCount = (int)(SAMPLE_RATE * duration);
      float[] samples = new float[sampleCount];

      for (int i = 0; i < sampleCount; i++)
      {
        float t = (float)i / SAMPLE_RATE;
        float envelope = CalcEnvelope(t, duration);

        if (isBuzzer)
        {
          samples[i] = envelope * (
            Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.5f +
            Mathf.Sin(2f * Mathf.PI * frequency * 1.5f * t) * 0.3f
          );
        }
        else
        {
          samples[i] = envelope * (
            Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.7f +
            Mathf.Sin(2f * Mathf.PI * frequency * 2f * t) * 0.2f +
            Mathf.Sin(2f * Mathf.PI * frequency * 3f * t) * 0.1f
          );
        }
      }

      var clip = AudioClip.Create(name, sampleCount, 1, SAMPLE_RATE, false);
      clip.SetData(samples, 0);
      return clip;
    }

    /// <summary>休憩音: C3 + C2 の合成で低い持続音</summary>
    private AudioClip CreateRestClip(string name, float duration)
    {
      float freqC3 = 130.81f;
      float freqC2 = 65.41f;
      int sampleCount = (int)(SAMPLE_RATE * duration);
      float[] samples = new float[sampleCount];

      for (int i = 0; i < sampleCount; i++)
      {
        float t = (float)i / SAMPLE_RATE;
        float envelope = CalcEnvelope(t, duration);
        samples[i] = envelope * (
          Mathf.Sin(2f * Mathf.PI * freqC3 * t) * 0.5f +
          Mathf.Sin(2f * Mathf.PI * freqC2 * t) * 0.5f
        );
      }

      var clip = AudioClip.Create(name, sampleCount, 1, SAMPLE_RATE, false);
      clip.SetData(samples, 0);
      return clip;
    }

    private float CalcEnvelope(float t, float duration)
    {
      float attack = 0.02f;
      float release = 0.08f;
      if (t < attack) return t / attack;
      else if (t > duration - release) return (duration - t) / release;
      else return 1f;
    }
  }
}
