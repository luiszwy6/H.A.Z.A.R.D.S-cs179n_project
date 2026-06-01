using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class ZombieAnimatorDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("Speed")]
    [SerializeField] private float movingThreshold = 0.1f;
    [SerializeField] private float dampTime = 0.1f;

    [Header("Animator Parameter Names")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string deadTrigger = "Dead";

    private int speedHash;
    private int attackHash;
    private int deadHash;

    private EnemyHealth enemyHealth;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        enemyHealth = GetComponent<EnemyHealth>();

        speedHash  = Animator.StringToHash(speedParam);
        attackHash = Animator.StringToHash(attackTrigger);
        deadHash   = Animator.StringToHash(deadTrigger);

        // Root motion drives position; disable agent's built-in position update.
        if (agent != null)
            agent.updatePosition = false;
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
            enemyHealth.onDeath.AddListener(TriggerDead);
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
            enemyHealth.onDeath.RemoveListener(TriggerDead);
    }

    private void Update()
    {
        if (animator == null || agent == null)
            return;

        float speed = agent.enabled && agent.isOnNavMesh
            ? agent.velocity.magnitude
            : 0f;

        float normalizedSpeed = speed > movingThreshold ? 1f : 0f;
        animator.SetFloat(speedHash, normalizedSpeed, dampTime, Time.deltaTime);
    }

    // Called by Unity when applyRootMotion = true on the Animator.
    private void OnAnimatorMove()
    {
        if (animator == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        Vector3 delta = animator.deltaPosition;
        delta.y = 0f;
        agent.Move(delta);

        // Keep transform in sync with the agent's internal position.
        transform.position = agent.nextPosition;
    }

    public void TriggerAttack()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(attackHash);
        animator.SetTrigger(attackHash);
    }

    public void TriggerDead()
    {
        if (animator == null)
            return;

        animator.SetTrigger(deadHash);
    }
}
