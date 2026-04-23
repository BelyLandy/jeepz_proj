using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class MenuBezierRoute3D : MonoBehaviour
{
    [Header("Control Points")]
    [SerializeField] private Transform[] controlPoints;
    [SerializeField] private bool closedLoop = false;

    [Header("Gizmos")]
    [SerializeField, Min(2)] private int gizmoResolutionPerSegment = 20;
    [SerializeField] private Color gizmoColor = new Color(0.3f, 0.9f, 1f, 1f);
    [SerializeField] private bool drawControlLines = true;
    [SerializeField] private bool drawPointLabels = false;

    public bool ClosedLoop => closedLoop;
    public Transform[] ControlPoints => controlPoints;

    public int SegmentCount
    {
        get
        {
            int count = controlPoints != null ? controlPoints.Length : 0;
            if (closedLoop)
            {
                if (count < 6 || count % 3 != 0)
                    return 0;

                return count / 3;
            }

            if (count < 4 || (count - 1) % 3 != 0)
                return 0;

            return (count - 1) / 3;
        }
    }

    public bool HasValidRoute => SegmentCount > 0;

    public Vector3 EvaluatePosition(float t)
    {
        if (!TryResolveSegment(t, out int segmentIndex, out float segmentT))
            return transform.position;

        GetSegmentPoints(segmentIndex, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3);
        return EvaluateCubic(p0, p1, p2, p3, segmentT);
    }

    public Vector3 EvaluateTangent(float t)
    {
        if (!TryResolveSegment(t, out int segmentIndex, out float segmentT))
            return Vector3.forward;

        GetSegmentPoints(segmentIndex, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3);
        Vector3 tangent = EvaluateCubicTangent(p0, p1, p2, p3, segmentT);
        return tangent.sqrMagnitude > 0.000001f ? tangent.normalized : Vector3.forward;
    }

    private bool TryResolveSegment(float t, out int segmentIndex, out float segmentT)
    {
        segmentIndex = 0;
        segmentT = 0f;

        int segmentCount = SegmentCount;
        if (segmentCount <= 0)
            return false;

        if (closedLoop)
            t = Mathf.Repeat(t, 1f);
        else
            t = Mathf.Clamp01(t);

        float scaled = t * segmentCount;
        if (!closedLoop && Mathf.Approximately(t, 1f))
        {
            segmentIndex = segmentCount - 1;
            segmentT = 1f;
            return true;
        }

        segmentIndex = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, segmentCount - 1);
        segmentT = Mathf.Clamp01(scaled - segmentIndex);
        return true;
    }

    private void GetSegmentPoints(int segmentIndex, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
    {
        if (closedLoop)
        {
            int baseIndex = segmentIndex * 3;
            p0 = GetPointWorld(baseIndex);
            p1 = GetPointWorld(baseIndex + 1);
            p2 = GetPointWorld(baseIndex + 2);
            p3 = GetPointWorld(baseIndex + 3);
            return;
        }

        int start = segmentIndex * 3;
        p0 = GetPointWorld(start);
        p1 = GetPointWorld(start + 1);
        p2 = GetPointWorld(start + 2);
        p3 = GetPointWorld(start + 3);
    }

    private Vector3 GetPointWorld(int index)
    {
        int count = controlPoints != null ? controlPoints.Length : 0;
        if (count == 0)
            return transform.position;

        if (closedLoop)
            index = ((index % count) + count) % count;
        else
            index = Mathf.Clamp(index, 0, count - 1);

        Transform point = controlPoints[index];
        return point != null ? point.position : transform.position;
    }

    private static Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float oneMinusT = 1f - t;
        return oneMinusT * oneMinusT * oneMinusT * p0
             + 3f * oneMinusT * oneMinusT * t * p1
             + 3f * oneMinusT * t * t * p2
             + t * t * t * p3;
    }

    private static Vector3 EvaluateCubicTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float oneMinusT = 1f - t;
        return 3f * oneMinusT * oneMinusT * (p1 - p0)
             + 6f * oneMinusT * t * (p2 - p1)
             + 3f * t * t * (p3 - p2);
    }

    private void OnDrawGizmos()
    {
        int segmentCount = SegmentCount;
        if (segmentCount <= 0)
            return;

        Gizmos.color = gizmoColor;

        for (int segment = 0; segment < segmentCount; segment++)
        {
            GetSegmentPoints(segment, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3);

            if (drawControlLines)
            {
                Gizmos.DrawLine(p0, p1);
                Gizmos.DrawLine(p2, p3);
                Gizmos.DrawSphere(p0, 0.04f);
                Gizmos.DrawSphere(p1, 0.03f);
                Gizmos.DrawSphere(p2, 0.03f);
                Gizmos.DrawSphere(p3, 0.04f);
            }

            Vector3 previous = p0;
            int resolution = Mathf.Max(2, gizmoResolutionPerSegment);
            for (int i = 1; i <= resolution; i++)
            {
                float t = i / (float)resolution;
                Vector3 current = EvaluateCubic(p0, p1, p2, p3, t);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }

#if UNITY_EDITOR
        if (drawPointLabels && controlPoints != null)
        {
            Handles.color = gizmoColor;
            for (int i = 0; i < controlPoints.Length; i++)
            {
                Transform point = controlPoints[i];
                if (point == null)
                    continue;

                Handles.Label(point.position, $"P{i}");
            }
        }
#endif
    }
}
