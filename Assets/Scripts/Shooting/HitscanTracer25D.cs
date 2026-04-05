using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HitscanTracer25D : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] private float moveSpeed = 220f;
    [SerializeField] private float endHoldTime = 0.02f;

    [Header("Plane Lock")]
    [SerializeField] private bool lockWorldZ = true;
    [SerializeField] private float worldZ = 0f;

    [Header("References")]
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private GameObject headVisual;

    private Action<HitscanTracer25D> releaseCallback;
    private Vector3 targetPosition;
    private float reachedTargetTime;
    private bool isPlaying;
    private bool hasReachedTarget;

    public bool IsPlaying => isPlaying;

    private void Reset()
    {
        if (trail == null)
            trail = GetComponentInChildren<TrailRenderer>();
    }

    private void Awake()
    {
        if (trail == null)
            trail = GetComponentInChildren<TrailRenderer>();

        StopAndHide(clearTrail: true);
    }

    private void OnDisable()
    {
        StopAndHide(clearTrail: true);
    }

    public void ConfigurePool(Action<HitscanTracer25D> onRelease)
    {
        releaseCallback = onRelease;
    }

    public void Play(Vector3 startPosition, Vector3 endPosition)
    {
        if (lockWorldZ)
        {
            startPosition.z = worldZ;
            endPosition.z = worldZ;
        }

        gameObject.SetActive(true);
        transform.position = startPosition;
        transform.rotation = Quaternion.identity;

        targetPosition = endPosition;
        reachedTargetTime = 0f;
        isPlaying = true;
        hasReachedTarget = false;

        if (trail != null)
        {
            trail.Clear();
            trail.emitting = true;
        }

        if (headVisual != null)
            headVisual.SetActive(true);
    }

    private void Update()
    {
        if (!isPlaying)
            return;

        Vector3 current = transform.position;
        Vector3 next = Vector3.MoveTowards(current, targetPosition, moveSpeed * Time.deltaTime);
        if (lockWorldZ)
            next.z = worldZ;

        transform.position = next;

        Vector3 delta = targetPosition - next;
        if (delta.sqrMagnitude > 0.000001f)
        {
            float angleZ = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angleZ);
        }

        if (!hasReachedTarget && (targetPosition - next).sqrMagnitude <= 0.000001f)
        {
            hasReachedTarget = true;
            reachedTargetTime = Time.time;

            if (headVisual != null)
                headVisual.SetActive(false);

            if (trail != null)
                trail.emitting = false;
        }

        if (hasReachedTarget && Time.time >= reachedTargetTime + endHoldTime)
            Release();
    }

    private void Release()
    {
        isPlaying = false;
        releaseCallback?.Invoke(this);
    }

    public void StopAndHide(bool clearTrail)
    {
        isPlaying = false;
        hasReachedTarget = false;
        reachedTargetTime = 0f;

        if (trail != null)
        {
            trail.emitting = false;
            if (clearTrail)
                trail.Clear();
        }

        if (headVisual != null)
            headVisual.SetActive(false);
    }
}
