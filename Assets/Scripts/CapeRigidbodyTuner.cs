using UnityEngine;

[ExecuteAlways]
public class CapeRigidbodyTuner : MonoBehaviour
{
    [Header("Кости плаща (сверху вниз)")]
    public Rigidbody2D[] bones;

    [Header("Базовые значения (верхняя кость)")]
    public float baseMass           = 0.4f;
    public float baseLinearDamping  = 0.01f;
    public float baseAngularDamping = 0.0f;
    public float baseGravityScale   = 0.5f;

    [Header("Добавка к нижней кости")]
    public float massMultiplierBottom              = 1.3f;
    public float linearDampingMultiplierBottom     = 1.0f;
    public float angularDampingMultiplierBottom    = 1.0f;

    void OnEnable()
    {
        ApplySettings();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ApplySettings();
    }
#endif

    void ApplySettings()
    {
        if (bones == null || bones.Length == 0) return;
        int n = bones.Length;
        if (n == 1)
        {
            SetupBone(bones[0], 0f);
            return;
        }

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / (n - 1); // 0..1 сверху вниз
            SetupBone(bones[i], t);
        }
    }

    void SetupBone(Rigidbody2D rb, float t)
    {
        if (rb == null) return;

        float mass         = Mathf.Lerp(baseMass, baseMass * massMultiplierBottom, t);
        float linDamp      = Mathf.Lerp(baseLinearDamping,  baseLinearDamping  * linearDampingMultiplierBottom,  t);
        float angDamp      = Mathf.Lerp(baseAngularDamping,  baseAngularDamping * angularDampingMultiplierBottom, t);
        float gravityScale = baseGravityScale;

        rb.mass         = mass;
        rb.linearDamping         = linDamp;      // <-- вот тут раньше был linearDrag
        rb.angularDamping  = angDamp;
        rb.gravityScale = gravityScale;

        rb.sleepMode     = RigidbodySleepMode2D.NeverSleep;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }
}
