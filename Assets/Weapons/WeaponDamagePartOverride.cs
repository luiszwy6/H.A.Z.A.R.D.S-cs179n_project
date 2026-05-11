using System;
using UnityEngine;

public enum EnemyBodyPart
{
    Generic,
    Head,
    Body,
    Limb
}

[Serializable]
public class WeaponDamagePartOverride
{
    [Header("Target Part")]
    public EnemyBodyPart bodyPart = EnemyBodyPart.Generic;

    [Header("Damage Multiplier Override")]
    public bool overrideDamageMultiplier = false;
    [Min(0f)] public float damageMultiplier = 1f;

    [Header("Armor Pierce Override")]
    public bool overrideArmorPierceLevel = false;

    [Range(0, 2)]
    public int armorPierceLevel = 0;
}