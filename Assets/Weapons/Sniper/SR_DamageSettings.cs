using UnityEngine;

[DisallowMultipleComponent]
public class SR_DamageSetting : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float Base_dmg = 90f;

    [Range(0, 2)]
    [SerializeField] private int ArmoPierLevel = 2;

    [Header("Part Overrides")]
    [SerializeField] private WeaponDamagePartOverride[] partOverrides;

    [Header("Knockback")]
    [SerializeField] private float KnockbackValue = 0f;

    [Header("Penetration")]
    [Min(0)]
    [SerializeField] private int PenetrationLevel = 1;

    [Range(0f, 100f)]
    [SerializeField] private float penetrationDamageDecayPercent = 25f;

    [Header("Debug")]
    [SerializeField] private bool logDamagePayload = false;

    public float BaseDamage => Mathf.Max(0f, Base_dmg);
    public int ArmorPierceLevel => Mathf.Clamp(ArmoPierLevel, 0, 2);
    public float Knockback => Mathf.Max(0f, KnockbackValue);
    public int PenetrationCount => Mathf.Max(0, PenetrationLevel);
    public float PenetrationDamageDecayPercent => Mathf.Clamp(penetrationDamageDecayPercent, 0f, 100f);
    public WeaponDamagePartOverride[] PartOverrides => partOverrides;

    public void ApplyToProjectile(BulletProjectile projectile)
    {
        if (projectile == null)
            return;

        projectile.SetDamagePayload(
            BaseDamage,
            ArmorPierceLevel,
            Knockback,
            PenetrationCount,
            PenetrationDamageDecayPercent,
            PartOverrides
        );

        if (logDamagePayload)
        {
            Debug.Log(
                $"[SR_DamageSetting] Applied damage payload. Base_dmg={BaseDamage}, ArmoPierLevel={ArmorPierceLevel}, Knockback={Knockback}, PenetrationLevel={PenetrationCount}, Decay={PenetrationDamageDecayPercent}%, PartOverrides={(partOverrides != null ? partOverrides.Length : 0)}",
                this
            );
        }
    }
}