using UnityEngine;

public class ShootLock : StateMachineBehaviour
{
    private PlayerWeaponSlots FindWeaponSlots(Animator animator)
    {
        PlayerWeaponSlots weaponSlots = animator.GetComponent<PlayerWeaponSlots>();

        if (weaponSlots != null)
            return weaponSlots;

        weaponSlots = animator.GetComponentInParent<PlayerWeaponSlots>();

        if (weaponSlots != null)
            return weaponSlots;

        return animator.transform.root.GetComponentInChildren<PlayerWeaponSlots>(true);
    }

    private ARShootSettings FindARShootSettings(Animator animator)
    {
        PlayerWeaponSlots weaponSlots = FindWeaponSlots(animator);

        if (weaponSlots != null && weaponSlots.CurrentARShootSettings != null)
            return weaponSlots.CurrentARShootSettings;

        ARShootSettings shoot = animator.GetComponent<ARShootSettings>();
        if (shoot != null)
            return shoot;

        shoot = animator.GetComponentInParent<ARShootSettings>();
        if (shoot != null)
            return shoot;

        shoot = animator.GetComponentInChildren<ARShootSettings>(true);
        if (shoot != null)
            return shoot;

        return animator.transform.root.GetComponentInChildren<ARShootSettings>(true);
    }

    private SG_ShootSettings FindSGShootSettings(Animator animator)
    {
        PlayerWeaponSlots weaponSlots = FindWeaponSlots(animator);

        if (weaponSlots != null && weaponSlots.CurrentSGShootSettings != null)
            return weaponSlots.CurrentSGShootSettings;

        SG_ShootSettings shoot = animator.GetComponent<SG_ShootSettings>();
        if (shoot != null)
            return shoot;

        shoot = animator.GetComponentInParent<SG_ShootSettings>();
        if (shoot != null)
            return shoot;

        shoot = animator.GetComponentInChildren<SG_ShootSettings>(true);
        if (shoot != null)
            return shoot;

        return animator.transform.root.GetComponentInChildren<SG_ShootSettings>(true);
    }

    private void SetShootLock(Animator animator, bool locked, int layerIndex, string eventName)
    {
        ARShootSettings arShoot = FindARShootSettings(animator);
        SG_ShootSettings sgShoot = FindSGShootSettings(animator);

        bool lockedAny = false;

        if (arShoot != null)
        {
            arShoot.externalShootLock = locked;
            lockedAny = true;
        }

        if (sgShoot != null)
        {
            sgShoot.externalShootLock = locked;
            lockedAny = true;
        }

        Debug.Log(
            $"[ShootLock {eventName}] animator={animator.name}, layer={layerIndex}, " +
            $"locked={locked}, arShoot={(arShoot != null ? arShoot.name : "NULL")}, " +
            $"sgShoot={(sgShoot != null ? sgShoot.name : "NULL")}, lockedAny={lockedAny}",
            animator
        );
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        SetShootLock(animator, true, layerIndex, "ENTER");
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        SetShootLock(animator, false, layerIndex, "EXIT");
    }
}