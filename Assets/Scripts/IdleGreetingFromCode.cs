using UnityEngine;

public class IdleGreetingFromCode : MonoBehaviour
{
    [Header("Targets (локальные позиции)")]
    public Transform Head;
    public Transform Body;
    public Transform Shoulder_Left;

    [Header("Sync")]
    public BeatConductor Conductor;
    [Tooltip("Сколько битов занимает один цикл этой анимации (1 = один цикл на бит)")]
    public float BeatsPerLoop = 1f;

    [Header("Режим применений")]
    public bool Additive = false;
    public float Intensity = 1f;

    Vector3 _headBase, _bodyBase, _shoulderBase;

    AnimationCurve headX = new AnimationCurve(
        new Keyframe(0f,   0.4400645f, 0,0),
        new Keyframe(0.5f, 0.4630000f, 0,0),
        new Keyframe(1f,   0.4400645f, 0,0)
    );
    AnimationCurve headY = new AnimationCurve(
        new Keyframe(0f,   0.4874378f, 0,0),
        new Keyframe(0.5f, 0.4700000f, 0,0),
        new Keyframe(1f,   0.4874378f, 0,0)
    );

    AnimationCurve bodyX = new AnimationCurve(
        new Keyframe(0f,   0.2540645f, 0,0),
        new Keyframe(0.5f, 0.2140000f, 0,0),
        new Keyframe(1f,   0.2540645f, 0,0)
    );
    AnimationCurve bodyY = new AnimationCurve(
        new Keyframe(0f,   0.2554379f, 0,0),
        new Keyframe(0.5f, 0.1980000f, 0,0),
        new Keyframe(1f,   0.2554379f, 0,0)
    );

    AnimationCurve shLX = new AnimationCurve(
        new Keyframe(0f,   0.1880645f, 0,0),
        new Keyframe(0.5f, 0.1770000f, 0,0),
        new Keyframe(1f,   0.1880645f, 0,0)
    );
    AnimationCurve shLY = new AnimationCurve(
        new Keyframe(0f,   0.2794378f, 0,0),
        new Keyframe(0.5f, 0.2620000f, 0,0),
        new Keyframe(1f,   0.2794378f, 0,0)
    );

    void Start()
    {
        if (Head) _headBase = Head.localPosition;
        if (Body) _bodyBase = Body.localPosition;
        if (Shoulder_Left) _shoulderBase = Shoulder_Left.localPosition;

        if (Conductor != null)
            Conductor.OnBeatPhase += OnBeatPhase;
    }

    void OnDestroy()
    {
        if (Conductor != null)
            Conductor.OnBeatPhase -= OnBeatPhase;
    }

    void OnBeatPhase(float beatPhase)
    {
        float tNorm = Mathf.Repeat(beatPhase * BeatsPerLoop, 1f);

        Vector3 head = new Vector3(headX.Evaluate(tNorm), headY.Evaluate(tNorm), 0f);
        Vector3 body = new Vector3(bodyX.Evaluate(tNorm), bodyY.Evaluate(tNorm), 0f);
        Vector3 shl  = new Vector3(shLX.Evaluate(tNorm),  shLY.Evaluate(tNorm),  0f);

        if (Intensity != 1f)
        {
            head = LerpAround(head, new Vector3(0.4400645f, 0.4874378f, 0f), Intensity);
            body = LerpAround(body, new Vector3(0.2540645f, 0.2554379f, 0f), Intensity);
            shl  = LerpAround(shl,  new Vector3(0.1880645f, 0.2794378f, 0f), Intensity);
        }

        if (Additive)
        {
            if (Head) Head.localPosition = _headBase + (head - new Vector3(0.4400645f, 0.4874378f, 0f));
            if (Body) Body.localPosition = _bodyBase + (body - new Vector3(0.2540645f, 0.2554379f, 0f));
            if (Shoulder_Left) Shoulder_Left.localPosition = _shoulderBase + (shl - new Vector3(0.1880645f, 0.2794378f, 0f));
        }
        else
        {
            if (Head) Head.localPosition = head;
            if (Body) Body.localPosition = body;
            if (Shoulder_Left) Shoulder_Left.localPosition = shl;
        }
    }

    Vector3 LerpAround(Vector3 value, Vector3 center, float k)
    {
        return center + (value - center) * k;
    }
}
