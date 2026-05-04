using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class SquadFormation : MonoBehaviour
{
    [System.Serializable]
    private class FormationSlot
    {
        public string slotName;
        public Vector3 localOffset;

        [Header("Optional Role Override")]
        public bool overrideCurrentRole;
        public SquadRole currentRoleOverride;

        public FormationSlot(string slotName, Vector3 localOffset)
        {
            this.slotName = slotName;
            this.localOffset = localOffset;
            overrideCurrentRole = false;
            currentRoleOverride = SquadRole.Operator_A;
        }

        public FormationSlot(string slotName, Vector3 localOffset, bool overrideCurrentRole, SquadRole currentRoleOverride)
        {
            this.slotName = slotName;
            this.localOffset = localOffset;
            this.overrideCurrentRole = overrideCurrentRole;
            this.currentRoleOverride = currentRoleOverride;
        }
    }

    [System.Serializable]
    private class FormationPreset
    {
        public int aliveActionMemberCount;

        [Header("Pointman Slot")]
        public Vector3 pointmanOffset;

        [Header("Follower Slots")]
        public List<FormationSlot> followerSlots = new List<FormationSlot>();

        public FormationPreset(int aliveActionMemberCount, Vector3 pointmanOffset, List<FormationSlot> followerSlots)
        {
            this.aliveActionMemberCount = aliveActionMemberCount;
            this.pointmanOffset = pointmanOffset;
            this.followerSlots = followerSlots;
        }

        public bool TryGetFollowerSlot(int followerIndex, out FormationSlot slot)
        {
            slot = null;

            if (followerSlots == null)
                return false;

            if (followerIndex < 0 || followerIndex >= followerSlots.Count)
                return false;

            slot = followerSlots[followerIndex];
            return slot != null;
        }
    }

    [Header("Formation Presets")]
    [SerializeField]
    private List<FormationPreset> formationPresets = new List<FormationPreset>()
    {
        new FormationPreset(
            5,
            Vector3.zero,
            new List<FormationSlot>()
            {
                new FormationSlot("Follower 1 - Front Left", new Vector3(-1.0f, 0f, -1.2f)),
                new FormationSlot("Follower 2 - Front Right", new Vector3(1.0f, 0f, -1.2f)),
                new FormationSlot("Follower 3 - Back Center", new Vector3(0f, 0f, -2.4f)),
                new FormationSlot("Follower 4 - Rear", new Vector3(0f, 0f, -3.6f))
            }
        ),

        new FormationPreset(
            4,
            Vector3.zero,
            new List<FormationSlot>()
            {
                new FormationSlot("Follower 1 - Left", new Vector3(-0.9f, 0f, -1.2f)),
                new FormationSlot("Follower 2 - Right", new Vector3(0.9f, 0f, -1.2f)),
                new FormationSlot("Follower 3 - Rear", new Vector3(0f, 0f, -2.5f))
            }
        ),

        new FormationPreset(
            3,
            new Vector3(-0.65f, 0f, 0f),
            new List<FormationSlot>()
            {
                new FormationSlot("Second Pointman - Front Right", new Vector3(1.3f, 0f, 0f), true, SquadRole.Pointman),
                new FormationSlot("Rear - Center Back", new Vector3(0.65f, 0f, -1.3f))
            }
        ),

        new FormationPreset(
            2,
            Vector3.zero,
            new List<FormationSlot>()
            {
                new FormationSlot("Follower 1 - Rear", new Vector3(0f, 0f, -1.3f))
            }
        )
    };

    [Header("Follower Assignment Priority")]
    [SerializeField]
    private SquadRole[] followerPriority =
    {
        SquadRole.Pointman,
        SquadRole.Operator_A,
        SquadRole.Operator_B,
        SquadRole.Operator_C,
        SquadRole.RearGuard
    };

    [Header("NavMesh")]
    [SerializeField] private bool sampleFormationPositionOnNavMesh = true;
    [SerializeField] private float navMeshSampleRadius = 1.5f;

    [Header("Debug")]
    [SerializeField] private SquadMember debugCurrentPointman;
    [SerializeField] private int debugAliveActionMemberCount;

    public bool TryGetFormationPosition(
        SquadMember member,
        List<SquadMember> aliveActionMembers,
        SquadMember currentPointman,
        out Vector3 position)
    {
        position = member.transform.position;

        if (member == null)
            return false;

        if (aliveActionMembers == null)
            return false;

        if (currentPointman == null)
            return false;

        FormationPreset preset = GetPresetForAliveCount(aliveActionMembers.Count);

        if (preset == null)
            return false;

        if (member == currentPointman)
        {
            if (currentPointman.PersonalGoal == null)
                return false;

            Vector3 pointmanPosition =
                currentPointman.PersonalGoal.position +
                currentPointman.PersonalGoal.TransformDirection(preset.pointmanOffset);

            if (sampleFormationPositionOnNavMesh)
                pointmanPosition = GetNearestNavMeshPosition(pointmanPosition);

            position = pointmanPosition;
            return true;
        }

        int followerIndex = GetFollowerIndex(member, aliveActionMembers, currentPointman);

        if (followerIndex < 0)
            return false;

        FormationSlot slot;

        if (!preset.TryGetFollowerSlot(followerIndex, out slot))
            return false;

        Vector3 worldPosition =
            currentPointman.transform.position +
            currentPointman.transform.TransformDirection(slot.localOffset);

        if (sampleFormationPositionOnNavMesh)
            worldPosition = GetNearestNavMeshPosition(worldPosition);

        position = worldPosition;
        return true;
    }

    public bool TryGetCurrentRoleOverride(
        SquadMember member,
        List<SquadMember> aliveActionMembers,
        SquadMember currentPointman,
        out SquadRole role)
    {
        role = member.Role;

        if (member == null)
            return false;

        if (aliveActionMembers == null)
            return false;

        if (currentPointman == null)
            return false;

        if (member == currentPointman)
        {
            role = SquadRole.Pointman;
            return true;
        }

        FormationPreset preset = GetPresetForAliveCount(aliveActionMembers.Count);

        if (preset == null)
            return false;

        int followerIndex = GetFollowerIndex(member, aliveActionMembers, currentPointman);

        if (followerIndex < 0)
            return false;

        FormationSlot slot;

        if (!preset.TryGetFollowerSlot(followerIndex, out slot))
            return false;

        if (!slot.overrideCurrentRole)
            return false;

        role = slot.currentRoleOverride;
        return true;
    }

    public void CacheDebugData(SquadMember currentPointman, int aliveActionMemberCount)
    {
        debugCurrentPointman = currentPointman;
        debugAliveActionMemberCount = aliveActionMemberCount;
    }

    private int GetFollowerIndex(
        SquadMember member,
        List<SquadMember> aliveActionMembers,
        SquadMember currentPointman)
    {
        List<SquadMember> orderedFollowers = GetOrderedFollowers(aliveActionMembers, currentPointman);

        for (int i = 0; i < orderedFollowers.Count; i++)
        {
            if (orderedFollowers[i] == member)
                return i;
        }

        return -1;
    }

    private List<SquadMember> GetOrderedFollowers(
        List<SquadMember> aliveActionMembers,
        SquadMember currentPointman)
    {
        List<SquadMember> orderedFollowers = new List<SquadMember>();

        for (int i = 0; i < followerPriority.Length; i++)
        {
            SquadRole role = followerPriority[i];

            for (int j = 0; j < aliveActionMembers.Count; j++)
            {
                SquadMember member = aliveActionMembers[j];

                if (member == null)
                    continue;

                if (member == currentPointman)
                    continue;

                if (member.Role != role)
                    continue;

                if (!orderedFollowers.Contains(member))
                    orderedFollowers.Add(member);
            }
        }

        for (int i = 0; i < aliveActionMembers.Count; i++)
        {
            SquadMember member = aliveActionMembers[i];

            if (member == null)
                continue;

            if (member == currentPointman)
                continue;

            if (!orderedFollowers.Contains(member))
                orderedFollowers.Add(member);
        }

        return orderedFollowers;
    }

    private FormationPreset GetPresetForAliveCount(int aliveActionMemberCount)
    {
        FormationPreset exactPreset = null;

        for (int i = 0; i < formationPresets.Count; i++)
        {
            FormationPreset preset = formationPresets[i];

            if (preset == null)
                continue;

            if (preset.aliveActionMemberCount == aliveActionMemberCount)
            {
                exactPreset = preset;
                break;
            }
        }

        if (exactPreset != null)
            return exactPreset;

        FormationPreset closestPreset = null;
        int closestDifference = int.MaxValue;

        for (int i = 0; i < formationPresets.Count; i++)
        {
            FormationPreset preset = formationPresets[i];

            if (preset == null)
                continue;

            int difference = Mathf.Abs(preset.aliveActionMemberCount - aliveActionMemberCount);

            if (difference < closestDifference)
            {
                closestDifference = difference;
                closestPreset = preset;
            }
        }

        return closestPreset;
    }

    private Vector3 GetNearestNavMeshPosition(Vector3 position)
    {
        NavMeshHit hit;

        if (NavMesh.SamplePosition(position, out hit, navMeshSampleRadius, NavMesh.AllAreas))
            return hit.position;

        return position;
    }

    private void OnDrawGizmosSelected()
    {
        if (debugCurrentPointman == null)
            return;

        FormationPreset preset = GetPresetForAliveCount(debugAliveActionMemberCount);

        if (preset == null)
            return;

        if (debugCurrentPointman.PersonalGoal != null)
        {
            Vector3 pointmanWorldPosition =
                debugCurrentPointman.PersonalGoal.position +
                debugCurrentPointman.PersonalGoal.TransformDirection(preset.pointmanOffset);

            Gizmos.DrawWireCube(pointmanWorldPosition, Vector3.one * 0.35f);
        }

        if (preset.followerSlots == null)
            return;

        for (int i = 0; i < preset.followerSlots.Count; i++)
        {
            FormationSlot slot = preset.followerSlots[i];

            if (slot == null)
                continue;

            Vector3 worldPosition =
                debugCurrentPointman.transform.position +
                debugCurrentPointman.transform.TransformDirection(slot.localOffset);

            Gizmos.DrawWireSphere(worldPosition, 0.3f);
            Gizmos.DrawLine(debugCurrentPointman.transform.position, worldPosition);
        }
    }
}