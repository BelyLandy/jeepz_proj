using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Transform playerController;

    [Header("IK Targets (Transforms)")]
    [SerializeField] private Transform leftFootIKTarget;
    [SerializeField] private Transform rightFootIKTarget;
    [SerializeField] private Transform pelvisIKTarget;

    [Header("Rig Constraints")]
    [SerializeField] private MultiPositionConstraint pelvisConstraint;                 // hips position constraint
    [SerializeField] private TwoBoneIKConstraint leftFootIK;                          // left leg IK constraint
    [SerializeField] private TwoBoneIKConstraint rightFootIK;                         // right leg IK constraint
    [SerializeField] private MultiRotationConstraint leftFootRotationIKConstraintTarget;
    [SerializeField] private MultiRotationConstraint rightFootRotationIKConstraintTarget;

    [Header("Raycast")]
    [SerializeField] private LayerMask ikLayerMask = ~0;
    [SerializeField] private float distanceToGround = 0.15f;

    [Header("Animator Params")]
    [SerializeField] private string leftFootWeightParam = "IK_LeftFootWeight";
    [SerializeField] private string rightFootWeightParam = "IK_RightFootWeight";

    [Header("Rotation Offsets (Euler)")]
    [SerializeField] private Vector3 leftFootRotationOffset;
    [SerializeField] private Vector3 rightFootRotationOffset;

    [Header("Toggles")]
    [SerializeField] private bool isAllowedToUseFootIK = true;
    public bool affectHip = true;

    // Smoothing (как на скринах)
    private float smoothedHipsWeight = 0f;
    private float smoothLeftFootWeight = 0f;
    private float smoothRightFootWeight = 0f;

    // Hips IK (как на скринах)
    private float baseHipsPositionY;
    private float lowestFootY;
    private float hipsTargetY;
    private float hipsCurrentY;
    private Vector3 currentHipsPosition;
    private bool hipsBaseInitialized;

    private void Reset()
    {
        playerAnimator = GetComponentInChildren<Animator>();
        playerController = transform;
    }

    private void Awake()
    {
        if (!playerAnimator) playerAnimator = GetComponentInChildren<Animator>();
        if (!playerController) playerController = transform;

        if (pelvisIKTarget)
            hipsCurrentY = pelvisIKTarget.position.y;
    }

    // Надежнее вызывать IK здесь (Animator IK pass)
    private void OnAnimatorIK(int layerIndex)
    {
        if (!playerAnimator) return;
        UpdateFootIK();
    }

    private void UpdateFootIK()
    {
        //Casting Ray towards ground from left foot.
        Ray rayLeft = new Ray(playerAnimator.GetIKPosition(AvatarIKGoal.LeftFoot) + Vector3.up, Vector3.down);
        bool isRayCastHitLeftFoot = Physics.Raycast(rayLeft, out RaycastHit leftFootHit, distanceToGround + 2f, ikLayerMask);

        //Casting Ray towards ground from right foot.
        Ray rayRight = new Ray(playerAnimator.GetIKPosition(AvatarIKGoal.RightFoot) + Vector3.up, Vector3.down);
        bool isRayCastHitRightFoot = Physics.Raycast(rayRight, out RaycastHit rightFootHit, distanceToGround + 2f, ikLayerMask);

        //IK functions
        if (isAllowedToUseFootIK)
        {
            //SetWeight
            SetWeightOfConstraint(leftFootHit, rightFootHit);
            FootIK(isRayCastHitLeftFoot, isRayCastHitRightFoot, leftFootHit, rightFootHit);
            HipsIK(isRayCastHitLeftFoot, isRayCastHitRightFoot, leftFootHit, rightFootHit);
        }
        else
        {
            // если отключили IK — аккуратно загасим веса
            smoothedHipsWeight = Mathf.Lerp(smoothedHipsWeight, 0f, Time.deltaTime * 10f);
            smoothLeftFootWeight = Mathf.Lerp(smoothLeftFootWeight, 0f, Time.deltaTime * 20f);
            smoothRightFootWeight = Mathf.Lerp(smoothRightFootWeight, 0f, Time.deltaTime * 20f);
            ApplyWeights();
        }
    }

    public void SetWeightOfConstraint(RaycastHit leftFootHit, RaycastHit rightFootHit)
    {
        //Calculate average slope angle value on the basis of players feet. its gone help for indepth polish.
        float leftSlope = Vector3.Angle(Vector3.up, leftFootHit.normal);
        float rightSlope = Vector3.Angle(Vector3.up, rightFootHit.normal);
        float averageSlopeAngle = (leftSlope + rightSlope) * 0.5f;
        float slopNormalizedValue = averageSlopeAngle / 90f; //90 degree is 100% slop angle.

        //Calculate a weight on the basis of feet height difference for constraints.
        float targetWeight = Mathf.Clamp01(Mathf.Abs(leftFootHit.point.y - rightFootHit.point.y) / 0.3f); //0.3f is maxHeightDifference.

        //Ignore tiny variations
        if (targetWeight < 0.01f) targetWeight = 0f;

        smoothedHipsWeight = Mathf.Lerp(smoothedHipsWeight, targetWeight, Time.deltaTime * 10f);

        //Feet position
        float currentLeftFootY = playerAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y;
        float currentRightFootY = playerAnimator.GetBoneTransform(HumanBodyBones.RightFoot).position.y;

        float leftFootWeight = Mathf.Clamp01(playerAnimator.GetFloat(leftFootWeightParam) + slopNormalizedValue);
        float rightFootWeight = Mathf.Clamp01(playerAnimator.GetFloat(rightFootWeightParam) + slopNormalizedValue);

        if (currentLeftFootY < leftFootHit.point.y)
        {
            smoothLeftFootWeight = Mathf.Lerp(smoothLeftFootWeight, 1f, Time.deltaTime * 20f);
        }
        else
        {
            smoothLeftFootWeight = Mathf.Lerp(smoothLeftFootWeight, leftFootWeight, Time.deltaTime * 20f);
        }

        if (currentRightFootY < rightFootHit.point.y)
        {
            smoothRightFootWeight = Mathf.Lerp(smoothRightFootWeight, 1f, Time.deltaTime * 20f);
        }
        else
        {
            smoothRightFootWeight = Mathf.Lerp(smoothRightFootWeight, rightFootWeight, Time.deltaTime * 20f);
        }

        //Palvis and Feet position weights
        ApplyWeights();
    }

    private void ApplyWeights()
    {
        if (pelvisConstraint)
            pelvisConstraint.weight = Mathf.Clamp(smoothedHipsWeight, 0f, 0.95f); //Hips

        if (leftFootIK)
            leftFootIK.weight = Mathf.Clamp(smoothLeftFootWeight, 0f, 0.95f); //Left foot.

        if (rightFootIK)
            rightFootIK.weight = Mathf.Clamp(smoothRightFootWeight, 0f, 0.95f); //Right foot.

        //Feet rotation weights
        if (leftFootRotationIKConstraintTarget)
            leftFootRotationIKConstraintTarget.weight = Mathf.Clamp(smoothLeftFootWeight, 0f, 0.95f);

        if (rightFootRotationIKConstraintTarget)
            rightFootRotationIKConstraintTarget.weight = Mathf.Clamp(smoothRightFootWeight, 0f, 0.95f);
    }

    private void FootIK(bool isRayCastHitLeftFoot, bool isRayCastHitRightFoot, RaycastHit leftFootHit, RaycastHit rightFootHit)
    {
        if (!playerAnimator) return;

        //Left foot===========================================>
        if (isRayCastHitLeftFoot && leftFootIKTarget)
        {
            //Position================
            float currentLeftFootY = playerAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y;
            Vector3 footPosition = leftFootHit.point;

            if (currentLeftFootY < leftFootHit.point.y)
                footPosition.y += distanceToGround + 0.1f;
            else
                footPosition.y += distanceToGround;

            leftFootIKTarget.position = footPosition;

            //Rotation================
            if (leftFootRotationIKConstraintTarget)
            {
                Quaternion footRotationOffset = Quaternion.Euler(leftFootRotationOffset);
                Vector3 forward = playerController ? playerController.forward : transform.forward;
                Vector3 footForward = Vector3.ProjectOnPlane(forward, leftFootHit.normal).normalized;
                if (footForward.sqrMagnitude < 0.0001f)
                    footForward = Vector3.ProjectOnPlane(transform.forward, leftFootHit.normal).normalized;

                Quaternion footRotation = Quaternion.LookRotation(footForward, leftFootHit.normal) * footRotationOffset;

                leftFootRotationIKConstraintTarget.transform.rotation =
                    Quaternion.Slerp(leftFootRotationIKConstraintTarget.transform.rotation, footRotation, Time.deltaTime * 10f);
            }
        }

        //Right foot===========================================>
        if (isRayCastHitRightFoot && rightFootIKTarget)
        {
            //Position================
            float currentRightFootY = playerAnimator.GetBoneTransform(HumanBodyBones.RightFoot).position.y;
            Vector3 footPosition = rightFootHit.point;

            if (currentRightFootY < rightFootHit.point.y)
                footPosition.y += distanceToGround + 0.1f;
            else
                footPosition.y += distanceToGround;

            rightFootIKTarget.position = footPosition;

            //Rotation================
            if (rightFootRotationIKConstraintTarget)
            {
                Quaternion footRotationOffset = Quaternion.Euler(rightFootRotationOffset);
                Vector3 forward = playerController ? playerController.forward : transform.forward;
                Vector3 footForward = Vector3.ProjectOnPlane(forward, rightFootHit.normal).normalized;
                if (footForward.sqrMagnitude < 0.0001f)
                    footForward = Vector3.ProjectOnPlane(transform.forward, rightFootHit.normal).normalized;

                Quaternion footRotation = Quaternion.LookRotation(footForward, rightFootHit.normal) * footRotationOffset;

                rightFootRotationIKConstraintTarget.transform.rotation =
                    Quaternion.Slerp(rightFootRotationIKConstraintTarget.transform.rotation, footRotation, Time.deltaTime * 10f);
            }
        }
    }

    private void HipsIK(bool isRayCastHitLeftFoot, bool isRayCastHitRightFoot, RaycastHit leftFootHit, RaycastHit rightFootHit)
    {
        if (!affectHip) return;
        if (!playerAnimator) return;
        if (!pelvisIKTarget) return;

        if (isRayCastHitLeftFoot && isRayCastHitRightFoot) //if 2 feet raycast hit ground then only proceed.
        {
            // Get foot hits Y
            float leftY = leftFootHit.point.y;
            float rightY = rightFootHit.point.y;

            lowestFootY = Mathf.Min(leftY, rightY);

            // один раз фиксируем "базовую" высоту таза относительно пола
            if (!hipsBaseInitialized)
            {
                baseHipsPositionY = pelvisIKTarget.position.y - lowestFootY;
                hipsCurrentY = pelvisIKTarget.position.y;
                hipsBaseInitialized = true;
            }

            hipsTargetY = baseHipsPositionY + lowestFootY;

            if (Mathf.Abs(hipsTargetY - hipsCurrentY) > 0.01f) // Deadzone to prevent tiny jitters
            {
                hipsCurrentY = Mathf.Lerp(hipsCurrentY, hipsTargetY, Time.deltaTime * 15f);
            }

            pelvisIKTarget.position = new Vector3(pelvisIKTarget.position.x, hipsCurrentY, pelvisIKTarget.position.z);

            // Store for reference
            currentHipsPosition = playerAnimator.bodyPosition;
        }
    }
}