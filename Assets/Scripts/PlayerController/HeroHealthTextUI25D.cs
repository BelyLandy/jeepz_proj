using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HeroHealthTextUI25D : MonoBehaviour
{
    [SerializeField] private HeroHealth25D heroHealth;
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private Text legacyText;
    [SerializeField] private string format = "HP: {0} / {1}";

    private void Reset()
    {
        if (heroHealth == null)
            heroHealth = GetComponentInParent<HeroHealth25D>();

        if (tmpText == null)
            tmpText = GetComponent<TMP_Text>();

        if (legacyText == null)
            legacyText = GetComponent<Text>();
    }

    private void Awake()
    {
        if (heroHealth == null)
            heroHealth = GetComponentInParent<HeroHealth25D>();

        if (tmpText == null)
            tmpText = GetComponent<TMP_Text>();

        if (legacyText == null)
            legacyText = GetComponent<Text>();

        RefreshText();
    }

    private void OnValidate()
    {
        if (heroHealth == null)
            heroHealth = GetComponentInParent<HeroHealth25D>();

        if (tmpText == null)
            tmpText = GetComponent<TMP_Text>();

        if (legacyText == null)
            legacyText = GetComponent<Text>();
    }

    private void LateUpdate()
    {
        RefreshText();
    }

    private void RefreshText()
    {
        string value = heroHealth != null
            ? string.Format(format, heroHealth.CurrentHealth, heroHealth.MaxHealth)
            : string.Format(format, 0, 0);

        if (tmpText != null)
            tmpText.text = value;

        if (legacyText != null)
            legacyText.text = value;
    }
}
