using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class FollowTransformLightDir2D : MonoBehaviour
{
    public Transform lightTransform;          // источник света
    public float minZ = 0.001f;               // чтобы вектор не становился "плоским"

    [Header("Shader property names")]
    public string lightDirProperty = "_LightDir";

    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;

    // --- ИНИЦИАЛИЗАЦИЯ ---
    private void EnsureInit()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        EnsureInit();
        UpdateLightDir();

        #if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
        #endif
    }

    private void OnDisable()
    {
        #if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
        #endif
    }

    private void Update()
    {
        // в Play и Edit режимах
        UpdateLightDir();
    }

    #if UNITY_EDITOR
    private void EditorTick()
    {
        if (!Application.isPlaying)
        {
            UpdateLightDir();
            // необязательно, но помогает видеть изменения сразу
            SceneView.RepaintAll();
        }
    }
    #endif

    private void OnValidate()
    {
        // вызывается при изменении полей в инспекторе — тоже обновим
        UpdateLightDir();
    }

    private void OnDrawGizmosSelected()
    {
        if (lightTransform)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, lightTransform.position);
            Gizmos.DrawSphere(lightTransform.position, 0.05f);
        }
    }

    // --- ОСНОВНАЯ ЛОГИКА ---
    private void UpdateLightDir()
    {
        EnsureInit();
        if (_renderer == null) return;

        // если источника нет — можно очистить MPB (по желанию) и выйти
        if (lightTransform == null)
        {
            _renderer.GetPropertyBlock(_mpb);              // _mpb гарантированно не null
            // _mpb.Clear();                                // раскомментируй, если хочешь сбрасывать
            // _renderer.SetPropertyBlock(_mpb);
            return;
        }

        // позиция источника в локальных координатах спрайта
        Vector3 posLocal = transform.InverseTransformPoint(lightTransform.position);
        if (posLocal.z < minZ) posLocal.z = minZ;
        Vector3 L_local = posLocal.normalized;

        _renderer.GetPropertyBlock(_mpb);                  // ← здесь раньше падало из-за null MPB
        _mpb.SetVector(lightDirProperty, L_local);
        _renderer.SetPropertyBlock(_mpb);
    }
}
