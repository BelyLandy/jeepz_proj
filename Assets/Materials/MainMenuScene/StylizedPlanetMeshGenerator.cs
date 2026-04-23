using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class StylizedPlanetMeshGenerator : MonoBehaviour
{
    public enum PlanetBiome
    {
        Water,
        Plains,
        Tropical,
        Desert,
        Mountain,
        Snow,
        VolcanicRock,
        Lava
    }

    [Serializable]
    private struct CellSampleData
    {
        public Vector3 sphereDir;
        public float latitude;
        public float continentValue;
        public float landMask;
        public float waterMask;
        public float landHeight01;
        public float dryness;
        public float tropicalMask;
        public float desertMask;
        public float mountainMask;
        public float snowMask;
        public float volcanoMask;
        public float lavaMask;
        public PlanetBiome biome;
    }

    private static readonly Vector3[] FaceNormals =
    {
        Vector3.up,
        Vector3.down,
        Vector3.left,
        Vector3.right,
        Vector3.forward,
        Vector3.back,
    };

    [Header("Geometry")]
    [Min(0.1f)] public float Radius = 1f;
    [Range(2, 64)] public int ResolutionPerFace = 12;
    public bool GenerateOnValidate = true;
    public bool UseFlatCellNormals = true;

    [Header("Geography")]
    [Range(0.0f, 1.0f)] public float SeaLevel = 0.414f;
    [Range(0.001f, 8.0f)] public float ContinentScale = 1.15f;
    [Range(0.001f, 12.0f)] public float ContinentWarpScale = 2.8f;
    [Range(0.0f, 1.0f)] public float ContinentWarpStrength = 0.14f;
    [Range(0.001f, 0.25f)] public float CoastBlend = 0.03f;
    [Range(0.001f, 20.0f)] public float TerrainDetailScale = 6.5f;
    [Range(0.0f, 1.0f)] public float TerrainDetailStrength = 0.12f;
    [Range(0.0f, 1.0f)] public float TropicsWidth = 0.495f;
    [Range(0.001f, 20.0f)] public float DesertScale = 5.5f;
    [Range(0.0f, 1.0f)] public float DesertThreshold = 0.58f;
    [Range(0.001f, 0.25f)] public float DesertBlend = 0.08f;
    [Range(0.001f, 20.0f)] public float MountainScale = 7.0f;
    [Range(0.0f, 1.0f)] public float MountainThreshold = 0.62f;
    [Range(0.001f, 0.25f)] public float MountainBlend = 0.12f;
    [Range(0.0f, 1.0f)] public float SnowLatitudeStart = 0.72f;
    [Range(0.001f, 0.25f)] public float SnowBlend = 0.147f;

    [Header("Evolution Offsets")]
    public Vector3 ContinentOffset = Vector3.zero;
    public Vector3 ContinentWarpOffset = Vector3.zero;
    public Vector3 DesertOffset = Vector3.zero;
    public Vector3 MountainOffset = Vector3.zero;
    public Vector3 VolcanoOffset = Vector3.zero;
    public Vector3 LavaOffset = Vector3.zero;

    [Header("Volcano")]
    [Range(0.001f, 20.0f)] public float VolcanoScale = 6.0f;
    [Range(0.0f, 1.0f)] public float VolcanoThreshold = 0.72f;
    [Range(0.001f, 0.25f)] public float VolcanoBlend = 0.08f;
    [Range(0.5f, 2.0f)] public float VolcanoHeightBias = 1.25f;
    [Range(0.0f, 1.0f)] public float LavaThreshold = 0.84f;
    [Range(0.0f, 1.0f)] public float VolcanoLatitudeLimit = 0.72f;

    [Header("Displacement")]
    [Range(0.0f, 1.0f)] public float DisplacementStrength = 0.18f;
    [Range(2, 12)] public int HeightSteps = 4;
    [Range(0.0f, 0.5f)] public float OceanLevelOffset = 0.075f;
    [Range(0.0f, 2.0f)] public float MountainHeightBias = 1.15f;
    [Range(0.0f, 1.0f)] public float WaterFlattening = 0.8f;

    [Header("Biome Colors")]
    public Color OceanDeepColor = new Color32(0x00, 0x00, 0x00, 0xFF);
    public Color OceanShallowColor = new Color32(0x0A, 0x7B, 0xFF, 0xFF);
    public Color PlainsColor = new Color32(0x91, 0xDA, 0x24, 0xFF);
    public Color TropicalColor = new Color32(0xC4, 0xDD, 0x00, 0xFF);
    public Color DesertColor = new Color32(0xFF, 0xDA, 0x24, 0xFF);
    public Color MountainColor = new Color32(0x6E, 0x9C, 0xD1, 0xFF);
    public Color SnowColor = new Color32(0xE2, 0xFE, 0xFC, 0xFF);
    public Color VolcanicRockColor = new Color32(0x5A, 0x2A, 0x8A, 0xFF);
    public Color LavaColor = new Color32(0xFF, 0x4F, 0xD8, 0xFF);

    private Mesh _generatedMesh;
    private MeshFilter _meshFilter;

    private void Awake()
    {
        EnsureComponents();
        Regenerate();
    }

    private void Reset()
    {
        EnsureComponents();
        Regenerate();
    }

    private void OnValidate()
    {
        SanitizeParameters();
        if (GenerateOnValidate)
        {
            Regenerate();
        }
    }

    [ContextMenu("Regenerate")]
    public void Regenerate()
    {
        EnsureComponents();
        SanitizeParameters();

        var vertices = new List<Vector3>(EstimatedVertexCount());
        var normals = new List<Vector3>(EstimatedVertexCount());
        var colors = new List<Color>(EstimatedVertexCount());
        var uvs = new List<Vector2>(EstimatedVertexCount());
        var uv2s = new List<Vector4>(EstimatedVertexCount());
        var triangles = new List<int>(EstimatedTriangleIndexCount());

        BuildPlanet(vertices, normals, colors, uvs, uv2s, triangles);
        ApplyMesh(vertices, normals, colors, uvs, uv2s, triangles);
    }

    [ContextMenu("Clear Generated Mesh")]
    public void ClearGeneratedMesh()
    {
        EnsureComponents();

        if (_generatedMesh == null)
        {
            return;
        }

        _generatedMesh.Clear();
        _meshFilter.sharedMesh = _generatedMesh;
    }

    private void EnsureComponents()
    {
        if (_meshFilter == null)
        {
            _meshFilter = GetComponent<MeshFilter>();
        }

        if (_generatedMesh == null)
        {
            _generatedMesh = new Mesh
            {
                name = "StylizedPlanetMesh"
            };
            _generatedMesh.MarkDynamic();
        }
    }

    private void SanitizeParameters()
    {
        Radius = Mathf.Max(0.1f, Radius);
        ResolutionPerFace = Mathf.Clamp(ResolutionPerFace, 2, 64);
        HeightSteps = Mathf.Clamp(HeightSteps, 2, 12);
        ContinentScale = Mathf.Max(0.001f, ContinentScale);
        ContinentWarpScale = Mathf.Max(0.001f, ContinentWarpScale);
        TerrainDetailScale = Mathf.Max(0.001f, TerrainDetailScale);
        DesertScale = Mathf.Max(0.001f, DesertScale);
        MountainScale = Mathf.Max(0.001f, MountainScale);
        VolcanoScale = Mathf.Max(0.001f, VolcanoScale);
        CoastBlend = Mathf.Max(0.001f, CoastBlend);
        DesertBlend = Mathf.Max(0.001f, DesertBlend);
        MountainBlend = Mathf.Max(0.001f, MountainBlend);
        SnowBlend = Mathf.Max(0.001f, SnowBlend);
        VolcanoBlend = Mathf.Max(0.001f, VolcanoBlend);
        SeaLevel = Mathf.Clamp01(SeaLevel);
        TropicsWidth = Mathf.Clamp01(TropicsWidth);
        DesertThreshold = Mathf.Clamp01(DesertThreshold);
        MountainThreshold = Mathf.Clamp01(MountainThreshold);
        SnowLatitudeStart = Mathf.Clamp01(SnowLatitudeStart);
        LavaThreshold = Mathf.Clamp01(LavaThreshold);
        VolcanoThreshold = Mathf.Clamp01(VolcanoThreshold);
        VolcanoLatitudeLimit = Mathf.Clamp01(VolcanoLatitudeLimit);
    }

    private int EstimatedVertexCount()
    {
        int cellCount = 6 * ResolutionPerFace * ResolutionPerFace;
        return cellCount * 4;
    }

    private int EstimatedTriangleIndexCount()
    {
        int cellCount = 6 * ResolutionPerFace * ResolutionPerFace;
        return cellCount * 6;
    }

    private void BuildPlanet(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Color> colors,
        List<Vector2> uvs,
        List<Vector4> uv2s,
        List<int> triangles)
    {
        foreach (Vector3 faceNormal in FaceNormals)
        {
            BuildFace(faceNormal, vertices, normals, colors, uvs, uv2s, triangles);
        }
    }

    private void BuildFace(
        Vector3 faceNormal,
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Color> colors,
        List<Vector2> uvs,
        List<Vector4> uv2s,
        List<int> triangles)
    {
        Vector3 axisA = new Vector3(faceNormal.y, faceNormal.z, faceNormal.x);
        Vector3 axisB = Vector3.Cross(faceNormal, axisA);
        float invRes = 1f / ResolutionPerFace;

        for (int y = 0; y < ResolutionPerFace; y++)
        {
            for (int x = 0; x < ResolutionPerFace; x++)
            {
                Vector3 dir00 = CubeToSphere(faceNormal, axisA, axisB, x * invRes, y * invRes);
                Vector3 dir10 = CubeToSphere(faceNormal, axisA, axisB, (x + 1) * invRes, y * invRes);
                Vector3 dir11 = CubeToSphere(faceNormal, axisA, axisB, (x + 1) * invRes, (y + 1) * invRes);
                Vector3 dir01 = CubeToSphere(faceNormal, axisA, axisB, x * invRes, (y + 1) * invRes);

                Vector3 cellCenterDir = (dir00 + dir10 + dir11 + dir01).normalized;
                CellSampleData cellSample = SamplePoint(cellCenterDir);
                Color cellColor = EvaluateBiomeColor(cellSample);
                Vector3 cellNormal = UseFlatCellNormals ? cellCenterDir : Vector3.zero;

                Vector3 v00 = BuildDisplacedPosition(dir00);
                Vector3 v10 = BuildDisplacedPosition(dir10);
                Vector3 v11 = BuildDisplacedPosition(dir11);
                Vector3 v01 = BuildDisplacedPosition(dir01);

                Vector4 cellExtra = new Vector4(
                    Mathf.Clamp01(cellSample.lavaMask),
                    Mathf.Clamp01(cellSample.volcanoMask),
                    0f,
                    0f);

                AddCellQuad(
                    vertices,
                    normals,
                    colors,
                    uvs,
                    uv2s,
                    triangles,
                    v00,
                    v10,
                    v11,
                    v01,
                    dir00,
                    dir10,
                    dir11,
                    dir01,
                    cellNormal,
                    cellColor,
                    cellCenterDir,
                    cellExtra);
            }
        }
    }

    private Vector3 CubeToSphere(Vector3 faceNormal, Vector3 axisA, Vector3 axisB, float u01, float v01)
    {
        float u = u01 * 2f - 1f;
        float v = v01 * 2f - 1f;
        Vector3 pointOnCube = faceNormal + u * axisA + v * axisB;
        return pointOnCube.normalized;
    }

    private Vector3 BuildDisplacedPosition(Vector3 sphereDir)
    {
        CellSampleData sample = SamplePoint(sphereDir);
        float displacement = EvaluateDisplacement(sample);
        return sphereDir * Mathf.Max(0.0001f, Radius + displacement);
    }

    private CellSampleData SamplePoint(Vector3 sphereDir)
    {
        sphereDir.Normalize();

        CellSampleData data = new CellSampleData
        {
            sphereDir = sphereDir,
            latitude = Mathf.Abs(sphereDir.y)
        };

        float continentBase = SampleTriplanarNoise(sphereDir, ContinentScale, ContinentOffset);
        float continentWarp = (SampleTriplanarNoise(sphereDir, ContinentWarpScale, new Vector3(17.3f, 9.1f, -5.4f) + ContinentWarpOffset) - 0.5f) * ContinentWarpStrength;

        data.continentValue = Mathf.Clamp01(continentBase + continentWarp);
        data.landMask = SmoothMask(data.continentValue, SeaLevel - CoastBlend, SeaLevel + CoastBlend);
        data.waterMask = 1f - data.landMask;
        data.landHeight01 = Mathf.Clamp01((data.continentValue - SeaLevel) / Mathf.Max(0.0001f, 1f - SeaLevel));

        float tropicalMaskRaw = 1f - SmoothMask(data.latitude, TropicsWidth, TropicsWidth + 0.15f);
        data.tropicalMask = tropicalMaskRaw * data.landMask;

        data.dryness = SampleTriplanarNoise(data.sphereDir, DesertScale, new Vector3(-14.5f, 21.7f, 4.2f) + DesertOffset);
        data.desertMask = SmoothMask(data.dryness, DesertThreshold - DesertBlend, DesertThreshold + DesertBlend) * data.tropicalMask * data.landMask;

        float detailNoise = SampleTriplanarNoise(data.sphereDir, TerrainDetailScale, new Vector3(3.7f, -8.2f, 12.4f));
        float mountainNoise = SampleTriplanarNoise(data.sphereDir, MountainScale, new Vector3(9.8f, -2.6f, 27.1f) + MountainOffset);
        float mountainInput = mountainNoise + data.landHeight01 * 0.35f + detailNoise * TerrainDetailStrength;
        data.mountainMask = SmoothMask(mountainInput, MountainThreshold - MountainBlend, MountainThreshold + MountainBlend) * data.landMask;

        float poleSnow = SmoothMask(data.latitude, SnowLatitudeStart, SnowLatitudeStart + SnowBlend);
        float mountainSnow = SmoothMask(mountainNoise + data.landHeight01 * 0.4f, 0.78f, 1.0f) * data.mountainMask;
        data.snowMask = Mathf.Clamp01(Mathf.Max(poleSnow, mountainSnow) * data.landMask);

        data.volcanoMask = EvaluateVolcanoMask(data.sphereDir, data.latitude, data.mountainMask, data.landHeight01, data.snowMask);
        data.lavaMask = EvaluateLavaMask(data.sphereDir, data.volcanoMask);

        data.biome = EvaluateBiome(data);
        return data;
    }

    private float EvaluateVolcanoMask(Vector3 sphereDir, float latitude, float mountainMask, float landHeight01, float snowMask)
    {
        float latitudeLimit = 1f - SmoothMask(latitude, VolcanoLatitudeLimit, Mathf.Min(1f, VolcanoLatitudeLimit + 0.15f));
        float volcanoNoise = SampleTriplanarNoise(sphereDir, VolcanoScale, new Vector3(-23.7f, 4.1f, 18.8f) + VolcanoOffset);
        float volcanoRegion = SmoothMask(volcanoNoise, VolcanoThreshold - VolcanoBlend, VolcanoThreshold + VolcanoBlend);
        float mountainSupport = Mathf.Lerp(0.35f, 1f, mountainMask) * mountainMask;
        float interiorSupport = Mathf.Lerp(0.55f, 1f, landHeight01);
        float snowBlock = 1f - Mathf.Clamp01(snowMask * 1.15f);
        return Mathf.Clamp01(volcanoRegion * mountainSupport * interiorSupport * latitudeLimit * snowBlock);
    }

    private float EvaluateLavaMask(Vector3 sphereDir, float volcanoMask)
    {
        if (volcanoMask <= 0.001f)
        {
            return 0f;
        }

        float lavaNoise = SampleTriplanarNoise(sphereDir, VolcanoScale * 1.65f, new Vector3(31.5f, -12.7f, 7.6f) + LavaOffset);
        float lavaCore = SmoothMask(lavaNoise, LavaThreshold - VolcanoBlend * 0.5f, Mathf.Min(1f, LavaThreshold + VolcanoBlend * 0.35f));
        return Mathf.Clamp01(lavaCore * volcanoMask);
    }

    private PlanetBiome EvaluateBiome(CellSampleData sample)
    {
        if (sample.landMask < 0.5f)
        {
            return PlanetBiome.Water;
        }

        if (sample.snowMask > 0.55f)
        {
            return PlanetBiome.Snow;
        }

        if (sample.lavaMask > 0.42f)
        {
            return PlanetBiome.Lava;
        }

        if (sample.volcanoMask > 0.42f)
        {
            return PlanetBiome.VolcanicRock;
        }

        if (sample.mountainMask > 0.58f)
        {
            return PlanetBiome.Mountain;
        }

        if (sample.desertMask > 0.5f)
        {
            return PlanetBiome.Desert;
        }

        if (sample.tropicalMask > 0.45f)
        {
            return PlanetBiome.Tropical;
        }

        return PlanetBiome.Plains;
    }

    private float EvaluateDisplacement(CellSampleData sample)
    {
        if (sample.biome == PlanetBiome.Water)
        {
            float waterDepth01 = Mathf.Clamp01((SeaLevel - sample.continentValue) / Mathf.Max(0.0001f, SeaLevel));
            float flattenedDepth = Mathf.Lerp(waterDepth01, 0.35f, WaterFlattening);
            float steppedDepth = Quantize01(flattenedDepth);
            return -OceanLevelOffset * Mathf.Lerp(0.35f, 1f, steppedDepth);
        }

        float landBase = Mathf.Lerp(0.08f, 0.58f, sample.landHeight01);
        float mountainous = sample.mountainMask * 0.35f * MountainHeightBias;
        float volcanicLift = sample.volcanoMask * 0.18f * VolcanoHeightBias;
        float snowyBoost = sample.snowMask * 0.08f;

        float biomeMultiplier = 1f;
        float additiveOffset = 0f;

        switch (sample.biome)
        {
            case PlanetBiome.Plains:
                biomeMultiplier = 0.82f;
                break;
            case PlanetBiome.Tropical:
                biomeMultiplier = 0.88f;
                break;
            case PlanetBiome.Desert:
                biomeMultiplier = 0.78f;
                break;
            case PlanetBiome.Mountain:
                biomeMultiplier = 1.18f;
                break;
            case PlanetBiome.Snow:
                biomeMultiplier = 1.08f;
                break;
            case PlanetBiome.VolcanicRock:
                biomeMultiplier = 1.15f * VolcanoHeightBias;
                additiveOffset = 0.04f;
                break;
            case PlanetBiome.Lava:
                biomeMultiplier = 1.05f * VolcanoHeightBias;
                additiveOffset = -0.015f;
                break;
        }

        float raw = Mathf.Clamp01((landBase + mountainous + volcanicLift + snowyBoost + additiveOffset) * biomeMultiplier);
        float stepped = Quantize01(raw);
        return stepped * DisplacementStrength;
    }

    private Color EvaluateBiomeColor(CellSampleData sample)
    {
        Color baseColor;

        if (sample.biome == PlanetBiome.Water)
        {
            float waterDepth01 = Mathf.Clamp01((SeaLevel - sample.continentValue) / Mathf.Max(0.0001f, SeaLevel));
            baseColor = Color.Lerp(OceanShallowColor, OceanDeepColor, waterDepth01);
        }
        else
        {
            switch (sample.biome)
            {
                case PlanetBiome.Plains:
                    baseColor = PlainsColor;
                    break;
                case PlanetBiome.Tropical:
                    baseColor = TropicalColor;
                    break;
                case PlanetBiome.Desert:
                    baseColor = DesertColor;
                    break;
                case PlanetBiome.Mountain:
                    baseColor = MountainColor;
                    break;
                case PlanetBiome.Snow:
                    baseColor = SnowColor;
                    break;
                case PlanetBiome.VolcanicRock:
                    baseColor = VolcanicRockColor;
                    break;
                case PlanetBiome.Lava:
                    baseColor = LavaColor;
                    break;
                default:
                    baseColor = PlainsColor;
                    break;
            }

            float nearCoast = Mathf.Clamp01(1f - sample.landHeight01);
            float coastTint = Mathf.Lerp(1f, 1.06f, nearCoast * 0.35f);
            baseColor *= coastTint;
        }

        float valueVariation = SampleTriplanarNoise(sample.sphereDir, 9.0f, new Vector3(11.8f, -4.1f, 2.4f));
        float valueScale = Mathf.Lerp(0.96f, 1.04f, valueVariation);

        if (sample.biome == PlanetBiome.VolcanicRock)
        {
            valueScale = Mathf.Lerp(0.92f, 1.03f, valueVariation);
        }
        else if (sample.biome == PlanetBiome.Lava)
        {
            valueScale = Mathf.Lerp(0.98f, 1.08f, valueVariation);
        }

        baseColor *= valueScale;
        baseColor.a = sample.biome == PlanetBiome.Water ? 1f : 0f;
        return ClampColor01(baseColor);
    }

    private void AddCellQuad(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Color> colors,
        List<Vector2> uvs,
        List<Vector4> uv2s,
        List<int> triangles,
        Vector3 v00,
        Vector3 v10,
        Vector3 v11,
        Vector3 v01,
        Vector3 dir00,
        Vector3 dir10,
        Vector3 dir11,
        Vector3 dir01,
        Vector3 cellNormal,
        Color cellColor,
        Vector3 outwardReference,
        Vector4 cellExtra)
    {
        int startIndex = vertices.Count;

        vertices.Add(v00);
        vertices.Add(v10);
        vertices.Add(v11);
        vertices.Add(v01);

        if (UseFlatCellNormals)
        {
            normals.Add(cellNormal);
            normals.Add(cellNormal);
            normals.Add(cellNormal);
            normals.Add(cellNormal);
        }
        else
        {
            normals.Add(dir00);
            normals.Add(dir10);
            normals.Add(dir11);
            normals.Add(dir01);
        }

        colors.Add(cellColor);
        colors.Add(cellColor);
        colors.Add(cellColor);
        colors.Add(cellColor);

        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(1f, 1f));
        uvs.Add(new Vector2(0f, 1f));

        uv2s.Add(cellExtra);
        uv2s.Add(cellExtra);
        uv2s.Add(cellExtra);
        uv2s.Add(cellExtra);

        Vector3 geometricNormal = Vector3.Cross(v10 - v00, v11 - v00).normalized;
        bool outwardFacing = Vector3.Dot(geometricNormal, outwardReference) > 0f;

        if (outwardFacing)
        {
            triangles.Add(startIndex + 0);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 0);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
        }
        else
        {
            triangles.Add(startIndex + 0);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 0);
            triangles.Add(startIndex + 3);
            triangles.Add(startIndex + 2);
        }
    }

    private void ApplyMesh(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Color> colors,
        List<Vector2> uvs,
        List<Vector4> uv2s,
        List<int> triangles)
    {
        _generatedMesh.Clear();
        _generatedMesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;

        _generatedMesh.SetVertices(vertices);
        _generatedMesh.SetNormals(normals);
        _generatedMesh.SetColors(colors);
        _generatedMesh.SetUVs(0, uvs);
        _generatedMesh.SetUVs(1, uv2s);
        _generatedMesh.SetTriangles(triangles, 0, true);
        _generatedMesh.RecalculateBounds();

        _meshFilter.sharedMesh = _generatedMesh;
    }

    private float Quantize01(float value)
    {
        value = Mathf.Clamp01(value);
        if (HeightSteps <= 1)
        {
            return value;
        }

        float maxStepIndex = HeightSteps - 1;
        return Mathf.Round(value * maxStepIndex) / maxStepIndex;
    }

    private float SampleTriplanarNoise(Vector3 sphereDir, float scale, Vector3 offset)
    {
        Vector3 blend = new Vector3(Mathf.Abs(sphereDir.x), Mathf.Abs(sphereDir.y), Mathf.Abs(sphereDir.z));
        blend = new Vector3(Mathf.Pow(blend.x, 4f), Mathf.Pow(blend.y, 4f), Mathf.Pow(blend.z, 4f));
        float sum = blend.x + blend.y + blend.z;
        if (sum <= 0.0001f)
        {
            return 0f;
        }

        blend /= sum;
        Vector3 p = sphereDir * scale + offset;

        float noiseX = Mathf.PerlinNoise(p.y + 113.17f, p.z + 57.41f);
        float noiseY = Mathf.PerlinNoise(p.x + 73.93f, p.z + 19.31f);
        float noiseZ = Mathf.PerlinNoise(p.x + 41.07f, p.y + 89.63f);

        return noiseX * blend.x + noiseY * blend.y + noiseZ * blend.z;
    }

    private float SmoothMask(float value, float edgeStart, float edgeEnd)
    {
        float t = Mathf.InverseLerp(edgeStart, edgeEnd, value);
        return t * t * (3f - 2f * t);
    }

    private Color ClampColor01(Color color)
    {
        return new Color(
            Mathf.Clamp01(color.r),
            Mathf.Clamp01(color.g),
            Mathf.Clamp01(color.b),
            Mathf.Clamp01(color.a));
    }
}
