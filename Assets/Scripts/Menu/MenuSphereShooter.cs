using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class MenuSphereShooter : MonoBehaviour
{
    public enum LaunchMode
    {
        Straight = 0,
        BallisticForward = 1,
        BallisticCrosshairPass = 2,
        BallisticTrackUntilCrosshairPass = 3,
    }

    [Header("References")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private MenuPointerController pointerController;
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private MenuSphereProjectile projectilePrefab;

    [Header("Action Lookup")]
    [Tooltip("Если включено, Shoot будет искаться в текущей active action map у PlayerInput.")]
    [SerializeField] private bool useCurrentActionMap = true;

    [Tooltip("Используется только если Use Current Action Map выключен или у PlayerInput ещё нет currentActionMap.")]
    [SerializeField] private string actionMapName = "UI";
    [SerializeField] private string shootActionName = "Shoot";

    [Header("Launch")]
    [SerializeField] private LaunchMode launchMode = LaunchMode.BallisticCrosshairPass;

    [Header("Fire")]
    [SerializeField, Min(0f)] private float maxShotsPerSecond = 10f;
    [SerializeField] private bool allowHoldFire = true;

    [Header("Ballistic Targeting")]
    [SerializeField, Min(0.0001f)] private float desiredCrosshairTravelSpeed = 20f;
    [SerializeField, Min(0.01f)] private float minTimeToCrosshair = 0.22f;
    [SerializeField, Min(0.01f)] private float maxTimeToCrosshair = 0.70f;

    [Header("Tracking Until Pass")]
    [SerializeField] private MenuSphereProjectile.CrosshairTrackingStyle trackingStyle = MenuSphereProjectile.CrosshairTrackingStyle.Hard;
    [SerializeField, Min(0.0001f)] private float crosshairPassRadius = 0.5f;
    [SerializeField, Min(0f)] private float softTrackingResponsiveness = 10f;

    private InputActionMap resolvedActionMap;
    private InputAction shootAction;
    private float nextAllowedShotTime;

    private void Reset()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (pointerController == null)
            pointerController = GetComponent<MenuPointerController>();
    }

    private void Awake()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (pointerController == null)
            pointerController = GetComponent<MenuPointerController>();

        ClampSettings();
    }

    private void OnValidate()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (pointerController == null)
            pointerController = GetComponent<MenuPointerController>();

        if (string.IsNullOrWhiteSpace(actionMapName))
            actionMapName = "UI";

        if (string.IsNullOrWhiteSpace(shootActionName))
            shootActionName = "Shoot";

        ClampSettings();
    }

    private void OnEnable()
    {
        nextAllowedShotTime = 0f;
        ResolveActions();
    }

    private void Update()
    {
        RefreshResolvedActionMapIfNeeded();

        if (shootAction == null || projectilePrefab == null || pointerController == null || muzzleTransform == null)
            return;

        bool wantsToFire = allowHoldFire ? shootAction.IsPressed() : shootAction.WasPressedThisFrame();
        if (!wantsToFire)
            return;

        if (Time.time < nextAllowedShotTime)
            return;

        if (Fire())
            nextAllowedShotTime = Time.time + GetShotInterval();
    }

    private float GetShotInterval()
    {
        if (maxShotsPerSecond <= 0f)
            return 0f;

        return 1f / maxShotsPerSecond;
    }

    private void ClampSettings()
    {
        maxShotsPerSecond = Mathf.Max(0f, maxShotsPerSecond);
        desiredCrosshairTravelSpeed = Mathf.Max(0.0001f, desiredCrosshairTravelSpeed);
        minTimeToCrosshair = Mathf.Max(0.01f, minTimeToCrosshair);
        maxTimeToCrosshair = Mathf.Max(minTimeToCrosshair, maxTimeToCrosshair);
        crosshairPassRadius = Mathf.Max(0.0001f, crosshairPassRadius);
        softTrackingResponsiveness = Mathf.Max(0f, softTrackingResponsiveness);
    }

    private bool Fire()
    {
        if (projectilePrefab == null || muzzleTransform == null || pointerController == null)
            return false;

        Vector3 muzzlePosition = muzzleTransform.position;
        Vector3 targetPosition = pointerController.CrosshairWorldPosition;
        Vector3 launchVector = targetPosition - muzzlePosition;

        if (launchVector.sqrMagnitude <= 0.000001f)
            launchVector = muzzleTransform.forward;

        if (launchVector.sqrMagnitude <= 0.000001f)
            return false;

        MenuSphereProjectile projectileInstance = Instantiate(projectilePrefab, muzzlePosition, Quaternion.identity);
        projectileInstance.ConfigureLaunchCrosshairTarget(targetPosition);
        bool usedBallisticVelocity = false;

        if (ShouldUseBallisticTargeting(projectileInstance)
            && TryBuildBallisticVelocityToTarget(muzzlePosition, targetPosition, projectileInstance, out Vector3 initialVelocity))
        {
            projectileInstance.LaunchVelocity(initialVelocity);
            usedBallisticVelocity = true;

            if (launchMode == LaunchMode.BallisticTrackUntilCrosshairPass)
            {
                projectileInstance.ConfigureTracking(
                    pointerController,
                    trackingStyle,
                    desiredCrosshairTravelSpeed,
                    minTimeToCrosshair,
                    maxTimeToCrosshair,
                    crosshairPassRadius,
                    softTrackingResponsiveness);
            }
        }

        if (!usedBallisticVelocity)
            projectileInstance.Launch(launchVector.normalized);

        return true;
    }

    private bool ShouldUseBallisticTargeting(MenuSphereProjectile projectile)
    {
        if (projectile == null || projectile.CurrentTrajectoryMode != MenuSphereProjectile.TrajectoryMode.Ballistic)
            return false;

        return launchMode == LaunchMode.BallisticCrosshairPass
               || launchMode == LaunchMode.BallisticTrackUntilCrosshairPass;
    }

    private bool TryBuildBallisticVelocityToTarget(
        Vector3 origin,
        Vector3 target,
        MenuSphereProjectile projectile,
        out Vector3 initialVelocity)
    {
        initialVelocity = Vector3.zero;
        if (projectile == null)
            return false;

        Vector3 delta = target - origin;
        if (projectile.LockWorldZ)
            delta.z = 0f;

        float distance = delta.magnitude;
        float timeToCrosshair = distance / desiredCrosshairTravelSpeed;
        timeToCrosshair = Mathf.Clamp(timeToCrosshair, minTimeToCrosshair, maxTimeToCrosshair);
        if (timeToCrosshair <= 0.0001f)
            return false;

        Vector3 acceleration = Vector3.down * projectile.GravityAcceleration;
        if (projectile.LockWorldZ)
            acceleration.z = 0f;

        initialVelocity = (delta - 0.5f * acceleration * timeToCrosshair * timeToCrosshair) / timeToCrosshair;

        if (projectile.LockWorldZ)
            initialVelocity.z = 0f;

        return float.IsFinite(initialVelocity.x)
               && float.IsFinite(initialVelocity.y)
               && float.IsFinite(initialVelocity.z)
               && initialVelocity.sqrMagnitude > 0.000001f;
    }

    private void ResolveActions()
    {
        resolvedActionMap = ResolveActionMap();
        shootAction = resolvedActionMap != null ? resolvedActionMap.FindAction(shootActionName, throwIfNotFound: false) : null;
    }

    private void RefreshResolvedActionMapIfNeeded()
    {
        InputActionMap desiredMap = ResolveActionMap();
        if (desiredMap == resolvedActionMap && shootAction != null)
            return;

        ResolveActions();
    }

    private InputActionMap ResolveActionMap()
    {
        if (playerInput == null || playerInput.actions == null)
            return null;

        if (useCurrentActionMap && playerInput.currentActionMap != null)
            return playerInput.currentActionMap;

        if (string.IsNullOrWhiteSpace(actionMapName))
            return null;

        return playerInput.actions.FindActionMap(actionMapName, throwIfNotFound: false);
    }
}
