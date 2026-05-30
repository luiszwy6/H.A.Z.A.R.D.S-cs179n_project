using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SquadManager : MonoBehaviour
{
    [Header("Members")]
    [SerializeField] private List<SquadMember> members = new List<SquadMember>();
    [SerializeField] private bool autoCollectMembersInChildren = true;

    [Header("Enemy Type Lists")]
    [SerializeField] private List<SquadMember> shieldMembers = new List<SquadMember>();
    [SerializeField] private List<SquadMember> arMembers = new List<SquadMember>();
    [SerializeField] private List<SquadMember> shotgunMembers = new List<SquadMember>();
    [SerializeField] private List<SquadMember> sniperMembers = new List<SquadMember>();

    [Header("Tactical Override")]
    [SerializeField] private bool tacticalOverrideActive = false;

    public List<SquadMember> Members
    {
        get { return members; }
    }

    public IReadOnlyList<SquadMember> ShieldMembers
    {
        get { return shieldMembers; }
    }

    public IReadOnlyList<SquadMember> ARMembers
    {
        get { return arMembers; }
    }

    public IReadOnlyList<SquadMember> ShotgunMembers
    {
        get { return shotgunMembers; }
    }

    public IReadOnlyList<SquadMember> SniperMembers
    {
        get { return sniperMembers; }
    }

    public bool TacticalOverrideActive
    {
        get { return tacticalOverrideActive; }
    }

    private void Awake()
    {
        if (autoCollectMembersInChildren)
            CollectMembersInChildren();

        RefreshMemberTypeLists();
    }

    private void Update()
    {
        RefreshMemberTypeLists();
    }

    private void CollectMembersInChildren()
    {
        SquadMember[] foundMembers = GetComponentsInChildren<SquadMember>();

        for (int i = 0; i < foundMembers.Length; i++)
        {
            RegisterMember(foundMembers[i]);
        }
    }

    public void RegisterMember(SquadMember member)
    {
        if (member == null)
            return;

        if (!members.Contains(member))
            members.Add(member);

        member.SetSquadManager(this);
        RefreshMemberTypeLists();
    }

    public void RegisterMember(SquadMember member, SquadEnemyType enemyType)
    {
        if (member == null)
            return;

        member.SetEnemyType(enemyType);
        RegisterMember(member);
    }

    public SquadMember RegisterEnemy(GameObject enemy, SquadEnemyType enemyType)
    {
        if (enemy == null)
            return null;

        SquadMember member = enemy.GetComponent<SquadMember>();

        if (member == null)
            member = enemy.AddComponent<SquadMember>();

        RegisterMember(member, enemyType);
        return member;
    }

    public void UnregisterMember(SquadMember member)
    {
        if (member == null)
            return;

        if (members.Contains(member))
            members.Remove(member);

        RefreshMemberTypeLists();
    }

    public void SetTacticalOverrideActive(bool active)
    {
        tacticalOverrideActive = active;
    }

    public void RefreshSquad()
    {
        RefreshMemberTypeLists();
    }

    public void RefreshMemberTypeLists()
    {
        bool hadAliveShields = HasAliveShieldMember();

        shieldMembers.Clear();
        arMembers.Clear();
        shotgunMembers.Clear();
        sniperMembers.Clear();

        for (int i = members.Count - 1; i >= 0; i--)
        {
            SquadMember member = members[i];

            if (member == null)
            {
                members.RemoveAt(i);
                continue;
            }

            switch (member.EnemyType)
            {
                case SquadEnemyType.Shield:
                    shieldMembers.Add(member);
                    break;

                case SquadEnemyType.Shotgun:
                    shotgunMembers.Add(member);
                    break;

                case SquadEnemyType.Sniper:
                    sniperMembers.Add(member);
                    break;

                case SquadEnemyType.AR:
                default:
                    arMembers.Add(member);
                    break;
            }
        }

        if (hadAliveShields && !HasAliveShieldMember())
            ClearFollowShieldFromAllMembers();
    }

    private bool HasAliveShieldMember()
    {
        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (member != null &&
                member.IsAlive &&
                member.EnemyType == SquadEnemyType.Shield)
                return true;
        }

        return false;
    }

    private void ClearFollowShieldFromAllMembers()
    {
        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (member == null)
                continue;

            EnemyStatus status = member.Status;

            if (status != null)
                status.SetFollowingShield(false);
        }
    }

    public void GetTeammateStatuses(
        SquadMember requester,
        List<EnemyStatus> results,
        bool includeDead = false)
    {
        if (results == null)
            return;

        results.Clear();

        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (member == null || member == requester)
                continue;

            if (!includeDead && !member.IsAlive)
                continue;

            EnemyStatus status = member.Status;

            if (status != null)
                results.Add(status);
        }
    }

    public bool HasTeammateFlanking(SquadMember requester, SquadEnemyType? enemyType = null)
    {
        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (!IsValidTeammate(member, requester, false))
                continue;

            if (enemyType.HasValue && member.EnemyType != enemyType.Value)
                continue;

            EnemyStatus status = member.Status;

            if (status != null && status.IsFlanking)
                return true;
        }

        return false;
    }

    public bool HasTeammateSeeingTarget(SquadMember requester, SquadEnemyType? enemyType = null)
    {
        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (!IsValidTeammate(member, requester, false))
                continue;

            if (enemyType.HasValue && member.EnemyType != enemyType.Value)
                continue;

            EnemyStatus status = member.Status;

            if (status != null && status.CanSeeTarget)
                return true;
        }

        return false;
    }

    public bool HasTeammateReloading(SquadMember requester, SquadEnemyType? enemyType = null)
    {
        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (!IsValidTeammate(member, requester, false))
                continue;

            if (enemyType.HasValue && member.EnemyType != enemyType.Value)
                continue;

            EnemyStatus status = member.Status;

            if (status != null && status.IsReloading)
                return true;
        }

        return false;
    }

    public void RevealTargetToTeammates(
        SquadMember requester,
        Transform target,
        float duration)
    {
        if (target == null)
            return;

        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (!IsValidTeammate(member, requester, false))
                continue;

            EnemySensor sensor = member.GetComponent<EnemySensor>();

            if (sensor == null)
                continue;

            sensor.RevealTargetFromSquad(target, duration);
        }
    }

    public int CountAliveMembers(SquadEnemyType? enemyType = null)
    {
        int count = 0;

        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (member == null || !member.IsAlive)
                continue;

            if (enemyType.HasValue && member.EnemyType != enemyType.Value)
                continue;

            count++;
        }

        return count;
    }

    public SquadMember GetFirstAliveMember(SquadEnemyType enemyType)
    {
        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (member == null || !member.IsAlive)
                continue;

            if (member.EnemyType == enemyType)
                return member;
        }

        return null;
    }

    private bool IsValidTeammate(SquadMember member, SquadMember requester, bool includeDead)
    {
        if (member == null || member == requester)
            return false;

        if (!includeDead && !member.IsAlive)
            return false;

        return true;
    }
}
