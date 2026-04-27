using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroHealthShard25D : MonoBehaviour
{
    [Header("Phase 1 - Burst")]
    [SerializeField, Min(0.01f)] private float phaseOneDuration = 0.18f;
    [SerializeField, Min(0f)] private float phaseOneDistance = 0.45f;
    [SerializeField, Min(0f)] private float phaseOneHeight = 0.30f;

    [Header("Phase 2 - Homing Arc")]
    [SerializeField, Min(0.01f)] private float phaseTwoDuration = 0.32f;
    [SerializeField, Min(0f)] private float phaseTwoArcHeight = 0.18f;
    [SerializeField, Min(0f)] private float phaseTwoLateralOffset = 0.14f;
    [SerializeField, Min(0f)] private float arriveDistance = 0.06f;

    [Header("Final Magnet")]
    [SerializeField, Range(0.01f, 0.99f)] private float finalMagnetPortion = 0.15f;
    [SerializeField, Min(0f)] private float finalMagnetStrength = 1.35f;

    [Header("Jitter - Phase 1")]
    [SerializeField, Min(0f)] private float phaseOneDurationJitter = 0.04f;
    [SerializeField, Min(0f)] private float phaseOneDistanceJitter = 0.10f;
    [SerializeField, Min(0f)] private float phaseOneHeightJitter = 0.08f;

    [Header("Jitter - Phase 2")]
    [SerializeField, Min(0f)] private float phaseTwoDurationJitter = 0.07f;
    [SerializeField, Min(0f)] private float phaseTwoArcHeightJitter = 0.10f;
    [SerializeField, Min(0f)] private float phaseTwoLateralOffsetJitter = 0.12f;

    [Header("Jitter - Scatter / Magnet")]
    [SerializeField, Min(0f)] private float scatterAngleJitter = 35f;
    [SerializeField, Min(0f)] private float scatterVerticalJitter = 0.20f;
    [SerializeField, Min(0f)] private float finalMagnetStrengthJitter = 0.20f;

    [Header("Lifetime")]
    [SerializeField, Min(0.05f)] private float maxLifetime = 2f;

    [Header("Electric Arc")]
    [SerializeField] private bool electricArcEnabled = true;
    [SerializeField] private LineRenderer electricArcLine;
    [SerializeField, Range(2, 12)] private int electricArcPointCount = 6;
    [SerializeField, Min(0f)] private float electricArcLength = 0.32f;
    [SerializeField, Min(0f)] private float electricArcWidth = 0.07f;
    [SerializeField, Min(0f)] private float electricArcJitterAmplitude = 0.06f;
    [SerializeField, Min(0f)] private float electricArcNoiseSpeed = 18f;
    [SerializeField, Min(0f)] private float electricArcVelocityInfluence = 0.75f;
    [SerializeField, Min(0f)] private float electricArcLengthJitter = 0.05f;
    [SerializeField, Min(0f)] private float electricArcAmplitudeJitter = 0.03f;
    [SerializeField, Min(0f)] private float electricArcNoiseSpeedJitter = 3f;

    [Header("Optional")]
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private Transform coreVisualRoot;
    [SerializeField] private GameObject arrivalVfxPrefab;

    [Header("Arrival Fade")]
    [SerializeField] private bool fadeVisualsOnArrival = true;
    [SerializeField, Min(0.01f)] private float minimumArrivalVisualLingerTime = 0.18f;
    [SerializeField, Min(0.01f)] private float electricArcFadeOutTime = 0.08f;
    [SerializeField] private bool hideCoreVisualOnArrival = true;

    private HeroHealth25D heroHealth;
    private Transform receiveTarget;
    private int healAmount;

    private Vector3 phaseOneStart;
    private Vector3 phaseOneEnd;
    private Vector3 phaseTwoStart;
    private Vector3 lastKnownTargetPosition;

    private Vector3 runtimeScatterDirection;
    private float runtimePhaseOneDuration;
    private float runtimePhaseOneDistance;
    private float runtimePhaseOneHeight;
    private float runtimePhaseTwoDuration;
    private float runtimePhaseTwoArcHeight;
    private float runtimePhaseTwoLateralOffset;
    private float runtimeFinalMagnetStrength;

    private float runtimeElectricArcLength;
    private float runtimeElectricArcAmplitude;
    private float runtimeElectricArcNoiseSpeed;
    private float[] electricArcPointSeeds;
    private Vector3[] cachedElectricArcPositions;
    private float baseElectricArcWidthMultiplier = 1f;
    private Gradient baseElectricArcGradient;

    private Vector3 previousPosition;
    private Vector3 lastNonZeroMoveDirection;
    private float currentMoveSpeed;

    private float phaseStartTime;
    private float spawnedAt;
    private bool initialized;
    private bool inPhaseTwo;
    private bool arrived;
    private bool healGranted;
    private bool isArrivalFading;
    private float arrivalFadeStartTime;
    private float arrivalFadeDuration;
    private Vector3 frozenArrivalPosition;

    private void Reset()
    {
        if (trailRenderer == null)
            trailRenderer = GetComponentInChildren<TrailRenderer>();
        if (electricArcLine == null)
            electricArcLine = GetComponentInChildren<LineRenderer>();
    }

    private void Awake()
    {
        if (trailRenderer == null)
            trailRenderer = GetComponentInChildren<TrailRenderer>();
        if (electricArcLine == null)
            electricArcLine = GetComponentInChildren<LineRenderer>();

        CaptureElectricArcBaseVisuals();
        ConfigureElectricArcLine();
    }

    private void OnValidate()
    {
        phaseOneDuration = Mathf.Max(0.01f, phaseOneDuration);
        phaseOneDistance = Mathf.Max(0f, phaseOneDistance);
        phaseOneHeight = Mathf.Max(0f, phaseOneHeight);
        phaseTwoDuration = Mathf.Max(0.01f, phaseTwoDuration);
        phaseTwoArcHeight = Mathf.Max(0f, phaseTwoArcHeight);
        phaseTwoLateralOffset = Mathf.Max(0f, phaseTwoLateralOffset);
        arriveDistance = Mathf.Max(0f, arriveDistance);
        finalMagnetPortion = Mathf.Clamp(finalMagnetPortion, 0.01f, 0.99f);
        finalMagnetStrength = Mathf.Max(0f, finalMagnetStrength);

        phaseOneDurationJitter = Mathf.Max(0f, phaseOneDurationJitter);
        phaseOneDistanceJitter = Mathf.Max(0f, phaseOneDistanceJitter);
        phaseOneHeightJitter = Mathf.Max(0f, phaseOneHeightJitter);
        phaseTwoDurationJitter = Mathf.Max(0f, phaseTwoDurationJitter);
        phaseTwoArcHeightJitter = Mathf.Max(0f, phaseTwoArcHeightJitter);
        phaseTwoLateralOffsetJitter = Mathf.Max(0f, phaseTwoLateralOffsetJitter);
        scatterAngleJitter = Mathf.Max(0f, scatterAngleJitter);
        scatterVerticalJitter = Mathf.Max(0f, scatterVerticalJitter);
        finalMagnetStrengthJitter = Mathf.Max(0f, finalMagnetStrengthJitter);
        maxLifetime = Mathf.Max(0.05f, maxLifetime);

        electricArcPointCount = Mathf.Clamp(electricArcPointCount, 2, 12);
        electricArcLength = Mathf.Max(0f, electricArcLength);
        electricArcWidth = Mathf.Max(0f, electricArcWidth);
        electricArcJitterAmplitude = Mathf.Max(0f, electricArcJitterAmplitude);
        electricArcNoiseSpeed = Mathf.Max(0f, electricArcNoiseSpeed);
        electricArcVelocityInfluence = Mathf.Max(0f, electricArcVelocityInfluence);
        electricArcLengthJitter = Mathf.Max(0f, electricArcLengthJitter);
        electricArcAmplitudeJitter = Mathf.Max(0f, electricArcAmplitudeJitter);
        electricArcNoiseSpeedJitter = Mathf.Max(0f, electricArcNoiseSpeedJitter);

        minimumArrivalVisualLingerTime = Mathf.Max(0.01f, minimumArrivalVisualLingerTime);
        electricArcFadeOutTime = Mathf.Max(0.01f, electricArcFadeOutTime);

        if (trailRenderer == null)
            trailRenderer = GetComponentInChildren<TrailRenderer>();
        if (electricArcLine == null)
            electricArcLine = GetComponentInChildren<LineRenderer>();

        CaptureElectricArcBaseVisuals();
        ConfigureElectricArcLine();
    }

    private void OnEnable()
    {
        spawnedAt = Time.time;
        healGranted = false;
        isArrivalFading = false;

        if (coreVisualRoot != null)
            coreVisualRoot.gameObject.SetActive(true);

        if (trailRenderer != null)
        {
            trailRenderer.emitting = true;
            trailRenderer.Clear();
        }

        if (electricArcLine != null)
        {
            RestoreElectricArcBaseVisuals();
            ConfigureElectricArcLine();
            electricArcLine.enabled = electricArcEnabled;
        }
    }

    public void Initialize(HeroHealth25D targetHeroHealth, Transform targetReceivePoint, int healToGrant, Vector3 initialScatterDirection)
    {
        heroHealth = targetHeroHealth;
        receiveTarget = targetReceivePoint;
        healAmount = Mathf.Max(0, healToGrant);

        phaseOneStart = transform.position;

        runtimePhaseOneDuration = GetJitteredPositiveValue(phaseOneDuration, phaseOneDurationJitter, 0.01f);
        runtimePhaseOneDistance = GetJitteredPositiveValue(phaseOneDistance, phaseOneDistanceJitter, 0f);
        runtimePhaseOneHeight = GetJitteredPositiveValue(phaseOneHeight, phaseOneHeightJitter, 0f);
        runtimePhaseTwoDuration = GetJitteredPositiveValue(phaseTwoDuration, phaseTwoDurationJitter, 0.01f);
        runtimePhaseTwoArcHeight = GetJitteredSignedMagnitude(phaseTwoArcHeight, phaseTwoArcHeightJitter, false);
        runtimePhaseTwoLateralOffset = GetJitteredSignedMagnitude(phaseTwoLateralOffset, phaseTwoLateralOffsetJitter, true);
        runtimeFinalMagnetStrength = GetJitteredPositiveValue(finalMagnetStrength, finalMagnetStrengthJitter, 0f);
        runtimeScatterDirection = ComputeRuntimeScatterDirection(initialScatterDirection);

        runtimeElectricArcLength = GetJitteredPositiveValue(electricArcLength, electricArcLengthJitter, 0f);
        runtimeElectricArcAmplitude = GetJitteredPositiveValue(electricArcJitterAmplitude, electricArcAmplitudeJitter, 0f);
        runtimeElectricArcNoiseSpeed = GetJitteredPositiveValue(electricArcNoiseSpeed, electricArcNoiseSpeedJitter, 0f);
        InitializeElectricArcSeeds();

        phaseOneEnd = phaseOneStart + runtimeScatterDirection * runtimePhaseOneDistance;
        phaseOneEnd.z = phaseOneStart.z;
        phaseTwoStart = phaseOneEnd;
        lastKnownTargetPosition = GetCurrentTargetPosition();
        lastKnownTargetPosition.z = phaseOneStart.z;

        phaseStartTime = Time.time;
        spawnedAt = Time.time;
        inPhaseTwo = false;
        arrived = false;
        healGranted = false;
        isArrivalFading = false;
        arrivalFadeStartTime = 0f;
        arrivalFadeDuration = 0f;
        initialized = true;
        previousPosition = transform.position;
        lastNonZeroMoveDirection = runtimeScatterDirection.sqrMagnitude > 0.0001f ? runtimeScatterDirection : Vector3.up;
        currentMoveSpeed = 0f;

        if (coreVisualRoot != null)
            coreVisualRoot.gameObject.SetActive(true);

        if (trailRenderer != null)
        {
            trailRenderer.emitting = true;
            trailRenderer.Clear();
        }

        if (electricArcLine != null)
        {
            RestoreElectricArcBaseVisuals();
            ConfigureElectricArcLine();
            electricArcLine.enabled = electricArcEnabled;
        }
    }

    private void Update()
    {
        if (!initialized)
            return;

        if (isArrivalFading)
        {
            UpdateArrivalFadeState();
            return;
        }

        if (arrived)
            return;

        if (Time.time - spawnedAt >= maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (heroHealth == null || receiveTarget == null || !heroHealth.IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        if (!inPhaseTwo)
            UpdatePhaseOne();
        else
            UpdatePhaseTwo();

        UpdateMovementDirection();
        UpdateElectricArcVisual();
    }

    private void UpdatePhaseOne()
    {
        float duration = Mathf.Max(0.01f, runtimePhaseOneDuration);
        float t = Mathf.Clamp01((Time.time - phaseStartTime) / duration);

        Vector3 basePosition = Vector3.Lerp(phaseOneStart, phaseOneEnd, t);
        float arc = Mathf.Sin(t * Mathf.PI) * runtimePhaseOneHeight;
        basePosition.y += arc;
        basePosition.z = phaseOneStart.z;
        transform.position = basePosition;

        if (t >= 1f)
        {
            inPhaseTwo = true;
            phaseStartTime = Time.time;
            phaseTwoStart = transform.position;
            phaseTwoStart.z = phaseOneStart.z;
            lastKnownTargetPosition = GetCurrentTargetPosition();
            lastKnownTargetPosition.z = phaseOneStart.z;
        }
    }

    private void UpdatePhaseTwo()
    {
        float duration = Mathf.Max(0.01f, runtimePhaseTwoDuration);
        float t = Mathf.Clamp01((Time.time - phaseStartTime) / duration);

        lastKnownTargetPosition = GetCurrentTargetPosition();
        lastKnownTargetPosition.z = phaseOneStart.z;

        Vector3 basePosition = Vector3.Lerp(phaseTwoStart, lastKnownTargetPosition, t);
        Vector3 directionToTarget = lastKnownTargetPosition - phaseTwoStart;
        directionToTarget.z = 0f;

        Vector3 lateralAxis = ComputeLateralAxis(directionToTarget);
        float arcWeight = Mathf.Sin(t * Mathf.PI);
        float verticalArc = arcWeight * runtimePhaseTwoArcHeight;
        float lateralArc = arcWeight * runtimePhaseTwoLateralOffset;

        Vector3 offsetPosition = basePosition + Vector3.up * verticalArc + lateralAxis * lateralArc;
        offsetPosition.z = phaseOneStart.z;

        float magnetStartT = Mathf.Clamp01(1f - finalMagnetPortion);
        if (t > magnetStartT)
        {
            float magnetT = Mathf.InverseLerp(magnetStartT, 1f, t);
            float magnetBlend = Mathf.Clamp01(magnetT * runtimeFinalMagnetStrength);
            offsetPosition = Vector3.Lerp(offsetPosition, lastKnownTargetPosition, magnetBlend);
            offsetPosition.z = phaseOneStart.z;
        }

        transform.position = offsetPosition;

        Vector3 toTarget = lastKnownTargetPosition - transform.position;
        toTarget.z = 0f;
        if (toTarget.sqrMagnitude <= arriveDistance * arriveDistance || t >= 1f)
            Arrive();
    }

    private void UpdateMovementDirection()
    {
        Vector3 delta = transform.position - previousPosition;
        delta.z = 0f;
        if (delta.sqrMagnitude > 0.000001f)
            lastNonZeroMoveDirection = delta.normalized;

        currentMoveSpeed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        previousPosition = transform.position;
    }

    private void UpdateElectricArcVisual()
    {
        if (!electricArcEnabled || electricArcLine == null)
            return;

        ConfigureElectricArcLine();

        Vector3 forward = GetElectricArcForwardDirection();
        Vector3 lateral = ComputeLateralAxis(forward);
        Vector3 vertical = Vector3.up;

        float effectiveLength = runtimeElectricArcLength + currentMoveSpeed * electricArcVelocityInfluence * 0.01f;
        effectiveLength = Mathf.Max(0.01f, effectiveLength);

        Vector3 start = transform.position;
        Vector3 end = start - forward * effectiveLength;
        start.z = phaseOneStart.z;
        end.z = phaseOneStart.z;

        int pointCount = Mathf.Max(2, electricArcPointCount);
        electricArcLine.positionCount = pointCount;
        EnsureCachedElectricArcPositions(pointCount);

        for (int i = 0; i < pointCount; i++)
        {
            float t = pointCount <= 1 ? 0f : (float)i / (pointCount - 1);
            Vector3 point = Vector3.Lerp(start, end, t);

            if (i > 0 && i < pointCount - 1)
            {
                float shape = Mathf.Sin(t * Mathf.PI);
                float seed = electricArcPointSeeds != null && i < electricArcPointSeeds.Length ? electricArcPointSeeds[i] : i * 0.137f;
                float noiseTime = Time.time * runtimeElectricArcNoiseSpeed;
                float lateralNoise = Mathf.Sin(noiseTime + seed * 11.713f);
                float verticalNoise = Mathf.Cos(noiseTime * 0.83f + seed * 7.117f);
                float amplitude = runtimeElectricArcAmplitude * shape;
                point += lateral * (lateralNoise * amplitude + shape * electricArcWidth * 0.5f);
                point += vertical * (verticalNoise * amplitude * 0.45f);
            }

            point.z = phaseOneStart.z;
            cachedElectricArcPositions[i] = point;
            electricArcLine.SetPosition(i, point);
        }

        electricArcLine.enabled = true;
    }

    private void UpdateArrivalFadeState()
    {
        transform.position = frozenArrivalPosition;

        float elapsed = Time.time - arrivalFadeStartTime;
        float arcFade01 = 1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, electricArcFadeOutTime));
        ApplyElectricArcFade(arcFade01);

        if (elapsed >= arrivalFadeDuration)
            Destroy(gameObject);
    }

    private void EnterArrivalFadeState()
    {
        if (arrived || isArrivalFading)
            return;

        arrived = true;

        if (!healGranted && heroHealth != null && heroHealth.IsAlive && healAmount > 0)
        {
            heroHealth.Heal(healAmount);
            healGranted = true;
        }

        if (arrivalVfxPrefab != null)
            Instantiate(arrivalVfxPrefab, transform.position, Quaternion.identity);

        if (!fadeVisualsOnArrival)
        {
            Destroy(gameObject);
            return;
        }

        isArrivalFading = true;
        frozenArrivalPosition = transform.position;
        frozenArrivalPosition.z = phaseOneStart.z;
        arrivalFadeStartTime = Time.time;
        arrivalFadeDuration = minimumArrivalVisualLingerTime;
        if (trailRenderer != null)
            arrivalFadeDuration = Mathf.Max(arrivalFadeDuration, trailRenderer.time);

        if (hideCoreVisualOnArrival && coreVisualRoot != null)
            coreVisualRoot.gameObject.SetActive(false);

        if (trailRenderer != null)
            trailRenderer.emitting = false;

        if (electricArcLine != null)
        {
            EnsureCachedElectricArcPositions(electricArcLine.positionCount);
            for (int i = 0; i < electricArcLine.positionCount; i++)
            {
                Vector3 point = cachedElectricArcPositions != null && i < cachedElectricArcPositions.Length
                    ? cachedElectricArcPositions[i]
                    : frozenArrivalPosition;
                electricArcLine.SetPosition(i, point);
            }
        }
    }

    private void EnsureCachedElectricArcPositions(int pointCount)
    {
        pointCount = Mathf.Max(2, pointCount);
        if (cachedElectricArcPositions == null || cachedElectricArcPositions.Length != pointCount)
            cachedElectricArcPositions = new Vector3[pointCount];
    }

    private void CaptureElectricArcBaseVisuals()
    {
        if (electricArcLine == null)
            return;

        baseElectricArcWidthMultiplier = electricArcLine.widthMultiplier;
        baseElectricArcGradient = CloneGradient(electricArcLine.colorGradient);
    }

    private void RestoreElectricArcBaseVisuals()
    {
        if (electricArcLine == null)
            return;

        if (baseElectricArcGradient == null)
            CaptureElectricArcBaseVisuals();

        electricArcLine.widthMultiplier = baseElectricArcWidthMultiplier;
        if (baseElectricArcGradient != null)
            electricArcLine.colorGradient = CloneGradient(baseElectricArcGradient);
    }

    private void ApplyElectricArcFade(float fade01)
    {
        if (electricArcLine == null)
            return;

        fade01 = Mathf.Clamp01(fade01);
        electricArcLine.widthMultiplier = baseElectricArcWidthMultiplier * fade01;

        Gradient source = baseElectricArcGradient ?? electricArcLine.colorGradient;
        Gradient gradient = CloneGradient(source);
        GradientAlphaKey[] alphaKeys = gradient.alphaKeys;
        for (int i = 0; i < alphaKeys.Length; i++)
            alphaKeys[i].alpha *= fade01;
        gradient.SetKeys(gradient.colorKeys, alphaKeys);
        gradient.mode = source.mode;
        electricArcLine.colorGradient = gradient;

        if (fade01 <= 0.001f)
            electricArcLine.enabled = false;
    }

    private void ConfigureElectricArcLine()
    {
        if (electricArcLine == null)
            return;

        int pointCount = Mathf.Max(2, electricArcPointCount);
        if (electricArcLine.positionCount != pointCount)
            electricArcLine.positionCount = pointCount;
    }

    private void InitializeElectricArcSeeds()
    {
        int pointCount = Mathf.Max(2, electricArcPointCount);
        electricArcPointSeeds = new float[pointCount];
        for (int i = 0; i < pointCount; i++)
            electricArcPointSeeds[i] = Random.Range(-1000f, 1000f);
    }

    private Vector3 GetElectricArcForwardDirection()
    {
        Vector3 direction = lastNonZeroMoveDirection;
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = runtimeScatterDirection;

        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector3.up;

        return direction.normalized;
    }

    private Vector3 GetCurrentTargetPosition()
    {
        if (receiveTarget != null)
            return receiveTarget.position;

        if (heroHealth != null)
            return heroHealth.transform.position;

        return transform.position;
    }

    private Vector3 ComputeRuntimeScatterDirection(Vector3 initialScatterDirection)
    {
        Vector3 scatter = initialScatterDirection;
        scatter.z = 0f;
        if (scatter.sqrMagnitude <= 0.0001f)
            scatter = Vector3.up;
        else
            scatter.Normalize();

        float signedAngle = Random.Range(-scatterAngleJitter, scatterAngleJitter);
        Vector2 rotated = Rotate2D(new Vector2(scatter.x, scatter.y), signedAngle);
        Vector3 result = new Vector3(rotated.x, rotated.y, 0f);
        result.y += Random.Range(-scatterVerticalJitter, scatterVerticalJitter);
        result.z = 0f;

        if (result.sqrMagnitude <= 0.0001f)
            result = Vector3.up;
        else
            result.Normalize();

        return result;
    }

    private static Vector2 Rotate2D(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos);
    }

    private static Vector3 ComputeLateralAxis(Vector3 directionToTarget)
    {
        directionToTarget.z = 0f;
        if (directionToTarget.sqrMagnitude <= 0.0001f)
            return Vector3.right;

        directionToTarget.Normalize();
        Vector3 lateral = new Vector3(-directionToTarget.y, directionToTarget.x, 0f);
        if (lateral.sqrMagnitude <= 0.0001f)
            return Vector3.right;

        return lateral.normalized;
    }

    private static float GetRandomSigned(float magnitude)
    {
        return Random.Range(-magnitude, magnitude);
    }

    private static float GetJitteredPositiveValue(float baseValue, float jitter, float minValue)
    {
        return Mathf.Max(minValue, baseValue + GetRandomSigned(jitter));
    }

    private static float GetJitteredSignedMagnitude(float baseValue, float jitter, bool randomizeSign)
    {
        float magnitude = Mathf.Max(0f, baseValue + GetRandomSigned(jitter));
        if (magnitude <= 0f)
            return 0f;

        if (!randomizeSign)
            return magnitude;

        return Random.value < 0.5f ? -magnitude : magnitude;
    }

    private static Gradient CloneGradient(Gradient source)
    {
        Gradient clone = new Gradient();
        if (source == null)
            return clone;

        clone.SetKeys(source.colorKeys, source.alphaKeys);
        clone.mode = source.mode;
        return clone;
    }

    private void Arrive()
    {
        EnterArrivalFadeState();
    }
}
