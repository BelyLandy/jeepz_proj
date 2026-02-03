using UnityEngine;

public class PulseMover : MonoBehaviour
{
    public BeatConductor conductor;
    Vector3 _baseScale;
    float _pulse;

    void Start()
    {
        _baseScale = transform.localScale;
        conductor.OnBeat += () => { _pulse = 1f; };
        conductor.OnBeatPhase += (phase) =>
        {
            float tri = 1f - Mathf.Abs(2f * phase - 1f); // 0..1..0
            float smooth = Smoothstep01(tri);
            float size = 1f + 0.12f * smooth + 0.18f * _pulse;
            transform.localScale = _baseScale * size;

            _pulse = Mathf.MoveTowards(_pulse, 0f, Time.deltaTime * 4f);
        };
    }

    float Smoothstep01(float x) => x * x * (3f - 2f * x);
}
