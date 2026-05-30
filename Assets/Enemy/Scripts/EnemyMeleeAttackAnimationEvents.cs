using UnityEngine;

[DisallowMultipleComponent]
public class EnemyMeleeAttackAnimationEvents : MonoBehaviour
{
    [SerializeField] private EnemyMeleeAttacker meleeAttacker;
    [SerializeField] private bool searchInChildren = true;
    [SerializeField] private bool searchInParents = true;

    private void Awake()
    {
        ResolveMeleeAttacker();
    }

    private void Reset()
    {
        ResolveMeleeAttacker();
    }

    public void EnableAxeHitbox()
    {
        EnemyMeleeAttacker attacker = ResolveMeleeAttacker();

        if (attacker != null)
            attacker.EnableAxeHitbox();
    }

    public void DisableAxeHitbox()
    {
        EnemyMeleeAttacker attacker = ResolveMeleeAttacker();

        if (attacker != null)
            attacker.DisableAxeHitbox();
    }

    public void EnableDamage()
    {
        EnableAxeHitbox();
    }

    public void DisableDamage()
    {
        DisableAxeHitbox();
    }

    public void OpenDamageWindowForDefaultDuration()
    {
        EnemyMeleeAttacker attacker = ResolveMeleeAttacker();

        if (attacker != null)
            attacker.OpenDamageWindowForDefaultDuration();
    }

    private EnemyMeleeAttacker ResolveMeleeAttacker()
    {
        if (meleeAttacker != null)
            return meleeAttacker;

        meleeAttacker = GetComponent<EnemyMeleeAttacker>();

        if (meleeAttacker != null)
            return meleeAttacker;

        if (searchInChildren)
            meleeAttacker = GetComponentInChildren<EnemyMeleeAttacker>(true);

        if (meleeAttacker != null)
            return meleeAttacker;

        if (searchInParents)
            meleeAttacker = GetComponentInParent<EnemyMeleeAttacker>();

        return meleeAttacker;
    }
}
