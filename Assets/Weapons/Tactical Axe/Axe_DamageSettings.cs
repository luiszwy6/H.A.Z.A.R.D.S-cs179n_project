using UnityEngine;

[DisallowMultipleComponent]
public class Axe_DamageSettings : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float baseDamage = 20f;

    [Range(0, 2)]
    [SerializeField] private int armorPierceLevel = 0;

    [Header("Debug")]
    [SerializeField] private bool logDamagePayload = false;

    public float BaseDamage => Mathf.Max(0f, baseDamage);
    public int ArmorPierceLevel => Mathf.Clamp(armorPierceLevel, 0, 2);
    public bool LogDamagePayload => logDamagePayload;
}
