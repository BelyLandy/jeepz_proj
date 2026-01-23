using UnityEngine;

public class JellyWobble : MonoBehaviour
{
    [Header("Links")]
    public Transform target;      // *_Target (куда ставит анимация)
    public JellyRoot root;        // корень героя (скорость/ускорение)

    [Header("Position Jiggle")]
    public float posInertia = 0.03f;   // насколько отставать по скорости (в юнитах на (юнит/с))
    public float posStiffness = 120f;  // "жесткость" пружины
    public float posDamping = 18f;     // "вязкость" (больше = быстрее затухает)

    [Header("Rotation Jiggle")]
    public float rotInertia = 2.0f;    // градусов на (юнит/с)
    public float rotStiffness = 80f;
    public float rotDamping = 14f;

    [Header("Squash & Stretch")]
    public float squashAmount = 0.12f; // сила эффекта
    public float squashStiffness = 160f;
    public float squashDamping = 22f;
    public bool preserveArea = true;   // сохранять "объем" (площадь)

    Vector2 _pos, _posVel;
    float _rot, _rotVel;
    Vector2 _scale, _scaleVel;

    Vector2 _lastTargetPos;

    void OnEnable()
    {
        if (!target) target = transform.parent;
        _pos = Vector2.zero;
        _posVel = Vector2.zero;
        _rot = 0f;
        _rotVel = 0f;
        _scale = Vector2.one;
        _scaleVel = Vector2.zero;

        _lastTargetPos = target ? (Vector2)target.position : (Vector2)transform.position;
    }

    void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        // Скорость таргета (для желе при локальной анимации частей тоже)
        Vector2 targetPos = target ? (Vector2)target.position : (Vector2)transform.position;
        Vector2 targetVel = (targetPos - _lastTargetPos) / dt;
        _lastTargetPos = targetPos;

        Vector2 rootVel = root ? root.Velocity : Vector2.zero;
        Vector2 vel = rootVel + targetVel;

        // ----- POSITION: инерция назад по движению -----
        Vector2 desiredLocalPos = -vel * posInertia; // отставание "назад"
        _pos = SpringVec2(_pos, ref _posVel, desiredLocalPos, posStiffness, posDamping, dt);
        transform.localPosition = _pos;

        // ----- ROTATION: лёгкий наклон по движению -----
        // В 2D (XY) "наклон" делаем от X скорости
        float desiredRot = -vel.x * rotInertia;
        _rot = SpringFloat(_rot, ref _rotVel, desiredRot, rotStiffness, rotDamping, dt);
        transform.localRotation = Quaternion.Euler(0, 0, _rot);

        // ----- SQUASH & STRETCH: по скорости -----
        float speed = vel.magnitude;
        float stretch = 1f + speed * squashAmount;
        float squash = 1f - speed * squashAmount;

        Vector2 desiredScale = new Vector2(stretch, squash);

        if (preserveArea)
        {
            // чтобы площадь примерно сохранялась
            float area = desiredScale.x * desiredScale.y;
            if (area > 0.0001f)
            {
                float k = Mathf.Sqrt(1f / area);
                desiredScale *= k;
            }
        }

        _scale = SpringVec2(_scale, ref _scaleVel, desiredScale, squashStiffness, squashDamping, dt);
        transform.localScale = new Vector3(_scale.x, _scale.y, 1f);
    }

    static float SpringFloat(float x, ref float v, float xTarget, float k, float c, float dt)
    {
        // x'' = k*(target-x) - c*x'
        float a = k * (xTarget - x) - c * v;
        v += a * dt;
        x += v * dt;
        return x;
    }

    static Vector2 SpringVec2(Vector2 x, ref Vector2 v, Vector2 xTarget, float k, float c, float dt)
    {
        Vector2 a = k * (xTarget - x) - c * v;
        v += a * dt;
        x += v * dt;
        return x;
    }
}
