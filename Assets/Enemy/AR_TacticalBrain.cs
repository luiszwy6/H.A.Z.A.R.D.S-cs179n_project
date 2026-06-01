using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Behavior;

[DisallowMultipleComponent]
public class AR_TacticalBrain : MonoBehaviour
{
    public enum TacticalMoveReason
    {
        None,
        TakeCover,
        Flank,
        Advance,
        Retreat,
        SearchLastKnownPosition,
        PatrolLastKnownPosition,
        Reposition,
        ReloadTakeCover,
        CoverTeammate,
        EscapeGrenade,
        FollowShield,
        PreferredRange,
        BackAway,
        CombatStrafe
    }

    [System.Serializable]
    private class TakeCoverRule
    {
        [Header("Enable")]
        public bool enabled = true;

        [Header("Player Status Conditions")]
        public bool requirePlayerAiming = true;
        public bool requirePlayerShooting = true;
        public bool requirePlayerReloading = false;
        public bool requirePlayerVisible = false;

        [Header("Cover Search")]
        [Min(0f)] public float searchRadius = 25f;
        [Min(0f)] public float enemyRadius = 0.45f;
        [Min(0f)] public float minDistanceFromEnemy = 0.25f;
        [Min(0f)] public float minDistanceFromPlayer = 1.5f;
        public bool ignorePlayerCurrentCover = true;
        public bool requireAvailableCover = false;

        [Header("Current Cover")]
        public bool keepCurrentCoverWhenAlreadyInCover = true;

        [Header("NavMesh")]
        public bool sampleCoverPositionOnNavMesh = true;
        [Min(0f)] public float navMeshSampleRadius = 1.5f;
        public bool requireNavMeshReachable = true;

        [Header("Cache")]
        [Min(0.1f)] public float coverRefreshInterval = 2f;

        [Header("Debug")]
        public bool debugLogSelectedCover = false;
    }

    [System.Serializable]
    private class ReloadTakeCoverRule
    {
        [Header("Enable")]
        public bool enabled = true;

        [Header("Reload Conditions")]
        public bool takeCoverWhenMagazineEmpty = true;
        public bool takeCoverWhenCanReload = false;
        public bool takeCoverWhileReloading = true;
        public bool requirePlayerVisible = false;
    }

    [System.Serializable]
    private class PlayerCoverFlankRule
    {
        [Header("Enable")]
        public bool enabled = true;

        [Header("Trigger")]
        [Min(0f)] public float playerInCoverDelay = 6f;

        [Header("Flank Position")]
        [Min(0f)] public float runOutSideDistance = 7f;
        [Min(0f)] public float playerBehindDistance = 5f;
        [Min(0f)] public float playerSideOffset = 4f;
        [Min(0f)] public float minDistanceFromPlayer = 3f;
        [Min(0f)] public float stageReachDistance = 0.9f;

        [Header("NavMesh")]
        [Min(0f)] public float navMeshSampleRadius = 2f;
        public bool requireNavMeshReachable = true;
        [Min(0f)] public float reachDistance = 0.75f;

        [Header("Debug")]
        public bool debugLogSelectedFlank = false;
    }

    [System.Serializable]
    private class CoverTeammateRule
    {
        [Header("Enable")]
        public bool enabled = true;

        [Header("Trigger")]
        public SquadEnemyType teammateType = SquadEnemyType.AR;

        [Header("Cover Search")]
        [Min(0f)] public float searchRadius = 10f;
        public bool keepCurrentCoverTargetWhileTeammateFlanking = true;
        public bool setMovePointToSelfWhenNoCover = true;
    }

    [System.Serializable]
    private class EscapeGrenadeRule
    {
        [Header("Enable")]
        public bool enabled = true;

        [Header("Grenade Types")]
        public bool escapeFrag = true;
        public bool escapeFlashBang = true;
        public bool escapeSmoke = false;

        [Header("Detection")]
        [Min(0f)] public float detectionRadius = 10f;
        [Range(0f, 360f)] public float viewAngle = 140f;
        public bool requireLineOfSight = true;
        public LayerMask obstructionMask = ~0;
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Escape")]
        [Min(0f)] public float escapeDistance = 8f;
        [Range(0f, 1f)] public float minimumEscapeDistanceRatio = 0.9f;
        [Min(0f)] public float minSafeDistance = 6f;
        [Min(0f)] public float reachDistance = 1f;
        [Min(0f)] public float navMeshSampleRadius = 3f;
        public bool requireNavMeshReachable = true;

        [Header("After Reached")]
        [Min(0f)] public float waitAtEscapePointTime = 3f;

        [Header("Debug")]
        public bool debugLogSelectedEscape = false;
    }

    [System.Serializable]
    private class PreferredRangeRule
    {
        [Header("Enable")]
        public bool enabled = true;

        [Header("Target")]
        public bool requireTargetVisible = true;

        [Header("Timing")]
        [Min(0.02f)] public float refreshInterval = 0.35f;

        [Header("Debug")]
        public bool debugLogPreferredRange = false;
    }

    [System.Serializable]
    private class BackAwayRule
    {
        [Header("Enable")]
        public bool enabled = true;

        [Header("Target")]
        public bool requireTargetVisible = true;

        [Header("NavMesh")]
        [Min(0f)] public float navMeshSampleRadius = 2f;
        public bool requireNavMeshReachable = true;

        [Header("Reach")]
        [Min(0f)] public float reachDistance = 0.75f;

        [Header("Debug")]
        public bool debugLogBackAway = false;
    }

    [System.Serializable]
    private class CombatStrafeRule
    {
        [Header("Enable")]
        public bool enabled = true;

        [Header("Target")]
        public bool requireTargetVisible = true;

        [Header("Distance")]
        [Min(0f)] public float minDistanceFromTarget = 3.5f;
        [Min(0f)] public float maxDistanceFromTarget = 15f;

        [Header("Movement")]
        [Min(0f)] public float minStrafeDistance = 1.5f;
        [Min(0f)] public float maxStrafeDistance = 3.5f;
        [Min(0f)] public float reachDistance = 0.75f;
        [Min(0.02f)] public float refreshInterval = 0.4f;

        [Header("Wait At Point")]
        [Min(0f)] public float minWaitAtPointTime = 0.1f;
        [Min(0f)] public float maxWaitAtPointTime = 0.3f;

        [Header("NavMesh")]
        [Min(0f)] public float navMeshSampleRadius = 2f;
        public bool requireNavMeshReachable = true;
        public bool avoidTeammates = true;
        [Min(0f)] public float teammateAvoidDistance = 1.5f;
        [Min(1)] public int maxPickAttempts = 8;

        [Header("Debug")]
        public bool debugLogCombatStrafe = false;
    }

    [System.Serializable]
    private class FollowShieldRule
    {
        [Header("Enable")]
        public bool enabled = true;

        [Header("Trigger")]
        public bool requireNoShotgunAlive = true;
        public bool pauseForReload = true;

        [Header("Position")]
        [Min(0f)] public float behindShieldDistance = 2.2f;
        public float sideOffset = 1.2f;
        public bool randomizeInitialSide = true;

        [Header("Update")]
        [Min(0.02f)] public float refreshInterval = 0.2f;

        [Header("NavMesh")]
        [Min(0f)] public float navMeshSampleRadius = 1.5f;
        public bool requireNavMeshReachable = true;

        [Header("Debug")]
        public bool debugLogFollowShield = false;
    }

    [System.Serializable]
    private class CheckLastKnownPositionRule
    {
        [Header("Enable")]
        public bool enabled = true;

        [Header("Target")]
        public bool preferLastKnownMarker = true;

        [Header("Reach")]
        [Min(0f)] public float reachDistance = 1f;

        [Header("NavMesh")]
        public bool sampleOnNavMesh = true;
        [Min(0f)] public float navMeshSampleRadius = 2f;
        public bool requireNavMeshReachable = true;

        [Header("Debug")]
        public bool debugLogCheckPosition = false;
    }

    [System.Serializable]
    private class PatrolLastKnownPositionRule
    {
        [Header("Enable")]
        public bool enabled = true;

        [Header("Target")]
        public bool preferLastKnownMarker = true;

        [Header("Patrol")]
        [Min(0f)] public float patrolRadius = 6f;
        [Min(0f)] public float reachDistance = 1f;
        [Min(0f)] public float minWaitAtPointTime = 1f;
        [Min(0f)] public float maxWaitAtPointTime = 2f;

        [Header("NavMesh")]
        [Min(0f)] public float navMeshSampleRadius = 2f;
        public bool requireNavMeshReachable = true;
        [Min(1)] public int maxPickAttempts = 12;

        [Header("Debug")]
        public bool debugLogPatrol = false;
    }

    [Header("Refs")]
    [SerializeField] private BehaviorGraphAgent behaviorAgent;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private EnemySensor enemySensor;
    [SerializeField] private EnemyWeaponShooter enemyWeaponShooter;
    [SerializeField] private EnemyWeaponSettings enemyWeaponSettings;
    [SerializeField] private EnemyStatus enemyStatus;
    [SerializeField] private SquadMember squadMember;
    [SerializeField] private Transform tacticalMovePoint;

    [Header("Runtime Point")]
    [SerializeField] private bool autoCreateTacticalMovePoint = true;
    [SerializeField] private bool destroyPointOnDestroy = true;

    [Header("No Tactical Target")]
    [SerializeField] private bool movePointToEnemyPositionWhenNoTarget = true;
    [SerializeField] private bool sampleEnemyPositionOnNavMesh = false;
    [SerializeField] private float enemyPositionNavMeshSampleRadius = 1f;

    [Header("Tactical Target Failsafe")]
    [SerializeField] private bool resetStaleTacticalTarget = true;
    [Min(0f)] [SerializeField] private float tacticalTargetStaleTime = 15f;

    [Header("Combat Range Settings")]
    [Min(0f)] [SerializeField] private float attackRange = 15f;
    [Min(0f)] [SerializeField] private float tooCloseRange = 3.5f;
    [Min(0f)] [SerializeField] private float preferredRange = 8f;
    [Min(0f)] [SerializeField] private float chaseRange = 11f;
    [Min(0f)] [SerializeField] private float stopRange = 8.75f;
    [Min(0f)] [SerializeField] private float stopTolerance = 0.75f;
    [Min(0f)] [SerializeField] private float safeRange = 4f;
    [Min(0f)] [SerializeField] private float backAwayStepDistance = 1f;
    [Min(0f)] [SerializeField] private float preferredRangeNavMeshSampleRadius = 2f;
    [SerializeField] private bool requirePreferredRangeNavMeshReachable = true;

    [Header("Take Cover Rule")]
    [SerializeField] private TakeCoverRule takeCoverRule = new TakeCoverRule();

    [Header("Take Cover Stabilization")]
    [SerializeField] private bool keepCurrentCoverWhilePlayerThreatActive = true;
    [Min(0f)] [SerializeField] private float playerThreatClearDelay = 2f;
    [SerializeField] private bool clearTakeCoverWhenPlayerStopsShooting = true;
    [Min(0f)] [SerializeField] private float takeCoverShootingClearDelay = 0f;

    [Header("Reload Take Cover Rule")]
    [SerializeField] private ReloadTakeCoverRule reloadTakeCoverRule = new ReloadTakeCoverRule();

