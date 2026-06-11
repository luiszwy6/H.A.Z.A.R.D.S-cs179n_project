using System.Collections.Generic;
using UnityEngine;

public class EnemyStatus : MonoBehaviour
{
    [Header("Squad")]
    [SerializeField] private SquadMember squadMember;

    [Header("Cover")]
    [SerializeField] private bool isInCover;
    [SerializeField] private CoverTrigger currentCover;

    [Header("Combat")]
    [SerializeField] private bool isShooting;
    [SerializeField] private bool isReloading;
    [SerializeField] private bool canSeeTarget;
    [SerializeField] private bool isSmokeBlockingVision;
    [SerializeField] private bool isFlashBangStun;

    [Header("Boss")]
    [SerializeField] private bool is2ndPhase;

    [Header("Tactical")]
    [SerializeField] private bool isFlanking;
    [SerializeField] private bool isCoveringTeammate;
    [SerializeField] private bool isEscapingFromGrenade;
    [SerializeField] private bool isFollowingShield;
    [SerializeField] private bool isBackingAway;
    [SerializeField] private bool isCombatStrafing;
    [SerializeField] private bool isMeleeCombating;
    [SerializeField] private bool isGoingToLKP;
    [SerializeField] private bool isPatrol;

    private readonly List<EnemyStatus> teammateStatusesCache = new List<EnemyStatus>();

    public SquadMember SquadMember => squadMember;
    public SquadManager SquadManager => squadMember != null ? squadMember.SquadManager : null;
    public SquadEnemyType EnemyType => squadMember != null ? squadMember.EnemyType : SquadEnemyType.AR;

    public bool IsInCover => isInCover;
    public CoverTrigger CurrentCover => currentCover;

    public bool IsShooting => isShooting;
    public bool IsReloading => isReloading;
    public bool CanSeeTarget => canSeeTarget;
    public bool IsSmokeBlockingVision => isSmokeBlockingVision;
    public bool IsFlashBangStun => isFlashBangStun;
    public bool Is2ndPhase => is2ndPhase;

    public bool IsFlanking => isFlanking;
    public bool IsCoveringTeammate => isCoveringTeammate;
    public bool IsEscapingFromGrenade => isEscapingFromGrenade;
    public bool IsFollowingShield => isFollowingShield;
    public bool IsBackingAway => isBackingAway;
    public bool IsCombatStrafing => isCombatStrafing;
    public bool IsMeleeCombating => isMeleeCombating;
    public bool IsGoingToLKP => isGoingToLKP;
    public bool IsPatrol => isPatrol;

    public IReadOnlyList<EnemyStatus> TeammateStatuses
    {
        get
        {
            RefreshTeammateStatusesCache();
            return teammateStatusesCache;
        }
    }

    private void Awake()
    {
        if (squadMember == null)
            squadMember = GetComponent<SquadMember>();
    }

    public void SetInCover(bool value, CoverTrigger cover)
    {
        isInCover = value;
        currentCover = value ? cover : null;
    }

    public void SetShooting(bool value)
    {
        isShooting = value;
    }

    public void SetReloading(bool value)
    {
        isReloading = value;
    }

    public void SetCanSeeTarget(bool value)
    {
        canSeeTarget = value;
    }

    public void SetSmokeBlockingVision(bool value)
    {
        isSmokeBlockingVision = value;
    }

    public void SetFlashBangStun(bool value)
    {
        isFlashBangStun = value;

        if (value)
            isEscapingFromGrenade = false;
    }

    public void SetIs2ndPhase(bool value)
    {
        is2ndPhase = value;
    }

    public void SetFlanking(bool value)
    {
        isFlanking = value;
    }

    public void SetCoveringTeammate(bool value)
    {
        isCoveringTeammate = value;
    }

    public void SetEscapingFromGrenade(bool value)
    {
        isEscapingFromGrenade = value && !isFlashBangStun;
    }

    public void SetFollowingShield(bool value)
    {
        isFollowingShield = value;
    }

    public void SetBackingAway(bool value)
    {
        isBackingAway = value;
    }

    public void SetCombatStrafing(bool value)
    {
        isCombatStrafing = value;
    }

    public void SetMeleeCombating(bool value)
    {
        isMeleeCombating = value;
    }

    public void SetGoingToLKP(bool value)
    {
        isGoingToLKP = value;
    }

    public void SetPatrol(bool value)
    {
        isPatrol = value;
    }

    public void ClearCombat()
    {
        isShooting = false;
        isReloading = false;
    }

    public void GetTeammateStatuses(List<EnemyStatus> results, bool includeDead = false)
    {
        if (results == null)
            return;

        results.Clear();

        if (squadMember == null)
            squadMember = GetComponent<SquadMember>();

        if (squadMember == null)
            return;

        squadMember.GetTeammateStatuses(results, includeDead);
    }

    private void RefreshTeammateStatusesCache()
    {
        GetTeammateStatuses(teammateStatusesCache);
    }
}
