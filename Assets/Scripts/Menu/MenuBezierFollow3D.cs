using UnityEngine;

[DisallowMultipleComponent]
public sealed class MenuBezierFollow3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MenuBezierRoute3D route;

    [Header("Progress")]
    [SerializeField, Min(0f)] private float cyclesPerSecond = 0.15f;
    [SerializeField, Range(0f, 1f)] private float startT = 0f;

    [Header("Loop / Direction")]
    [SerializeField] private bool loop = true;
    [SerializeField] private bool pingPong = false;
    [SerializeField] private bool reverse = false;

    [Header("Position")]
    [SerializeField] private bool useLocalPosition = false;

    [Header("Rotation")]
    [SerializeField] private bool rotateToPath = false;
    [SerializeField, Min(0f)] private float rotationSmoothing = 10f;

    [Header("Time")]
    [SerializeField] private bool useUnscaledTime = true;

    private float progress01;
    private int directionSign = 1;

    private void OnEnable()
    {
        progress01 = Mathf.Clamp01(startT);
        directionSign = reverse ? -1 : 1;
        ApplyCurrentPosition(forceRotation: true, deltaTime: 0f);
    }

    private void OnValidate()
    {
        cyclesPerSecond = Mathf.Max(0f, cyclesPerSecond);
        rotationSmoothing = Mathf.Max(0f, rotationSmoothing);
        startT = Mathf.Clamp01(startT);
    }

    private void Update()
    {
        if (route == null || !route.HasValidRoute)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f)
            return;

        AdvanceProgress(dt);
        ApplyCurrentPosition(forceRotation: false, deltaTime: dt);
    }

    private void AdvanceProgress(float dt)
    {
        if (cyclesPerSecond <= 0f)
            return;

        float delta = cyclesPerSecond * dt * directionSign;
        progress01 += delta;

        if (pingPong)
        {
            while (progress01 > 1f || progress01 < 0f)
            {
                if (progress01 > 1f)
                {
                    progress01 = 2f - progress01;
                    directionSign *= -1;
                }
                else if (progress01 < 0f)
                {
                    progress01 = -progress01;
                    directionSign *= -1;
                }
            }

            progress01 = Mathf.Clamp01(progress01);
            return;
        }

        if (loop)
        {
            progress01 = Mathf.Repeat(progress01, 1f);
            return;
        }

        progress01 = Mathf.Clamp01(progress01);
    }

    private void ApplyCurrentPosition(bool forceRotation, float deltaTime)
    {
        Vector3 worldPosition = route.EvaluatePosition(progress01);
        if (useLocalPosition && transform.parent != null)
            transform.localPosition = transform.parent.InverseTransformPoint(worldPosition);
        else
            transform.position = worldPosition;

        if (!rotateToPath)
            return;

        Vector3 tangent = route.EvaluateTangent(progress01);
        if (tangent.sqrMagnitude <= 0.000001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
        if (forceRotation || rotationSmoothing <= 0f || deltaTime <= 0f)
        {
            transform.rotation = targetRotation;
            return;
        }

        float lerpFactor = 1f - Mathf.Exp(-rotationSmoothing * deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lerpFactor);
    }
}