    [Header("Player Cover Flank Rule")]
    [SerializeField] private PlayerCoverFlankRule playerCoverFlankRule = new PlayerCoverFlankRule();

    [Header("Cover Teammate Rule")]
    [SerializeField] private CoverTeammateRule coverTeammateRule = new CoverTeammateRule();

    [Header("Escape Grenade Rule")]
    [SerializeField] private EscapeGrenadeRule escapeGrenadeRule = new EscapeGrenadeRule();

    [Header("Preferred Range Rule")]
    [SerializeField] private PreferredRangeRule preferredRangeRule = new PreferredRangeRule();

    [Header("Back Away Rule")]
    [SerializeField] private BackAwayRule backAwayRule = new BackAwayRule();

    [Header("Combat Strafe Rule")]
    [SerializeField] private CombatStrafeRule combatStrafeRule = new CombatStrafeRule();

    [Header("Follow Shield Rule")]
    [SerializeField] private FollowShieldRule followShieldRule = new FollowShieldRule();

    [Header("Check Last Known Position Rule")]
    [SerializeField] private CheckLastKnownPositionRule checkLastKnownPositionRule = new CheckLastKnownPositionRule();

    [Header("Patrol Last Known Position Rule")]
    [SerializeField] private PatrolLastKnownPositionRule patrolLastKnownPositionRule = new PatrolLastKnownPositionRule();

    [Header("Squad Tactical Move Offset")]
    [SerializeField] private bool applySquadTacticalMoveOffset = true;
    [Min(0f)] [SerializeField] private float squadTacticalMoveOffsetSpacing = 1.5f;
    [Min(0f)] [SerializeField] private float squadTacticalMoveOffsetNavMeshSampleRadius = 1.5f;
    [SerializeField] private bool applySquadTacticalMoveOffsetToFlank = false;

    [Header("Behavior Blackboard")]
    [SerializeField] private bool uploadToBlackboard = true;
    [SerializeField] private string tacticalMovePointVariableName = "TacticalMovePoint";
    [SerializeField] private string tacticalMovePositionVariableName = "TacticalMovePosition";
    [SerializeField] private string hasTacticalMoveTargetVariableName = "HasTacticalMovePoint";
    [SerializeField] private string tacticalMoveReasonVariableName = "TacticalMoveReason";
    [SerializeField] private bool uploadCombatRangeSettingsToBlackboard = true;
    [SerializeField] private bool uploadCombatRangeSettingsOnlyOnce = true;
    [SerializeField] private string attackRangeVariableName = "AttackRange";
    [SerializeField] private string tooCloseRangeVariableName = "TooCloseRange";
    [SerializeField] private string preferredRangeVariableName = "PreferredRange";
    [SerializeField] private string chaseRangeVariableName = "ChaseRange";
    [SerializeField] private string stopRangeVariableName = "StopRange";
    [SerializeField] private string stopToleranceVariableName = "StopTolerance";
    [SerializeField] private string safeRangeVariableName = "SafeRange";
    [SerializeField] private string backAwayStepDistanceVariableName = "BackAwayStepDistance";
    [SerializeField] private bool uploadPlayerStatusToBlackboard = true;
    [SerializeField] private string playerStatusVariableName = "PlayerStatus";
    [SerializeField] private string playerObjectVariableName = "PlayerObject";
    [SerializeField] private string isPlayerInCoverVariableName = "IsPlayerInCover";

    private readonly List<CoverTrigger> cachedCovers = new List<CoverTrigger>();

    private float nextCoverRefreshTime;
    private float lastTacticalTargetUpdateTime = -999f;
    private float lastPlayerAimOrShootTime = -999f;
    private float lastPlayerShootingTime = -999f;
    private float playerCoverStartTime = -1f;
    private int currentFlankSide = 1;
    private int currentFlankStage = -1;
    private readonly Vector3[] flankStagePositions = new Vector3[3];
    private bool hasFlankStagePositions;
    private CoverTrigger observedPlayerCover;
    private CoverTrigger activeFlankCover;
    private CoverTrigger completedFlankCover;

    private bool hasTacticalMoveTarget;
    private Vector3 tacticalMovePosition;
    private TacticalMoveReason currentReason = TacticalMoveReason.None;
    private GrenadeWorldController activeEscapeGrenade;
    private GrenadeWorldController completedEscapeGrenade;
    private float activeEscapeWaitEndTime = -1f;
    private SquadMember followShieldTarget;
    private int followShieldSide;
    private float nextFollowShieldUpdateTime;
    private bool suppressNextSquadTacticalMoveOffset;
    private bool hasUploadedCombatRangeSettings;
    private float nextCombatStrafeUpdateTime;
    private float combatStrafeWaitEndTime = -1f;
    private float lastKnownPatrolWaitEndTime = -1f;
    private Vector3 lastKnownPatrolCenter;
    private float nextPreferredRangeUpdateTime;

    public Transform TacticalMovePoint => tacticalMovePoint;
    public bool HasTacticalMoveTarget => hasTacticalMoveTarget;
    public Vector3 TacticalMovePosition => tacticalMovePosition;
    public TacticalMoveReason CurrentReason => currentReason;

    private void Awake()
    {
        if (behaviorAgent == null)
            behaviorAgent = GetComponent<BehaviorGraphAgent>();

        if (navMeshAgent == null)
            navMeshAgent = GetComponent<NavMeshAgent>();

        if (enemySensor == null)
            enemySensor = GetComponent<EnemySensor>();

        if (enemyStatus == null)
            enemyStatus = GetComponent<EnemyStatus>();

        if (squadMember == null)
            squadMember = GetComponent<SquadMember>();

        if (enemyWeaponShooter == null)
            enemyWeaponShooter = GetComponentInChildren<EnemyWeaponShooter>(true);

        if (enemyWeaponSettings == null)
            enemyWeaponSettings = ResolveEnemyWeaponSettings();

        if (autoCreateTacticalMovePoint)
            CreateOrRegisterTacticalMovePoint();

        SetMovePointToEnemyPosition();

        RefreshCoverCache(true);
        UploadBlackboardValues();
    }

    private void Update()
    {
        UpdatePlayerThreatTimer();
        UpdatePlayerCoverTimer();
        UploadPlayerStatusBlackboardValues();
        UploadCombatRangeBlackboardValues();

        if (TickTacticalTargetFailsafe())
            return;

        if (TickActiveEscapeGrenade())
            return;

        if (TryStartImmediateGrenadeEscape())
            return;

        if (TickReloadTakeCover())
            return;

        if (TickFollowShield())
            return;

        if (TickActiveFlankSequence())
            return;

        if (TickActiveCoverTeammate())
            return;

        if (TickActiveTakeCover())
            return;

        if (TickActiveCombatStrafe())
            return;

        if (TickActiveLastKnownPositionSearch())
            return;

        if (TickActiveLastKnownPositionPatrol())
            return;

        EvaluateTacticalDecision();
    }

    private bool TickTacticalTargetFailsafe()
    {
        if (!resetStaleTacticalTarget)
            return false;

        if (!hasTacticalMoveTarget)
            return false;

        if (currentReason == TacticalMoveReason.None)
        {
            lastTacticalTargetUpdateTime = Time.time;
            return false;
        }

        if (currentReason == TacticalMoveReason.CoverTeammate &&
            ShouldCoverFlankingTeammate())
        {
            lastTacticalTargetUpdateTime = Time.time;
            return false;
        }

        if (currentReason == TacticalMoveReason.FollowShield &&
            ShouldARFollowShieldNow())
        {
            lastTacticalTargetUpdateTime = Time.time;
            return false;
        }

        float staleTime = Mathf.Max(0f, tacticalTargetStaleTime);

        if (Time.time < lastTacticalTargetUpdateTime + staleTime)
            return false;

        ResetFlankSequence();
        ClearTacticalTarget();
        return true;
    }

    private bool TickFollowShield()
    {
        if (!ShouldARFollowShieldNow())
        {
            if (currentReason == TacticalMoveReason.FollowShield)
                ClearTacticalTarget();

            return false;
        }

        if (ShouldPauseFollowShieldForReload())
        {
            if (hasTacticalMoveTarget || currentReason != TacticalMoveReason.None)
                ClearTacticalTarget();

            return true;
        }

        if (currentReason == TacticalMoveReason.FollowShield &&
            hasTacticalMoveTarget &&
            Time.time < nextFollowShieldUpdateTime)
        {
            return true;
        }

        nextFollowShieldUpdateTime =
            Time.time + Mathf.Max(0.02f, followShieldRule.refreshInterval);

        if (!TryFindFollowShieldPosition(out Vector3 followPosition))
        {
            if (currentReason == TacticalMoveReason.FollowShield)
                ClearTacticalTarget();

            return false;
        }

        suppressNextSquadTacticalMoveOffset = true;
        SetTacticalMoveTarget(followPosition, TacticalMoveReason.FollowShield);
        return true;
    }

    private bool TickActiveFlankSequence()
    {
        if (currentReason != TacticalMoveReason.Flank)
            return false;

        PlayerStatus playerStatus = PlayerStatus.Instance;

        if (playerStatus == null ||
            !playerStatus.IsInCover ||
            activeFlankCover != playerStatus.CurrentCover)
        {
            ResetFlankSequence();
            ClearFlankTacticalTarget();
            return true;
        }

        TickPlayerCoverFlankSequence(playerStatus);
        return true;
    }

    private bool TickActiveCoverTeammate()
    {
        if (currentReason != TacticalMoveReason.CoverTeammate)
            return false;

        if (ShouldCoverFlankingTeammate())
            return true;

        ClearTacticalTarget();
        return true;
    }

    private bool TickActiveTakeCover()
    {
        if (currentReason != TacticalMoveReason.TakeCover)
            return false;

        if (!ShouldClearTakeCoverBecausePlayerStoppedShooting())
            return false;

        ClearTacticalTarget();
        return true;
    }

    private bool TickActiveEscapeGrenade()
    {
        if (currentReason != TacticalMoveReason.EscapeGrenade)
            return false;

        if (IsFlashBangStunned())
        {
            CancelGrenadeEscapeForFlashBangStun();
            return true;
        }

        if (HasReachedCurrentGrenadeEscapePoint())
        {
            if (activeEscapeWaitEndTime < 0f)
            {
                activeEscapeWaitEndTime = Time.time + ResolveEscapePointWaitTime();
                lastTacticalTargetUpdateTime = Time.time;
                StopNavMeshAgentAtTacticalPoint();
                return true;
            }

            if (Time.time < activeEscapeWaitEndTime)
            {
                lastTacticalTargetUpdateTime = Time.time;
                StopNavMeshAgentAtTacticalPoint();
                return true;
            }

            CompleteGrenadeEscape();
            return true;
        }

        if (hasTacticalMoveTarget)
            return true;

        if (TryFindGrenadeEscapePosition(out Vector3 escapePosition))
        {
            SetTacticalMoveTarget(escapePosition, TacticalMoveReason.EscapeGrenade);
            return true;
        }

        ClearTacticalTarget();
        return true;
    }

    private void CompleteGrenadeEscape()
    {
        completedEscapeGrenade = activeEscapeGrenade;
        activeEscapeGrenade = null;
        activeEscapeWaitEndTime = -1f;

        SetEnemyEscapingFromGrenadeStatus(false);
        ClearTacticalTarget();
    }

