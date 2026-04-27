using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public enum EnemyRole
    {
        Melee,
        Ranged,
        Boss = 3
    }

    private enum PlannedAction
    {
        Patrol,
        Regroup,
        StageAssault,
        RoleAttack,
        Search
    }

    private struct WorldState
    {
        public bool hasTarget;
        public bool hasLiveTarget;
        public bool isCohesive;
        public bool assaultReady;
        public bool inRoleRange;
        public float distanceToSquad;
        public float distanceToTarget;
        public Vector3 squadCenter;
        public Vector3 targetPosition;
        public Vector3 stagingPosition;
    }

    [Header("Role")]
    public EnemyRole role = EnemyRole.Boss;
    public bool applyRoleDefaultsOnStart = true;

    [Header("Squad")]
    public string squadId = "BossSquad";
    public float cohesionRadius = 5.5f;
    public float stagingRadius = 8f;
    public float assaultSyncDelay = 1.75f;
    public int minimumReadyMembers = 2;

    [Header("Senses")]
    public float sightRange = 16f;
    public float fieldOfView = 110f;
    public float forgetTargetAfter = 5f;

    [Header("Combat")]
    public float attackRange = 2f;
    public float preferredRange = 1.8f;
    public float maxRoleRange = 2.8f;
    public float attackCooldown = 1.2f;
    public float strafeSpeedMultiplier = 0.65f;

    [Header("Patrol")]
    public Transform[] patrolPoints;
    public float patrolWaitTime = 1.75f;

    private float lastAttackTime;
    private float patrolWaitTimer;
    private int patrolIndex;
    private PlannedAction currentAction;

    private Transform player;
    private NavMeshAgent agent;
    private SquadMemory squad;

    private static readonly Dictionary<string, SquadMemory> SquadLookup = new Dictionary<string, SquadMemory>();

    private sealed class SquadMemory
    {
        private readonly List<EnemyAI> members = new List<EnemyAI>();
        private readonly Dictionary<EnemyAI, bool> readyByMember = new Dictionary<EnemyAI, bool>();

        public readonly string SquadId;
        public Transform SharedTarget;
        public Vector3 LastKnownTargetPos;
        public float LastSeenTime = float.NegativeInfinity;
        public float AssaultStartTime = float.NegativeInfinity;

        public SquadMemory(string squadId)
        {
            SquadId = squadId;
        }

        public int MemberCount => members.Count;

        public void Register(EnemyAI enemy)
        {
            if (enemy == null || members.Contains(enemy))
            {
                return;
            }

            members.Add(enemy);
            readyByMember[enemy] = false;
        }

        public void Unregister(EnemyAI enemy)
        {
            if (enemy == null)
            {
                return;
            }

            members.Remove(enemy);
            readyByMember.Remove(enemy);
        }

        public bool IsEmpty()
        {
            return members.Count == 0;
        }

        public int GetSlotIndex(EnemyAI enemy)
        {
            int index = members.IndexOf(enemy);
            return index < 0 ? 0 : index;
        }

        public Vector3 GetSquadCenter()
        {
            if (members.Count == 0)
            {
                return Vector3.zero;
            }

            Vector3 sum = Vector3.zero;
            int validMembers = 0;

            for (int i = 0; i < members.Count; i++)
            {
                EnemyAI member = members[i];
                if (member == null)
                {
                    continue;
                }

                sum += member.transform.position;
                validMembers++;
            }

            if (validMembers == 0)
            {
                return Vector3.zero;
            }

            return sum / validMembers;
        }

        public void MarkReady(EnemyAI enemy, bool isReady)
        {
            if (enemy == null)
            {
                return;
            }

            readyByMember[enemy] = isReady;
        }

        public int CountReadyMembers()
        {
            int ready = 0;
            foreach (KeyValuePair<EnemyAI, bool> kvp in readyByMember)
            {
                if (kvp.Key != null && kvp.Value)
                {
                    ready++;
                }
            }

            return ready;
        }

        public void ReportTarget(Transform target, Vector3 targetPosition, float now)
        {
            SharedTarget = target;
            LastKnownTargetPos = targetPosition;
            LastSeenTime = now;

            if (float.IsNegativeInfinity(AssaultStartTime))
            {
                AssaultStartTime = now;
                ResetReadiness();
            }
        }

        public bool HasKnownTarget(float now, float forgetAfter, out bool hasLiveTransform, out Vector3 knownPosition)
        {
            bool withinMemory = now - LastSeenTime <= forgetAfter;
            hasLiveTransform = SharedTarget != null;

            if (hasLiveTransform)
            {
                knownPosition = SharedTarget.position;
                LastKnownTargetPos = knownPosition;
                return true;
            }

            if (withinMemory)
            {
                knownPosition = LastKnownTargetPos;
                return true;
            }

            knownPosition = Vector3.zero;
            return false;
        }

        public bool IsAssaultReady(float now, float syncDelay, int minimumReady)
        {
            int minReadyClamped = Mathf.Clamp(minimumReady, 1, Mathf.Max(1, MemberCount));
            bool enoughMembersReady = CountReadyMembers() >= minReadyClamped;
            bool delayElapsed = now - AssaultStartTime >= syncDelay;

            return enoughMembersReady || delayElapsed;
        }

        public void ResetTargetState()
        {
            SharedTarget = null;
            LastSeenTime = float.NegativeInfinity;
            AssaultStartTime = float.NegativeInfinity;
            ResetReadiness();
        }

        public void ResetReadiness()
        {
            List<EnemyAI> keys = new List<EnemyAI>(readyByMember.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                readyByMember[keys[i]] = false;
            }
        }
    }

    private void OnEnable()
    {
        RegisterToSquad();
    }

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (applyRoleDefaultsOnStart)
        {
            ApplyRoleDefaults();
        }

        ResolvePlayer();
        RegisterToSquad();
        SetRandomPatrolStart();
    }

    private void OnDisable()
    {
        if (squad == null)
        {
            return;
        }

        squad.Unregister(this);
        if (squad.IsEmpty())
        {
            SquadLookup.Remove(squad.SquadId);
        }
    }

    private void Update()
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        if (player == null)
        {
            ResolvePlayer();
        }

        if (squad == null)
        {
            RegisterToSquad();
            if (squad == null)
            {
                return;
            }
        }

        float now = Time.time;
        ScanForPlayer(now);

        WorldState worldState = BuildWorldState(now);
        currentAction = Plan(worldState);
        ExecutePlan(currentAction, worldState, now);
    }

    private void RegisterToSquad()
    {
        if (string.IsNullOrWhiteSpace(squadId))
        {
            squadId = "Squad_Default";
        }

        if (!SquadLookup.TryGetValue(squadId, out squad))
        {
            squad = new SquadMemory(squadId);
            SquadLookup[squadId] = squad;
        }

        squad.Register(this);
    }

    private void ResolvePlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        player = playerObj != null ? playerObj.transform : null;
    }

    private void ScanForPlayer(float now)
    {
        if (player == null)
        {
            return;
        }

        if (CanSeePlayer())
        {
            squad.ReportTarget(player, player.position, now);
        }
    }

    private WorldState BuildWorldState(float now)
    {
        WorldState world = new WorldState();
        world.squadCenter = squad.GetSquadCenter();
        world.distanceToSquad = Vector3.Distance(transform.position, world.squadCenter);
        world.isCohesive = world.distanceToSquad <= cohesionRadius;

        world.hasTarget = squad.HasKnownTarget(now, forgetTargetAfter, out world.hasLiveTarget, out world.targetPosition);
        if (!world.hasTarget)
        {
            squad.ResetTargetState();
            world.assaultReady = false;
            world.distanceToTarget = float.PositiveInfinity;
            world.inRoleRange = false;
            world.stagingPosition = transform.position;
            return world;
        }

        world.distanceToTarget = Vector3.Distance(transform.position, world.targetPosition);
        world.stagingPosition = GetFormationPoint(world.targetPosition, stagingRadius);
        world.assaultReady = squad.IsAssaultReady(now, assaultSyncDelay, minimumReadyMembers);
        world.inRoleRange = world.distanceToTarget <= maxRoleRange && world.distanceToTarget >= preferredRange * 0.65f;

        return world;
    }

    private PlannedAction Plan(WorldState world)
    {
        List<(PlannedAction action, float cost)> candidates = new List<(PlannedAction, float)>();

        if (!world.hasTarget)
        {
            candidates.Add((PlannedAction.Patrol, 1f));
            candidates.Add((PlannedAction.Search, 2f));
            return SelectLowestCost(candidates);
        }

        if (!world.isCohesive)
        {
            candidates.Add((PlannedAction.Regroup, 0.75f + world.distanceToSquad));
        }

        if (world.isCohesive && !world.assaultReady)
        {
            float stageCost = 0.5f + Vector3.Distance(transform.position, world.stagingPosition);
            candidates.Add((PlannedAction.StageAssault, stageCost));
        }

        if (world.assaultReady)
        {
            float roleAttackCost = world.inRoleRange ? 0.25f : 1.25f;
            candidates.Add((PlannedAction.RoleAttack, roleAttackCost));
        }

        candidates.Add((PlannedAction.Search, 9f));
        return SelectLowestCost(candidates);
    }

    private PlannedAction SelectLowestCost(List<(PlannedAction action, float cost)> candidates)
    {
        PlannedAction selected = PlannedAction.Search;
        float bestCost = float.PositiveInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].cost < bestCost)
            {
                selected = candidates[i].action;
                bestCost = candidates[i].cost;
            }
        }

        return selected;
    }

    private void ExecutePlan(PlannedAction action, WorldState world, float now)
    {
        switch (action)
        {
            case PlannedAction.Patrol:
                squad.MarkReady(this, false);
                ExecutePatrol();
                break;
            case PlannedAction.Regroup:
                squad.MarkReady(this, false);
                MoveTo(world.squadCenter, runSpeedMultiplier: 1f);
                break;
            case PlannedAction.StageAssault:
                ExecuteStaging(world);
                break;
            case PlannedAction.RoleAttack:
                squad.MarkReady(this, true);
                ExecuteRoleAttack(world.targetPosition, now);
                break;
            default:
                squad.MarkReady(this, false);
                MoveTo(world.targetPosition, runSpeedMultiplier: 1f);
                break;
        }
    }

    private void ExecutePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            agent.ResetPath();
            return;
        }

        Transform patrolTarget = patrolPoints[patrolIndex];
        MoveTo(patrolTarget.position, runSpeedMultiplier: 0.9f);

        if (!agent.pathPending && agent.remainingDistance <= 0.6f)
        {
            patrolWaitTimer += Time.deltaTime;
            if (patrolWaitTimer >= patrolWaitTime)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                patrolWaitTimer = 0f;
            }
        }
    }

    private void ExecuteStaging(WorldState world)
    {
        float stageDistance = Vector3.Distance(transform.position, world.stagingPosition);
        bool stageReady = stageDistance <= 1.15f;
        squad.MarkReady(this, stageReady);
        MoveTo(world.stagingPosition, runSpeedMultiplier: 1f);
    }

    private void ExecuteRoleAttack(Vector3 targetPosition, float now)
    {
        float distance = Vector3.Distance(transform.position, targetPosition);
        FaceTowards(targetPosition);

        switch (role)
        {
            case EnemyRole.Boss:
                ExecuteBossRole(targetPosition, distance, now);
                break;

            case EnemyRole.Ranged:
                ExecuteRangedRole(targetPosition, distance, now);
                break;

            default:
                ExecuteMeleeRole(targetPosition, distance, now);
                break;
        }
    }

    private void ExecuteMeleeRole(Vector3 targetPosition, float distance, float now)
    {
        if (distance > preferredRange)
        {
            MoveTo(targetPosition, runSpeedMultiplier: 1.2f);
        }
        else
        {
            agent.ResetPath();
            TryAttack(now);
        }
    }

    private void ExecuteBossRole(Vector3 targetPosition, float distance, float now)
    {
        Vector3 toTarget = (targetPosition - transform.position).normalized;
        int directionSign = squad.GetSlotIndex(this) % 2 == 0 ? 1 : -1;
        Vector3 lateral = Vector3.Cross(Vector3.up, toTarget).normalized * directionSign * 1.25f;
        Vector3 holdPoint = targetPosition - toTarget * preferredRange + lateral;

        if (distance > preferredRange + 0.4f)
        {
            MoveTo(holdPoint, runSpeedMultiplier: 0.85f);
        }
        else
        {
            agent.ResetPath();
            TryAttack(now);
        }
    }

    private void ExecuteRangedRole(Vector3 targetPosition, float distance, float now)
    {
        if (distance < preferredRange)
        {
            Vector3 retreatDir = (transform.position - targetPosition).normalized;
            Vector3 retreatPoint = transform.position + retreatDir * 4f;
            MoveTo(retreatPoint, runSpeedMultiplier: 1f);
            return;
        }

        if (distance > maxRoleRange)
        {
            MoveTo(targetPosition, runSpeedMultiplier: 1.1f);
            return;
        }

        Vector3 toTarget = (targetPosition - transform.position).normalized;
        Vector3 strafeDir = Vector3.Cross(Vector3.up, toTarget).normalized;
        int directionSign = squad.GetSlotIndex(this) % 2 == 0 ? 1 : -1;
        Vector3 strafePoint = transform.position + strafeDir * directionSign * 2.5f;
        MoveTo(strafePoint, strafeSpeedMultiplier);
        TryAttack(now);
    }

    private void TryAttack(float now)
    {
        if (now < lastAttackTime + attackCooldown)
        {
            return;
        }

        lastAttackTime = now;
        Debug.Log($"{name} ({role}) attacks as squad {squadId}.", this);
    }

    private void MoveTo(Vector3 destination, float runSpeedMultiplier)
    {
        if (!agent.isOnNavMesh)
        {
            return;
        }

        float baseSpeed = GetBaseRoleSpeed();
        agent.speed = baseSpeed * runSpeedMultiplier;
        agent.stoppingDistance = Mathf.Max(0.5f, preferredRange * 0.85f);
        agent.SetDestination(destination);
    }

    private bool CanSeePlayer()
    {
        if (player == null)
        {
            return false;
        }

        Vector3 origin = transform.position + Vector3.up * 1.4f;
        Vector3 direction = player.position - origin;
        float distance = direction.magnitude;

        if (distance > sightRange)
        {
            return false;
        }

        float angle = Vector3.Angle(transform.forward, direction.normalized);
        if (angle > fieldOfView * 0.5f)
        {
            return false;
        }

        if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit, sightRange))
        {
            return false;
        }

        return hit.transform.CompareTag("Player");
    }

    private Vector3 GetFormationPoint(Vector3 targetPosition, float radius)
    {
        int slot = squad.GetSlotIndex(this);
        int count = Mathf.Max(1, squad.MemberCount);
        float angle = (360f / count) * slot;
        Vector3 ringOffset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
        return targetPosition + ringOffset;
    }

    private void FaceTowards(Vector3 worldPoint)
    {
        Vector3 direction = worldPoint - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 9f);
    }

    private void SetRandomPatrolStart()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            patrolIndex = 0;
            return;
        }

        patrolIndex = Random.Range(0, patrolPoints.Length);
    }

    public void ConfigureForSpawn(string newSquadId, EnemyRole newRole)
    {
        squadId = newSquadId;
        role = newRole;
        ApplyRoleDefaults();
        RegisterToSquad();
    }

    private float GetBaseRoleSpeed()
    {
        switch (role)
        {
            case EnemyRole.Boss:
                return 2.3f;
            case EnemyRole.Ranged:
                return 3.3f;
            default:
                return 3.0f;
        }
    }

    private void ApplyRoleDefaults()
    {
        switch (role)
        {
            case EnemyRole.Melee:
                attackRange = 2f;
                preferredRange = 1.8f;
                maxRoleRange = 3.2f;
                attackCooldown = 1f;
                break;

            case EnemyRole.Ranged:
                attackRange = 8f;
                preferredRange = 5.25f;
                maxRoleRange = 8.2f;
                attackCooldown = 1.4f;
                break;

            case EnemyRole.Boss:
                attackRange = 3f;
                preferredRange = 2.3f;
                maxRoleRange = 5f;
                attackCooldown = 1.5f;
                break;
        }

        if (agent != null)
        {
            agent.speed = GetBaseRoleSpeed();

            switch (role)
            {
                case EnemyRole.Melee:
                    agent.radius = 0.42f;
                    agent.avoidancePriority = 55;
                    break;
                case EnemyRole.Ranged:
                    agent.radius = 0.4f;
                    agent.avoidancePriority = 35;
                    break;
                case EnemyRole.Boss:
                    agent.radius = 0.7f;
                    agent.avoidancePriority = 10;
                    break;
            }
        }
    }
}
