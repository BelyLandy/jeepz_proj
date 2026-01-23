using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class SyncHalfCycleToBeat : MonoBehaviour
{
    public BeatConductor conductor;               // укажи ссылку в Инспекторе
    public float beatsPerHalfCycle = 1f;          // сколько битов занимает «туда»
    public string shaderProp = "_HalfCycle";      // имя свойства в графе (Float)

    Renderer _renderer;
    MaterialPropertyBlock _mpb;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        if (conductor == null || conductor.bpm <= 0) return;

        double secPerBeat = 60.0 / conductor.bpm;
        float halfCycle = (float)(secPerBeat * beatsPerHalfCycle);

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(shaderProp, halfCycle);
        _renderer.SetPropertyBlock(_mpb);
    }
}
