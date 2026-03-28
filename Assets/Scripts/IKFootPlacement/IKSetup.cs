using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class IKSetup : MonoBehaviour
{
    [Tooltip("If the namings of your bones are the same, then this will fill automatically")]
    [SerializeField] private Transform hips;

    [Tooltip("If the namings of your bones are the same, then this will fill automatically")]
    [SerializeField] private List<IKLeg> leftLegsTransforms = new List<IKLeg>();

    [Tooltip("If the namings of your bones are the same, then this will fill automatically")]
    [SerializeField] private List<IKLeg> rightLegsTransforms = new List<IKLeg>();

    private RigBuilder rigBuilder;

    public string hipsName = "Hips";
    public string leftUpLegName = "LeftUpLeg";
    public string leftLegName = "LeftLeg";
    public string leftFootName = "LeftFoot";
    public string rightUpLegName = "RightUpLeg";
    public string rightLegName = "RightLeg";
    public string rightFootName = "RightFoot";

    public bool SetupIKRig()
    {
        if (!ValidateReferences())
            return false;

        Animator animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("This GameObject does not have the Animator component attached.");
            return false;
        }

        rigBuilder = GetComponent<RigBuilder>();
        if (rigBuilder == null)
        {
            rigBuilder = gameObject.AddComponent<RigBuilder>();
        }

        CleanupPreviousGeneratedRig();

        Transform avatarRoot = GetAvatarRoot();
        if (avatarRoot == null)
        {
            Debug.LogError("Could not determine the avatar root from the hips transform.");
            return false;
        }

        GameObject rigRootObject = new GameObject("FootPlacementRig");
        rigRootObject.transform.SetParent(avatarRoot, false);
        Rig rig = rigRootObject.AddComponent<Rig>();

        GameObject ikRoot = new GameObject("IK");
        ikRoot.transform.SetParent(rigRootObject.transform, false);

        GameObject constraintsRoot = new GameObject("IKConstraints");
        constraintsRoot.transform.SetParent(ikRoot.transform, false);

        List<TwoBoneIKConstraint> createdConstraints = new List<TwoBoneIKConstraint>();

        CreateLegRig(
            parentForConstraints: constraintsRoot.transform,
            parentForEffectors: ikRoot.transform,
            legs: leftLegsTransforms,
            legNamePrefix: leftLegName,
            isLeftSide: true,
            createdConstraints: createdConstraints);

        CreateLegRig(
            parentForConstraints: constraintsRoot.transform,
            parentForEffectors: ikRoot.transform,
            legs: rightLegsTransforms,
            legNamePrefix: rightLegName,
            isLeftSide: false,
            createdConstraints: createdConstraints);

        IKFootPlacement footPlacement = GetComponent<IKFootPlacement>();
        if (footPlacement == null)
        {
            footPlacement = gameObject.AddComponent<IKFootPlacement>();
        }

        footPlacement.hips = hips;
        footPlacement.iKConstraints.Clear();

        // Preserve the original left-to-right ordering expected by the foot adjustment logic.
        for (int i = 0; i < createdConstraints.Count; i++)
        {
            footPlacement.iKConstraints.Add(createdConstraints[i]);
        }

        rigBuilder.layers.Clear();
        rigBuilder.layers.Add(new RigLayer(rig, true));
        rigBuilder.enabled = true;

        animator.Rebind();
        rigBuilder.Clear();
        if (!rigBuilder.Build())
        {
            Debug.LogError("RigBuilder.Build() failed. Check Animation Rigging package installation and references.");
            return false;
        }

        Debug.Log("Setup finished successfully!");
        return true;
    }

    private void CreateLegRig(
        Transform parentForConstraints,
        Transform parentForEffectors,
        List<IKLeg> legs,
        string legNamePrefix,
        bool isLeftSide,
        List<TwoBoneIKConstraint> createdConstraints)
    {
        if (legs == null || legs.Count == 0)
            return;

        string sideName = isLeftSide ? "Left" : "Right";
        GameObject sideRoot = new GameObject(legs.Count == 1 ? $"{sideName}IKLeg" : $"{sideName}IKLegs");
        sideRoot.transform.SetParent(parentForEffectors, false);

        for (int i = 0; i < legs.Count; i++)
        {
            IKLeg leg = legs[i];
            string suffix = legs.Count == 1 ? string.Empty : (i + 1).ToString();

            GameObject constraintObject = new GameObject($"{legNamePrefix}{suffix}IKConstraint");
            constraintObject.transform.SetParent(parentForConstraints, false);

            GameObject targetObject = new GameObject($"{legNamePrefix}{suffix}Target");
            targetObject.transform.SetParent(sideRoot.transform, false);
            targetObject.transform.position = leg.foot.position;
            targetObject.transform.rotation = leg.foot.rotation;
            targetObject.AddComponent<RigTransform>();

            GameObject hintObject = new GameObject($"{legNamePrefix}{suffix}Hint");
            hintObject.transform.SetParent(sideRoot.transform, false);
            hintObject.transform.position = leg.leg.position;
            hintObject.transform.rotation = leg.leg.rotation;
            hintObject.AddComponent<RigTransform>();

            TwoBoneIKConstraint constraint = constraintObject.AddComponent<TwoBoneIKConstraint>();
            constraint.data.root = leg.upLeg;
            constraint.data.mid = leg.leg;
            constraint.data.tip = leg.foot;
            constraint.data.target = targetObject.transform;
            constraint.data.hint = hintObject.transform;
            constraint.data.maintainTargetRotationOffset = true;
            constraint.data.targetPositionWeight = 1f;
            constraint.data.targetRotationWeight = 1f;
            constraint.data.hintWeight = 1f;
            constraint.weight = 1f;

            createdConstraints.Add(constraint);
        }
    }

    private Transform GetAvatarRoot()
    {
        if (hips == null)
            return null;

        Transform current = hips;
        while (current.parent != null && current.parent != transform)
        {
            current = current.parent;
        }

        return current;
    }

    private void CleanupPreviousGeneratedRig()
    {
        DestroyChildIfExists(transform, "IK");

        Transform avatarRoot = GetAvatarRoot();
        if (avatarRoot != null)
        {
            DestroyChildIfExists(avatarRoot, "FootPlacementRig");
        }

        Rig rootRig = GetComponent<Rig>();
        if (rootRig != null)
        {
            DestroyComponentImmediate(rootRig);
        }

        IKFootPlacement footPlacement = GetComponent<IKFootPlacement>();
        if (footPlacement != null)
        {
            footPlacement.iKConstraints.Clear();
        }
    }

    private void DestroyChildIfExists(Transform parent, string childName)
    {
        if (parent == null)
            return;

        Transform child = parent.Find(childName);
        if (child != null)
        {
            DestroyObjectImmediate(child.gameObject);
        }
    }

    private void DestroyComponentImmediate(Component component)
    {
        if (component == null)
            return;

        if (Application.isPlaying)
            Destroy(component);
        else
            DestroyImmediate(component);
    }

    private void DestroyObjectImmediate(GameObject go)
    {
        if (go == null)
            return;

        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    private bool ValidateReferences()
    {
        if (hips == null)
        {
            Debug.LogError("Not all of the references are attached. Please try finding references again, or attach each component manually before proceeding.");
            return false;
        }

        foreach (var leg in leftLegsTransforms)
        {
            if (leg.upLeg == null || leg.leg == null || leg.foot == null)
            {
                Debug.LogError("Not all of the left leg references are attached.");
                return false;
            }
        }

        foreach (var leg in rightLegsTransforms)
        {
            if (leg.upLeg == null || leg.leg == null || leg.foot == null)
            {
                Debug.LogError("Not all of the right leg references are attached.");
                return false;
            }
        }

        return true;
    }

    public void FindReferences()
    {
        if (gameObject.GetComponent<Animator>() == null)
        {
            Debug.LogError("This GameObject does not have the Animator component attached. Please attach this script to the correct GameObject and try again.");
            return;
        }

        if (hips == null)
        {
            GameObject hipsObject = GameObject.Find(hipsName);
            if (hipsObject != null)
                hips = hipsObject.transform;
        }

        leftLegsTransforms.Clear();
        rightLegsTransforms.Clear();

        FindLegReferences(leftLegsTransforms, leftUpLegName, leftLegName, leftFootName);
        FindLegReferences(rightLegsTransforms, rightUpLegName, rightLegName, rightFootName);

        if (rightLegsTransforms.Count == 0 && leftLegsTransforms.Count == 0)
        {
            Debug.LogError("Could not automatically detect character's legs references. Be sure that the namings of the joints are correct. Try reseting IK Setup script or manually assign each reference.");
            return;
        }

        Debug.Log("All references found successfully!");
    }

    private void FindLegReferences(List<IKLeg> destination, string upLegBaseName, string legBaseName, string footBaseName)
    {
        IKLeg leg = new IKLeg();
        GameObject upLeg = GameObject.Find(upLegBaseName);
        GameObject lowerLeg = GameObject.Find(legBaseName);
        GameObject foot = GameObject.Find(footBaseName);

        if (upLeg != null && lowerLeg != null && foot != null)
        {
            leg.upLeg = upLeg.transform;
            leg.leg = lowerLeg.transform;
            leg.foot = foot.transform;
            destination.Add(leg);
        }

        int i = 1;
        while (true)
        {
            GameObject upLegIndexed = GameObject.Find(upLegBaseName + i);
            GameObject legIndexed = GameObject.Find(legBaseName + i);
            GameObject footIndexed = GameObject.Find(footBaseName + i);

            if (upLegIndexed == null || legIndexed == null || footIndexed == null)
                break;

            leg = new IKLeg
            {
                upLeg = upLegIndexed.transform,
                leg = legIndexed.transform,
                foot = footIndexed.transform
            };
            destination.Add(leg);
            i++;
        }
    }
}
