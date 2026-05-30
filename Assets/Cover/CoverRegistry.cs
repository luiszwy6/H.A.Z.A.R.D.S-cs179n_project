using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public static class CoverRegistry
{
    private static readonly List<CoverTrigger> covers = new List<CoverTrigger>();

    public static IReadOnlyList<CoverTrigger> Covers => covers;

    public static void Register(CoverTrigger cover)
    {
        if (cover == null)
        {
            return;
        }

        if (!covers.Contains(cover))
        {
            covers.Add(cover);
        }
    }

    public static void Unregister(CoverTrigger cover)
    {
        if (cover == null)
        {
            return;
        }

        covers.Remove(cover);
    }

    public static bool TryFindNearestCover(
        Vector3 requesterPosition,
        GameObject requester,
        float requesterRadius,
        float searchRadius,
        out CoverTrigger bestCover,
        out Vector3 bestPosition,
        out Quaternion bestRotation)
    {
        bestCover = null;
        bestPosition = requesterPosition;
        bestRotation = Quaternion.identity;

        float bestDistanceSqr = searchRadius * searchRadius;

        for (int i = covers.Count - 1; i >= 0; i--)
        {
            CoverTrigger cover = covers[i];

            if (cover == null)
            {
                covers.RemoveAt(i);
                continue;
            }

            if (!cover.IsAvailableFor(requester))
            {
                continue;
            }

            float distanceSqr = (cover.transform.position - requesterPosition).sqrMagnitude;

            if (distanceSqr > bestDistanceSqr)
            {
                continue;
            }

            if (!cover.TryGetCoverPose(
                    requesterPosition,
                    requesterRadius,
                    out Vector3 coverPosition,
                    out Quaternion coverRotation))
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestCover = cover;
            bestPosition = coverPosition;
            bestRotation = coverRotation;
        }

        return bestCover != null;
    }

    public static bool TryFindBestCoverAgainstThreat(
        Vector3 requesterPosition,
        GameObject requester,
        Vector3 threatPosition,
        float requesterRadius,
        float searchRadius,
        LayerMask coverBlockMask,
        out CoverTrigger bestCover,
        out Vector3 bestPosition,
        out Quaternion bestRotation)
    {
        bestCover = null;
        bestPosition = requesterPosition;
        bestRotation = Quaternion.identity;

        float bestScore = float.NegativeInfinity;

        for (int i = covers.Count - 1; i >= 0; i--)
        {
            CoverTrigger cover = covers[i];

            if (cover == null)
            {
                covers.RemoveAt(i);
                continue;
            }

            if (!cover.IsAvailableFor(requester))
            {
                continue;
            }

            float distanceToCover = Vector3.Distance(requesterPosition, cover.transform.position);

            if (distanceToCover > searchRadius)
            {
                continue;
            }

            Vector3 samplePosition = GetSamplePositionAwayFromThreat(cover, threatPosition);

            if (!cover.TryGetCoverPose(
                    samplePosition,
                    requesterRadius,
                    out Vector3 coverPosition,
                    out Quaternion coverRotation))
            {
                continue;
            }

            if (!IsNavMeshReachable(requesterPosition, coverPosition))
            {
                continue;
            }

            float score = 0f;

            if (DoesCoverBlockThreat(threatPosition, coverPosition, coverBlockMask))
            {
                score += 100f;
            }
            else
            {
                score -= 100f;
            }

            score -= distanceToCover * 1.5f;

            float distanceToThreat = Vector3.Distance(coverPosition, threatPosition);

            if (distanceToThreat < 4f)
            {
                score -= 35f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestCover = cover;
                bestPosition = coverPosition;
                bestRotation = coverRotation;
            }
        }

        return bestCover != null;
    }

    private static Vector3 GetSamplePositionAwayFromThreat(CoverTrigger cover, Vector3 threatPosition)
    {
        Vector3 coverCenter = cover.transform.position;

        if (cover.SurfaceCollider != null)
        {
            coverCenter = cover.SurfaceCollider.bounds.center;
        }

        Vector3 awayFromThreat = coverCenter - threatPosition;
        awayFromThreat.y = 0f;

        if (awayFromThreat.sqrMagnitude < 0.0001f)
        {
            awayFromThreat = -cover.transform.forward;
        }

        awayFromThreat.Normalize();

        return coverCenter + awayFromThreat * 2f;
    }

    private static bool DoesCoverBlockThreat(
        Vector3 threatPosition,
        Vector3 coverPosition,
        LayerMask coverBlockMask)
    {
        Vector3 threatEye = threatPosition + Vector3.up * 1.5f;
        Vector3 coverEye = coverPosition + Vector3.up * 1.2f;

        Vector3 direction = coverEye - threatEye;
        float distance = direction.magnitude;

        if (distance <= 0.01f)
        {
            return false;
        }

        direction.Normalize();

        return Physics.Raycast(
            threatEye,
            direction,
            distance,
            coverBlockMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private static bool IsNavMeshReachable(Vector3 start, Vector3 end)
    {
        NavMeshPath path = new NavMeshPath();

        if (!NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path))
        {
            return false;
        }

        return path.status == NavMeshPathStatus.PathComplete;
    }
}