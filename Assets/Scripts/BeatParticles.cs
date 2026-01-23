using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class BeatBurst : MonoBehaviour
{
    public BeatConductor conductor;
    [Min(1)] public int particlesPerBeat = 1;

    ParticleSystem _ps;
    ParticleSystem.EmissionModule _emission;

    void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
        _emission = _ps.emission;

        _emission.enabled = true;
        _emission.rateOverTime = 0f;

        if (!_ps.isPlaying) _ps.Play();
    }

    void OnEnable()
    {
        if (conductor != null) conductor.OnBeat += HandleBeat;
    }

    void OnDisable()
    {
        if (conductor != null) conductor.OnBeat -= HandleBeat;
    }

    void HandleBeat()
    {
        // Ровно на бит выпускаем заданное количество частиц
        _ps.Emit(particlesPerBeat);
    }
}
