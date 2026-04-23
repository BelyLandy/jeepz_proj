using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyJumpLink25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("Traversal")]
    [SerializeField] private bool bidirectional = false;
    [SerializeField, Min(0f)] private float approachDistance = 0.35f;
    [SerializeField, Min(0f)] private float landingTolerance = 0.4f;
    [SerializeField, Min(0f)] private float jumpCooldownAfterUse = 0.5f;
    [SerializeField, Min(0f)] private float traversalCost = 1f;
    [SerializeField, Min(0.05f)] private float flightTime = 0.55f;
    [SerializeField, Min(0f)] private float minimumAirTime = 0.12f;
    [SerializeField] private bool enabledLink = true;

    public Transform StartPoint => startPoint != null ? startPoint : transform;
    public Transform EndPoint => endPoint != null ? endPoint : transform;
    public bool Bidirectional => bidirectional;
    public float ApproachDistance => approachDistance;
    public float LandingTolerance => landingTolerance;
    public float JumpCooldownAfterUse => jumpCooldownAfterUse;
    public float TraversalCost => traversalCost;
    public float FlightTime => flightTime;
    public float MinimumAirTime => minimumAirTime;
    public bool EnabledLink => enabledLink;

    private void Reset()
    {
        if (startPoint == null)
            startPoint = transform;
        if (endPoint == null)
            endPoint = transform;
        ClampSettings();
    }

    private void OnValidate()
    {
        if (startPoint == null)
            startPoint = transform;
        if (endPoint == null)
            endPoint = transform;
        ClampSettings();
    }

    public bool TryGetTraversal(Vector3 fromPosition, Vector3 desiredDestination, out Vector3 traversalStart, out Vector3 traversalEnd)
    {
        traversalStart = Vector3.zero;
        traversalEnd = Vector3.zero;

        if (!enabledLink || StartPoint == null || EndPoint == null)
            return false;

        Vector3 forwardStart = StartPoint.position;
        Vector3 forwardEnd = EndPoint.position;

        float forwardScore = Mathf.Abs(desiredDestination.x - forwardEnd.x) + Mathf.Abs(fromPosition.x - forwardStart.x) * 0.35f;
        float reverseScore = float.PositiveInfinity;

        if (bidirectional)
            reverseScore = Mathf.Abs(desiredDestination.x - forwardStart.x) + Mathf.Abs(fromPosition.x - forwardEnd.x) * 0.35f;

        if (forwardScore <= reverseScore)
        {
            traversalStart = forwardStart;
            traversalEnd = forwardEnd;
            return true;
        }

        traversalStart = forwardEnd;
        traversalEnd = forwardStart;
        return true;
    }

    private void ClampSettings()
    {
        approachDistance = Mathf.Max(0f, approachDistance);
        landingTolerance = Mathf.Max(0f, landingTolerance);
        jumpCooldownAfterUse = Mathf.Max(0f, jumpCooldownAfterUse);
        traversalCost = Mathf.Max(0f, traversalCost);
        flightTime = Mathf.Max(0.05f, flightTime);
        minimumAirTime = Mathf.Max(0f, minimumAirTime);
    }
}
