using UnityEngine;
using System;

public class BeatConductor : MonoBehaviour
{
    public AudioSource music;
    public double bpm = 120.0;
    public double startDelay = 0.1; // чтобы успеть подготовиться

    public event Action OnBeat;      // подпишитесь и делайте что угодно на бит
    public event Action<float> OnBeatPhase; // непрерывная фаза внутри бита [0..1]

    double _secPerBeat;
    double _songStartDsp;
    double _nextBeatDsp;

    void Awake()
    {
        _secPerBeat = 60.0 / bpm;
    }

    void Start()
    {
        // Стартуем музыку по DSP-времени (sample-accurate)
        _songStartDsp = AudioSettings.dspTime + startDelay;
        music.PlayScheduled(_songStartDsp);

        _nextBeatDsp = _songStartDsp + _secPerBeat; // первый бит после старта
    }

    void Update()
    {
        double dsp = AudioSettings.dspTime;

        // События на каждом бите (если FPS просел, пробежимся циклом)
        while (dsp >= _nextBeatDsp)
        {
            OnBeat?.Invoke();
            _nextBeatDsp += _secPerBeat;
        }

        // Непрерывная фаза внутри текущего бита (удобно для плавных анимаций)
        double t = (dsp - _songStartDsp) / _secPerBeat;
        float phase = (float)(t - Math.Floor(t)); // 0..1
        OnBeatPhase?.Invoke(phase);
    }

    // Если BPM меняется на лету:
    public void SetBpm(double newBpm)
    {
        // сохраняем текущую музыкальную позицию и пересчитываем
        double dsp = AudioSettings.dspTime;
        double songBeats = (dsp - _songStartDsp) / _secPerBeat;

        bpm = newBpm;
        _secPerBeat = 60.0 / bpm;
        _songStartDsp = dsp - songBeats * _secPerBeat;
        _nextBeatDsp = Math.Ceiling(songBeats + 1.0) * _secPerBeat + _songStartDsp;
    }
}
