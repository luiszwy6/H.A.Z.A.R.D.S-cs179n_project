using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public Transform[] patrolPoints;

    [Header("Ranges")]
    public float sightRange = 12f;
    public float attackRange = 2f;

    [Header("FOV")]
    public float fieldOfView = 90f;

    [Header("Patrol")]
    public float waitTime = 2f;
    private float waitTimer;

    [Header("Attack")]
    public float attackCooldown = 1.5f;
    private float lastAttackTime;

    private Transform player;
    private NavMeshAgent agent;
    private Animator animator;

    private enum State { Patrol, Chase, Attack }
    private State currentState;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        player = GameObject.FindGameObjectWithTag("Player").transform;

        currentState = State.Patrol;
        GoToRandomPoint();
    }

    void Update()
    {
        if (player == null || !agent.isOnNavMesh) return;

        float distance = Vector3.Distance(transform.position, player.position);
        bool canSeePlayer = CanSeePlayer();

        switch (currentState)
        {
            case State.Patrol:
                PatrolLogic(distance, canSeePlayer);
                break;

            case State.Chase:
                ChaseLogic(distance, canSeePlayer);
                break;

            case State.Attack:
                AttackLogic(distance);
                break;
        }

        UpdateAnimations();
    }

    void PatrolLogic(float distance, bool canSeePlayer)
    {
        if (canSeePlayer && distance <= sightRange)
        {
            currentState = State.Chase;
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            waitTimer += Time.deltaTime;

            if (waitTimer >= waitTime)
            {
                GoToRandomPoint();
                waitTimer = 0f;
            }
        }
    }

    void ChaseLogic(float distance, bool canSeePlayer)
    {
        if (!canSeePlayer || distance > sightRange * 1.2f)
        {
            currentState = State.Patrol;
            GoToRandomPoint();
            return;
        }

        if (distance <= attackRange)
        {
            currentState = State.Attack;
            return;
        }

        agent.SetDestination(player.position);
    }

    void AttackLogic(float distance)
    {
        agent.ResetPath();

        FacePlayer();

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            Attack();
            lastAttackTime = Time.time;
        }

        if (distance > attackRange)
        {
            currentState = State.Chase;
        }
    }

    void GoToRandomPoint()
    {
        if (patrolPoints.Length == 0) return;

        int randomIndex = Random.Range(0, patrolPoints.Length);
        agent.SetDestination(patrolPoints[randomIndex].position);
    }

    bool CanSeePlayer()
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 dirToPlayer = (player.position - origin).normalized;

        float angle = Vector3.Angle(transform.forward, dirToPlayer);
        if (angle > fieldOfView / 2f)
            return false;

        if (Physics.Raycast(origin, dirToPlayer, out RaycastHit hit, sightRange))
        {
            return hit.transform.CompareTag("Player");
        }

        return false;
    }

    void Attack()
    {
        Debug.Log("enemy attack");
    }

    void FacePlayer()
    {
        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0;
        transform.forward = dir;
    }

    void UpdateAnimations()
    {
        if (animator == null) return;

        bool isMoving = agent.velocity.magnitude > 0.1f;
        bool isAttacking = currentState == State.Attack;

        animator.SetBool("isMoving", isMoving && !isAttacking);
        animator.SetBool("isAttacking", isAttacking);
    }
}