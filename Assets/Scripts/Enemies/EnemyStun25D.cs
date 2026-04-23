using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyStun25D : MonoBehaviour
{
    [SerializeField] private EnemyCharacter25D character;
    [SerializeField, Min(0f)] private float defaultStunDuration = 0.25f;
    [SerializeField] private bool refreshOnNewStun = true;

    private float stunEndTime = float.NegativeInfinity;
    private bool isStunned;

    public bool IsStunned => isStunned;
    public float RemainingStunTime => isStunned ? Mathf.Max(0f, stunEndTime - Time.time) : 0f;

    private void Reset()
    {
        if (character == null)
            character = GetComponent<EnemyCharacter25D>();

        ClampSettings();
    }

    private void Awake()
    {
        if (character == null)
            character = GetComponent<EnemyCharacter25D>();

        ClampSettings();
    }

    private void OnValidate()
    {
        if (character == null)
            character = GetComponent<EnemyCharacter25D>();

        ClampSettings();
    }

    private void Update()
    {
        if (!isStunned)
            return;

        if (Time.time < stunEndTime)
            return;

        ClearStun();
    }

    public void ApplyDefaultStun()
    {
        ApplyStun(defaultStunDuration);
    }

    public void ApplyStun(float duration)
    {
        duration = Mathf.Max(0f, duration);
        if (duration <= 0f)
            return;

        if (isStunned && !refreshOnNewStun)
            return;

        stunEndTime = Time.time + duration;
        isStunned = true;

        if (character != null)
            character.SetStunControlLocked(true);
    }

    public void ClearStun()
    {
        isStunned = false;
        stunEndTime = float.NegativeInfinity;

        if (character != null && !character.IsDead)
            character.SetStunControlLocked(false);
    }

    private void ClampSettings()
    {
        defaultStunDuration = Mathf.Max(0f, defaultStunDuration);
    }
}
