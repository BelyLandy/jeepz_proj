using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ClothArcProjectile : MonoBehaviour
{
    [Header("Refs")]
    public VerletCloth2D cloth;

    [Header("Timing")]
    [Min(0.05f)] public float timeToHit = 0.8f;   // время долёта до точки клика (крестик)
    [Min(0.05f)] public float lifetime = 2.0f;    // сколько живёт снаряд всего (после попадания ещё летит)

    [Header("Arc (XY gravity)")]
    public Vector2 arcGravity = new Vector2(0f, -14f); // влияет на форму дуги

    [Header("Z motion (pseudo 3D)")]
    public float zInFrontOfCloth = 3.0f; // стартуем "перед" тканью (ближе к камере)
    public float zBehindCloth = 6.0f;    // после попадания уходим "за" ткань

    [Header("Scale")]
    public float startScale = 3.0f;
    public float endScale = 0.6f;

    [Header("Hole")]
    public float holeRadiusAtScale1 = 0.35f; // радиус дырки при scale=1

    [Header("Render (optional)")]
    public int sortingOrderBeforeHit = 20;
    public int sortingOrderAfterHit = -20;

    private SpriteRenderer _sr;

    private Vector2 _spawnXY;
    private Vector2 _v0;               // начальная скорость (XY), чтобы попасть в цель за timeToHit
    private float _clothZ;
    private float _t;
    private bool _hitDone;

    private Vector3 _prevPos3;

    public void Init(VerletCloth2D clothRef, Vector2 spawnXY, Vector2 targetXY, float clothZ)
    {
        cloth = clothRef;
        _clothZ = clothZ;

        _sr = GetComponent<SpriteRenderer>();
        _sr.sortingOrder = sortingOrderBeforeHit;

        _spawnXY = spawnXY;

        // вычисляем v0 так, чтобы при t=timeToHit оказаться в targetXY при постоянном ускорении arcGravity
        // target = spawn + v0*t + 0.5*g*t^2 => v0 = (target - spawn - 0.5*g*t^2)/t
        float t = Mathf.Max(0.05f, timeToHit);
        _v0 = (targetXY - spawnXY - 0.5f * arcGravity * t * t) / t;

        // стартовая позиция (перед тканью)
        Vector3 startPos = new Vector3(spawnXY.x, spawnXY.y, _clothZ - Mathf.Abs(zInFrontOfCloth));
        transform.position = startPos;
        transform.localScale = Vector3.one * startScale;

        _prevPos3 = startPos;
        _t = 0f;
        _hitDone = false;
    }

    private void Update()
    {
        if (cloth == null)
        {
            Destroy(gameObject);
            return;
        }

        _t += Time.deltaTime;
        if (_t >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // XY: баллистика (парабола)
        Vector2 xy = _spawnXY + _v0 * _t + 0.5f * arcGravity * _t * _t;

        // Z: до попадания идём к clothZ, после — уходим за ткань
        float z;
        if (_t <= timeToHit)
        {
            float k = Mathf.Clamp01(_t / timeToHit);
            z = Mathf.Lerp(_clothZ - Mathf.Abs(zInFrontOfCloth), _clothZ, k);
        }
        else
        {
            float k = Mathf.Clamp01((_t - timeToHit) / Mathf.Max(0.05f, (lifetime - timeToHit)));
            z = Mathf.Lerp(_clothZ, _clothZ + Mathf.Abs(zBehindCloth), k);
        }

        // Scale: уменьшаем до endScale к моменту попадания, дальше держим
        float s;
        if (_t <= timeToHit)
            s = Mathf.Lerp(startScale, endScale, Mathf.Clamp01(_t / timeToHit));
        else
            s = endScale;

        transform.localScale = Vector3.one * s;

        Vector3 curPos3 = new Vector3(xy.x, xy.y, z);
        transform.position = curPos3;

        // --- МОМЕНТ ПЕРЕСЕЧЕНИЯ ПЛОСКОСТИ ТКАНИ (z = clothZ) ---
        // Когда за один кадр прошли через z=clothZ, вырезаем дырку ТОЧНО в точке пересечения.
        if (!_hitDone)
        {
            float z0 = _prevPos3.z;
            float z1 = curPos3.z;

            bool crossed =
                (z0 < _clothZ && z1 >= _clothZ) ||
                (z0 > _clothZ && z1 <= _clothZ);

            if (crossed)
            {
                float denom = (z1 - z0);
                float a = Mathf.Abs(denom) < 1e-6f ? 1f : (_clothZ - z0) / denom;
                a = Mathf.Clamp01(a);

                Vector3 hitPos3 = Vector3.Lerp(_prevPos3, curPos3, a);
                Vector2 hitXY = new Vector2(hitPos3.x, hitPos3.y);

                float holeRadius = holeRadiusAtScale1 * s;
                cloth.CutCircle(hitXY, holeRadius);

                _hitDone = true;

                // опционально: “после попадания” рисуем снаряд за тканью
                if (_sr != null) _sr.sortingOrder = sortingOrderAfterHit;
            }
        }

        _prevPos3 = curPos3;
    }
}
