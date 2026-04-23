using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlanetEvolutionController : MonoBehaviour
{
    [Serializable]
    public class References
    {
        public StylizedPlanetMeshGenerator PlanetGenerator;
        public Renderer CloudRenderer;
    }

    [Serializable]
    public class Toggles
    {
        public bool EnableSurfaceEvolution = true;
        public bool EnableCloudEvolution = true;
    }

    [Serializable]
    public class TimingSettings
    {
        [Min(0.02f)] public float RebuildInterval = 0.10f;
        public bool UseUnscaledTime = false;
    }

    [Serializable]
    public class SurfaceOffsetRanges
    {
        [Min(0f)] public float ContinentOffsetRange = 18f;
        [Min(0f)] public float ContinentWarpOffsetRange = 14f;
        [Min(0f)] public float DesertOffsetRange = 12f;
        [Min(0f)] public float MountainOffsetRange = 12f;
        [Min(0f)] public float VolcanoOffsetRange = 10f;
        [Min(0f)] public float LavaOffsetRange = 10f;
    }

    [Serializable]
    public class SurfaceScalarRanges
    {
        [Min(0f)] public float SeaLevelAmplitude = 0.06f;
        [Min(0f)] public float DesertThresholdAmplitude = 0.06f;
        [Min(0f)] public float MountainThresholdAmplitude = 0.05f;
        [Min(0f)] public float VolcanoThresholdAmplitude = 0.07f;
        [Min(0f)] public float LavaThresholdAmplitude = 0.08f;
    }

    [Serializable]
    public class CloudRanges
    {
        [Min(0f)] public float CloudOffsetRange = 16f;
        [Min(0f)] public float CloudWarpOffsetRange = 16f;
        [Min(0f)] public float CloudScaleAmplitude = 1.20f;
        [Min(0f)] public float CloudWarpScaleAmplitude = 2.20f;
        [Min(0f)] public float CloudWarpStrengthAmplitude = 0.24f;
        [Min(0f)] public float CloudCoverageAmplitude = 0.18f;
    }

    [Serializable]
    public class SpeedSettings
    {
        [Min(0.001f)] public float ContinentMorphSpeed = 0.060f;
        [Min(0.001f)] public float WaterMorphSpeed = 0.075f;
        [Min(0.001f)] public float BiomeMorphSpeed = 0.110f;
        [Min(0.001f)] public float VolcanoMorphSpeed = 0.160f;
        [Min(0.001f)] public float CloudOffsetSpeed = 0.180f;
        [Min(0.001f)] public float CloudWarpOffsetSpeed = 0.260f;
        [Min(0.001f)] public float CloudScaleSpeed = 0.140f;
        [Min(0.001f)] public float CloudCoverageSpeed = 0.200f;
    }

    [Serializable]
    private struct SurfaceBaseState
    {
        public Vector3 ContinentOffset;
        public Vector3 ContinentWarpOffset;
        public Vector3 DesertOffset;
        public Vector3 MountainOffset;
        public Vector3 VolcanoOffset;
        public Vector3 LavaOffset;
        public float SeaLevel;
        public float DesertThreshold;
        public float MountainThreshold;
        public float VolcanoThreshold;
        public float LavaThreshold;

        public float ContinentScale;
        public float ContinentWarpScale;
        public float ContinentWarpStrength;
        public float CoastBlend;
        public float TerrainDetailScale;
        public float TerrainDetailStrength;
        public float TropicsWidth;
        public float DesertScale;
        public float DesertBlend;
        public float MountainScale;
        public float MountainBlend;
        public float SnowLatitudeStart;
        public float SnowBlend;
        public float VolcanoScale;
        public float VolcanoBlend;
        public float VolcanoLatitudeLimit;
    }

    [Serializable]
    private struct CloudBaseState
    {
        public Vector3 CloudOffset;
        public Vector3 CloudWarpOffset;
        public float CloudScale;
        public float CloudWarpScale;
        public float CloudWarpStrength;
        public float CloudCoverage;
    }

    [Serializable]
    private struct WorldState
    {
        public Vector3 ContinentOffset;
        public Vector3 ContinentWarpOffset;
        public Vector3 DesertOffset;
        public Vector3 MountainOffset;
        public Vector3 VolcanoOffset;
        public Vector3 LavaOffset;
        public float SeaLevel;
        public float DesertThreshold;
        public float MountainThreshold;
        public float VolcanoThreshold;
        public float LavaThreshold;

        public Vector3 CloudOffset;
        public Vector3 CloudWarpOffset;
        public float CloudScale;
        public float CloudWarpScale;
        public float CloudWarpStrength;
        public float CloudCoverage;
    }

    [Serializable]
    private struct PhaseBank
    {
        public Vector3 ContinentOffset;
        public Vector3 ContinentWarpOffset;
        public Vector3 DesertOffset;
        public Vector3 MountainOffset;
        public Vector3 VolcanoOffset;
        public Vector3 LavaOffset;
        public float SeaLevel;
        public float DesertThreshold;
        public float MountainThreshold;
        public float VolcanoThreshold;
        public float LavaThreshold;

        public Vector3 CloudOffset;
        public Vector3 CloudWarpOffset;
        public float CloudScale;
        public float CloudWarpScale;
        public float CloudWarpStrength;
        public float CloudCoverage;
    }

    [Header("References")]
    public References Refs = new References();

    [Header("Toggles")]
    public Toggles Mode = new Toggles();

    [Header("Timing")]
    public TimingSettings Timing = new TimingSettings();

    [Header("Speeds")]
    public SpeedSettings Speeds = new SpeedSettings();

    [Header("Surface Offset Ranges")]
    public SurfaceOffsetRanges SurfaceOffsets = new SurfaceOffsetRanges();

    [Header("Surface Scalar Ranges")]
    public SurfaceScalarRanges SurfaceScalars = new SurfaceScalarRanges();

    [Header("Cloud Ranges")]
    public CloudRanges CloudSettings = new CloudRanges();

    [Header("Randomization")]
    public int EvolutionSeed = 1337;

    private SurfaceBaseState _surfaceBase;
    private CloudBaseState _cloudBase;
    private PhaseBank _phases;
    private Material _cloudMaterialRuntime;
    private bool _initialized;
    private float _surfaceTimer;
    private float _elapsedTime;

    private static readonly int CloudScaleId = Shader.PropertyToID("_CloudScale");
    private static readonly int CloudWarpScaleId = Shader.PropertyToID("_CloudWarpScale");
    private static readonly int CloudWarpStrengthId = Shader.PropertyToID("_CloudWarpStrength");
    private static readonly int CloudCoverageId = Shader.PropertyToID("_CloudCoverage");
    private static readonly int CloudOffsetId = Shader.PropertyToID("_CloudOffset");
    private static readonly int CloudWarpOffsetId = Shader.PropertyToID("_CloudWarpOffset");

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            Initialize();
        }
    }

    private void OnValidate()
    {
        if (Timing != null)
        {
            Timing.RebuildInterval = Mathf.Max(0.02f, Timing.RebuildInterval);
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || !_initialized)
        {
            return;
        }

        float delta = Timing.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _elapsedTime += delta;

        WorldState state = EvaluateWorldState(_elapsedTime);

        if (Mode.EnableCloudEvolution)
        {
            ApplyCloudState(state);
        }

        if (Mode.EnableSurfaceEvolution && Refs.PlanetGenerator != null)
        {
            _surfaceTimer += delta;
            if (_surfaceTimer >= Timing.RebuildInterval)
            {
                _surfaceTimer = 0f;
                ApplySurfaceState(state);
                Refs.PlanetGenerator.Regenerate();
            }
        }
    }

    [ContextMenu("Capture Current Settings As Base")]
    public void CaptureCurrentSettingsAsBase()
    {
        InitializeReferences();
        CaptureBaseValues();
        BuildPhaseBank();
        _elapsedTime = 0f;
        _surfaceTimer = 0f;
        _initialized = Refs.PlanetGenerator != null || Refs.CloudRenderer != null;

        if (_initialized)
        {
            WorldState state = EvaluateWorldState(0f);
            if (Mode.EnableCloudEvolution)
            {
                ApplyCloudState(state);
            }
            if (Mode.EnableSurfaceEvolution && Refs.PlanetGenerator != null)
            {
                ApplySurfaceState(state);
                Refs.PlanetGenerator.Regenerate();
            }
        }
    }

    private void Initialize()
    {
        InitializeReferences();
        if (Refs.PlanetGenerator == null && Refs.CloudRenderer == null)
        {
            return;
        }

        CaptureBaseValues();
        BuildPhaseBank();
        _elapsedTime = 0f;
        _surfaceTimer = 0f;
        _initialized = true;

        WorldState state = EvaluateWorldState(0f);
        if (Mode.EnableCloudEvolution)
        {
            ApplyCloudState(state);
        }
    }

    private void InitializeReferences()
    {
        if (Refs.PlanetGenerator == null)
        {
            Refs.PlanetGenerator = GetComponentInChildren<StylizedPlanetMeshGenerator>();
        }

        if (Refs.CloudRenderer == null)
        {
            PlanetLayerRotator rotator = GetComponentInChildren<PlanetLayerRotator>();
            if (rotator != null)
            {
                Refs.CloudRenderer = rotator.GetComponent<Renderer>();
            }
        }

        if (Refs.CloudRenderer != null)
        {
            _cloudMaterialRuntime = Refs.CloudRenderer.material;
        }
    }

    private void CaptureBaseValues()
    {
        if (Refs.PlanetGenerator != null)
        {
            StylizedPlanetMeshGenerator g = Refs.PlanetGenerator;
            _surfaceBase.ContinentOffset = g.ContinentOffset;
            _surfaceBase.ContinentWarpOffset = g.ContinentWarpOffset;
            _surfaceBase.DesertOffset = g.DesertOffset;
            _surfaceBase.MountainOffset = g.MountainOffset;
            _surfaceBase.VolcanoOffset = g.VolcanoOffset;
            _surfaceBase.LavaOffset = g.LavaOffset;
            _surfaceBase.SeaLevel = g.SeaLevel;
            _surfaceBase.DesertThreshold = g.DesertThreshold;
            _surfaceBase.MountainThreshold = g.MountainThreshold;
            _surfaceBase.VolcanoThreshold = g.VolcanoThreshold;
            _surfaceBase.LavaThreshold = g.LavaThreshold;

            _surfaceBase.ContinentScale = g.ContinentScale;
            _surfaceBase.ContinentWarpScale = g.ContinentWarpScale;
            _surfaceBase.ContinentWarpStrength = g.ContinentWarpStrength;
            _surfaceBase.CoastBlend = g.CoastBlend;
            _surfaceBase.TerrainDetailScale = g.TerrainDetailScale;
            _surfaceBase.TerrainDetailStrength = g.TerrainDetailStrength;
            _surfaceBase.TropicsWidth = g.TropicsWidth;
            _surfaceBase.DesertScale = g.DesertScale;
            _surfaceBase.DesertBlend = g.DesertBlend;
            _surfaceBase.MountainScale = g.MountainScale;
            _surfaceBase.MountainBlend = g.MountainBlend;
            _surfaceBase.SnowLatitudeStart = g.SnowLatitudeStart;
            _surfaceBase.SnowBlend = g.SnowBlend;
            _surfaceBase.VolcanoScale = g.VolcanoScale;
            _surfaceBase.VolcanoBlend = g.VolcanoBlend;
            _surfaceBase.VolcanoLatitudeLimit = g.VolcanoLatitudeLimit;
        }

        if (_cloudMaterialRuntime != null)
        {
            _cloudBase.CloudOffset = GetVectorIfExists(_cloudMaterialRuntime, CloudOffsetId, Vector4.zero);
            _cloudBase.CloudWarpOffset = GetVectorIfExists(_cloudMaterialRuntime, CloudWarpOffsetId, Vector4.zero);
            _cloudBase.CloudScale = GetFloatIfExists(_cloudMaterialRuntime, CloudScaleId, 2.6f);
            _cloudBase.CloudWarpScale = GetFloatIfExists(_cloudMaterialRuntime, CloudWarpScaleId, 6.0f);
            _cloudBase.CloudWarpStrength = GetFloatIfExists(_cloudMaterialRuntime, CloudWarpStrengthId, 0.18f);
            _cloudBase.CloudCoverage = GetFloatIfExists(_cloudMaterialRuntime, CloudCoverageId, 0.54f);
        }
    }

    private void BuildPhaseBank()
    {
        System.Random rng = new System.Random(EvolutionSeed);
        _phases.ContinentOffset = RandomPhaseVector(rng);
        _phases.ContinentWarpOffset = RandomPhaseVector(rng);
        _phases.DesertOffset = RandomPhaseVector(rng);
        _phases.MountainOffset = RandomPhaseVector(rng);
        _phases.VolcanoOffset = RandomPhaseVector(rng);
        _phases.LavaOffset = RandomPhaseVector(rng);
        _phases.SeaLevel = RandomPhase(rng);
        _phases.DesertThreshold = RandomPhase(rng);
        _phases.MountainThreshold = RandomPhase(rng);
        _phases.VolcanoThreshold = RandomPhase(rng);
        _phases.LavaThreshold = RandomPhase(rng);

        _phases.CloudOffset = RandomPhaseVector(rng);
        _phases.CloudWarpOffset = RandomPhaseVector(rng);
        _phases.CloudScale = RandomPhase(rng);
        _phases.CloudWarpScale = RandomPhase(rng);
        _phases.CloudWarpStrength = RandomPhase(rng);
        _phases.CloudCoverage = RandomPhase(rng);
    }

    private WorldState EvaluateWorldState(float t)
    {
        WorldState state = new WorldState();

        state.ContinentOffset = _surfaceBase.ContinentOffset + EvaluateOffset(t, Speeds.ContinentMorphSpeed, SurfaceOffsets.ContinentOffsetRange, _phases.ContinentOffset);
        state.ContinentWarpOffset = _surfaceBase.ContinentWarpOffset + EvaluateOffset(t, Speeds.ContinentMorphSpeed * 1.13f, SurfaceOffsets.ContinentWarpOffsetRange, _phases.ContinentWarpOffset);
        state.DesertOffset = _surfaceBase.DesertOffset + EvaluateOffset(t, Speeds.BiomeMorphSpeed, SurfaceOffsets.DesertOffsetRange, _phases.DesertOffset);
        state.MountainOffset = _surfaceBase.MountainOffset + EvaluateOffset(t, Speeds.BiomeMorphSpeed * 0.9f, SurfaceOffsets.MountainOffsetRange, _phases.MountainOffset);
        state.VolcanoOffset = _surfaceBase.VolcanoOffset + EvaluateOffset(t, Speeds.VolcanoMorphSpeed, SurfaceOffsets.VolcanoOffsetRange, _phases.VolcanoOffset);
        state.LavaOffset = _surfaceBase.LavaOffset + EvaluateOffset(t, Speeds.VolcanoMorphSpeed * 1.18f, SurfaceOffsets.LavaOffsetRange, _phases.LavaOffset);

        state.SeaLevel = ClampSeaLevel(_surfaceBase.SeaLevel + EvaluateScalar(t, Speeds.WaterMorphSpeed, SurfaceScalars.SeaLevelAmplitude, _phases.SeaLevel));
        state.DesertThreshold = Mathf.Clamp(_surfaceBase.DesertThreshold + EvaluateScalar(t, Speeds.BiomeMorphSpeed, SurfaceScalars.DesertThresholdAmplitude, _phases.DesertThreshold), 0.18f, 0.92f);
        state.MountainThreshold = Mathf.Clamp(_surfaceBase.MountainThreshold + EvaluateScalar(t, Speeds.BiomeMorphSpeed * 0.92f, SurfaceScalars.MountainThresholdAmplitude, _phases.MountainThreshold), 0.20f, 0.95f);
        state.VolcanoThreshold = Mathf.Clamp(_surfaceBase.VolcanoThreshold + EvaluateScalar(t, Speeds.VolcanoMorphSpeed, SurfaceScalars.VolcanoThresholdAmplitude, _phases.VolcanoThreshold), 0.18f, 0.97f);
        state.LavaThreshold = Mathf.Clamp(_surfaceBase.LavaThreshold + EvaluateScalar(t, Speeds.VolcanoMorphSpeed * 1.1f, SurfaceScalars.LavaThresholdAmplitude, _phases.LavaThreshold), 0.15f, 0.99f);

        state.CloudOffset = _cloudBase.CloudOffset + EvaluateOffset(t, Speeds.CloudOffsetSpeed, CloudSettings.CloudOffsetRange, _phases.CloudOffset);
        state.CloudWarpOffset = _cloudBase.CloudWarpOffset + EvaluateOffset(t, Speeds.CloudWarpOffsetSpeed, CloudSettings.CloudWarpOffsetRange, _phases.CloudWarpOffset);
        state.CloudScale = Mathf.Clamp(_cloudBase.CloudScale + EvaluateScalar(t, Speeds.CloudScaleSpeed, CloudSettings.CloudScaleAmplitude, _phases.CloudScale), 0.1f, 16.0f);
        state.CloudWarpScale = Mathf.Clamp(_cloudBase.CloudWarpScale + EvaluateScalar(t, Speeds.CloudScaleSpeed * 1.2f, CloudSettings.CloudWarpScaleAmplitude, _phases.CloudWarpScale), 0.1f, 24.0f);
        state.CloudWarpStrength = Mathf.Clamp01(_cloudBase.CloudWarpStrength + EvaluateScalar(t, Speeds.CloudWarpOffsetSpeed, CloudSettings.CloudWarpStrengthAmplitude, _phases.CloudWarpStrength));
        state.CloudCoverage = Mathf.Clamp01(_cloudBase.CloudCoverage + EvaluateScalar(t, Speeds.CloudCoverageSpeed, CloudSettings.CloudCoverageAmplitude, _phases.CloudCoverage));

        return state;
    }

    private Vector3 EvaluateOffset(float time, float speed, float range, Vector3 phase)
    {
        if (range <= 0f)
        {
            return Vector3.zero;
        }

        return new Vector3(
            SignedPerlin(time * speed, phase.x) * range,
            SignedPerlin(time * speed, phase.y) * range,
            SignedPerlin(time * speed, phase.z) * range);
    }

    private float EvaluateScalar(float time, float speed, float amplitude, float phase)
    {
        if (amplitude <= 0f)
        {
            return 0f;
        }

        return SignedPerlin(time * speed, phase) * amplitude;
    }

    private void ApplySurfaceState(WorldState state)
    {
        StylizedPlanetMeshGenerator g = Refs.PlanetGenerator;
        if (g == null)
        {
            return;
        }

        // Frozen artistic baseline.
        g.ContinentScale = _surfaceBase.ContinentScale;
        g.ContinentWarpScale = _surfaceBase.ContinentWarpScale;
        g.ContinentWarpStrength = _surfaceBase.ContinentWarpStrength;
        g.CoastBlend = _surfaceBase.CoastBlend;
        g.TerrainDetailScale = _surfaceBase.TerrainDetailScale;
        g.TerrainDetailStrength = _surfaceBase.TerrainDetailStrength;
        g.TropicsWidth = _surfaceBase.TropicsWidth;
        g.DesertScale = _surfaceBase.DesertScale;
        g.DesertBlend = _surfaceBase.DesertBlend;
        g.MountainScale = _surfaceBase.MountainScale;
        g.MountainBlend = _surfaceBase.MountainBlend;
        g.SnowLatitudeStart = _surfaceBase.SnowLatitudeStart;
        g.SnowBlend = _surfaceBase.SnowBlend;
        g.VolcanoScale = _surfaceBase.VolcanoScale;
        g.VolcanoBlend = _surfaceBase.VolcanoBlend;
        g.VolcanoLatitudeLimit = _surfaceBase.VolcanoLatitudeLimit;

        g.ContinentOffset = state.ContinentOffset;
        g.ContinentWarpOffset = state.ContinentWarpOffset;
        g.DesertOffset = state.DesertOffset;
        g.MountainOffset = state.MountainOffset;
        g.VolcanoOffset = state.VolcanoOffset;
        g.LavaOffset = state.LavaOffset;

        g.SeaLevel = state.SeaLevel;
        g.DesertThreshold = state.DesertThreshold;
        g.MountainThreshold = state.MountainThreshold;
        g.VolcanoThreshold = state.VolcanoThreshold;
        g.LavaThreshold = state.LavaThreshold;
    }

    private void ApplyCloudState(WorldState state)
    {
        if (_cloudMaterialRuntime == null)
        {
            return;
        }

        SetFloatIfExists(_cloudMaterialRuntime, CloudScaleId, state.CloudScale);
        SetFloatIfExists(_cloudMaterialRuntime, CloudWarpScaleId, state.CloudWarpScale);
        SetFloatIfExists(_cloudMaterialRuntime, CloudWarpStrengthId, state.CloudWarpStrength);
        SetFloatIfExists(_cloudMaterialRuntime, CloudCoverageId, state.CloudCoverage);
        SetVectorIfExists(_cloudMaterialRuntime, CloudOffsetId, state.CloudOffset);
        SetVectorIfExists(_cloudMaterialRuntime, CloudWarpOffsetId, state.CloudWarpOffset);
    }

    private static float SignedPerlin(float time, float phase)
    {
        return Mathf.PerlinNoise(time + phase, phase * 0.6180339887f) * 2f - 1f;
    }

    private static float RandomPhase(System.Random rng)
    {
        return (float)rng.NextDouble() * 1000f;
    }

    private static Vector3 RandomPhaseVector(System.Random rng)
    {
        return new Vector3(RandomPhase(rng), RandomPhase(rng), RandomPhase(rng));
    }

    private float ClampSeaLevel(float value)
    {
        return Mathf.Clamp(value, 0.18f, 0.72f);
    }

    private static float GetFloatIfExists(Material material, int propertyId, float fallback)
    {
        return material != null && material.HasProperty(propertyId) ? material.GetFloat(propertyId) : fallback;
    }

    private static Vector3 GetVectorIfExists(Material material, int propertyId, Vector4 fallback)
    {
        if (material != null && material.HasProperty(propertyId))
        {
            Vector4 v = material.GetVector(propertyId);
            return new Vector3(v.x, v.y, v.z);
        }

        return new Vector3(fallback.x, fallback.y, fallback.z);
    }

    private static void SetFloatIfExists(Material material, int propertyId, float value)
    {
        if (material != null && material.HasProperty(propertyId))
        {
            material.SetFloat(propertyId, value);
        }
    }

    private static void SetVectorIfExists(Material material, int propertyId, Vector3 value)
    {
        if (material != null && material.HasProperty(propertyId))
        {
            material.SetVector(propertyId, new Vector4(value.x, value.y, value.z, 0f));
        }
    }
}
