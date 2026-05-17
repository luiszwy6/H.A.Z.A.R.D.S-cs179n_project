using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class EnemyBackTrigger : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform enemyRoot;
    [SerializeField] private Transform facingRoot;
    [SerializeField] private EnemyStunReceiver stunReceiver;
    [SerializeField] private Collider triggerCollider;

    [Header("Rules")]
    [SerializeField] private bool requireBehindDirection = false;

    [Range(-1f, 1f)]
    [SerializeField] private float behindDotThreshold = -0.25f;

    [Header("Player Melee BackStab")]
    [SerializeField] private bool allowPlayerBackStab = true;

    [Header("Debug")]
    [SerializeField] private bool logPassThrough = false;
    [SerializeField] private bool logPlayerBackStabZone = false;

    public Transform EnemyRoot
    {
        get
        {
            ResolveReferences();
            return enemyRoot;
        }
    }

    public Transform FacingRoot
    {
        get
        {
            ResolveReferences();
            return facingRoot;
        }
    }

    public EnemyStunReceiver StunReceiver
    {
        get
        {
            ResolveReferences();
            return stunReceiver;
        }
    }

    public bool LogPassThrough => logPassThrough;
    public bool AllowPlayerBackStab => allowPlayerBackStab;

    private void Reset()
    {
        ResolveReferences();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void Awake()
    {
        ResolveReferences();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        ResolveReferences();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TrySetPlayerBackTrigger(other, true);
    }

    private void OnTriggerStay(Collider other)
    {
        TrySetPlayerBackTrigger(other, true);
    }

    private void OnTriggerExit(Collider other)
    {
        TrySetPlayerBackTrigger(other, false);
    }

    public bool TryGetStunReceiver(out EnemyStunReceiver receiver)
    {
        ResolveReferences();

        receiver = stunReceiver;
        return receiver != null;
    }

    public bool IsBehindPosition(Vector3 worldPosition)
    {
        ResolveReferences();

        if (!requireBehindDirection)
            return true;

        Transform face = facingRoot != null ? facingRoot : enemyRoot;

        if (face == null)
            return true;

        Vector3 forward = face.forward;
        forward.y = 0f;

        Vector3 toPoint = worldPosition - face.position;
        toPoint.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f || toPoint.sqrMagnitude <= 0.0001f)
            return false;

        forward.Normalize();
        toPoint.Normalize();

        float dot = Vector3.Dot(forward, toPoint);
        return dot <= behindDotThreshold;
    }

    private void TrySetPlayerBackTrigger(Collider other, bool inside)
    {
        if (!allowPlayerBackStab)
            return;

        if (other == null)
            return;

        PlayerMeleeAttack meleeAttack = other.GetComponentInParent<PlayerMeleeAttack>();

        if (meleeAttack == null)
            return;

        if (inside)
        {
            if (!IsBehindPosition(meleeAttack.transform.position))
                return;

            meleeAttack.SetCurrentEnemyBackTrigger(this);

            if (logPlayerBackStabZone)
                Debug.Log($"[EnemyBackTrigger] Player entered back trigger: {meleeAttack.name}", this);
        }
        else
        {
            meleeAttack.ClearCurrentEnemyBackTrigger(this);

            if (logPlayerBackStabZone)
                Debug.Log($"[EnemyBackTrigger] Player exited back trigger: {meleeAttack.name}", this);
        }
    }

    private void ResolveReferences()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (enemyRoot == null)
            enemyRoot = transform.root;

        if (facingRoot == null)
            facingRoot = enemyRoot;

        if (stunReceiver == null)
            stunReceiver = GetComponentInParent<EnemyStunReceiver>();
    }
}