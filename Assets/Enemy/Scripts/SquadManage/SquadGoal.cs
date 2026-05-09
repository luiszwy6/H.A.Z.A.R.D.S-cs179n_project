using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SquadFormation))]
public class SquadGoal : MonoBehaviour
{
    [Header("Squad Movement Rule")]
    [SerializeField] private int minimumAliveMembersForSquadMovement = 3;

    [Header("Squad Formation")]
    [SerializeField] private SquadFormation squadFormation;

    [Header("Pointman Replacement Order")]
    [SerializeField]
    private SquadRole[] pointmanPriority =
    {
        SquadRole.Pointman,
        SquadRole.Operator_A,
        SquadRole.Operator_B,
        SquadRole.Operator_C,
        SquadRole.RearGuard
    };

    [Header("Debug")]
    [SerializeField] private SquadMember currentPointman;
    [SerializeField] private bool squadMovementEnabled;

    public SquadMember CurrentPointman
    {
        get { return currentPointman; }
    }

    public bool SquadMovementEnabled
    {
        get { return squadMovementEnabled; }
    }

    private void Awake()
    {
        EnsureSquadFormation();
    }

    public void RefreshGoals(List<SquadMember> members, SquadManager squadManager)
    {
        EnsureSquadFormation();

        if (squadManager != null && squadManager.CancelLeadBy)
        {
            SetIndependentMovement(members);
            return;
        }

        List<SquadMember> aliveActionMembers = GetAliveActionMembers(members);

        if (aliveActionMembers.Count < minimumAliveMembersForSquadMovement)
        {
            SetIndependentMovement(members);
            return;
        }

        SetSquadMovement(members, aliveActionMembers, squadManager);
    }

    private void EnsureSquadFormation()
    {
        if (squadFormation == null)
            squadFormation = GetComponent<SquadFormation>();
    }

    private List<SquadMember> GetAliveActionMembers(List<SquadMember> members)
    {
        List<SquadMember> aliveActionMembers = new List<SquadMember>();

        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (member == null)
                continue;

            if (!member.IsAlive)
                continue;

            if (member.IsSniper)
                continue;

            aliveActionMembers.Add(member);
        }

        return aliveActionMembers;
    }

    private void SetSquadMovement(
        List<SquadMember> members,
        List<SquadMember> aliveActionMembers,
        SquadManager squadManager)
    {
        squadMovementEnabled = true;
        currentPointman = GetBestPointmanCandidate(aliveActionMembers);

        if (currentPointman == null || currentPointman.PersonalGoal == null)
        {
            SetIndependentMovement(members);
            return;
        }

        SetCurrentRoles(members);

        if (squadManager != null && squadManager.CancelFormation)
        {
            SetLeadByPointmanWithoutFormation(aliveActionMembers);
            return;
        }

        SetLeadByPointmanWithFormation(aliveActionMembers);
    }

    private void SetLeadByPointmanWithoutFormation(List<SquadMember> aliveActionMembers)
    {
        for (int i = 0; i < aliveActionMembers.Count; i++)
        {
            SquadMember member = aliveActionMembers[i];

            if (member == null)
                continue;

            member.SetMoveTarget(currentPointman.PersonalGoal, true);
        }
    }

    private void SetLeadByPointmanWithFormation(List<SquadMember> aliveActionMembers)
    {
        if (squadFormation != null)
            squadFormation.CacheDebugData(currentPointman, aliveActionMembers.Count);

        for (int i = 0; i < aliveActionMembers.Count; i++)
        {
            SquadMember member = aliveActionMembers[i];

            if (member == null)
                continue;

            SquadRole overrideRole;

            if (squadFormation != null &&
                squadFormation.TryGetCurrentRoleOverride(member, aliveActionMembers, currentPointman, out overrideRole))
            {
                member.SetCurrentRole(overrideRole);
            }

            Vector3 formationPosition;

            if (squadFormation != null &&
                squadFormation.TryGetFormationPosition(member, aliveActionMembers, currentPointman, out formationPosition))
            {
                member.SetMovePosition(formationPosition, true);
            }
            else
            {
                member.SetMoveTarget(currentPointman.PersonalGoal, true);
            }
        }
    }

    private void SetCurrentRoles(List<SquadMember> members)
    {
        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (member == null)
                continue;

            member.ResetCurrentRole();

            if (!member.IsAlive || member.IsSniper)
            {
                member.ClearMoveTarget();
                continue;
            }

            if (member == currentPointman)
                member.SetCurrentRole(SquadRole.Pointman);
        }
    }

    private void SetIndependentMovement(List<SquadMember> members)
    {
        squadMovementEnabled = false;
        currentPointman = null;

        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (member == null)
                continue;

            member.ResetCurrentRole();

            if (!member.IsAlive || member.IsSniper)
            {
                member.ClearMoveTarget();
                continue;
            }

            member.SetMoveTarget(member.PersonalGoal, false);
        }
    }

    private SquadMember GetBestPointmanCandidate(List<SquadMember> aliveActionMembers)
    {
        for (int i = 0; i < pointmanPriority.Length; i++)
        {
            SquadMember candidate = FindAliveMemberByRole(aliveActionMembers, pointmanPriority[i]);

            if (candidate != null)
                return candidate;
        }

        return null;
    }

    private SquadMember FindAliveMemberByRole(List<SquadMember> aliveActionMembers, SquadRole role)
    {
        for (int i = 0; i < aliveActionMembers.Count; i++)
        {
            SquadMember member = aliveActionMembers[i];

            if (member == null)
                continue;

            if (member.Role == role)
                return member;
        }

        return null;
    }
}