using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class FollowTransformLightDir2D : MonoBehaviour
{
    public Transform lightTransform;
    public float minZ = 0.001f;

    [Header("Shader property names")]
    public string lightDirProperty = "_LightDir";

    private Renderer _renderer;
    private SpriteRenderer _spriteRenderer;
    private MaterialPropertyBlock _mpb;

    private void EnsureInit()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
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
        UpdateLightDir();
    }

    #if UNITY_EDITOR
    private void EditorTick()
    {
        if (!Application.isPlaying)
        {
            UpdateLightDir();
            SceneView.RepaintAll();
        }
    }
    #endif

    private void OnValidate()
    {
        UpdateLightDir();
    }

    private void UpdateLightDir()
    {
        EnsureInit();
        if (_renderer == null) return;

        if (lightTransform == null)
        {
            _renderer.GetPropertyBlock(_mpb);
            return;
        }

        Vector3 posLocal = transform.InverseTransformPoint(lightTransform.position);

        if (_spriteRenderer != null)
        {
            if (_spriteRenderer.flipX) posLocal.x = -posLocal.x;
            if (_spriteRenderer.flipY) posLocal.y = -posLocal.y;
        }

        if (posLocal.z < minZ) posLocal.z = minZ;

        Vector3 L_local = posLocal.normalized;

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetVector(lightDirProperty, L_local);
        _renderer.SetPropertyBlock(_mpb);
    }
}
