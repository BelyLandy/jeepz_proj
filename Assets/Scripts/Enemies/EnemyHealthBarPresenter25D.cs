using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyHealthBarPresenter25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyHealth25D health;
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private Transform followTarget;

    [Header("Placement")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private bool parentToFollowTarget = true;
    [SerializeField] private bool keepWorldRotation = true;
    [SerializeField] private bool compensateNegativeParentScale = true;

    [Header("Visibility")]
    [SerializeField] private bool showAlways;
    [SerializeField] private bool hideWhenFull = true;
    [SerializeField] private bool showWhenDamaged = true;
    [SerializeField, Min(0f)] private float showOnDamageDuration = 2f;
    [SerializeField] private bool hideOnDeath = true;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField, Min(0f)] private float destroyOnDeathDelay;

    [Header("Sprite Fill")]
    [SerializeField] private string fillPivotName = "FillPivot";
    [SerializeField] private string fillSpriteName = "FillSprite";
    [SerializeField] private string backgroundSpriteName = "BackgroundSprite";
    [SerializeField] private string borderSpriteName = "BorderSprite";
    [SerializeField] private Transform fillPivot;
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private SpriteRenderer fillRenderer;
    [SerializeField] private SpriteRenderer borderRenderer;

    [Header("Leader Visual")]
    [SerializeField] private string leaderIconSpriteName = "LeaderIconSprite";
    [SerializeField] private SpriteRenderer leaderIconRenderer;
    [SerializeField, Min(0f)] private float leaderScaleMultiplier = 1.25f;
    [SerializeField] private bool showAlwaysWhenLeader = true;

    [Header("Always On Top Rendering")]
    [SerializeField] private bool applyAlwaysOnTopMaterial = true;
    [SerializeField] private Material alwaysOnTopMaterial;
    [SerializeField] private bool setLightingOffOnHealthBar;
    [SerializeField] private bool applySortingOverride = true;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int baseSortingOrder = 5000;
    [SerializeField] private int backgroundSortingOffset;
    [SerializeField] private int fillSortingOffset = 1;
    [SerializeField] private int borderSortingOffset = 2;
    [SerializeField] private int leaderIconSortingOffset = 3;

    [Header("Camera Facing")]
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool useTaggedCameraFallback = true;
    [SerializeField] private string targetCameraTag = "HealthBarCamera";
    [SerializeField] private bool useMainCameraFallback = true;
    [SerializeField] private bool hideWhenBehindCamera = true;
    [SerializeField] private bool detachFromFollowTargetForBillboard = true;

    [Header("Debug")]
    [SerializeField] private bool logMissingPrefabWarning = true;

    private GameObject healthBarInstance;
    private Transform healthBarRoot;
    private Vector3 baseHealthBarScale = Vector3.one;
    private Vector3 baseFillScale = Vector3.one;

    private bool isLeaderVisualActive;
    private float visibleUntilTime = float.NegativeInfinity;
    private float lastHealth01 = 1f;
    private bool subscribed;
    private bool missingPrefabWarningLogged;
    private bool isBehindCamera;
    private Material runtimeAlwaysOnTopMaterial;
    private Camera cachedTaggedCamera;
    private string cachedTaggedCameraTag;
    private bool hasTriedResolveTaggedCamera;

    public bool IsLeaderVisualActive => isLeaderVisualActive;
    public GameObject HealthBarInstance => healthBarInstance;
    public bool IsBehindCamera => isBehindCamera;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureHealthBarInstance();
        BindHealth();
        RefreshImmediate();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureHealthBarInstance();
        BindHealth();
        RefreshImmediate();
    }

    private void OnDisable()
    {
        UnbindHealth();

        if (healthBarInstance != null)
            healthBarInstance.SetActive(false);
    }

    private void OnDestroy()
    {
        UnbindHealth();
        DestroyRuntimeAlwaysOnTopMaterial();

        if (healthBarInstance != null)
        {
            Destroy(healthBarInstance);
            ClearHealthBarRuntimeReferences();
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
        showOnDamageDuration = Mathf.Max(0f, showOnDamageDuration);
        destroyOnDeathDelay = Mathf.Max(0f, destroyOnDeathDelay);
        leaderScaleMultiplier = Mathf.Max(0f, leaderScaleMultiplier);

        if (string.IsNullOrWhiteSpace(targetCameraTag))
            targetCameraTag = "HealthBarCamera";

        InvalidateCameraCache();
    }

    private void LateUpdate()
    {
        if (healthBarRoot == null)
            return;

        UpdateHealthBarPosition();
        UpdateHealthBarCameraFacing();
        ApplyRootScale();
        RefreshVisibility();
    }

    public void SetLeaderVisual(bool isLeader)
    {
        isLeaderVisualActive = isLeader;

        if (healthBarInstance == null && !isLeader)
            return;

        EnsureHealthBarInstance();

        if (leaderIconRenderer != null)
            leaderIconRenderer.enabled = isLeader;

        ApplyRootScale();
        RefreshVisibility();
    }

    public void ForceRefresh()
    {
        EnsureHealthBarInstance();
        RefreshImmediate();
    }

    private void ResolveReferences()
    {
        if (health == null)
            health = GetComponentInParent<EnemyHealth25D>();
        if (followTarget == null)
            followTarget = transform;
    }

    private void EnsureHealthBarInstance()
    {
        if (healthBarInstance != null)
            return;

        if (healthBarPrefab == null)
        {
            if (logMissingPrefabWarning && !missingPrefabWarningLogged)
            {
                Debug.LogWarning("[EnemyHealthBarPresenter25D] Missing healthBarPrefab. Assign a SpriteRenderer health bar prefab on the enemy.", this);
                missingPrefabWarningLogged = true;
            }
            return;
        }

        Transform parent = ShouldDetachForBillboard() ? null : parentToFollowTarget && followTarget != null ? followTarget : null;
        healthBarInstance = Instantiate(healthBarPrefab, parent);
        healthBarRoot = healthBarInstance.transform;

        if (parent != null)
            healthBarRoot.localPosition = localOffset;
        else
            healthBarRoot.position = (followTarget != null ? followTarget.position : transform.position) + localOffset;

        healthBarRoot.localRotation = Quaternion.identity;
        baseHealthBarScale = healthBarRoot.localScale;

        ResolvePrefabReferences();

        if (fillPivot != null)
            baseFillScale = fillPivot.localScale;

        if (leaderIconRenderer != null)
            leaderIconRenderer.enabled = isLeaderVisualActive;

        ApplyRenderingSettings();
        ApplyRootScale();
    }

    private void ResolvePrefabReferences()
    {
        if (healthBarRoot == null)
            return;

        if (fillPivot == null)
            fillPivot = FindDeepChild(healthBarRoot, fillPivotName);

        if (backgroundRenderer == null)
            backgroundRenderer = FindChildSpriteRenderer(backgroundSpriteName);

        if (fillRenderer == null)
            fillRenderer = FindChildSpriteRenderer(fillSpriteName);

        if (borderRenderer == null)
            borderRenderer = FindChildSpriteRenderer(borderSpriteName);

        if (leaderIconRenderer == null)
            leaderIconRenderer = FindChildSpriteRenderer(leaderIconSpriteName);
    }

    private SpriteRenderer FindChildSpriteRenderer(string childName)
    {
        if (healthBarRoot == null || string.IsNullOrWhiteSpace(childName))
            return null;

        Transform child = FindDeepChild(healthBarRoot, childName);
        return child != null ? child.GetComponent<SpriteRenderer>() : null;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;

        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Transform found = FindDeepChild(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void BindHealth()
    {
        if (subscribed || health == null)
            return;

        health.HealthChanged += HandleHealthChanged;
        health.Died += HandleDied;
        subscribed = true;
    }

    private void UnbindHealth()
    {
        if (!subscribed || health == null)
            return;

        health.HealthChanged -= HandleHealthChanged;
        health.Died -= HandleDied;
        subscribed = false;
    }

    private void HandleHealthChanged(float current, float max)
    {
        EnsureHealthBarInstance();

        float health01 = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        bool damaged = health01 < lastHealth01;

        SetFill(health01);

        if (damaged && showWhenDamaged)
            visibleUntilTime = Time.time + showOnDamageDuration;

        lastHealth01 = health01;
        RefreshVisibility();
    }

    private void HandleDied()
    {
        if (destroyOnDeath && healthBarInstance != null)
        {
            GameObject instanceToDestroy = healthBarInstance;
            ClearHealthBarRuntimeReferences();

            if (destroyOnDeathDelay <= 0f)
                Destroy(instanceToDestroy);
            else
                Destroy(instanceToDestroy, destroyOnDeathDelay);

            return;
        }

        if (hideOnDeath && healthBarInstance != null)
            healthBarInstance.SetActive(false);
    }

    private void SetFill(float health01)
    {
        if (fillPivot == null)
            return;

        Vector3 scale = baseFillScale;
        scale.x *= Mathf.Clamp01(health01);
        fillPivot.localScale = scale;
    }

    private void RefreshImmediate()
    {
        if (health == null)
            return;

        lastHealth01 = health.Health01;
        SetFill(lastHealth01);
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (healthBarInstance == null)
            return;

        if (hideWhenBehindCamera && isBehindCamera)
        {
            healthBarInstance.SetActive(false);
            return;
        }

        if (health != null && health.IsDead)
        {
            healthBarInstance.SetActive(!hideOnDeath && !destroyOnDeath);
            return;
        }

        bool shouldShow = showAlways;

        if (isLeaderVisualActive && showAlwaysWhenLeader)
            shouldShow = true;

        if (hideWhenFull && health != null && health.Health01 >= 0.999f && !showAlways)
        {
            shouldShow = false;

            if (isLeaderVisualActive && showAlwaysWhenLeader)
                shouldShow = true;
        }

        if (showWhenDamaged && Time.time < visibleUntilTime)
            shouldShow = true;

        healthBarInstance.SetActive(shouldShow);
    }

    private void UpdateHealthBarPosition()
    {
        if (healthBarRoot == null)
            return;

        if (ShouldDetachForBillboard())
        {
            Vector3 basePosition = followTarget != null ? followTarget.position : transform.position;
            healthBarRoot.position = basePosition + localOffset;
            return;
        }

        if (!parentToFollowTarget && followTarget != null)
            healthBarRoot.position = followTarget.position + localOffset;
        else if (parentToFollowTarget)
            healthBarRoot.localPosition = localOffset;
    }

    private void UpdateHealthBarCameraFacing()
    {
        if (healthBarRoot == null)
            return;

        Camera cam = ResolveTargetCamera();
        isBehindCamera = false;

        if (cam != null && hideWhenBehindCamera)
        {
            Vector3 viewport = cam.WorldToViewportPoint(healthBarRoot.position);
            isBehindCamera = viewport.z < 0f;
        }

        if (faceCamera && cam != null)
        {
            healthBarRoot.rotation = cam.transform.rotation;
            return;
        }

        if (keepWorldRotation)
            healthBarRoot.rotation = Quaternion.identity;
    }

    private Camera ResolveTargetCamera()
    {
        if (targetCamera != null)
            return targetCamera;

        Camera taggedCamera = ResolveTaggedCamera();
        if (taggedCamera != null)
            return taggedCamera;

        return useMainCameraFallback ? Camera.main : null;
    }

    private Camera ResolveTaggedCamera()
    {
        if (!useTaggedCameraFallback)
            return null;

        if (string.IsNullOrWhiteSpace(targetCameraTag))
            return null;

        if (cachedTaggedCamera != null && cachedTaggedCameraTag == targetCameraTag)
            return cachedTaggedCamera;

        if (hasTriedResolveTaggedCamera && cachedTaggedCameraTag == targetCameraTag)
            return null;

        cachedTaggedCamera = null;
        cachedTaggedCameraTag = targetCameraTag;
        hasTriedResolveTaggedCamera = true;

        GameObject taggedObject;

        try
        {
            taggedObject = GameObject.FindGameObjectWithTag(targetCameraTag);
        }
        catch (UnityException)
        {
            return null;
        }

        if (taggedObject == null)
            return null;

        cachedTaggedCamera = taggedObject.GetComponent<Camera>();

        if (cachedTaggedCamera == null)
            cachedTaggedCamera = taggedObject.GetComponentInChildren<Camera>();

        return cachedTaggedCamera;
    }

    private void InvalidateCameraCache()
    {
        cachedTaggedCamera = null;
        cachedTaggedCameraTag = null;
        hasTriedResolveTaggedCamera = false;
    }

    private bool ShouldDetachForBillboard()
    {
        return faceCamera && detachFromFollowTargetForBillboard;
    }

    private void ApplyRootScale()
    {
        if (healthBarRoot == null)
            return;

        Vector3 scale = baseHealthBarScale;

        if (isLeaderVisualActive)
            scale *= leaderScaleMultiplier;

        if (compensateNegativeParentScale && !ShouldDetachForBillboard() && healthBarRoot.parent != null)
        {
            Vector3 parentLossyScale = healthBarRoot.parent.lossyScale;

            if (parentLossyScale.x < 0f)
                scale.x = -Mathf.Abs(scale.x);
            else
                scale.x = Mathf.Abs(scale.x);
        }

        healthBarRoot.localScale = scale;
    }

    private void ApplyRenderingSettings()
    {
        if (healthBarInstance == null)
            return;

        SpriteRenderer[] renderers = healthBarInstance.GetComponentsInChildren<SpriteRenderer>(true);
        Material materialToApply = GetRuntimeAlwaysOnTopMaterial();

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (materialToApply != null)
                renderer.sharedMaterial = materialToApply;

            if (applySortingOverride)
            {
                renderer.sortingLayerName = string.IsNullOrWhiteSpace(sortingLayerName) ? "Default" : sortingLayerName;
                renderer.sortingOrder = ResolveSortingOrder(renderer);
            }
        }
    }

    private Material GetRuntimeAlwaysOnTopMaterial()
    {
        if (!applyAlwaysOnTopMaterial || alwaysOnTopMaterial == null)
            return null;

        if (runtimeAlwaysOnTopMaterial == null)
            runtimeAlwaysOnTopMaterial = Instantiate(alwaysOnTopMaterial);

        ApplyHealthBarMaterialProperties(runtimeAlwaysOnTopMaterial);
        return runtimeAlwaysOnTopMaterial;
    }

    private void ApplyHealthBarMaterialProperties(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_LightingOff"))
            material.SetFloat("_LightingOff", setLightingOffOnHealthBar ? 1f : 0f);

        if (material.HasProperty("_UseAlwaysOnTop"))
            material.SetFloat("_UseAlwaysOnTop", applyAlwaysOnTopMaterial ? 1f : 0f);
    }

    private int ResolveSortingOrder(SpriteRenderer renderer)
    {
        if (renderer == null)
            return baseSortingOrder;

        if (renderer == backgroundRenderer || NameContains(renderer, "Background"))
            return baseSortingOrder + backgroundSortingOffset;

        if (renderer == fillRenderer || NameContains(renderer, "Fill"))
            return baseSortingOrder + fillSortingOffset;

        if (renderer == borderRenderer || NameContains(renderer, "Border"))
            return baseSortingOrder + borderSortingOffset;

        if (renderer == leaderIconRenderer || NameContains(renderer, "Leader") || NameContains(renderer, "Icon"))
            return baseSortingOrder + leaderIconSortingOffset;

        return baseSortingOrder;
    }

    private static bool NameContains(UnityEngine.Object obj, string token)
    {
        return obj != null && !string.IsNullOrWhiteSpace(token) && obj.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ClearHealthBarRuntimeReferences()
    {
        healthBarInstance = null;
        healthBarRoot = null;
        fillPivot = null;
        backgroundRenderer = null;
        fillRenderer = null;
        borderRenderer = null;
        leaderIconRenderer = null;
        isBehindCamera = false;
    }

    private void DestroyRuntimeAlwaysOnTopMaterial()
    {
        if (runtimeAlwaysOnTopMaterial == null)
            return;

        Destroy(runtimeAlwaysOnTopMaterial);
        runtimeAlwaysOnTopMaterial = null;
    }
}
