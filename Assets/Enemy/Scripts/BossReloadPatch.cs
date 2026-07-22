using UnityEngine;

/// <summary>
/// Ensures the boss always has a tactical move target while reloading,
/// so the behavior graph's reload branch (which requires HasTacticalMovePoint)
/// can activate and play the reload animation.
///
/// Add to the boss GameObject alongside AR_TacticalBrain.
/// Does NOT modify AR_TacticalBrain.cs.
/// </summary>
[DisallowMultipleComponent]
public class BossReloadPatch : MonoBehaviour
{
    [SerializeField] private AR_TacticalBrain tacticalBrain;
    [SerializeField] private EnemyWeaponSettings weaponSettings;

    private void Awake()
    {
        if (tacticalBrain  == null) tacticalBrain  = GetComponent<AR_TacticalBrain>();
        if (weaponSettings == null) weaponSettings = GetComponentInChildren<EnemyWeaponSettings>(true);
    }

    // LateUpdate runs after AR_TacticalBrain.Update(), so we can restore any
    // target that was cleared this frame before the behavior graph evaluates.
    private void LateUpdate()
    {
        if (tacticalBrain == null || weaponSettings == null) return;
        if (!weaponSettings.IsReloading) return;
        if (tacticalBrain.HasTacticalMoveTarget) return;

        // Pin the tactical point to the boss's current position so the
        // behavior graph sees HasTacticalMovePoint = true and can run the
        // reload node (boss stands in place while reloading, animation plays).
        tacticalBrain.SetTacticalMoveTarget(
            transform.position,
            AR_TacticalBrain.TacticalMoveReason.Reposition);
    }
}
