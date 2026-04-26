using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class EnemyVisionConeVisual25D : MonoBehaviour
{
    private enum VisionConeVisualState
    {
        Hidden = 0,
        CalmVisible = 1,
        AlertVisible = 2,
    }

    [Header("References")]
    [SerializeField] private EnemyPerception25D perception;
    [SerializeField] private EnemyBrainBT25D brain;
    [SerializeField] private Transform coneOrigin;
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private Transform eyeOriginOverride;

    [Header("Geometry")]
    [SerializeField, Min(0.01f)] private float coneRadiusMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float coneAngleMultiplier = 1f;
    [SerializeField, Min(3)] private int coneSegments = 20;
    [SerializeField] private float localZOffset = 0f;

    [Header("Visibility / Fade")]
    [SerializeField] private bool useSmoothFade = true;
    [SerializeField, Min(0.01f)] private float showFadeDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float hideFadeDuration = 0.15f;
    [SerializeField, Range(0f, 1f)] private float maxAlphaCalm = 0.22f;
    [SerializeField, Range(0f, 1f)] private float maxAlphaAlert = 0.32f;

    [Header("Materials / Style")]
    [SerializeField] private bool useSeparateMaterials = true;
    [SerializeField] private Material calmMaterial;
    [SerializeField] private Material alertMaterial;
    [SerializeField] private string alphaPropertyName = "_Alpha";
    [SerializeField] private string intensityPropertyName = "_Intensity";
    [SerializeField] private float calmIntensity = 1f;
    [SerializeField] private float alertIntensity = 1.2f;

    [Header("Behaviour")]
    [SerializeField] private bool hideRendererWhenFullyInvisible = true;
    [SerializeField] private bool rebuildMeshOnStart = true;
    [SerializeField] private bool followOriginEveryFrame = true;

    private Mesh coneMesh;
    private MaterialPropertyBlock propertyBlock;
    private float currentAlpha;
    private float targetAlpha;
    private VisionConeVisualState currentState = VisionConeVisualState.Hidden;
    private VisionConeVisualState targetState = VisionConeVisualState.Hidden;
    private float lastBuiltRadius = -1f;
    private float lastBuiltAngle = -1f;
    private int lastBuiltSegments = -1;

    public bool IsVisible => currentAlpha > 0.001f;
    public float CurrentAlpha => currentAlpha;

    private void Reset()
    {
        AutoAssign();
        EnsureMeshComponents();
    }

    private void Awake()
    {
        AutoAssign();
        EnsureMeshComponents();
        EnsureRuntimeObjects();
        if (rebuildMeshOnStart)
            RebuildConeMesh(true);
        ApplyRendererVisibility();
    }

    private void OnEnable()
    {
        AutoAssign();
        EnsureMeshComponents();
        EnsureRuntimeObjects();
        if (rebuildMeshOnStart)
            RebuildConeMesh(true);
        UpdateVisualStyleIfNeeded(force: true);
        ApplyMaterialProperties();
        ApplyRendererVisibility();
    }

    private void OnDisable()
    {
        if (meshRenderer != null && hideRendererWhenFullyInvisible)
            meshRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (coneMesh != null)
        {
            if (Application.isPlaying)
                Destroy(coneMesh);
            else
                DestroyImmediate(coneMesh);
        }
    }

    private void OnValidate()
    {
        coneRadiusMultiplier = Mathf.Max(0.01f, coneRadiusMultiplier);
        coneAngleMultiplier = Mathf.Max(0.01f, coneAngleMultiplier);
        coneSegments = Mathf.Max(3, coneSegments);
        showFadeDuration = Mathf.Max(0.01f, showFadeDuration);
        hideFadeDuration = Mathf.Max(0.01f, hideFadeDuration);
        maxAlphaCalm = Mathf.Clamp01(maxAlphaCalm);
        maxAlphaAlert = Mathf.Clamp01(maxAlphaAlert);

        AutoAssign();
        EnsureMeshComponents();

        if (!Application.isPlaying)
        {
            EnsureRuntimeObjects();
            RebuildConeMesh(true);
            targetState = EvaluateTargetState();
            currentState = targetState;
            currentAlpha = GetTargetAlphaForState(targetState);
            targetAlpha = currentAlpha;
            UpdateVisualStyleIfNeeded(force: true);
            UpdateTransformFromPerception();
            ApplyMaterialProperties();
            ApplyRendererVisibility();
        }
    }

    private void LateUpdate()
    {
        AutoAssign();
        EnsureMeshComponents();
        EnsureRuntimeObjects();

        RebuildConeMesh(false);
        targetState = EvaluateTargetState();
        UpdateVisualStyleIfNeeded();
        UpdateFade(Application.isPlaying ? Time.deltaTime : 0f);

        if (followOriginEveryFrame)
            UpdateTransformFromPerception();

        ApplyMaterialProperties();
        ApplyRendererVisibility();
    }

    private void AutoAssign()
    {
        if (perception == null)
            perception = GetComponentInParent<EnemyPerception25D>();
        if (brain == null)
            brain = GetComponentInParent<EnemyBrainBT25D>();
        if (coneOrigin == null)
            coneOrigin = transform;
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
    }

    private void EnsureMeshComponents()
    {
        if (meshFilter == null)
            meshFilter = gameObject.GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        if (meshRenderer == null)
            meshRenderer = gameObject.GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
    }

    private void EnsureRuntimeObjects()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (coneMesh == null)
        {
            coneMesh = new Mesh { name = "EnemyVisionConeVisual25D_Mesh" };
            coneMesh.MarkDynamic();
        }

        if (meshFilter != null && meshFilter.sharedMesh != coneMesh)
            meshFilter.sharedMesh = coneMesh;
    }

    private VisionConeVisualState EvaluateTargetState()
    {
        if (perception == null)
            return VisionConeVisualState.Hidden;

        if (brain != null)
        {
            if (brain.IsInActiveCombatPressure)
                return VisionConeVisualState.Hidden;
        }

        return perception.IsAlert ? VisionConeVisualState.AlertVisible : VisionConeVisualState.CalmVisible;
    }

    private void UpdateFade(float deltaTime)
    {
        targetAlpha = GetTargetAlphaForState(targetState);
        if (!useSmoothFade || !Application.isPlaying)
        {
            currentAlpha = targetAlpha;
            currentState = targetState;
            return;
        }

        if (Mathf.Approximately(currentAlpha, targetAlpha))
        {
            currentAlpha = targetAlpha;
            currentState = targetState;
            return;
        }

        float duration = targetAlpha > currentAlpha ? showFadeDuration : hideFadeDuration;
        float step = duration > 0.0001f ? deltaTime / duration : 1f;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, step);
        currentState = targetState;
    }

    private float GetTargetAlphaForState(VisionConeVisualState state)
    {
        switch (state)
        {
            case VisionConeVisualState.CalmVisible:
                return maxAlphaCalm;
            case VisionConeVisualState.AlertVisible:
                return maxAlphaAlert;
            default:
                return 0f;
        }
    }

    private void UpdateVisualStyleIfNeeded(bool force = false)
    {
        if (meshRenderer == null)
            return;

        if (!force && currentState == targetState)
            return;

        if (!useSeparateMaterials)
            return;

        Material desiredMaterial = null;
        switch (targetState)
        {
            case VisionConeVisualState.CalmVisible:
                desiredMaterial = calmMaterial != null ? calmMaterial : alertMaterial;
                break;
            case VisionConeVisualState.AlertVisible:
                desiredMaterial = alertMaterial != null ? alertMaterial : calmMaterial;
                break;
            default:
                desiredMaterial = currentState == VisionConeVisualState.AlertVisible && alertMaterial != null ? alertMaterial : calmMaterial;
                break;
        }

        if (desiredMaterial != null && meshRenderer.sharedMaterial != desiredMaterial)
            meshRenderer.sharedMaterial = desiredMaterial;
    }

    private void ApplyRendererVisibility()
    {
        if (meshRenderer == null)
            return;

        bool shouldBeVisible = !hideRendererWhenFullyInvisible || currentAlpha > 0.001f;
        if (meshRenderer.enabled != shouldBeVisible)
            meshRenderer.enabled = shouldBeVisible;
    }

    private void ApplyMaterialProperties()
    {
        if (meshRenderer == null || propertyBlock == null)
            return;

        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(alphaPropertyName, currentAlpha);

        float intensity = targetState == VisionConeVisualState.AlertVisible ? alertIntensity : calmIntensity;
        propertyBlock.SetFloat(intensityPropertyName, intensity);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private void RebuildConeMesh(bool force)
    {
        if (perception == null || coneMesh == null)
            return;

        float radius = Mathf.Max(0.01f, perception.DetectionRadiusForVisionCone * coneRadiusMultiplier);
        float angle = Mathf.Clamp(perception.FieldOfViewDegreesForVisionCone * coneAngleMultiplier, 0.1f, 179f);
        int segments = Mathf.Max(3, coneSegments);

        if (!force && Mathf.Approximately(radius, lastBuiltRadius) && Mathf.Approximately(angle, lastBuiltAngle) && segments == lastBuiltSegments)
            return;

        int vertexCount = segments + 2;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0f, 0.5f);

        float halfAngle = angle * 0.5f;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float sampleAngle = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
            float x = Mathf.Cos(sampleAngle) * radius;
            float y = Mathf.Sin(sampleAngle) * radius;
            int index = i + 1;
            vertices[index] = new Vector3(x, y, 0f);
            uv[index] = new Vector2(Mathf.InverseLerp(-halfAngle, halfAngle, Mathf.Lerp(-halfAngle, halfAngle, t)), 1f);
        }

        for (int i = 0; i < segments; i++)
        {
            int triIndex = i * 3;
            triangles[triIndex] = 0;
            triangles[triIndex + 1] = i + 1;
            triangles[triIndex + 2] = i + 2;
        }

        coneMesh.Clear();
        coneMesh.vertices = vertices;
        coneMesh.uv = uv;
        coneMesh.triangles = triangles;
        coneMesh.RecalculateBounds();
        coneMesh.RecalculateNormals();

        lastBuiltRadius = radius;
        lastBuiltAngle = angle;
        lastBuiltSegments = segments;
    }

    private Transform GetEffectiveOrigin()
    {
        if (eyeOriginOverride != null)
            return eyeOriginOverride;
        if (perception != null && perception.EyeOriginTransform != null)
            return perception.EyeOriginTransform;
        if (coneOrigin != null)
            return coneOrigin;
        return transform;
    }

    private void UpdateTransformFromPerception()
    {
        Transform origin = GetEffectiveOrigin();
        if (origin != null)
        {
            Vector3 pos = origin.position;
            pos.z += localZOffset;
            transform.position = pos;
        }

        Vector3 forward = perception != null ? perception.CurrentVisionForward : Vector3.right;
        forward.z = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.right;

        transform.rotation = Quaternion.FromToRotation(Vector3.right, forward.normalized);
    }
}
