using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SquadManager : MonoBehaviour
{
    [Header("Members")]
    [SerializeField] private List<SquadMember> members = new List<SquadMember>();
    [SerializeField] private bool autoCollectMembersInChildren = true;

    [Header("Squad Goal")]
    [SerializeField] private SquadGoal squadGoal;

    [Header("Squad Control")]
    [SerializeField] private bool cancelFormation = false;
    [SerializeField] private bool cancelLeadBy = false;

    [Header("Tactical Override")]
    [SerializeField] private bool tacticalOverrideActive = false;

    public SquadGoal SquadGoal
    {
        get { return squadGoal; }
    }

    public List<SquadMember> Members
    {
        get { return members; }
    }

    public bool CancelFormation
    {
        get { return cancelFormation; }
    }

    public bool CancelLeadBy
    {
        get { return cancelLeadBy; }
    }

    public bool TacticalOverrideActive
    {
        get { return tacticalOverrideActive; }
    }

    private void Awake()
    {
        if (squadGoal == null)
            squadGoal = GetComponent<SquadGoal>();

        if (autoCollectMembersInChildren)
            CollectMembersInChildren();

        RefreshSquad();
    }

    private void Update()
    {
        RefreshSquad();
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
    }

    public void UnregisterMember(SquadMember member)
    {
        if (member == null)
            return;

        if (members.Contains(member))
            members.Remove(member);

        RefreshSquad();
    }

    public void SetCancelFormation(bool cancel)
    {
        cancelFormation = cancel;
        RefreshSquad();
    }

    public void SetCancelLeadBy(bool cancel)
    {
        cancelLeadBy = cancel;
        RefreshSquad();
    }

    public void SetTacticalOverrideActive(bool active)
    {
        tacticalOverrideActive = active;
        RefreshSquad();
    }

    public void RefreshSquad()
    {
        if (tacticalOverrideActive)
            return;

        if (squadGoal == null)
            return;

        squadGoal.RefreshGoals(members, this);
    }
}