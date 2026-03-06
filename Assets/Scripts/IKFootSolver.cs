using UnityEngine;

/// <summary>
/// Foot IK placement для Animation Rigging:
/// - позиция: двигаем LegIK_target
/// - вращение: крутим LegIK_rotation (MultiRotationConstraint повернёт кость стопы)
/// - отключаем обновление в воздухе / сразу после прыжка
/// - FIX: при повороте Model_Root по Y оси для spacing/offset не ломаются (учёт yaw через axisReference)
/// </summary>
public class IKFootPlacementRigging : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform body = default;                 // лучше всего: Model_Root (тот же, что Animator/RigBuilder)
    [SerializeField] private Transform footTarget = default;           // RightLegIK_target / LeftLegIK_target
    [SerializeField] private Transform footRotationDriver = default;   // RightLegIK_rotation / LeftLegIK_rotation
    [SerializeField] private RBCharacter25D character = default;       // контроллер (grounded/jump)

    [Header("Axis reference (важно при повороте Model_Root)")]
    [Tooltip("Трансформ, от которого берём yaw (поворот по Y) для осей, когда ignoreRootRotation=true. Обычно это Model_Root.")]
    [SerializeField] private Transform axisReference = default;

    [Header("Ground")]
    [SerializeField] private LayerMask terrainLayer = ~0;
    [SerializeField] private float raycastHeight = 1.0f;
    [SerializeField] private float raycastDownDistance = 2.0f;

    [Tooltip("Оффсет в осях (side/up/move): X=вбок, Y=вверх, Z=вперёд по moveAxis.")]
    [SerializeField] private Vector3 footOffset = new Vector3(0, 0.03f, 0);

    [Header("Rotation")]
    [SerializeField] private bool alignToGroundNormal = true;
    [Tooltip("Доп. оффсет к вращению стопы (Euler), если нужно подправить.")]
    [SerializeField] private Vector3 rotationOffsetEuler = Vector3.zero;

    [Header("Smoothing")]
    [SerializeField] private float positionLerpSpeed = 20f;
    [SerializeField] private float rotationLerpSpeed = 20f;

    [Header("Disable while airborne")]
    [SerializeField] private bool disableWhenNotGrounded = true;
    [SerializeField] private float disableAfterJumpTime = 0.12f;

    [Header("Axis / Rotation Independence")]
    [SerializeField] private bool ignoreRootRotation = true;
    [SerializeField] private Vector3 moveAxisWorld = Vector3.right;
    [SerializeField] private Vector3 moveAxisLocal = Vector3.right;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = false;

    private float footSpacing;
    private Quaternion plantedBaseRot;

    private Vector3 currentPos;
    private Quaternion currentRot;

    private void Reset()
    {
        character = GetComponentInParent<RBCharacter25D>();
    }

    private void Start() => Init();
    private void OnEnable() => Init();

    private void Init()
    {
        if (character == null)
            character = GetComponentInParent<RBCharacter25D>();

        if (axisReference == null)
            axisReference = body != null ? body : transform;

        if (footRotationDriver == null)
            footRotationDriver = transform;

        if (footTarget == null)
            footTarget = transform;

        currentPos = footTarget.position;
        currentRot = footRotationDriver.rotation;

        plantedBaseRot = footRotationDriver.rotation;

        if (!body) return;

        GetAxes(out _, out var sideDir, out _);

        // spacing считается в тех же осях, что и ray origin
        footSpacing = Vector3.Dot(footTarget.position - body.position, sideDir);
    }

    private void LateUpdate()
    {
        if (body == null || footTarget == null || footRotationDriver == null)
            return;

        // Не трогаем IK в воздухе / сразу после прыжка
        if (character != null)
        {
            bool airborne = disableWhenNotGrounded && !character.IsGroundedNow;
            bool justJumped = (Time.time - character.LastJumpTime) < disableAfterJumpTime;
            if (airborne || justJumped)
                return;
        }

        GetAxes(out var moveDir, out var sideDir, out var upDir);

        Vector3 origin = body.position + upDir * raycastHeight + sideDir * footSpacing;
        Vector3 dir = -upDir;
        float maxDist = raycastHeight + raycastDownDistance;

        Vector3 targetPos = currentPos;
        Quaternion targetRot = currentRot;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDist, terrainLayer, QueryTriggerInteraction.Ignore))
        {
            targetPos = hit.point
                        + sideDir * footOffset.x
                        + upDir * footOffset.y
                        + moveDir * footOffset.z;

            if (alignToGroundNormal)
            {
                Quaternion groundAligned = AlignUpKeepForward(plantedBaseRot, hit.normal);
                targetRot = groundAligned * Quaternion.Euler(rotationOffsetEuler);
            }
            else
            {
                targetRot = plantedBaseRot * Quaternion.Euler(rotationOffsetEuler);
            }

            if (drawDebug)
            {
                Debug.DrawRay(origin, dir * maxDist, Color.red);
                Debug.DrawLine(hit.point, hit.point + hit.normal * 0.25f, Color.green);
            }
        }
        else if (drawDebug)
        {
            Debug.DrawRay(origin, dir * maxDist, Color.red);
        }

        float tPos = 1f - Mathf.Exp(-positionLerpSpeed * Time.deltaTime);
        float tRot = 1f - Mathf.Exp(-rotationLerpSpeed * Time.deltaTime);

        currentPos = Vector3.Lerp(currentPos, targetPos, tPos);
        currentRot = Quaternion.Slerp(currentRot, targetRot, tRot);

        footTarget.position = currentPos;
        footRotationDriver.rotation = currentRot;
    }

    private static Quaternion AlignUpKeepForward(Quaternion baseRot, Vector3 desiredUp)
    {
        Vector3 up = desiredUp.sqrMagnitude > 0.0001f ? desiredUp.normalized : Vector3.up;

        Vector3 fwd = baseRot * Vector3.forward;
        Vector3 fwdOnPlane = Vector3.ProjectOnPlane(fwd, up);

        if (fwdOnPlane.sqrMagnitude < 1e-6f)
        {
            Vector3 right = baseRot * Vector3.right;
            fwdOnPlane = Vector3.ProjectOnPlane(right, up);
            if (fwdOnPlane.sqrMagnitude < 1e-6f)
                fwdOnPlane = Vector3.ProjectOnPlane(Vector3.forward, up);
        }

        return Quaternion.LookRotation(fwdOnPlane.normalized, up);
    }

    private void GetAxes(out Vector3 moveDir, out Vector3 sideDir, out Vector3 upDir)
    {
        if (ignoreRootRotation)
        {
            // мир-вверх всегда вверх
            upDir = Vector3.up;

            Vector3 baseMove = (moveAxisWorld.sqrMagnitude > 0.0001f) ? moveAxisWorld.normalized : Vector3.right;

            // FIX: учитываем yaw от axisReference (обычно Model_Root), иначе при его повороте spacing ломается
            float yaw = axisReference ? axisReference.eulerAngles.y : 0f;
            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);

            moveDir = (yawRot * baseMove).normalized;

            sideDir = Vector3.Cross(upDir, moveDir).normalized;
            if (sideDir.sqrMagnitude < 0.0001f) sideDir = Vector3.forward;
        }
        else
        {
            // полностью в локале body
            upDir = body.up;

            moveDir = body.TransformDirection(moveAxisLocal).normalized;
            if (moveDir.sqrMagnitude < 0.0001f) moveDir = body.right;

            sideDir = Vector3.Cross(upDir, moveDir).normalized;
            if (sideDir.sqrMagnitude < 0.0001f) sideDir = body.forward;
        }
    }
}