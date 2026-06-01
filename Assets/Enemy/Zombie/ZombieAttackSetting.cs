using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class ZombieAttackSetting : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ZombieAnimatorDriver animatorDriver;
    [SerializeField] private NavMeshAgent agent;

    [Header("Attack Trigger")]
    [Min(0f)] [SerializeField] private float attackRange = 2f;
    [Min(0f)] [SerializeField] private float attackCooldown = 1.5f;

    [Header("Damage")]
    [Min(0f)] [SerializeField] private float damage = 25f;
    [Range(0, 2)] [SerializeField] private int armorPierceLevel = 0;
    [SerializeField] private bool hitPlayerOncePerSwing = true;

    [Header("Hitbox")]
    [SerializeField] private Collider attackHitbox;

    private float nextAttackTime;
    private bool hitboxOpen;
    private readonly HashSet<PlayerHealth> hitThisSwing = new HashSet<PlayerHealth>();

    private void Awake()
    {
        if (animatorDriver == null)
            animatorDriver = GetComponent<ZombieAnimatorDriver>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        SetupHitbox();
    }

    private void SetupHitbox()
    {
        if (attackHitbox == null)
            return;

        attackHitbox.enabled = false;
        attackHitbox.isTrigger = true;

        ZombieHitboxReporter reporter = attackHitbox.GetComponent<ZombieHitboxReporter>();

        if (reporter == null)
            reporter = attackHitbox.gameObject.AddComponent<ZombieHitboxReporter>();

        reporter.Setup(this);
    }

    private void Update()
    {
        if (Time.time < nextAttackTime)
            return;

        PlayerStatus playerStatus = PlayerStatus.Instance;

        if (playerStatus == null)
            return;

        float distance = Vector3.Distance(transform.position, playerStatus.transform.position);

        if (distance > attackRange)
            return;

        nextAttackTime = Time.time + attackCooldown;

        if (agent != null)
        {
            agent.isStopped = true;
            agent.updateRotation = false;
        }

        animatorDriver?.TriggerAttack();
    }

    // Called by Animator Event
    public void OnAttackWindowOpen()
    {
        hitThisSwing.Clear();
        hitboxOpen = true;

        if (attackHitbox != null)
            attackHitbox.enabled = true;
    }

    // Called by Animator Event
    public void OnAttackWindowClose()
    {
        hitboxOpen = false;

        if (attackHitbox != null)
            attackHitbox.enabled = false;

        if (agent != null)
        {
            agent.isStopped = false;
            agent.updateRotation = true;
        }
    }

    public void OnHitboxContact(Collider other)
    {
        if (!hitboxOpen)
            return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
            return;

        if (hitPlayerOncePerSwing && hitThisSwing.Contains(playerHealth))
            return;

        hitThisSwing.Add(playerHealth);

        bool damaged = false;

        if (TryBuildHit(other, out RaycastHit hit))
            damaged = playerHealth.TryApplyBulletDamage(hit, damage, armorPierceLevel, 0f, out _, out _);

        if (!damaged)
            playerHealth.TakeDamage(damage);
    }

    private bool TryBuildHit(Collider targetCollider, out RaycastHit hit)
    {
        hit = default;

        if (attackHitbox == null)
            return false;

        Vector3 source = attackHitbox.bounds.center;
        Vector3 target = targetCollider.bounds.center;
        Vector3 direction = target - source;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        direction.Normalize();

        float distance = Vector3.Distance(source, target) + 1f;
        Ray ray = new Ray(source - direction * 0.1f, direction);

        return targetCollider.Raycast(ray, out hit, distance);
    }
}
