using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAnimController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private RotationAnim rotationAnim;
    [SerializeField] private Transform rotationSource;

    [Header("Animator Params")]
    [SerializeField] private string idleMirrorParam = "IdleMirror";
    [SerializeField] private float idleMirrorDampTime = 0.1f;

    void Reset()
    {
        rotationAnim = GetComponent<RotationAnim>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (rotationSource == null)
            rotationSource = transform;
    }

    void Awake()
    {
        if (rotationAnim == null)
            rotationAnim = GetComponent<RotationAnim>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (rotationSource == null)
            rotationSource = transform;
    }

    void LateUpdate()
    {
        if (animator == null || rotationAnim == null || rotationSource == null)
            return;

        float currentYaw = rotationSource.eulerAngles.y;

        // rightYaw  -> +1
        // leftYaw   -> -1
        float idleMirror = YawToIdleMirror(
            currentYaw,
            rotationAnim.leftYaw,
            rotationAnim.rightYaw
        );

        animator.SetFloat(idleMirrorParam, idleMirror, idleMirrorDampTime, Time.deltaTime);
    }

    private float YawToIdleMirror(float currentYaw, float leftYaw, float rightYaw)
    {
        float totalDelta = Mathf.DeltaAngle(rightYaw, leftYaw);

        if (Mathf.Abs(totalDelta) < 0.0001f)
            return 1f;

        float currentDelta = Mathf.DeltaAngle(rightYaw, currentYaw);

        float t = Mathf.Clamp01(currentDelta / totalDelta);

        // t = 0  => rightYaw => +1
        // t = 1  => leftYaw  => -1
        return Mathf.Lerp(1f, -1f, t);
    }
}