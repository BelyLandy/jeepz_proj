using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;

public class IKFootPlacement : MonoBehaviour
{
    [Header("Two Bone IK Constraints")]
    public List<TwoBoneIKConstraint> iKConstraints = new List<TwoBoneIKConstraint>();

    [Header("Hips Transform")]
    public Transform hips;

    [Header("Raycast Properties")]
    public float raycastHeight = 0.5f;
    public float raycastLength = 1f;
    public LayerMask groundMask = ~0;

    [Header("Feet Offset Weight")]
    [Range(0, 1)] public float feetPositionOffsetWeight = 1f;
    [Range(0, 1)] public float feetRotationOffsetWeight = 1f;

    [Header("Feet Offset Parameters")]
    [Range(0, 5)] public float feetPositionOffsetSmoothing = 0.035f;
    [Range(0, 5)] public float feetRotationOffsetSmoothing = 0.1f;

    [Header("Body Offset Weight")]
    [Range(0, 1)] public float bodyPositionOffsetWeight = 1f;
    [Range(0, 1)] public float bodyRotationOffsetWeight = 0f;

    [Header("Body Offset Parameters")]
    [Range(0, 5)] public float bodyPositionOffsetSmoothing = 0.035f;
    [Range(0, 5)] public float bodyRotationOffsetSmoothing = 0.2f;
    public bool invertBodyPositionOffset = false;
    public bool invertBodyRotationOffset = false;

    [Header("Stationary to Walk Feet Adjustment")]
    [Range(0, 5)] public float stationaryToWalkSmoothing = 0.2f;

    [Header("Stationary to Rotate Feet Adjustment")]
    [Range(0, 5)] public float stationaryToRotateSmoothing = 0.06f;
    [Range(0, 360)] public float maxStationaryRotationAngle = 60f;
    public bool invertAdjustmentDirection = false;

    [Header("Debug Rays")]
    public bool drawDebugRay = true;

    [HideInInspector] public float lastStationaryRotation = 0f;
    private float stationaryRotation = 0f;

    [HideInInspector] public bool isActive = true;
    [HideInInspector] public bool startup = true;
    [HideInInspector] public bool jumped = false;
    [HideInInspector] public bool isMoving = true;
    [HideInInspector] public bool isGrounded = true;

    private Animator animator;
    private RigBuilder rigBuilder;
    private PlayableGraph rigBuilderGraph;
    private AnimationScriptPlayable animationScriptPlayable;
    private IKFootPlacementJob iKFootPlacementJob;
    private Playable originalSourcePlayable;

    private void OnEnable()
    {
        startup = true;
        animator = GetComponent<Animator>();
        rigBuilder = GetComponent<RigBuilder>();

        if (animator == null)
        {
            Debug.LogError("Animator component is missing in this GameObject!");
            DisableIKFootPlacement();
            return;
        }

        if (rigBuilder == null)
        {
            Debug.LogError("Rig Builder component is missing in this GameObject!");
            DisableIKFootPlacement();
            return;
        }

        animator.Rebind();
        rigBuilder.Clear();
        if (!rigBuilder.Build())
        {
            Debug.LogError("Rig Builder failed to rebuild its graph.");
            DisableIKFootPlacement();
        }
    }

    private void OnDisable()
    {
        if (rigBuilder != null)
        {
            PlayableGraph graph = rigBuilder.graph;
            if (graph.IsValid())
            {
                var output = graph.GetOutputByType<AnimationPlayableOutput>(0);
                if (output.IsOutputValid() && originalSourcePlayable.IsValid())
                {
                    output.SetSourcePlayable(originalSourcePlayable);
                }
            }
        }

        animationScriptPlayable = default;
        originalSourcePlayable = default;
    }

    private void Update()
    {
        if (startup)
        {
            Startup();
        }

        if (!animationScriptPlayable.IsValid())
            return;

        iKFootPlacementJob = animationScriptPlayable.GetJobData<IKFootPlacementJob>();

        CheckIfLanded();
        CheckParameters();
        CheckRotations();
        SetJobParameters();
        if (isActive)
        {
            GetRaycastData();
        }

        animationScriptPlayable.SetJobData(iKFootPlacementJob);
    }

