using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Update Enemy Weapon Ammo",
    story: "[Self] updates enemy weapon ammo data",
    category: "Enemy/Combat",
    id: "b21a0f6764084ea4a47dd29db4bc8871"
)]
public partial class UpdateEnemyWeaponAmmoAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> WeaponObject;

    [SerializeReference] public BlackboardVariable<int> CurrentAmmoInMagazine;
    [SerializeReference] public BlackboardVariable<int> MagazineSize;
    [SerializeReference] public BlackboardVariable<int> CurrentReserveAmmo;

    [SerializeReference] public BlackboardVariable<bool> IsMagazineEmpty;
    [SerializeReference] public BlackboardVariable<bool> IsMagazineFull;
    [SerializeReference] public BlackboardVariable<bool> IsReloading;
    [SerializeReference] public BlackboardVariable<bool> CanShoot;
    [SerializeReference] public BlackboardVariable<bool> CanReload;
    [SerializeReference] public BlackboardVariable<bool> HasReserveAmmo;

    private EnemyWeaponSettings enemyWeaponSettings;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        enemyWeaponSettings = ResolveEnemyWeaponSettings();

        if (enemyWeaponSettings == null)
            return Status.Failure;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (enemyWeaponSettings == null)
        {
            enemyWeaponSettings = ResolveEnemyWeaponSettings();

            if (enemyWeaponSettings == null)
                return Status.Failure;
        }

        if (CurrentAmmoInMagazine != null)
            CurrentAmmoInMagazine.Value = enemyWeaponSettings.CurrentAmmoInMagazine;

        if (MagazineSize != null)
            MagazineSize.Value = enemyWeaponSettings.MagazineSize;

        if (CurrentReserveAmmo != null)
            CurrentReserveAmmo.Value = enemyWeaponSettings.CurrentReserveAmmo;

        if (IsMagazineEmpty != null)
            IsMagazineEmpty.Value = enemyWeaponSettings.IsMagazineEmpty;

        if (IsMagazineFull != null)
            IsMagazineFull.Value = enemyWeaponSettings.IsMagazineFull;

        if (IsReloading != null)
            IsReloading.Value = enemyWeaponSettings.IsReloading;

        if (CanShoot != null)
            CanShoot.Value = enemyWeaponSettings.CanShoot();

        if (CanReload != null)
            CanReload.Value = enemyWeaponSettings.CanReload();

        if (HasReserveAmmo != null)
            HasReserveAmmo.Value = enemyWeaponSettings.HasReserveAmmo;

        return Status.Success;
    }

    protected override void OnEnd()
    {
    }

    private EnemyWeaponSettings ResolveEnemyWeaponSettings()
    {
        if (WeaponObject != null && WeaponObject.Value != null)
        {
            EnemyWeaponSettings weaponSettings =
                WeaponObject.Value.GetComponentInChildren<EnemyWeaponSettings>(true);

            if (weaponSettings != null)
                return weaponSettings;
        }

        return Self.Value.GetComponentInChildren<EnemyWeaponSettings>(true);
    }
}