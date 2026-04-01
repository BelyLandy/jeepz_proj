using UnityEngine;

[DisallowMultipleComponent]
public sealed class RBCharacter25DHeadBumpSurface : MonoBehaviour
{
    public enum Mode
    {
        UseGroundMask = 0,
        ForceEnabled = 1,
        ForceDisabled = 2,
    }

    [SerializeField] private Mode mode = Mode.UseGroundMask;

    public bool AllowsHeadBump(LayerMask groundMask)
    {
        switch (mode)
        {
            case Mode.ForceEnabled:
                return true;

            case Mode.ForceDisabled:
                return false;

            default:
                return ((1 << gameObject.layer) & groundMask.value) != 0;
        }
    }

    public static bool AllowsHeadBump(Collider targetCollider, LayerMask groundMask)
    {
        if (targetCollider == null)
            return false;

        // One-way platforms are valid support surfaces from above,
        // but they must never behave like a ceiling/head-bump surface.
        if (targetCollider.GetComponentInParent<OneWayBoxPlatform>() != null)
            return false;

        RBCharacter25DHeadBumpSurface surface =
            targetCollider.GetComponentInParent<RBCharacter25DHeadBumpSurface>();

        if (surface != null)
            return surface.AllowsHeadBump(groundMask);

        return ((1 << targetCollider.gameObject.layer) & groundMask.value) != 0;
    }
}
