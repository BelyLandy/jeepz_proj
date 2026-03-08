using UnityEngine;
using Unity.Cinemachine;

public class ComposerOffsetByRotation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RotationAnim rotationAnim;
    [SerializeField] private Transform heroTransform;
    [SerializeField] private CinemachinePositionComposer positionComposer;

    [Header("Target Offset for sides")]
    [SerializeField] private Vector3 leftTargetOffset = new Vector3(1f, 0f, 0f);
    [SerializeField] private Vector3 rightTargetOffset = new Vector3(-1f, 0f, 0f);

    [Header("Extra smoothing")]
    [SerializeField] private bool useExtraSmoothing = false;
    [SerializeField] private float baseSmoothSpeed = 5f;

    [SerializeField]
    private AnimationCurve smoothSpeedByTurnProgress =
        new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.5f, 1.5f),
            new Keyframe(1f, 0.5f)
        );

    private Vector3 currentOffset;

    void Reset()
    {
        positionComposer = GetComponent<CinemachinePositionComposer>();
    }

    void Awake()
    {
        if (rotationAnim == null)
            rotationAnim = FindFirstObjectByType<RotationAnim>();

        if (heroTransform == null && rotationAnim != null)
            heroTransform = rotationAnim.transform;

        if (positionComposer == null)
            positionComposer = GetComponent<CinemachinePositionComposer>();

        if (positionComposer != null)
            currentOffset = positionComposer.TargetOffset;
    }

    void LateUpdate()
    {
        if (rotationAnim == null || heroTransform == null || positionComposer == null)
            return;

        float currentYaw = heroTransform.eulerAngles.y;

        float yawT = GetYawLerp01(currentYaw, rotationAnim.rightYaw, rotationAnim.leftYaw);

        Vector3 rawTargetOffset = Vector3.Lerp(rightTargetOffset, leftTargetOffset, yawT);

        if (useExtraSmoothing)
        {
            float turnProgress = rotationAnim.FacingSign < 0 ? yawT : 1f - yawT;

            float curveMultiplier = Mathf.Max(0f, smoothSpeedByTurnProgress.Evaluate(turnProgress));
            float effectiveSpeed = baseSmoothSpeed * curveMultiplier;

            float k = 1f - Mathf.Exp(-effectiveSpeed * Time.deltaTime);
            currentOffset = Vector3.Lerp(currentOffset, rawTargetOffset, k);

            positionComposer.TargetOffset = currentOffset;
        }
        else
        {
            positionComposer.TargetOffset = rawTargetOffset;
            currentOffset = rawTargetOffset;
        }
    }

    private float GetYawLerp01(float currentYaw, float fromYaw, float toYaw)
    {
        float totalDelta = Mathf.DeltaAngle(fromYaw, toYaw);

        if (Mathf.Abs(totalDelta) < 0.0001f)
            return 0f;

        float currentDelta = Mathf.DeltaAngle(fromYaw, currentYaw);
        return Mathf.Clamp01(currentDelta / totalDelta);
    }
}