    private float ResolveEscapePointWaitTime()
    {
        if (escapeGrenadeRule == null)
            return 3f;

        return Mathf.Max(0f, escapeGrenadeRule.waitAtEscapePointTime);
    }

    private void StopNavMeshAgentAtTacticalPoint()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            return;

        navMeshAgent.isStopped = true;
        navMeshAgent.ResetPath();
        navMeshAgent.velocity = Vector3.zero;
    }

    private bool HasReachedCurrentGrenadeEscapePoint()
    {
        if (!hasTacticalMoveTarget)
            return false;

        float reachDistance = escapeGrenadeRule != null
            ? Mathf.Max(0f, escapeGrenadeRule.reachDistance)
            : 1f;

        return Vector3.Distance(transform.position, tacticalMovePosition) <= reachDistance;
    }

    private void CreateOrRegisterTacticalMovePoint()
    {
        TacticalMovePointManager manager = TacticalMovePointManager.GetOrCreate();

        if (manager == null)
            return;

        tacticalMovePoint = manager.RegisterPoint(transform, tacticalMovePoint);
    }

    private bool TickReloadTakeCover()
    {
        if (!ShouldTakeCoverForReload())
        {
            if (currentReason == TacticalMoveReason.ReloadTakeCover)
                ClearTacticalTarget();

            return false;
        }

        if (currentReason == TacticalMoveReason.ReloadTakeCover && hasTacticalMoveTarget)
            return true;

        PlayerStatus playerStatus = PlayerStatus.Instance;

        if (playerStatus == null)
        {
            ClearTacticalTarget();
            return true;
        }

        if (TryFindCoverPosition(playerStatus, out Vector3 coverPosition))
        {
            SetTacticalMoveTarget(coverPosition, TacticalMoveReason.ReloadTakeCover);
            return true;
        }

        ClearTacticalTarget();
        return true;
    }

    private void EvaluateTacticalDecision()
    {
        PlayerStatus playerStatus = PlayerStatus.Instance;

        if (playerStatus == null)
        {
            ClearTacticalTarget();
            return;
        }

        if (!CanUseVisibleTarget())
        {
            if (TryUpdateLastKnownPositionTarget())
                return;

            ClearTacticalTarget();
            return;
        }

        if (ShouldTakeCoverForReload())
        {
            Vector3 coverPosition;

            if (TryFindCoverPosition(playerStatus, out coverPosition))
            {
                SetTacticalMoveTarget(coverPosition, TacticalMoveReason.ReloadTakeCover);
                return;
            }

            ClearTacticalTarget();
            return;
        }

        if (ShouldFlankPlayerInCover(playerStatus))
        {
            if (TickPlayerCoverFlankSequence(playerStatus))
                return;
        }

        if (ShouldCoverFlankingTeammate())
        {
            TickCoverFlankingTeammate(playerStatus);
            return;
        }

        if (currentReason == TacticalMoveReason.CoverTeammate)
        {
            ClearTacticalTarget();
            return;
        }

        if (HasCompletedFlankForCurrentPlayerCover(playerStatus))
            return;

        if (ShouldTakeCover(playerStatus))
        {
            if (ShouldKeepCurrentTakeCoverTarget())
                return;

            Vector3 coverPosition;

            if (TryFindCoverPosition(playerStatus, out coverPosition))
            {
                SetTacticalMoveTarget(coverPosition, TacticalMoveReason.TakeCover);
                return;
            }

            ClearTacticalTarget();
            return;
        }

        if (ShouldKeepCurrentTakeCoverTarget())
            return;

        if (TryUpdateBackAwayTarget(playerStatus))
            return;

        if (TryUpdateCombatStrafeTarget(playerStatus))
            return;

        if (TryUpdatePreferredRangeTarget(playerStatus))
            return;

        ClearTacticalTarget();
    }

    private bool TickActiveLastKnownPositionSearch()
    {
        if (currentReason != TacticalMoveReason.SearchLastKnownPosition)
            return false;

        if (CanUseVisibleTarget())
        {
            ClearTacticalTarget();
            return false;
        }

        if (HasReachedLastKnownPositionPoint())
        {
            SetEnemyGoingToLKPStatus(false);

            if (TryStartLastKnownPositionPatrol())
                return true;

            ClearTacticalTarget();
            return true;
        }

        if (hasTacticalMoveTarget)
            return true;

        if (TryStartLastKnownPositionSearch())
            return true;

        ClearTacticalTarget();
        return true;
    }

    private bool TickActiveLastKnownPositionPatrol()
    {
        if (currentReason != TacticalMoveReason.PatrolLastKnownPosition)
            return false;

        if (CanUseVisibleTarget())
        {
            ClearTacticalTarget();
            return false;
        }

        if (HasReachedLastKnownPatrolPoint())
        {
            if (lastKnownPatrolWaitEndTime < 0f)
            {
                lastKnownPatrolWaitEndTime = Time.time + ResolveLastKnownPatrolWaitTime();
                lastTacticalTargetUpdateTime = Time.time;
                StopNavMeshAgentAtTacticalPoint();
                return true;
            }

            if (Time.time < lastKnownPatrolWaitEndTime)
            {
                lastTacticalTargetUpdateTime = Time.time;
                StopNavMeshAgentAtTacticalPoint();
                return true;
            }

            lastKnownPatrolWaitEndTime = -1f;
        }

        if (hasTacticalMoveTarget && !HasReachedLastKnownPatrolPoint())
            return true;

        if (TryFindLastKnownPatrolPosition(out Vector3 patrolPosition))
        {
            SetTacticalMoveTarget(patrolPosition, TacticalMoveReason.PatrolLastKnownPosition);
            return true;
        }

        ClearTacticalTarget();
        return true;
    }

    private bool TryUpdateLastKnownPositionTarget()
    {
        if (TryStartLastKnownPositionSearch())
            return true;

        return TryStartLastKnownPositionPatrol();
    }

    private bool TryStartLastKnownPositionSearch()
    {
        if (checkLastKnownPositionRule == null || !checkLastKnownPositionRule.enabled)
            return false;

        if (!TryResolveLastKnownPosition(checkLastKnownPositionRule.preferLastKnownMarker, out Vector3 lastKnownPosition))
            return false;

        Vector3 movePosition = lastKnownPosition;

        if (checkLastKnownPositionRule.sampleOnNavMesh &&
            !TrySampleNavMeshPosition(
                movePosition,
                checkLastKnownPositionRule.navMeshSampleRadius,
                out movePosition))
        {
            return false;
        }

        if (checkLastKnownPositionRule.requireNavMeshReachable && !IsNavMeshReachable(movePosition))
            return false;

        SetTacticalMoveTarget(movePosition, TacticalMoveReason.SearchLastKnownPosition);

        if (checkLastKnownPositionRule.debugLogCheckPosition)
            Debug.Log($"[AR_TacticalBrain] {name} checking LKP move point={movePosition}", this);

        return true;
    }

    private bool TryStartLastKnownPositionPatrol()
    {
        if (patrolLastKnownPositionRule == null || !patrolLastKnownPositionRule.enabled)
            return false;

        if (!TryResolveLastKnownPosition(patrolLastKnownPositionRule.preferLastKnownMarker, out lastKnownPatrolCenter))
            return false;

        if (!TryFindLastKnownPatrolPosition(out Vector3 patrolPosition))
            return false;

        SetTacticalMoveTarget(patrolPosition, TacticalMoveReason.PatrolLastKnownPosition);
        return true;
    }

    private bool TryFindLastKnownPatrolPosition(out Vector3 patrolPosition)
    {
        patrolPosition = transform.position;

        if (patrolLastKnownPositionRule == null)
            return false;

        float radius = Mathf.Max(0f, patrolLastKnownPositionRule.patrolRadius);
        int attempts = Mathf.Max(1, patrolLastKnownPositionRule.maxPickAttempts);
        float sampleRadius = Mathf.Max(0f, patrolLastKnownPositionRule.navMeshSampleRadius);

        for (int i = 0; i < attempts; i++)
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 candidate = lastKnownPatrolCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (!TrySampleNavMeshPosition(candidate, sampleRadius, out patrolPosition))
                continue;

            if (patrolLastKnownPositionRule.requireNavMeshReachable && !IsNavMeshReachable(patrolPosition))
                continue;

            lastKnownPatrolWaitEndTime = -1f;

            if (patrolLastKnownPositionRule.debugLogPatrol)
                Debug.Log($"[AR_TacticalBrain] {name} LKP patrol move point={patrolPosition}", this);

            return true;
        }

        return false;
    }

    private bool TryResolveLastKnownPosition(bool preferMarker, out Vector3 position)
    {
        position = transform.position;

        if (enemySensor == null)
            enemySensor = GetComponent<EnemySensor>();

        if (enemySensor == null || !enemySensor.HasLastKnownPosition)
            return false;

        if (preferMarker && enemySensor.LastKnownPositionMarker != null)
            position = enemySensor.LastKnownPositionMarker.position;
        else
            position = enemySensor.LastKnownPosition;

        return true;
    }

    private bool HasReachedLastKnownPositionPoint()
    {
        if (!hasTacticalMoveTarget)
            return false;

        float reachDistance = checkLastKnownPositionRule != null
            ? Mathf.Max(0f, checkLastKnownPositionRule.reachDistance)
            : 1f;

        return Vector3.Distance(transform.position, tacticalMovePosition) <= reachDistance;
    }

    private bool HasReachedLastKnownPatrolPoint()
    {
        if (!hasTacticalMoveTarget)
            return false;

        float reachDistance = patrolLastKnownPositionRule != null
            ? Mathf.Max(0f, patrolLastKnownPositionRule.reachDistance)
            : 1f;

        return Vector3.Distance(transform.position, tacticalMovePosition) <= reachDistance;
    }

    private float ResolveLastKnownPatrolWaitTime()
    {
        if (patrolLastKnownPositionRule == null)
            return 1.5f;

        float min = Mathf.Max(0f, patrolLastKnownPositionRule.minWaitAtPointTime);
        float max = Mathf.Max(0f, patrolLastKnownPositionRule.maxWaitAtPointTime);

        if (max < min)
            max = min;

        return UnityEngine.Random.Range(min, max);
    }

    private bool TryUpdateBackAwayTarget(PlayerStatus playerStatus)
    {
        if (backAwayRule == null || !backAwayRule.enabled)
            return false;

        if (playerStatus == null)
            return false;

        if (backAwayRule.requireTargetVisible && !CanUseVisibleTarget())
            return false;

        float currentDistance = Vector3.Distance(transform.position, playerStatus.transform.position);

        if (currentDistance >= Mathf.Max(0f, safeRange))
        {
            if (currentReason == TacticalMoveReason.BackAway)
                ClearTacticalTarget();

            return false;
        }

        if (currentReason == TacticalMoveReason.BackAway &&
            hasTacticalMoveTarget &&
            !HasReachedBackAwayPoint())
        {
            return true;
        }

        if (!TryFindBackAwayPosition(playerStatus.transform.position, out Vector3 backAwayPosition))
            return false;

        SetTacticalMoveTarget(backAwayPosition, TacticalMoveReason.BackAway);

        if (backAwayRule.debugLogBackAway)
        {
            Debug.Log(
                $"[AR_TacticalBrain] {name} back away move point={backAwayPosition}",
                this
            );
        }

        return true;
    }

    private bool HasReachedBackAwayPoint()
    {
        if (!hasTacticalMoveTarget)
            return false;

        float reachDistance = backAwayRule != null
            ? Mathf.Max(0.05f, backAwayRule.reachDistance)
            : 0.75f;

        return Vector3.Distance(transform.position, tacticalMovePosition) <= reachDistance;
    }

    private bool TryFindBackAwayPosition(Vector3 targetPosition, out Vector3 backAwayPosition)
    {
        backAwayPosition = transform.position;

        Vector3 awayDirection = transform.position - targetPosition;
        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude <= 0.01f)
            awayDirection = -transform.forward;

        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude <= 0.01f)
            awayDirection = Vector3.back;

        awayDirection.Normalize();

        Vector3 candidatePosition = transform.position + awayDirection * Mathf.Max(0f, backAwayStepDistance);
        float sampleRadius = backAwayRule != null
            ? Mathf.Max(0f, backAwayRule.navMeshSampleRadius)
            : 2f;

        if (!TrySampleNavMeshPosition(candidatePosition, sampleRadius, out backAwayPosition))
            return false;

        if (backAwayRule != null &&
            backAwayRule.requireNavMeshReachable &&
            !IsNavMeshReachable(backAwayPosition))
        {
            return false;
        }

        return true;
    }

    private bool TryUpdateCombatStrafeTarget(PlayerStatus playerStatus)
    {
        if (!IsCombatStrafeAllowed(playerStatus))
        {
            if (currentReason == TacticalMoveReason.CombatStrafe)
                ClearTacticalTarget();

            return false;
        }

        if (currentReason == TacticalMoveReason.CombatStrafe &&
            hasTacticalMoveTarget &&
            Time.time < nextCombatStrafeUpdateTime &&
            !HasReachedCombatStrafePoint())
        {
            return true;
        }

        if (!TryFindCombatStrafePosition(out Vector3 strafePosition))
            return false;

        SetTacticalMoveTarget(strafePosition, TacticalMoveReason.CombatStrafe);
        nextCombatStrafeUpdateTime = Time.time + Mathf.Max(0.02f, combatStrafeRule.refreshInterval);

        if (combatStrafeRule.debugLogCombatStrafe)
        {
            Debug.Log(
                $"[AR_TacticalBrain] {name} combat strafe move point={strafePosition}",
                this
            );
        }

        return true;
    }

    private bool TickActiveCombatStrafe()
    {
        if (currentReason != TacticalMoveReason.CombatStrafe)
            return false;

        PlayerStatus playerStatus = PlayerStatus.Instance;

        if (!IsCombatStrafeAllowed(playerStatus))
        {
            ClearTacticalTarget();
            return true;
        }

        if (!HasReachedCombatStrafePoint())
            return true;

        if (combatStrafeWaitEndTime < 0f)
        {
            combatStrafeWaitEndTime = Time.time + ResolveRandomCombatStrafeWaitTime();
            return true;
        }

        if (Time.time < combatStrafeWaitEndTime)
            return true;

        combatStrafeWaitEndTime = -1f;

        if (!TryFindCombatStrafePosition(out Vector3 strafePosition))
        {
            ClearTacticalTarget();
            return true;
        }

        SetTacticalMoveTarget(strafePosition, TacticalMoveReason.CombatStrafe);
        nextCombatStrafeUpdateTime = Time.time + Mathf.Max(0.02f, combatStrafeRule.refreshInterval);

        if (combatStrafeRule.debugLogCombatStrafe)
        {
            Debug.Log(
                $"[AR_TacticalBrain] {name} combat strafe next move point={strafePosition}",
                this
            );
        }

        return true;
    }

    private bool IsCombatStrafeAllowed(PlayerStatus playerStatus)
    {
        if (combatStrafeRule == null || !combatStrafeRule.enabled)
            return false;

        if (playerStatus == null)
            return false;

        if (combatStrafeRule.requireTargetVisible && !CanUseVisibleTarget())
            return false;

        float currentDistance = Vector3.Distance(transform.position, playerStatus.transform.position);
        float preferredReadyDistance = ResolvePreferredRange() + ResolveStopTolerance();

        if (currentDistance > preferredReadyDistance)
            return false;

        if (currentDistance < combatStrafeRule.minDistanceFromTarget ||
            currentDistance > combatStrafeRule.maxDistanceFromTarget)
        {
            return false;
        }

        if (currentReason == TacticalMoveReason.PreferredRange &&
            hasTacticalMoveTarget &&
            Vector3.Distance(transform.position, tacticalMovePosition) > 0.1f)
        {
            return false;
        }

        return true;
    }

    private bool HasReachedCombatStrafePoint()
    {
        if (!hasTacticalMoveTarget)
            return false;

        float reachDistance = combatStrafeRule != null
            ? Mathf.Max(0f, combatStrafeRule.reachDistance)
            : 0.75f;

        return Vector3.Distance(transform.position, tacticalMovePosition) <= reachDistance;
    }

    private float ResolveRandomCombatStrafeWaitTime()
    {
        if (combatStrafeRule == null)
            return 0f;

        float min = Mathf.Max(0f, combatStrafeRule.minWaitAtPointTime);
        float max = Mathf.Max(0f, combatStrafeRule.maxWaitAtPointTime);

        if (max < min)
            max = min;

        return UnityEngine.Random.Range(min, max);
    }

    private bool TryFindCombatStrafePosition(out Vector3 strafePosition)
    {
        strafePosition = transform.position;

        int attempts = combatStrafeRule != null
            ? Mathf.Max(1, combatStrafeRule.maxPickAttempts)
            : 8;

        for (int i = 0; i < attempts; i++)
        {
            Vector3 candidatePosition = GetCombatStrafeCandidate(i);
            float sampleRadius = combatStrafeRule != null
                ? Mathf.Max(0f, combatStrafeRule.navMeshSampleRadius)
                : 2f;

            if (!TrySampleNavMeshPosition(candidatePosition, sampleRadius, out Vector3 sampledPosition))
                continue;

            if (combatStrafeRule != null &&
                combatStrafeRule.requireNavMeshReachable &&
                !IsNavMeshReachable(sampledPosition))
            {
                continue;
            }

            if (IsTooCloseToTeammate(sampledPosition))
                continue;

            strafePosition = sampledPosition;
            return true;
        }

        return false;
    }

    private Vector3 GetCombatStrafeCandidate(int attempt)
    {
        float min = combatStrafeRule != null ? Mathf.Max(0f, combatStrafeRule.minStrafeDistance) : 1.5f;
        float max = combatStrafeRule != null ? Mathf.Max(0f, combatStrafeRule.maxStrafeDistance) : 3.5f;

        if (max < min)
            max = min;

        float distance = UnityEngine.Random.Range(min, max);
        float side = attempt == 0
            ? (UnityEngine.Random.value < 0.5f ? -1f : 1f)
            : (attempt % 2 == 0 ? 1f : -1f);

        Vector3 right = transform.right;
        right.y = 0f;

        if (right.sqrMagnitude <= 0.01f)
            right = Vector3.right;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.01f)
            forward = Vector3.forward;

        float forwardOffset = attempt <= 1
            ? 0f
            : UnityEngine.Random.Range(-0.5f, 0.5f) * distance;

        return transform.position +
               right.normalized * side * distance +
               forward.normalized * forwardOffset;
    }

    private bool IsTooCloseToTeammate(Vector3 position)
    {
        if (combatStrafeRule == null || !combatStrafeRule.avoidTeammates)
            return false;

        if (squadMember == null || squadMember.SquadManager == null)
            return false;

        float avoidDistance = Mathf.Max(0f, combatStrafeRule.teammateAvoidDistance);

        if (avoidDistance <= 0f)
            return false;

        float avoidDistanceSqr = avoidDistance * avoidDistance;
        var members = squadMember.SquadManager.Members;

        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (member == null || member == squadMember || !member.IsAlive)
                continue;

            if ((position - member.transform.position).sqrMagnitude < avoidDistanceSqr)
                return true;
        }

        return false;
    }

    private bool CanUseVisibleTarget()
    {
        if (enemySensor == null)
            enemySensor = GetComponent<EnemySensor>();

        if (enemySensor == null)
            return false;

        enemySensor.RefreshSensor();
        return enemySensor.CanSeeTarget;
    }

    private bool TryUpdatePreferredRangeTarget(PlayerStatus playerStatus)
    {
        if (preferredRangeRule == null || !preferredRangeRule.enabled)
            return false;

        if (playerStatus == null)
            return false;

        if (preferredRangeRule.requireTargetVisible)
        {
            if (enemySensor == null)
                enemySensor = GetComponent<EnemySensor>();

            if (enemySensor == null)
                return false;

            enemySensor.RefreshSensor();

            if (!enemySensor.CanSeeTarget)
            {
                if (currentReason == TacticalMoveReason.PreferredRange)
                    ClearTacticalTarget();

                return false;
            }
        }

        float preferredRange = ResolvePreferredRange();
        float stopTolerance = ResolveStopTolerance();
        float currentDistance = Vector3.Distance(transform.position, playerStatus.transform.position);

        if (ShouldKeepCurrentPreferredRangeTarget())
            return true;

        if (currentDistance <= preferredRange + stopTolerance)
        {
            SetTacticalMoveTarget(transform.position, TacticalMoveReason.PreferredRange);
            ScheduleNextPreferredRangeUpdate();
            return true;
        }

        if (!TryFindPreferredRangePosition(playerStatus.transform.position, preferredRange, out Vector3 preferredPosition))
        {
            if (currentReason == TacticalMoveReason.PreferredRange)
                ClearTacticalTarget();

            return false;
        }

        SetTacticalMoveTarget(preferredPosition, TacticalMoveReason.PreferredRange);
        ScheduleNextPreferredRangeUpdate();

        if (preferredRangeRule.debugLogPreferredRange)
        {
            Debug.Log(
                $"[AR_TacticalBrain] {name} preferred range move point={preferredPosition}",
                this
            );
        }

        return true;
    }

    private bool ShouldKeepCurrentPreferredRangeTarget()
    {
        if (currentReason != TacticalMoveReason.PreferredRange)
            return false;

        if (!hasTacticalMoveTarget)
            return false;

        return Time.time < nextPreferredRangeUpdateTime;
    }

    private void ScheduleNextPreferredRangeUpdate()
    {
        float refreshInterval = preferredRangeRule != null
            ? Mathf.Max(0.02f, preferredRangeRule.refreshInterval)
            : 0.35f;

        nextPreferredRangeUpdateTime = Time.time + refreshInterval;
    }

    private bool TryFindPreferredRangePosition(
        Vector3 targetPosition,
        float preferredRange,
        out Vector3 preferredPosition)
    {
        preferredPosition = transform.position;

        Vector3 awayFromTarget = transform.position - targetPosition;
        awayFromTarget.y = 0f;

        if (awayFromTarget.sqrMagnitude <= 0.01f)
            awayFromTarget = -transform.forward;

        awayFromTarget.y = 0f;

        if (awayFromTarget.sqrMagnitude <= 0.01f)
            awayFromTarget = Vector3.back;

        awayFromTarget.Normalize();

        Vector3 candidatePosition = targetPosition + awayFromTarget * preferredRange;
        float sampleRadius = Mathf.Max(0f, preferredRangeNavMeshSampleRadius);

        if (!TrySampleNavMeshPosition(candidatePosition, sampleRadius, out preferredPosition))
            return false;

        if (requirePreferredRangeNavMeshReachable && !IsNavMeshReachable(preferredPosition))
            return false;

        if (Vector3.Distance(transform.position, preferredPosition) <= Mathf.Max(0f, stopTolerance))
            return false;

        return true;
    }

    private float ResolvePreferredRange()
    {
        return Mathf.Max(0f, preferredRange);
    }

    private float ResolveStopTolerance()
    {
        return Mathf.Max(0f, stopTolerance);
    }

    private bool TryStartImmediateGrenadeEscape()
    {
        if (IsFlashBangStunned())
            return false;

        if (currentReason == TacticalMoveReason.EscapeGrenade)
            return false;

        if (!TryFindGrenadeEscapePosition(out Vector3 grenadeEscapePosition))
            return false;

        SetTacticalMoveTarget(grenadeEscapePosition, TacticalMoveReason.EscapeGrenade);
        return true;
    }

    private void UpdatePlayerThreatTimer()
    {
        PlayerStatus playerStatus = PlayerStatus.Instance;

        if (playerStatus == null)
            return;

        if (IsPlayerAimOrShootThreatActive(playerStatus))
            lastPlayerAimOrShootTime = Time.time;

        if (playerStatus.IsAnyShooting)
            lastPlayerShootingTime = Time.time;
    }

    private bool ShouldKeepCurrentTakeCoverTarget()
    {
        if (!keepCurrentCoverWhilePlayerThreatActive)
            return false;

        if (!hasTacticalMoveTarget)
            return false;

        if (currentReason != TacticalMoveReason.TakeCover)
            return false;

        if (clearTakeCoverWhenPlayerStopsShooting)
            return !HasPlayerShootingBeenClearLongEnough();

        return !HasPlayerThreatBeenClearLongEnough();
    }

    private bool ShouldClearTakeCoverBecausePlayerStoppedShooting()
    {
        if (!clearTakeCoverWhenPlayerStopsShooting)
            return false;

        if (!hasTacticalMoveTarget)
            return false;

        return HasPlayerShootingBeenClearLongEnough();
    }

    private bool HasPlayerThreatBeenClearLongEnough()
    {
        float clearDelay = Mathf.Max(0f, playerThreatClearDelay);
        return Time.time >= lastPlayerAimOrShootTime + clearDelay;
    }

    private bool HasPlayerShootingBeenClearLongEnough()
    {
        float clearDelay = Mathf.Max(0f, takeCoverShootingClearDelay);
        return Time.time >= lastPlayerShootingTime + clearDelay;
    }

    private bool IsPlayerAimOrShootThreatActive(PlayerStatus playerStatus)
    {
        if (playerStatus == null)
            return false;

        return playerStatus.IsAiming || playerStatus.IsAnyShooting;
    }

    private void UpdatePlayerCoverTimer()
    {
        PlayerStatus playerStatus = PlayerStatus.Instance;

        if (playerStatus == null || !playerStatus.IsInCover)
        {
            playerCoverStartTime = -1f;
            observedPlayerCover = null;
            completedFlankCover = null;
            ResetFlankSequence();
            ClearFlankTacticalTargetIfActive();
            return;
        }

        CoverTrigger currentCover = playerStatus.CurrentCover;

        if (observedPlayerCover != currentCover)
        {
            observedPlayerCover = currentCover;
            playerCoverStartTime = Time.time;
            completedFlankCover = null;
            ResetFlankSequence();
            ClearFlankTacticalTargetIfActive();
            return;
        }

        if (playerCoverStartTime < 0f)
            playerCoverStartTime = Time.time;
    }

    private bool ShouldFlankPlayerInCover(PlayerStatus playerStatus)
    {
        if (playerCoverFlankRule == null)
            return false;

        if (!playerCoverFlankRule.enabled)
            return false;

        if (playerStatus == null || !playerStatus.IsInCover)
            return false;

        if (HasSquadTeammateFlanking(SquadEnemyType.AR))
            return false;

        if (completedFlankCover != null && completedFlankCover == playerStatus.CurrentCover)
            return false;

        if (playerCoverStartTime < 0f)
            return false;

        float coverDelay = Mathf.Max(0f, playerCoverFlankRule.playerInCoverDelay);
        return Time.time >= playerCoverStartTime + coverDelay;
    }

    private bool ShouldCoverFlankingTeammate()
    {
        if (coverTeammateRule == null)
            return false;

        if (!coverTeammateRule.enabled)
            return false;

        if (currentReason == TacticalMoveReason.Flank)
            return false;

        if (squadMember == null)
            squadMember = GetComponent<SquadMember>();

        if (squadMember == null)
            return false;

        if (squadMember.EnemyType != SquadEnemyType.AR)
            return false;

        return HasSquadTeammateFlanking(coverTeammateRule.teammateType);
    }

    private void TickCoverFlankingTeammate(PlayerStatus playerStatus)
    {
        if (coverTeammateRule.keepCurrentCoverTargetWhileTeammateFlanking &&
            currentReason == TacticalMoveReason.CoverTeammate &&
            hasTacticalMoveTarget)
        {
            return;
        }

        Vector3 coverPosition;

        if (TryFindCoverPosition(
                playerStatus,
                out coverPosition,
                coverTeammateRule.searchRadius))
        {
            SetTacticalMoveTarget(coverPosition, TacticalMoveReason.CoverTeammate);
            return;
        }

        if (coverTeammateRule.setMovePointToSelfWhenNoCover)
            SetTacticalMoveTarget(transform.position, TacticalMoveReason.CoverTeammate);
        else
            ClearTacticalTarget();
    }

    private bool HasSquadTeammateFlanking(SquadEnemyType enemyType)
    {
        if (squadMember == null)
            squadMember = GetComponent<SquadMember>();

        if (squadMember == null || squadMember.SquadManager == null)
            return false;

        return squadMember.SquadManager.HasTeammateFlanking(squadMember, enemyType);
    }

    private bool ShouldARFollowShieldNow()
    {
        if (followShieldRule == null || !followShieldRule.enabled)
            return false;

        if (squadMember == null)
            squadMember = GetComponent<SquadMember>();

        if (squadMember == null || !squadMember.IsAlive)
            return false;

        if (squadMember.EnemyType != SquadEnemyType.AR)
            return false;

        SquadManager manager = squadMember.SquadManager;

        if (manager == null)
            return false;

        if (manager.CountAliveMembers(SquadEnemyType.Shield) <= 0)
            return false;

        if (followShieldRule.requireNoShotgunAlive &&
            manager.CountAliveMembers(SquadEnemyType.Shotgun) > 0)
        {
            return false;
        }

        if (enemyStatus == null)
            enemyStatus = GetComponent<EnemyStatus>();

        if (enemyStatus != null && enemyStatus.IsFollowingShield)
            return true;

        if (HasOtherAliveARFollowingShield(manager))
            return false;

        return manager.GetFirstAliveMember(SquadEnemyType.AR) == squadMember;
    }

    private bool ShouldPauseFollowShieldForReload()
    {
        if (followShieldRule == null || !followShieldRule.pauseForReload)
            return false;

        EnemyWeaponSettings weaponSettings = ResolveEnemyWeaponSettings();

        if (weaponSettings != null && weaponSettings.IsReloading)
            return true;

        if (enemyWeaponShooter == null)
            enemyWeaponShooter = GetComponentInChildren<EnemyWeaponShooter>(true);

        if (enemyWeaponShooter != null)
            return enemyWeaponShooter.NeedsReload();

        if (weaponSettings == null)
            return false;

        return weaponSettings.IsMagazineEmpty && weaponSettings.HasReserveAmmo;
    }

    private bool HasOtherAliveARFollowingShield(SquadManager manager)
    {
        if (manager == null)
            return false;

        List<SquadMember> members = manager.Members;

        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (member == null || member == squadMember || !member.IsAlive)
                continue;

            if (member.EnemyType != SquadEnemyType.AR)
                continue;

            EnemyStatus status = member.Status;

            if (status != null && status.IsFollowingShield)
                return true;
        }

        return false;
    }

    private bool TryFindFollowShieldPosition(out Vector3 followPosition)
    {
        followPosition = transform.position;

        if (!TryGetBestAliveShield(out SquadMember shieldMember))
        {
            ClearFollowShieldTarget();
            return false;
        }

        if (followShieldTarget != shieldMember)
        {
            followShieldTarget = shieldMember;
            followShieldSide = 0;
        }

        Transform shieldTransform = shieldMember.transform;

        if (!TryGetShieldBehindDirection(shieldTransform, out Vector3 behindDirection))
            return false;

        Vector3 sideDirection = Vector3.Cross(Vector3.up, behindDirection).normalized;

        if (sideDirection.sqrMagnitude <= 0.01f)
            sideDirection = shieldTransform.right;

        sideDirection.y = 0f;

        if (sideDirection.sqrMagnitude <= 0.01f)
            sideDirection = Vector3.right;

        sideDirection.Normalize();

        int side = ResolveFollowShieldSide();
        Vector3 candidate =
            shieldTransform.position +
            behindDirection * Mathf.Max(0f, followShieldRule.behindShieldDistance) +
            sideDirection * followShieldRule.sideOffset * side;

        if (!TryValidateFollowShieldPosition(candidate, out followPosition))
            return false;

        if (followShieldRule.debugLogFollowShield)
        {
            Debug.Log(
                $"[AR_TacticalBrain] {name} following shield: {shieldMember.name}, position={followPosition}",
                this
            );
        }

        return true;
    }

    private bool TryGetBestAliveShield(out SquadMember bestShield)
    {
        bestShield = null;

        if (squadMember == null || squadMember.SquadManager == null)
            return false;

        IReadOnlyList<SquadMember> shields = squadMember.SquadManager.ShieldMembers;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < shields.Count; i++)
        {
            SquadMember shield = shields[i];

            if (shield == null || !shield.IsAlive)
                continue;

            float distance = Vector3.Distance(transform.position, shield.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestShield = shield;
            }
        }

        return bestShield != null;
    }

    private bool TryGetShieldBehindDirection(
        Transform shieldTransform,
        out Vector3 behindDirection)
    {
        behindDirection = Vector3.zero;

        if (shieldTransform == null)
            return false;

        Vector3 referencePosition = ResolveShieldReferencePosition(shieldTransform);
        behindDirection = shieldTransform.position - referencePosition;
        behindDirection.y = 0f;

        if (behindDirection.sqrMagnitude <= 0.01f)
        {
            behindDirection = -shieldTransform.forward;
            behindDirection.y = 0f;
        }

        if (behindDirection.sqrMagnitude <= 0.01f)
            behindDirection = transform.position - shieldTransform.position;

        behindDirection.y = 0f;

        if (behindDirection.sqrMagnitude <= 0.01f)
            return false;

        behindDirection.Normalize();
        return true;
    }

    private Vector3 ResolveShieldReferencePosition(Transform shieldTransform)
    {
        PlayerStatus playerStatus = PlayerStatus.Instance;

        if (playerStatus != null)
            return playerStatus.transform.position;

        if (enemySensor == null)
            enemySensor = GetComponent<EnemySensor>();

        if (enemySensor != null && enemySensor.Target != null)
            return enemySensor.Target.position;

        return shieldTransform.position + shieldTransform.forward;
    }

    private int ResolveFollowShieldSide()
    {
        if (followShieldSide == 0)
        {
            if (followShieldRule.randomizeInitialSide)
                followShieldSide = Random.value < 0.5f ? -1 : 1;
            else
                followShieldSide = 1;
        }

        return followShieldSide;
    }

    private bool TryValidateFollowShieldPosition(
        Vector3 candidate,
        out Vector3 followPosition)
    {
        followPosition = candidate;

        if (!TrySampleNavMeshPosition(
                candidate,
                followShieldRule.navMeshSampleRadius,
                out candidate))
        {
            return false;
        }

        if (followShieldRule.requireNavMeshReachable && !IsNavMeshReachable(candidate))
            return false;

        followPosition = candidate;
        return true;
    }

    private void ClearFollowShieldTarget()
    {
        followShieldTarget = null;
        followShieldSide = 0;
        nextFollowShieldUpdateTime = 0f;
    }

    private bool HasCompletedFlankForCurrentPlayerCover(PlayerStatus playerStatus)
    {
        if (playerStatus == null || !playerStatus.IsInCover)
            return false;

        return completedFlankCover != null &&
               completedFlankCover == playerStatus.CurrentCover;
    }

    private bool TickPlayerCoverFlankSequence(PlayerStatus playerStatus)
    {
        if (playerStatus == null)
            return false;

        if (currentReason == TacticalMoveReason.Flank &&
            hasTacticalMoveTarget &&
            activeFlankCover == playerStatus.CurrentCover)
        {
            if (!HasReachedCurrentTacticalMovePoint())
                return true;

            return AdvancePlayerCoverFlankSequence(playerStatus);
        }

        return StartPlayerCoverFlankSequence(playerStatus);
    }

    private bool StartPlayerCoverFlankSequence(PlayerStatus playerStatus)
    {
        if (playerStatus == null)
            return false;

        int firstSide = Random.value < 0.5f ? -1 : 1;
        int secondSide = -firstSide;

        if (TryPreparePlayerCoverFlankRoute(playerStatus, firstSide) &&
            TrySetPreparedPlayerCoverFlankStage(playerStatus, 0))
        {
            return true;
        }

        if (TryPreparePlayerCoverFlankRoute(playerStatus, secondSide) &&
            TrySetPreparedPlayerCoverFlankStage(playerStatus, 0))
        {
            return true;
        }

        ResetFlankSequence();
        return false;
    }

    private bool AdvancePlayerCoverFlankSequence(PlayerStatus playerStatus)
    {
        int nextStage = currentFlankStage + 1;

        if (nextStage >= 3)
        {
            completedFlankCover = activeFlankCover;
            ResetFlankSequence();
            ClearFlankTacticalTarget();
            return true;
        }

        if (TrySetPreparedPlayerCoverFlankStage(playerStatus, nextStage))
            return true;

        completedFlankCover = activeFlankCover;
        ResetFlankSequence();
        ClearFlankTacticalTarget();
        return true;
    }

    private bool TrySetPreparedPlayerCoverFlankStage(
        PlayerStatus playerStatus,
        int stage)
    {
        if (playerStatus == null)
            return false;

        if (!hasFlankStagePositions)
            return false;

        if (stage < 0 || stage >= flankStagePositions.Length)
            return false;

        currentFlankStage = stage;
        activeFlankCover = playerStatus.CurrentCover;

        SetTacticalMoveTarget(flankStagePositions[stage], TacticalMoveReason.Flank);
        SetEnemyFlankingStatus(true);
        return true;
    }

    private bool TryPreparePlayerCoverFlankRoute(PlayerStatus playerStatus, int flankSide)
    {
        hasFlankStagePositions = false;

        if (playerStatus == null)
            return false;

        Vector3 enemyPosition = transform.position;
        Vector3 playerPosition = playerStatus.transform.position;
        Vector3 playerForward = playerStatus.transform.forward;
        Vector3 enemyRight = transform.right;

        playerForward.y = 0f;
        enemyRight.y = 0f;

        if (playerForward.sqrMagnitude <= 0.001f)
            playerForward = Vector3.forward;

        if (enemyRight.sqrMagnitude <= 0.001f)
            enemyRight = Vector3.right;

        playerForward.Normalize();
        enemyRight.Normalize();

        currentFlankSide = flankSide < 0 ? -1 : 1;

        Vector3 sideDirection = enemyRight * currentFlankSide;
        Vector3 sideRunPoint = enemyPosition + sideDirection * playerCoverFlankRule.runOutSideDistance;
        Vector3 sideBehindPlayerPoint =
            playerPosition -
            playerForward * playerCoverFlankRule.playerBehindDistance +
            sideDirection * playerCoverFlankRule.playerSideOffset;
        Vector3 behindPlayerPoint =
            playerPosition -
            playerForward * playerCoverFlankRule.playerBehindDistance;

        if (!TryResolveFlankCandidate(playerPosition, sideRunPoint, sideDirection, 0, out flankStagePositions[0]))
            return false;

        if (!TryResolveFlankCandidate(playerPosition, sideBehindPlayerPoint, sideDirection, 1, out flankStagePositions[1]))
            return false;

        if (!TryResolveFlankCandidate(playerPosition, behindPlayerPoint, sideDirection, 2, out flankStagePositions[2]))
            return false;

        hasFlankStagePositions = true;
        return true;
    }

    private bool TryResolveFlankCandidate(
        Vector3 playerPosition,
        Vector3 candidate,
        Vector3 sideDirection,
        int stage,
        out Vector3 flankPosition)
    {
        flankPosition = candidate;

        float minDistanceFromPlayer = Mathf.Max(0f, playerCoverFlankRule.minDistanceFromPlayer);

        if (Vector3.Distance(playerPosition, candidate) < minDistanceFromPlayer)
            candidate += sideDirection * (minDistanceFromPlayer + 0.5f);

        if (!TrySampleNavMeshPosition(
                candidate,
                playerCoverFlankRule.navMeshSampleRadius,
                out candidate))
        {
            return false;
        }

        if (playerCoverFlankRule.requireNavMeshReachable && !IsNavMeshReachable(candidate))
            return false;

        flankPosition = candidate;

        if (playerCoverFlankRule.debugLogSelectedFlank)
        {
            Debug.Log(
                $"[AR_TacticalBrain] {name} selected flank stage={stage}, position={flankPosition}, side={currentFlankSide}",
                this
            );
        }

        return true;
    }

    private bool HasReachedCurrentTacticalMovePoint()
    {
        if (!hasTacticalMoveTarget)
            return false;

        float reachDistance = playerCoverFlankRule != null
            ? Mathf.Max(0f, playerCoverFlankRule.stageReachDistance)
            : 0.9f;

        return Vector3.Distance(transform.position, tacticalMovePosition) <= reachDistance;
    }

    private void ResetFlankSequence()
    {
        currentFlankStage = -1;
        hasFlankStagePositions = false;
        activeFlankCover = null;
        SetEnemyFlankingStatus(false);
    }

    private void ClearFlankTacticalTargetIfActive()
    {
        if (currentReason != TacticalMoveReason.Flank)
            return;

        ClearFlankTacticalTarget();
    }

    private void ClearFlankTacticalTarget()
    {
        currentReason = TacticalMoveReason.None;
        lastTacticalTargetUpdateTime = -999f;
        SetEnemyFollowingShieldStatus(false);
        ClearFollowShieldTarget();
        ApplyBaseTacticalMovePointState();
        UploadBlackboardValues();
    }

    private void SetEnemyFlankingStatus(bool value)
    {
        EnemyStatus status = ResolveEnemyStatus();

        if (status != null)
            status.SetFlanking(value);
    }

    private void SetEnemyCoveringTeammateStatus(bool value)
    {
        EnemyStatus status = ResolveEnemyStatus();

        if (status != null)
            status.SetCoveringTeammate(value);
    }

    private void SetEnemyEscapingFromGrenadeStatus(bool value)
    {
        EnemyStatus status = ResolveEnemyStatus();

        if (status != null)
            status.SetEscapingFromGrenade(value);
    }

    private bool IsFlashBangStunned()
    {
        EnemyStatus status = ResolveEnemyStatus();
        return status != null && status.IsFlashBangStun;
    }

    private void CancelGrenadeEscapeForFlashBangStun()
    {
        activeEscapeGrenade = null;
        activeEscapeWaitEndTime = -1f;
        SetEnemyEscapingFromGrenadeStatus(false);
        ClearTacticalTarget();
    }

    private void SetEnemyFollowingShieldStatus(bool value)
    {
        EnemyStatus status = ResolveEnemyStatus();

        if (status != null)
            status.SetFollowingShield(value);
    }

    private void SetEnemyBackingAwayStatus(bool value)
    {
        EnemyStatus status = ResolveEnemyStatus();

        if (status != null)
            status.SetBackingAway(value);
    }

    private void SetEnemyCombatStrafingStatus(bool value)
    {
        EnemyStatus status = ResolveEnemyStatus();

        if (status != null)
            status.SetCombatStrafing(value);
    }

    private void SetEnemyGoingToLKPStatus(bool value)
    {
        EnemyStatus status = ResolveEnemyStatus();

        if (status != null)
            status.SetGoingToLKP(value);
    }

    private void SetEnemyPatrolStatus(bool value)
    {
        EnemyStatus status = ResolveEnemyStatus();

        if (status != null)
            status.SetPatrol(value);
    }

    private EnemyStatus ResolveEnemyStatus()
    {
        if (enemyStatus != null)
            return enemyStatus;

        enemyStatus = GetComponent<EnemyStatus>();

        if (enemyStatus == null)
            enemyStatus = GetComponentInParent<EnemyStatus>();

        if (enemyStatus == null)
            enemyStatus = GetComponentInChildren<EnemyStatus>(true);

        return enemyStatus;
    }

    private bool ShouldTakeCoverForReload()
    {
        if (reloadTakeCoverRule == null)
            return false;

        if (!reloadTakeCoverRule.enabled)
            return false;

        if (reloadTakeCoverRule.requirePlayerVisible && !IsPlayerVisible())
            return false;

        EnemyWeaponSettings weaponSettings = ResolveEnemyWeaponSettings();

        if (reloadTakeCoverRule.takeCoverWhileReloading &&
            weaponSettings != null &&
            weaponSettings.IsReloading)
        {
            return true;
        }

        if (reloadTakeCoverRule.takeCoverWhenCanReload &&
            weaponSettings != null &&
            weaponSettings.CanReload())
        {
            return true;
        }

        if (!reloadTakeCoverRule.takeCoverWhenMagazineEmpty)
            return false;

        if (enemyWeaponShooter == null)
            enemyWeaponShooter = GetComponentInChildren<EnemyWeaponShooter>(true);

        if (enemyWeaponShooter != null)
            return enemyWeaponShooter.NeedsReload();

        if (weaponSettings == null)
            return false;

        return weaponSettings.IsMagazineEmpty && weaponSettings.HasReserveAmmo;
    }

    private bool TryFindGrenadeEscapePosition(out Vector3 escapePosition)
    {
        escapePosition = transform.position;

        if (escapeGrenadeRule == null || !escapeGrenadeRule.enabled)
            return false;

        GrenadeWorldController grenade = FindMostDangerousVisibleGrenade();

        if (grenade == null)
            return false;

        activeEscapeGrenade = grenade;

        Vector3 awayDirection = transform.position - grenade.transform.position;
        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude <= 0.01f)
            awayDirection = -transform.forward;

        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude <= 0.01f)
            awayDirection = Vector3.back;

        awayDirection.Normalize();

        if (!TryFindFixedDistanceGrenadeEscapePosition(
                transform.position,
                awayDirection,
                grenade.transform.position,
                out Vector3 candidate))
        {
            return false;
        }

        escapePosition = candidate;

        if (escapeGrenadeRule.debugLogSelectedEscape)
        {
            Debug.Log(
                $"[AR_TacticalBrain] {name} escaping grenade: {grenade.name}, type={grenade.GrenadeType}, position={escapePosition}",
                this
            );
        }

        return true;
    }

    private bool TryFindFixedDistanceGrenadeEscapePosition(
        Vector3 origin,
        Vector3 awayDirection,
        Vector3 grenadePosition,
        out Vector3 escapePosition)
    {
        escapePosition = origin;

        float escapeDistance = Mathf.Max(0f, escapeGrenadeRule.escapeDistance);
        float minTravelDistance = escapeDistance *
            Mathf.Clamp01(escapeGrenadeRule.minimumEscapeDistanceRatio);

        if (escapeDistance <= 0f)
            return false;

        float[] angleOffsets = { 0f, -25f, 25f, -50f, 50f, -75f, 75f, -100f, 100f };

        for (int i = 0; i < angleOffsets.Length; i++)
        {
            Vector3 direction = Quaternion.AngleAxis(angleOffsets[i], Vector3.up) * awayDirection;
            Vector3 candidate = origin + direction.normalized * escapeDistance;

            if (!TrySampleNavMeshPosition(
                    candidate,
                    escapeGrenadeRule.navMeshSampleRadius,
                    out candidate))
            {
                continue;
            }

            if (Vector3.Distance(origin, candidate) < minTravelDistance)
                continue;

            if (Vector3.Distance(candidate, grenadePosition) <
                Mathf.Max(0f, escapeGrenadeRule.minSafeDistance))
            {
                continue;
            }

            if (escapeGrenadeRule.requireNavMeshReachable && !IsNavMeshReachable(candidate))
                continue;

            escapePosition = candidate;
            return true;
        }

        return false;
    }

    private GrenadeWorldController FindMostDangerousVisibleGrenade()
    {
        if (completedEscapeGrenade != null && !completedEscapeGrenade.IsActiveThreat)
            completedEscapeGrenade = null;

        GrenadeWorldController bestGrenade = null;
        float bestDistance = float.MaxValue;
        List<GrenadeWorldController> grenades = GrenadeWorldController.ActiveGrenades;

        for (int i = grenades.Count - 1; i >= 0; i--)
        {
            GrenadeWorldController grenade = grenades[i];

            if (grenade == null)
            {
                grenades.RemoveAt(i);
                continue;
            }

            if (grenade == completedEscapeGrenade)
                continue;

            if (!IsDangerousVisibleGrenade(grenade, out float distance))
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestGrenade = grenade;
            }
        }

        return bestGrenade;
    }

    private bool IsDangerousVisibleGrenade(
        GrenadeWorldController grenade,
        out float distance)
    {
        distance = float.MaxValue;

        if (grenade == null || !grenade.IsActiveThreat)
            return false;

        if (!ShouldEscapeGrenadeType(grenade.GrenadeType))
            return false;

        Vector3 eyePosition = enemySensor != null
            ? enemySensor.GetEyePosition()
            : transform.position + Vector3.up * 1.5f;

        Vector3 grenadePosition = grenade.transform.position;
        Vector3 toGrenade = grenadePosition - eyePosition;
        distance = toGrenade.magnitude;

        if (distance > Mathf.Max(0f, escapeGrenadeRule.detectionRadius))
            return false;

        Vector3 flatToGrenade = toGrenade;
        flatToGrenade.y = 0f;

        if (flatToGrenade.sqrMagnitude > 0.01f)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude <= 0.01f)
                forward = Vector3.forward;

            float angle = Vector3.Angle(forward.normalized, flatToGrenade.normalized);

            if (angle > escapeGrenadeRule.viewAngle * 0.5f)
                return false;
        }

        if (!escapeGrenadeRule.requireLineOfSight)
            return true;

        if (distance <= 0.01f)
            return true;

        Vector3 direction = toGrenade / distance;

        if (!Physics.Raycast(
                eyePosition,
                direction,
                out RaycastHit hit,
                distance,
                escapeGrenadeRule.obstructionMask,
                escapeGrenadeRule.triggerInteraction))
        {
            return true;
        }

        return hit.transform == grenade.transform ||
               hit.transform.IsChildOf(grenade.transform);
    }

    private bool ShouldEscapeGrenadeType(PlayerGrenadeSlots.GrenadeType grenadeType)
    {
        switch (grenadeType)
        {
            case PlayerGrenadeSlots.GrenadeType.FlashBang:
                return escapeGrenadeRule.escapeFlashBang;

            case PlayerGrenadeSlots.GrenadeType.Smoke:
                return escapeGrenadeRule.escapeSmoke;

            case PlayerGrenadeSlots.GrenadeType.Frag:
            default:
                return escapeGrenadeRule.escapeFrag;
        }
    }

    private bool ShouldTakeCover(PlayerStatus playerStatus)
    {
        if (takeCoverRule == null)
            return false;

        if (!takeCoverRule.enabled)
            return false;

        if (playerStatus == null)
            return false;

        if (takeCoverRule.requirePlayerAiming && !playerStatus.IsAiming)
            return false;

        if (takeCoverRule.requirePlayerShooting && !playerStatus.IsAnyShooting)
            return false;

        if (takeCoverRule.requirePlayerReloading && !playerStatus.IsReloading)
            return false;

        if (takeCoverRule.requirePlayerVisible && !IsPlayerVisible())
            return false;

        return true;
    }

    private bool IsPlayerVisible()
    {
        if (enemySensor == null)
            enemySensor = GetComponent<EnemySensor>();

        if (enemySensor == null)
            return false;

        return enemySensor.CanSeeTarget;
    }

    private EnemyWeaponSettings ResolveEnemyWeaponSettings()
    {
        if (enemyWeaponSettings != null)
            return enemyWeaponSettings;

        if (enemyWeaponShooter == null)
            enemyWeaponShooter = GetComponentInChildren<EnemyWeaponShooter>(true);

        if (enemyWeaponShooter != null)
            enemyWeaponSettings = enemyWeaponShooter.GetEnemyWeaponSettings();

        if (enemyWeaponSettings == null)
            enemyWeaponSettings = GetComponentInChildren<EnemyWeaponSettings>(true);

        return enemyWeaponSettings;
    }

    private bool TryFindCoverPosition(PlayerStatus playerStatus, out Vector3 coverPosition)
    {
        return TryFindCoverPosition(playerStatus, out coverPosition, -1f);
    }

    private bool TryFindCoverPosition(
        PlayerStatus playerStatus,
        out Vector3 coverPosition,
        float searchRadiusOverride)
    {
        coverPosition = transform.position;

        if (TryUseCurrentCoverPositionIfAlreadyInCover(out coverPosition))
            return true;

        if (takeCoverRule == null)
            return false;

        RefreshCoverCache(false);

        if (cachedCovers.Count == 0)
            return false;

        Vector3 playerPosition = playerStatus != null
            ? playerStatus.transform.position
            : transform.position;

        CoverTrigger bestCover = null;
        Vector3 bestPosition = transform.position;
        float bestScore = float.MaxValue;

        for (int i = cachedCovers.Count - 1; i >= 0; i--)
        {
            CoverTrigger cover = cachedCovers[i];

            if (cover == null)
            {
                cachedCovers.RemoveAt(i);
                continue;
            }

            if (!cover.isActiveAndEnabled)
                continue;

            if (ShouldIgnoreCoverBecausePlayerUsesIt(playerStatus, cover))
                continue;

            if (takeCoverRule.requireAvailableCover && !cover.IsAvailableFor(gameObject))
                continue;

            bool foundPose = cover.TryGetCoverPose(
                transform.position,
                takeCoverRule.enemyRadius,
                out Vector3 candidatePosition,
                out Quaternion ignoredRotation
            );

            if (!foundPose)
                continue;

            float distanceFromEnemy = Vector3.Distance(transform.position, candidatePosition);

            float searchRadius = searchRadiusOverride >= 0f
                ? searchRadiusOverride
                : takeCoverRule.searchRadius;

            if (searchRadius > 0f &&
                distanceFromEnemy > searchRadius)
            {
                continue;
            }

            if (distanceFromEnemy < takeCoverRule.minDistanceFromEnemy)
                continue;

            float distanceFromPlayer = Vector3.Distance(playerPosition, candidatePosition);

            if (distanceFromPlayer < takeCoverRule.minDistanceFromPlayer)
                continue;

            if (takeCoverRule.sampleCoverPositionOnNavMesh)
            {
                if (!TrySampleNavMeshPosition(candidatePosition, out candidatePosition))
                    continue;
            }

            if (takeCoverRule.requireNavMeshReachable)
            {
                if (!IsNavMeshReachable(candidatePosition))
                    continue;
            }

            float score = distanceFromEnemy;

            if (score < bestScore)
            {
                bestScore = score;
                bestCover = cover;
                bestPosition = candidatePosition;
            }
        }

        if (bestCover == null)
            return false;

        coverPosition = bestPosition;

        if (takeCoverRule.debugLogSelectedCover)
        {
            Debug.Log(
                $"[AR_TacticalBrain] {name} selected cover: {bestCover.name}, position={coverPosition}",
                this
            );
        }

        return true;
    }

    private bool TryUseCurrentCoverPositionIfAlreadyInCover(out Vector3 coverPosition)
    {
        coverPosition = hasTacticalMoveTarget
            ? tacticalMovePosition
            : transform.position;

        if (takeCoverRule == null || !takeCoverRule.keepCurrentCoverWhenAlreadyInCover)
            return false;

        if (enemyStatus == null)
            enemyStatus = GetComponent<EnemyStatus>();

        if (enemyStatus == null || !enemyStatus.IsInCover)
            return false;

        suppressNextSquadTacticalMoveOffset = true;
        return true;
    }

    private bool ShouldIgnoreCoverBecausePlayerUsesIt(
        PlayerStatus playerStatus,
        CoverTrigger cover)
    {
        if (takeCoverRule == null || !takeCoverRule.ignorePlayerCurrentCover)
            return false;

        if (playerStatus == null || !playerStatus.IsInCover)
            return false;

        return cover != null && cover == playerStatus.CurrentCover;
    }

    private void RefreshCoverCache(bool force)
    {
        if (!force && Time.time < nextCoverRefreshTime)
            return;

        float refreshInterval = takeCoverRule != null
            ? Mathf.Max(0.1f, takeCoverRule.coverRefreshInterval)
            : 2f;

        nextCoverRefreshTime = Time.time + refreshInterval;

        cachedCovers.Clear();

#if UNITY_2023_1_OR_NEWER
        CoverTrigger[] covers = FindObjectsByType<CoverTrigger>(FindObjectsSortMode.None);
#else
        CoverTrigger[] covers = FindObjectsOfType<CoverTrigger>();
#endif

        if (covers == null)
            return;

        for (int i = 0; i < covers.Length; i++)
        {
            CoverTrigger cover = covers[i];

            if (cover == null)
                continue;

            if (!cover.isActiveAndEnabled)
                continue;

            cachedCovers.Add(cover);
        }
    }

    private bool TrySampleNavMeshPosition(Vector3 inputPosition, out Vector3 sampledPosition)
    {
        sampledPosition = inputPosition;

        float sampleRadius = takeCoverRule != null
            ? Mathf.Max(0f, takeCoverRule.navMeshSampleRadius)
            : 1.5f;

        return TrySampleNavMeshPosition(inputPosition, sampleRadius, out sampledPosition);
    }

    private bool TrySampleNavMeshPosition(
        Vector3 inputPosition,
        float sampleRadius,
        out Vector3 sampledPosition)
    {
        sampledPosition = inputPosition;

        sampleRadius = Mathf.Max(0f, sampleRadius);

        if (sampleRadius <= 0f)
            return true;

        int areaMask = navMeshAgent != null
            ? navMeshAgent.areaMask
            : NavMesh.AllAreas;

        if (NavMesh.SamplePosition(inputPosition, out NavMeshHit hit, sampleRadius, areaMask))
        {
            sampledPosition = hit.position;
            return true;
        }

        return false;
    }

    private bool IsNavMeshReachable(Vector3 destination)
    {
        int areaMask = navMeshAgent != null
            ? navMeshAgent.areaMask
            : NavMesh.AllAreas;

        NavMeshPath path = new NavMeshPath();

        if (!NavMesh.CalculatePath(transform.position, destination, areaMask, path))
            return false;

        return path.status == NavMeshPathStatus.PathComplete;
    }

    public void SetTacticalMoveTarget(Vector3 worldPosition, TacticalMoveReason reason)
    {
        if (autoCreateTacticalMovePoint && tacticalMovePoint == null)
            CreateOrRegisterTacticalMovePoint();

        if (tacticalMovePoint == null)
        {
            ClearTacticalTarget();
            return;
        }

        if (suppressNextSquadTacticalMoveOffset)
        {
            suppressNextSquadTacticalMoveOffset = false;
        }
        else
        {
            worldPosition = ApplySquadTacticalMoveOffset(worldPosition, reason);
        }

        tacticalMovePoint.position = worldPosition;
        tacticalMovePosition = worldPosition;
        hasTacticalMoveTarget = true;
        currentReason = reason;
        activeEscapeWaitEndTime = -1f;
        combatStrafeWaitEndTime = -1f;
        lastKnownPatrolWaitEndTime = -1f;
        lastTacticalTargetUpdateTime = Time.time;
        SetEnemyCoveringTeammateStatus(reason == TacticalMoveReason.CoverTeammate);
        SetEnemyEscapingFromGrenadeStatus(reason == TacticalMoveReason.EscapeGrenade);
        SetEnemyFollowingShieldStatus(reason == TacticalMoveReason.FollowShield);
        SetEnemyBackingAwayStatus(reason == TacticalMoveReason.BackAway);
        SetEnemyCombatStrafingStatus(reason == TacticalMoveReason.CombatStrafe);
        SetEnemyGoingToLKPStatus(reason == TacticalMoveReason.SearchLastKnownPosition);
        SetEnemyPatrolStatus(reason == TacticalMoveReason.PatrolLastKnownPosition);

        UploadBlackboardValues();
    }

    private Vector3 ApplySquadTacticalMoveOffset(Vector3 worldPosition, TacticalMoveReason reason)
    {
        if (!ShouldApplySquadTacticalMoveOffset(reason))
            return worldPosition;

        if (squadMember == null)
            squadMember = GetComponent<SquadMember>();

        if (squadMember == null || squadMember.SquadManager == null)
            return worldPosition;

        List<SquadMember> members = squadMember.SquadManager.Members;

        if (members == null || members.Count <= 1)
            return worldPosition;

        if (!TryGetAliveSquadIndex(members, out int aliveIndex))
            return worldPosition;

        if (aliveIndex <= 0)
            return worldPosition;

        float spacing = Mathf.Max(0f, squadTacticalMoveOffsetSpacing);

        if (spacing <= 0f)
            return worldPosition;

        int layer = (aliveIndex + 1) / 2;
        float side = aliveIndex % 2 == 1 ? -1f : 1f;
        Vector3 offsetDirection = GetSquadMoveOffsetDirection(worldPosition);
        Vector3 offsetPosition = worldPosition + offsetDirection * side * spacing * layer;

        if (!TrySampleNavMeshPosition(
                offsetPosition,
                squadTacticalMoveOffsetNavMeshSampleRadius,
                out offsetPosition))
        {
            return worldPosition;
        }

        if (!IsNavMeshReachable(offsetPosition))
            return worldPosition;

        return offsetPosition;
    }

    private bool ShouldApplySquadTacticalMoveOffset(TacticalMoveReason reason)
    {
        if (!applySquadTacticalMoveOffset)
            return false;

        if (reason == TacticalMoveReason.None)
            return false;

        if (reason == TacticalMoveReason.EscapeGrenade)
            return false;

        if (reason == TacticalMoveReason.FollowShield)
            return false;

        if (reason == TacticalMoveReason.Flank)
            return applySquadTacticalMoveOffsetToFlank;

        return true;
    }

    private bool TryGetAliveSquadIndex(List<SquadMember> members, out int aliveIndex)
    {
        aliveIndex = 0;

        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (member == null || !member.IsAlive)
                continue;

            if (member == squadMember)
                return true;

            aliveIndex++;
        }

        aliveIndex = -1;
        return false;
    }

    private Vector3 GetSquadMoveOffsetDirection(Vector3 worldPosition)
    {
        Vector3 moveDirection = worldPosition - transform.position;
        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude > 0.01f)
            return Vector3.Cross(Vector3.up, moveDirection.normalized).normalized;

        Vector3 right = transform.right;
        right.y = 0f;

        if (right.sqrMagnitude > 0.01f)
            return right.normalized;

        return Vector3.right;
    }

    public void ClearTacticalTarget()
    {
        currentReason = TacticalMoveReason.None;
        lastTacticalTargetUpdateTime = -999f;
        activeEscapeWaitEndTime = -1f;
        combatStrafeWaitEndTime = -1f;
        lastKnownPatrolWaitEndTime = -1f;
        suppressNextSquadTacticalMoveOffset = false;
        SetEnemyCoveringTeammateStatus(false);
        SetEnemyEscapingFromGrenadeStatus(false);
        SetEnemyFollowingShieldStatus(false);
        SetEnemyBackingAwayStatus(false);
        SetEnemyCombatStrafingStatus(false);
        SetEnemyGoingToLKPStatus(false);
        SetEnemyPatrolStatus(false);
        ClearFollowShieldTarget();

        ApplyBaseTacticalMovePointState();

        UploadBlackboardValues();
    }

    private void ApplyBaseTacticalMovePointState()
    {
        hasTacticalMoveTarget = false;

        if (ShouldKeepBaseTacticalMovePoint() || movePointToEnemyPositionWhenNoTarget)
            SetMovePointToEnemyPosition();
    }

    private bool ShouldKeepBaseTacticalMovePoint()
    {
        PlayerStatus playerStatus = PlayerStatus.Instance;

        if (playerStatus == null)
            return false;

        if (enemySensor == null)
            enemySensor = GetComponent<EnemySensor>();

        if (enemySensor == null)
            return false;

        enemySensor.RefreshSensor();
        return enemySensor.HasLastKnownPosition || enemySensor.TargetLocked;
    }

    private void SetMovePointToEnemyPosition()
    {
        Vector3 enemyPosition = transform.position;

        if (sampleEnemyPositionOnNavMesh)
        {
            int areaMask = navMeshAgent != null
                ? navMeshAgent.areaMask
                : NavMesh.AllAreas;

            float sampleRadius = Mathf.Max(0f, enemyPositionNavMeshSampleRadius);

            if (sampleRadius > 0f &&
                NavMesh.SamplePosition(enemyPosition, out NavMeshHit hit, sampleRadius, areaMask))
            {
                enemyPosition = hit.position;
            }
        }

        tacticalMovePosition = enemyPosition;

        if (tacticalMovePoint != null)
            tacticalMovePoint.position = enemyPosition;
    }

    private void UploadBlackboardValues()
    {
        if (!uploadToBlackboard)
            return;

        if (behaviorAgent == null)
            return;

        if (tacticalMovePoint != null)
            behaviorAgent.SetVariableValue(tacticalMovePointVariableName, tacticalMovePoint);

        behaviorAgent.SetVariableValue(tacticalMovePositionVariableName, tacticalMovePosition);
        behaviorAgent.SetVariableValue(hasTacticalMoveTargetVariableName, hasTacticalMoveTarget);

        if (hasTacticalMoveTargetVariableName != "HasTacticalMovePoint")
            behaviorAgent.SetVariableValue("HasTacticalMovePoint", hasTacticalMoveTarget);

        if (hasTacticalMoveTargetVariableName != "HasTacticalMoveTarget")
            behaviorAgent.SetVariableValue("HasTacticalMoveTarget", hasTacticalMoveTarget);

        behaviorAgent.SetVariableValue(tacticalMoveReasonVariableName, currentReason.ToString());

        UploadCombatRangeBlackboardValues();
    }

    private void UploadCombatRangeBlackboardValues()
    {
        if (!uploadToBlackboard || !uploadCombatRangeSettingsToBlackboard)
            return;

        if (uploadCombatRangeSettingsOnlyOnce && hasUploadedCombatRangeSettings)
            return;

        if (behaviorAgent == null)
            return;

        behaviorAgent.SetVariableValue(attackRangeVariableName, Mathf.Max(0f, attackRange));
        behaviorAgent.SetVariableValue(tooCloseRangeVariableName, Mathf.Max(0f, tooCloseRange));
        behaviorAgent.SetVariableValue(preferredRangeVariableName, ResolvePreferredRange());
        behaviorAgent.SetVariableValue(chaseRangeVariableName, Mathf.Max(0f, chaseRange));
        behaviorAgent.SetVariableValue(stopRangeVariableName, Mathf.Max(0f, stopRange));
        behaviorAgent.SetVariableValue(stopToleranceVariableName, ResolveStopTolerance());
        behaviorAgent.SetVariableValue(safeRangeVariableName, Mathf.Max(0f, safeRange));
        behaviorAgent.SetVariableValue(backAwayStepDistanceVariableName, Mathf.Max(0f, backAwayStepDistance));
        hasUploadedCombatRangeSettings = true;
    }

    private void UploadPlayerStatusBlackboardValues()
    {
        if (!uploadToBlackboard || !uploadPlayerStatusToBlackboard)
            return;

        if (behaviorAgent == null)
            return;

        PlayerStatus playerStatus = PlayerStatus.Instance;
        bool isPlayerInCover = playerStatus != null && playerStatus.IsInCover;

        behaviorAgent.SetVariableValue(isPlayerInCoverVariableName, isPlayerInCover);

        if (playerStatus != null)
        {
            behaviorAgent.SetVariableValue(playerStatusVariableName, playerStatus);
            behaviorAgent.SetVariableValue(playerObjectVariableName, playerStatus.gameObject);
        }
    }

    private void OnDestroy()
    {
        TacticalMovePointManager manager = TacticalMovePointManager.Instance;

        if (manager != null)
            manager.UnregisterPoint(transform, destroyPointOnDestroy);
    }
}
