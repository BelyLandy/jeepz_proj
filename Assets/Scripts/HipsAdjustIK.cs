using UnityEngine;
using UnityEngine.Animations.Rigging;

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public class HipsAdjustIK : MonoBehaviour
{
    [System.Serializable]
    public class LegSetup
    {
        public string label = "Leg";
        public bool enabled = true;

        [Header("IK")]
        public TwoBoneIKConstraint legIK;
        public Transform upperBone;   // Hip.R / Hip.L bone
        public Transform lowerBone;   // Knee.R / Knee.L bone
        public Transform footBone;    // Foot.R / Foot.L bone
        public Transform ikTarget;    // RightLegIK_target / LeftLegIK_target

        [Header("Influence")]
        [Range(0f, 1f)] public float influence = 1f;

        [HideInInspector] public float cachedUpperLength;
        [HideInInspector] public float cachedLowerLength;

        public bool IsValid =>
            enabled &&
            upperBone != null &&
            lowerBone != null &&
            footBone != null &&
            ikTarget != null;

        public void CacheLengths()
        {
            if (upperBone != null && lowerBone != null)
                cachedUpperLength = Vector3.Distance(upperBone.position, lowerBone.position);

            if (lowerBone != null && footBone != null)
                cachedLowerLength = Vector3.Distance(lowerBone.position, footBone.position);
        }

        public float GetUpperLength()
        {
            if (upperBone != null && lowerBone != null)
                return Vector3.Distance(upperBone.position, lowerBone.position);

            return cachedUpperLength;
        }

        public float GetLowerLength()
        {
            if (lowerBone != null && footBone != null)
                return Vector3.Distance(lowerBone.position, footBone.position);

            return cachedLowerLength;
        }

        public float GetIKWeight()
        {
            if (legIK != null)
                return legIK.weight;

            return 1f;
        }
    }

    [Header("Refs")]
    [SerializeField] private Transform hipsAdjustTarget;
    [SerializeField] private Transform rootSpace;
    [SerializeField] private MultiPositionConstraint hipsConstraint;

    [Header("Legs")]
    [SerializeField] private LegSetup rightLeg = new LegSetup { label = "Right Leg" };
    [SerializeField] private LegSetup leftLeg = new LegSetup { label = "Left Leg" };

    [Header("Reach settings")]
    [Range(0.85f, 1.05f)]
    [SerializeField] private float legReachMultiplier = 0.98f;

    [SerializeField, Min(0f)] private float keepKneeBendDistance = 0.02f;

    [SerializeField, Min(0f)] private float pelvisResponse = 1f;

    [SerializeField, Min(0f)] private float maxHipDrop = 0.35f;

    [SerializeField] private bool allowHipRise = false;

    [SerializeField, Min(0f)] private float maxHipRise = 0.05f;

    [SerializeField] private bool useMaxLegDemand = true;

    [SerializeField, Range(0f, 1f)] private float minLegIKWeightToAffectHips = 0.05f;

    [Header("Smoothing")]
    [SerializeField, Min(0f)] private float downLerpSpeed = 18f;
    [SerializeField, Min(0f)] private float upLerpSpeed = 10f;
    [SerializeField, Min(0f)] private float maxMoveSpeed = 2.5f;
    [SerializeField, Min(0f)] private float deadZone = 0.001f;

    [Header("Constraint weight")]
    [SerializeField, Min(0f)] private float weightLerpSpeed = 10f;

    [Header("Manual offsets")]
    [SerializeField] private Vector3 additionalLocalOffset = Vector3.zero;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;

    private Vector3 baseLocalPosition;
    private float currentHipOffset;
    private bool hasBasePose;

    private void Reset()
    {
        hipsAdjustTarget = transform;
    }

    private void Awake()
    {
        CacheRefs();
        CacheLegLengths();
        CaptureBasePose();
    }

    private void OnEnable()
    {
        CacheRefs();
        CacheLegLengths();
        CaptureBasePose();
    }

    private void OnValidate()
    {
        CacheRefs();
    }

    [ContextMenu("Capture Base Pose")]
    public void CaptureBasePose()
    {
        CacheRefs();

        if (hipsAdjustTarget == null)
            hipsAdjustTarget = transform;

        if (rootSpace == null)
        {
            if (hipsAdjustTarget.parent != null)
                rootSpace = hipsAdjustTarget.parent;
            else
                rootSpace = hipsAdjustTarget;
        }

        baseLocalPosition = rootSpace.InverseTransformPoint(hipsAdjustTarget.position);
        currentHipOffset = 0f;
        hasBasePose = true;
    }

    [ContextMenu("Cache Leg Lengths")]
    public void CacheLegLengths()
    {
        rightLeg?.CacheLengths();
        leftLeg?.CacheLengths();
    }

    private void CacheRefs()
    {
        if (hipsAdjustTarget == null)
            hipsAdjustTarget = transform;
    }

    private void Update()
    {
        if (!hasBasePose)
            CaptureBasePose();

        float desiredOffset = ComputeDesiredHipOffset();
        ApplyHipOffset(desiredOffset);
        UpdateConstraintWeight(1f);
    }

    private float ComputeDesiredHipOffset()
    {
        bool hasRight = TryGetLegDemand(rightLeg, out float rightDemand);
        bool hasLeft = TryGetLegDemand(leftLeg, out float leftDemand);

        if (!hasRight && !hasLeft)
            return 0f;

        float desired;

        if (useMaxLegDemand)
        {
            desired = Mathf.Max(rightDemand, leftDemand);
        }
        else
        {
            float total = 0f;
            float weight = 0f;

            if (hasRight)
            {
                total += rightDemand;
                weight += 1f;
            }

            if (hasLeft)
            {
                total += leftDemand;
                weight += 1f;
            }

            desired = weight > 0f ? total / weight : 0f;
        }

        desired *= pelvisResponse;

        float min = allowHipRise ? -maxHipRise : 0f;
        float max = maxHipDrop;

        return Mathf.Clamp(desired, min, max);
    }

    private bool TryGetLegDemand(LegSetup leg, out float demand)
    {
        demand = 0f;

        if (leg == null || !leg.IsValid || leg.influence <= 0f)
            return false;

        float ikWeight = leg.GetIKWeight();
        if (ikWeight < minLegIKWeightToAffectHips)
            return false;

        float upperLength = leg.GetUpperLength();
        float lowerLength = leg.GetLowerLength();
        float maxReach = (upperLength + lowerLength) * legReachMultiplier - keepKneeBendDistance;

        if (maxReach <= 0f)
            return false;

        float currentDistance = Vector3.Distance(leg.upperBone.position, leg.ikTarget.position);

        // ���� ���� �� ������ � ��� ���� �������� ����.
        float overExtension = currentDistance - maxReach;
        demand = Mathf.Max(0f, overExtension) * ikWeight * leg.influence;

        if (drawDebug)
        {
            Color lineColor = overExtension > 0f ? Color.red : Color.green;
            Debug.DrawLine(leg.upperBone.position, leg.ikTarget.position, lineColor);
        }

        return true;
    }

    private void ApplyHipOffset(float desiredOffset)
    {
        if (hipsAdjustTarget == null || rootSpace == null)
            return;

        float limitedDesired = desiredOffset;

        if (Application.isPlaying && maxMoveSpeed > 0f)
        {
            limitedDesired = Mathf.MoveTowards(
                currentHipOffset,
                desiredOffset,
                maxMoveSpeed * Time.deltaTime
            );
        }

        if (Application.isPlaying)
        {
            float speed = limitedDesired > currentHipOffset ? downLerpSpeed : upLerpSpeed;

            if (speed > 0f)
            {
                float t = GetDampFactor(speed);
                currentHipOffset = Mathf.Lerp(currentHipOffset, limitedDesired, t);
            }
            else
            {
                currentHipOffset = limitedDesired;
            }
        }
        else
        {
            currentHipOffset = limitedDesired;
        }

        if (Mathf.Abs(currentHipOffset - desiredOffset) <= deadZone)
            currentHipOffset = desiredOffset;

        Vector3 local = baseLocalPosition + additionalLocalOffset;
        local.y -= currentHipOffset;

        hipsAdjustTarget.position = rootSpace.TransformPoint(local);
    }

    private void UpdateConstraintWeight(float targetWeight)
    {
        if (hipsConstraint == null)
            return;

        if (!Application.isPlaying || weightLerpSpeed <= 0f)
        {
            hipsConstraint.weight = targetWeight;
            return;
        }

        float t = GetDampFactor(weightLerpSpeed);
        hipsConstraint.weight = Mathf.Lerp(hipsConstraint.weight, targetWeight, t);
    }

    private float GetDampFactor(float speed)
    {
        return 1f - Mathf.Exp(-speed * Time.deltaTime);
    }
}