    private void Startup()
    {
        if (hips == null)
        {
            Debug.LogError("The script is missing required Hips reference!");
            DisableIKFootPlacement();
            return;
        }

        if (iKConstraints == null || iKConstraints.Count == 0)
        {
            Debug.LogError("The script is missing required Two Bone IK constraints!");
            DisableIKFootPlacement();
            return;
        }

        for (int i = 0; i < iKConstraints.Count; i++)
        {
            if (!IsConstraintValid(iKConstraints[i], i))
            {
                DisableIKFootPlacement();
                return;
            }
        }

        rigBuilderGraph = rigBuilder.graph;
        if (!rigBuilderGraph.IsValid())
        {
            Debug.LogError("Rig Builder graph is invalid.");
            DisableIKFootPlacement();
            return;
        }

        var existingOutput = rigBuilderGraph.GetOutputByType<AnimationPlayableOutput>(0);
        if (!existingOutput.IsOutputValid())
        {
            Debug.LogError("Could not find an existing AnimationPlayableOutput in the Rig Builder graph!");
            DisableIKFootPlacement();
            return;
        }

        originalSourcePlayable = existingOutput.GetSourcePlayable();
        if (!originalSourcePlayable.IsValid())
        {
            Debug.LogError("Rig Builder output has no valid source playable.");
            DisableIKFootPlacement();
            return;
        }

        iKFootPlacementJob = new IKFootPlacementJob();
        if (!iKFootPlacementJob.Create(iKConstraints.Count))
        {
            Debug.LogError("Failed to create new Animation Job!");
            DisableIKFootPlacement();
            return;
        }

        iKFootPlacementJob.hips = animator.BindStreamTransform(hips);
        for (int i = 0; i < iKConstraints.Count; i++)
        {
            iKFootPlacementJob.targets[i] = animator.BindStreamTransform(iKConstraints[i].data.target);
            iKFootPlacementJob.hints[i] = animator.BindStreamTransform(iKConstraints[i].data.hint);
        }

        animationScriptPlayable = AnimationScriptPlayable.Create(rigBuilderGraph, iKFootPlacementJob);
        animationScriptPlayable.AddInput(originalSourcePlayable, 0, 1.0f);
        existingOutput.SetSourcePlayable(animationScriptPlayable);

        startup = false;
    }

    private bool IsConstraintValid(TwoBoneIKConstraint constraint, int index)
    {
        if (constraint == null)
        {
            Debug.LogError($"IK constraint at index {index} is null.");
            return false;
        }

        if (constraint.data.root == null || constraint.data.mid == null || constraint.data.tip == null)
        {
            Debug.LogError($"IK constraint '{constraint.name}' is missing root/mid/tip references.");
            return false;
        }

        if (constraint.data.target == null || constraint.data.hint == null)
        {
            Debug.LogError($"IK constraint '{constraint.name}' is missing target/hint references.");
            return false;
        }

        if (!constraint.data.target.IsChildOf(transform) || !constraint.data.hint.IsChildOf(transform))
        {
            Debug.LogError($"IK constraint '{constraint.name}' target and hint must stay inside the Animator hierarchy.");
            return false;
        }

        return true;
    }

