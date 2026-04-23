using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlasmaGloveStatusUI25D : MonoBehaviour
{
    [SerializeField] private CharacterPlasmaGlove25D plasmaGlove;
    [SerializeField] private Slider heatSlider;
    [SerializeField] private Slider ammoSlider;

    private void Reset()
    {
        if (plasmaGlove == null)
            plasmaGlove = GetComponentInParent<CharacterPlasmaGlove25D>();
    }

    private void Awake()
    {
        if (plasmaGlove == null)
            plasmaGlove = GetComponentInParent<CharacterPlasmaGlove25D>();

        RefreshSliders();
    }

    private void OnValidate()
    {
        if (plasmaGlove == null)
            plasmaGlove = GetComponentInParent<CharacterPlasmaGlove25D>();
    }

    private void LateUpdate()
    {
        RefreshSliders();
    }

    private void RefreshSliders()
    {
        if (heatSlider != null)
        {
            heatSlider.minValue = 0f;
            heatSlider.maxValue = 1f;
            heatSlider.value = plasmaGlove != null ? plasmaGlove.CurrentHeatNormalized : 0f;
        }

        if (ammoSlider != null)
        {
            float maxAmmo = plasmaGlove != null ? Mathf.Max(1, plasmaGlove.MaxAmmo) : 1f;
            ammoSlider.minValue = 0f;
            ammoSlider.maxValue = maxAmmo;
            ammoSlider.value = plasmaGlove != null ? plasmaGlove.CurrentAmmo : 0f;
        }
    }
}
