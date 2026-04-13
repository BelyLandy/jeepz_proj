using UnityEngine;

public enum FacingOverrideSource25D
{
    None = 0,
    WallSlide = 1,
    Vault = 2
}

[DisallowMultipleComponent]
public sealed class CharacterFacingResolver25D : MonoBehaviour
{
    [SerializeField] private int baseFacingSign = +1;
    [SerializeField] private int resolvedFacingSign = +1;
    [SerializeField] private FacingOverrideSource25D currentOverrideSource = FacingOverrideSource25D.None;

    [SerializeField] private bool wallSlideOverrideActive;
    [SerializeField] private int wallSlideOverrideSign = +1;

    [SerializeField] private bool vaultOverrideActive;
    [SerializeField] private int vaultOverrideSign = +1;

    public int BaseFacingSign => baseFacingSign;
    public int ResolvedFacingSign => resolvedFacingSign;
    public FacingOverrideSource25D CurrentOverrideSource => currentOverrideSource;
    public bool HasFacingOverride => currentOverrideSource != FacingOverrideSource25D.None;

    private void Awake()
    {
        baseFacingSign = NormalizeSign(baseFacingSign);
        wallSlideOverrideSign = NormalizeSign(wallSlideOverrideSign);
        vaultOverrideSign = NormalizeSign(vaultOverrideSign);
        RebuildResolvedFacing();
    }

    private void OnValidate()
    {
        baseFacingSign = NormalizeSign(baseFacingSign);
        wallSlideOverrideSign = NormalizeSign(wallSlideOverrideSign);
        vaultOverrideSign = NormalizeSign(vaultOverrideSign);
        RebuildResolvedFacing();
    }

    public void SetBaseFacingSign(int sign)
    {
        baseFacingSign = NormalizeSign(sign);
        RebuildResolvedFacing();
    }

    public void SetWallSlideOverride(int sign)
    {
        wallSlideOverrideActive = true;
        wallSlideOverrideSign = NormalizeSign(sign);
        RebuildResolvedFacing();
    }

    public void ClearWallSlideOverride()
    {
        wallSlideOverrideActive = false;
        RebuildResolvedFacing();
    }

    public void SetVaultOverride(int sign)
    {
        vaultOverrideActive = true;
        vaultOverrideSign = NormalizeSign(sign);
        RebuildResolvedFacing();
    }

    public void ClearVaultOverride()
    {
        vaultOverrideActive = false;
        RebuildResolvedFacing();
    }

    public void ClearAllOverrides()
    {
        wallSlideOverrideActive = false;
        vaultOverrideActive = false;
        RebuildResolvedFacing();
    }

    private void RebuildResolvedFacing()
    {
        if (vaultOverrideActive)
        {
            resolvedFacingSign = vaultOverrideSign;
            currentOverrideSource = FacingOverrideSource25D.Vault;
            return;
        }

        if (wallSlideOverrideActive)
        {
            resolvedFacingSign = wallSlideOverrideSign;
            currentOverrideSource = FacingOverrideSource25D.WallSlide;
            return;
        }

        resolvedFacingSign = baseFacingSign;
        currentOverrideSource = FacingOverrideSource25D.None;
    }

    private static int NormalizeSign(int sign)
    {
        return sign < 0 ? -1 : +1;
    }
}