    private void GetRaycastData()
    {
        if (!jumped && isGrounded)
        {
            RaycastHit bodyRaycastHit;
            Vector3 bodyRaycastOrigin = new Vector3(hips.position.x, transform.position.y + raycastHeight, hips.position.z);
            bool bodyHit = false;

            if (bodyPositionOffsetWeight > 0 || bodyRotationOffsetWeight > 0)
            {
                bodyHit = Physics.Raycast(
                    bodyRaycastOrigin,
                    transform.TransformDirection(Vector3.down),
                    out bodyRaycastHit,
                    raycastLength,
                    groundMask,
                    QueryTriggerInteraction.Ignore);
            }
            else
            {
                bodyRaycastHit = default;
            }

            if (bodyHit)
            {
                if (drawDebugRay)
                {
                    float raycastDistance = Vector3.Distance(bodyRaycastOrigin, bodyRaycastHit.point);
                    Debug.DrawLine(bodyRaycastOrigin, bodyRaycastHit.point, Color.blue);
                    Debug.DrawRay(bodyRaycastHit.point, transform.TransformDirection(Vector3.down) * (raycastLength - raycastDistance), Color.white);
                }

                iKFootPlacementJob.bodyRaycastHitPoint = bodyRaycastHit.point;
                iKFootPlacementJob.bodyRaycastOrigin = bodyRaycastOrigin;
                iKFootPlacementJob.bodyRaycastHitNormal = bodyRaycastHit.normal;
            }
            else
            {
                iKFootPlacementJob.bodyRaycastHitPoint = Vector3.zero;
                iKFootPlacementJob.bodyRaycastOrigin = Vector3.zero;
                iKFootPlacementJob.bodyRaycastHitNormal = Vector3.zero;
            }

            for (int i = 0; i < iKConstraints.Count; i++)
            {
                RaycastHit legRaycastHit;
                Vector3 legRaycastOrigin = new Vector3(
                    iKConstraints[i].data.target.position.x,
                    transform.position.y + raycastHeight,
                    iKConstraints[i].data.target.position.z);

                bool legHit = false;
                if (feetPositionOffsetWeight > 0 || feetRotationOffsetWeight > 0 || bodyPositionOffsetWeight > 0)
                {
                    legHit = Physics.Raycast(
                        legRaycastOrigin,
                        transform.TransformDirection(Vector3.down),
                        out legRaycastHit,
                        raycastLength,
                        groundMask,
                        QueryTriggerInteraction.Ignore);
                }
                else
                {
                    legRaycastHit = default;
                }

                if (legHit)
                {
                    if (drawDebugRay)
                    {
                        float raycastDistance = Vector3.Distance(legRaycastOrigin, legRaycastHit.point);
                        Debug.DrawLine(legRaycastOrigin, legRaycastHit.point, Color.red);
                        Debug.DrawRay(legRaycastHit.point, transform.TransformDirection(Vector3.down) * (raycastLength - raycastDistance), Color.white);
                    }

                    iKFootPlacementJob.legRaycastHitPoint[i] = legRaycastHit.point;
                    iKFootPlacementJob.legRaycastOrigin[i] = legRaycastOrigin;
                    iKFootPlacementJob.legRaycastHitNormal[i] = legRaycastHit.normal;
                }
                else
                {
                    iKFootPlacementJob.legRaycastHitPoint[i] = Vector3.zero;
                    iKFootPlacementJob.legRaycastOrigin[i] = Vector3.zero;
                    iKFootPlacementJob.legRaycastHitNormal[i] = Vector3.zero;
                }
            }
        }
        else
        {
            iKFootPlacementJob.bodyRaycastHitPoint = Vector3.zero;
            iKFootPlacementJob.bodyRaycastOrigin = Vector3.zero;
            iKFootPlacementJob.bodyRaycastHitNormal = Vector3.zero;

            for (int i = 0; i < iKConstraints.Count; i++)
            {
                iKFootPlacementJob.legRaycastHitPoint[i] = Vector3.zero;
                iKFootPlacementJob.legRaycastOrigin[i] = Vector3.zero;
                iKFootPlacementJob.legRaycastHitNormal[i] = Vector3.zero;
            }
        }
    }

    private void CheckParameters()
    {
        if (stationaryToRotateSmoothing <= 0 || maxStationaryRotationAngle == 0 || maxStationaryRotationAngle == 360)
        {
            isMoving = true;
        }
    }

    private void SetJobParameters()
    {
        iKFootPlacementJob.feetPositionOffsetSmoothing = feetPositionOffsetSmoothing;
        iKFootPlacementJob.feetRotationOffsetSmoothing = feetRotationOffsetSmoothing;
        iKFootPlacementJob.targetPositionOffsetWeight = feetPositionOffsetWeight;
        iKFootPlacementJob.targetRotationOffsetWeight = feetRotationOffsetWeight;

        iKFootPlacementJob.bodyPositionOffsetSmoothing = bodyPositionOffsetSmoothing;
        iKFootPlacementJob.bodyRotationOffsetSmoothing = bodyRotationOffsetSmoothing;
        iKFootPlacementJob.bodyPositionOffsetWeight = bodyPositionOffsetWeight;
        iKFootPlacementJob.bodyRotationOffsetWeight = bodyRotationOffsetWeight;
        iKFootPlacementJob.invertBodyPositionOffset = invertBodyPositionOffset;
        iKFootPlacementJob.invertBodyRotationOffset = invertBodyRotationOffset;

        iKFootPlacementJob.stationaryToRotateSmoothing = stationaryToRotateSmoothing;
        iKFootPlacementJob.stationaryToWalkSmoothing = stationaryToWalkSmoothing;
        iKFootPlacementJob.maxStationaryRotationAngle = maxStationaryRotationAngle;

        iKFootPlacementJob.rootPosition = transform.position;
        iKFootPlacementJob.isActive = isActive;
        iKFootPlacementJob.isGrounded = isGrounded;
        iKFootPlacementJob.isMoving = isMoving;
        iKFootPlacementJob.jumped = jumped;
        iKFootPlacementJob.deltaTime = Time.deltaTime;
    }

