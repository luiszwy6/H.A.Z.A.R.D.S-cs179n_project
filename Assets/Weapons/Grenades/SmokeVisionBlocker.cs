using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SmokeVisionBlocker : MonoBehaviour
{
    private static readonly List<SmokeVisionBlocker> ActiveBlockers =
        new List<SmokeVisionBlocker>();

    [SerializeField] private float radius = 6f;
    [SerializeField] private bool blockVision = false;

    public float Radius => Mathf.Max(0f, radius);
    public bool BlocksVision => blockVision && isActiveAndEnabled && Radius > 0f;
    public static int ActiveCount => ActiveBlockers.Count;

    private void OnEnable()
    {
        if (!ActiveBlockers.Contains(this))
            ActiveBlockers.Add(this);
    }

    private void OnDisable()
    {
        ActiveBlockers.Remove(this);
    }

    public void SetRadius(float value)
    {
        radius = Mathf.Max(0f, value);
    }

    public void SetBlockingEnabled(bool value)
    {
        blockVision = value;
    }

    public static bool IsLineBlocked(Vector3 start, Vector3 end)
    {
        return GetBlockingBlocker(start, end) != null;
    }

    public static SmokeVisionBlocker GetBlockingBlocker(Vector3 start, Vector3 end)
    {
        for (int i = ActiveBlockers.Count - 1; i >= 0; i--)
        {
            SmokeVisionBlocker blocker = ActiveBlockers[i];

            if (blocker == null)
            {
                ActiveBlockers.RemoveAt(i);
                continue;
            }

            if (!blocker.BlocksVision)
                continue;

            if (LineIntersectsSphere(start, end, blocker.transform.position, blocker.Radius))
                return blocker;
        }

        return null;
    }

    private static bool LineIntersectsSphere(
        Vector3 start,
        Vector3 end,
        Vector3 center,
        float sphereRadius)
    {
        Vector3 segment = end - start;
        float segmentLengthSq = segment.sqrMagnitude;

        if (segmentLengthSq <= 0.0001f)
            return Vector3.Distance(start, center) <= sphereRadius;

        float t = Vector3.Dot(center - start, segment) / segmentLengthSq;
        t = Mathf.Clamp01(t);

        Vector3 closestPoint = start + segment * t;
        return Vector3.Distance(closestPoint, center) <= sphereRadius;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.35f);
        Gizmos.DrawSphere(transform.position, Radius);
    }
}
