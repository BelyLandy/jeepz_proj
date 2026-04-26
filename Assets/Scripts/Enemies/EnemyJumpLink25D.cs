using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyJumpLink25D : MonoBehaviour
{
    public enum ArcMode
    {
        Parabola = 0,
        Bezier = 1,
    }

    [Header("References")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("Traversal")]
    [SerializeField] private bool bidirectional = false;
    [SerializeField, Min(0f)] private float approachDistance = 0.35f;
    [SerializeField, Min(0f)] private float approachHorizontalTolerance = 0.35f;
    [SerializeField, Min(0f)] private float approachVerticalTolerance = 0.45f;
    [SerializeField, Min(0f)] private float landingTolerance = 0.4f;
    [SerializeField, Min(0f)] private float jumpCooldownAfterUse = 0.5f;
    [SerializeField, Min(0f)] private float traversalCost = 1f;
    [SerializeField, Min(0.05f)] private float flightTime = 0.55f;
    [SerializeField, Min(0f)] private float minimumAirTime = 0.12f;
    [SerializeField] private bool enabledLink = true;

    [Header("Arc Shape")]
    [SerializeField] private ArcMode arcMode = ArcMode.Parabola;
    [SerializeField, Min(0f)] private float arcHeight = 1.5f;
    [SerializeField] private Transform controlPoint;

    [Header("Interrupt")]
    [SerializeField] private bool interruptOnProjectileHit = true;
    [SerializeField, Min(0f)] private float interruptKnockbackHorizontalForce = 8f;
    [SerializeField, Min(0f)] private float interruptKnockbackVerticalForce = 5f;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawArcGizmos = true;
    [SerializeField] private Color arcGizmoColor = new Color(0.25f, 0.9f, 1f, 0.85f);

    public Transform StartPoint => startPoint != null ? startPoint : transform;
    public Transform EndPoint => endPoint != null ? endPoint : transform;
    public bool Bidirectional => bidirectional;
    public float ApproachDistance => approachDistance;
    public float ApproachHorizontalTolerance => approachHorizontalTolerance > 0f ? approachHorizontalTolerance : Mathf.Max(0.01f, approachDistance);
    public float ApproachVerticalTolerance => approachVerticalTolerance > 0f ? approachVerticalTolerance : Mathf.Max(0.01f, approachDistance);
    public float LandingTolerance => landingTolerance;
    public float JumpCooldownAfterUse => jumpCooldownAfterUse;
    public float TraversalCost => traversalCost;
    public float FlightTime => flightTime;
    public float MinimumAirTime => minimumAirTime;
    public bool EnabledLink => enabledLink;
    public ArcMode TrajectoryArcMode => arcMode;
    public float ArcHeight => arcHeight;
    public Transform ControlPoint => controlPoint;
    public bool InterruptOnProjectileHit => interruptOnProjectileHit;
    public float InterruptKnockbackHorizontalForce => interruptKnockbackHorizontalForce;
    public float InterruptKnockbackVerticalForce => interruptKnockbackVerticalForce;

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

        float forwardScore = ScoreTraversalCandidate(fromPosition, desiredDestination, forwardStart, forwardEnd);
        float reverseScore = float.PositiveInfinity;

        if (bidirectional)
            reverseScore = ScoreTraversalCandidate(fromPosition, desiredDestination, forwardEnd, forwardStart);

        bool useForward = forwardScore < reverseScore;
        if (bidirectional && Mathf.Abs(forwardScore - reverseScore) <= 0.01f)
        {
            float forwardStartVerticalDelta = Mathf.Abs(fromPosition.y - forwardStart.y);
            float reverseStartVerticalDelta = Mathf.Abs(fromPosition.y - forwardEnd.y);
            useForward = forwardStartVerticalDelta <= reverseStartVerticalDelta;
        }
        else if (!bidirectional)
        {
            useForward = true;
        }

        if (useForward)
        {
            traversalStart = forwardStart;
            traversalEnd = forwardEnd;
            return true;
        }

        traversalStart = forwardEnd;
        traversalEnd = forwardStart;
        return true;
    }

    private float ScoreTraversalCandidate(Vector3 fromPosition, Vector3 desiredDestination, Vector3 candidateStart, Vector3 candidateEnd)
    {
        Vector2 from2D = new Vector2(fromPosition.x, fromPosition.y);
        Vector2 desired2D = new Vector2(desiredDestination.x, desiredDestination.y);
        Vector2 start2D = new Vector2(candidateStart.x, candidateStart.y);
        Vector2 end2D = new Vector2(candidateEnd.x, candidateEnd.y);

        float startDistance2D = Vector2.Distance(from2D, start2D);
        float endDistance2D = Vector2.Distance(desired2D, end2D);
        float startVerticalDelta = Mathf.Abs(fromPosition.y - candidateStart.y);
        return startDistance2D * 1.5f + endDistance2D * 0.75f + startVerticalDelta * 0.5f;
    }

    public Vector3 EvaluateTraversalPosition(Vector3 traversalStart, Vector3 traversalEnd, float normalizedTime)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);

        if (arcMode == ArcMode.Bezier && controlPoint != null)
        {
            Vector3 p0 = traversalStart;
            Vector3 p1 = controlPoint.position;
            Vector3 p2 = traversalEnd;
            float omt = 1f - normalizedTime;
            return omt * omt * p0 + 2f * omt * normalizedTime * p1 + normalizedTime * normalizedTime * p2;
        }

        Vector3 position = Vector3.Lerp(traversalStart, traversalEnd, normalizedTime);
        float arcOffset = arcHeight * 4f * normalizedTime * (1f - normalizedTime);
        position.y += arcOffset;
        return position;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawArcGizmos)
            return;

        Transform start = StartPoint;
        Transform end = EndPoint;
        if (start == null || end == null)
            return;

        Gizmos.color = arcGizmoColor;
        Gizmos.DrawSphere(start.position, 0.08f);
        Gizmos.DrawSphere(end.position, 0.08f);

        if (arcMode == ArcMode.Bezier && controlPoint != null)
        {
            Gizmos.DrawSphere(controlPoint.position, 0.06f);
            Gizmos.DrawLine(start.position, controlPoint.position);
            Gizmos.DrawLine(controlPoint.position, end.position);
        }

        const int segments = 20;
        Vector3 previous = EvaluateTraversalPosition(start.position, end.position, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 current = EvaluateTraversalPosition(start.position, end.position, t);
            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }

    private void ClampSettings()
    {
        approachDistance = Mathf.Max(0f, approachDistance);
        approachHorizontalTolerance = Mathf.Max(0f, approachHorizontalTolerance);
        approachVerticalTolerance = Mathf.Max(0f, approachVerticalTolerance);
        landingTolerance = Mathf.Max(0f, landingTolerance);
        jumpCooldownAfterUse = Mathf.Max(0f, jumpCooldownAfterUse);
        traversalCost = Mathf.Max(0f, traversalCost);
        flightTime = Mathf.Max(0.05f, flightTime);
        minimumAirTime = Mathf.Max(0f, minimumAirTime);
        arcHeight = Mathf.Max(0f, arcHeight);
        interruptKnockbackHorizontalForce = Mathf.Max(0f, interruptKnockbackHorizontalForce);
        interruptKnockbackVerticalForce = Mathf.Max(0f, interruptKnockbackVerticalForce);
    }
}