    private void CheckRotations()
    {
        float currentStationaryRotation = transform.eulerAngles.y;

        if (stationaryToRotateSmoothing > 0 && maxStationaryRotationAngle != 0 && maxStationaryRotationAngle != 360 && iKConstraints.Count != 0)
        {
            if (!isMoving && isGrounded)
            {
                if (!iKFootPlacementJob.adjustFeet)
                {
                    float rotationDifference = Mathf.DeltaAngle(stationaryRotation, currentStationaryRotation);
                    if (rotationDifference < 0)
                        iKFootPlacementJob.adjustDirection = !invertAdjustmentDirection ? "left" : "right";
                    else if (rotationDifference > 0)
                        iKFootPlacementJob.adjustDirection = !invertAdjustmentDirection ? "right" : "left";

                    float lastRotationDifference = Mathf.DeltaAngle(lastStationaryRotation, currentStationaryRotation);
                    if (Mathf.Abs(lastRotationDifference) > maxStationaryRotationAngle / 2f)
                    {
                        lastStationaryRotation = currentStationaryRotation;
                        if (iKFootPlacementJob.adjustDirection == "right")
                            iKFootPlacementJob.adjustedFoot[0] = true;
                        else
                            iKFootPlacementJob.adjustedFoot[iKConstraints.Count - 1] = true;
                        iKFootPlacementJob.adjustFeet = true;
                    }
                }
                else
                {
                    float lastRotationDifference = Mathf.DeltaAngle(lastStationaryRotation, currentStationaryRotation);
                    if (Mathf.Abs(lastRotationDifference) > maxStationaryRotationAngle / 2f)
                    {
                        lastStationaryRotation = currentStationaryRotation;
                        for (int i = 0; i < iKConstraints.Count; i++)
                            iKFootPlacementJob.adjustedFoot[i] = true;
                        iKFootPlacementJob.adjustFeet = true;
                        iKFootPlacementJob.adjustDirection = "both";
                    }
                }
            }
            else if (isMoving && isGrounded && iKFootPlacementJob.lerpSpeed > 0)
            {
                float lastRotationDifference = Mathf.DeltaAngle(lastStationaryRotation, currentStationaryRotation);
                if (Mathf.Abs(lastRotationDifference) > maxStationaryRotationAngle / 4f)
                {
                    lastStationaryRotation = currentStationaryRotation;
                    for (int i = 0; i < iKConstraints.Count; i++)
                        iKFootPlacementJob.adjustedFoot[i] = true;
                    iKFootPlacementJob.adjustDirection = "both";
                }
            }
            else
            {
                lastStationaryRotation = currentStationaryRotation;
            }

            if (drawDebugRay)
            {
                Debug.DrawRay(transform.position, transform.forward, Color.yellow);
                Debug.DrawRay(transform.position, Quaternion.Euler(0, lastStationaryRotation + (maxStationaryRotationAngle / 2.0f), 0) * Vector3.forward, Color.red);
                Debug.DrawRay(transform.position, Quaternion.Euler(0, lastStationaryRotation - (maxStationaryRotationAngle / 2.0f), 0) * Vector3.forward, Color.red);
            }
        }
        else
        {
            lastStationaryRotation = currentStationaryRotation;
        }

        stationaryRotation = currentStationaryRotation;
    }

    private void CheckIfLanded()
    {
        if (jumped && isGrounded)
            jumped = false;
    }

    private void DisableIKFootPlacement()
    {
        enabled = false;
    }
}
