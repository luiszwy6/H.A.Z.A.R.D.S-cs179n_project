using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class LeftHandIKSelector : MonoBehaviour
{
    [System.Serializable]
    public class WeaponIKEntry
    {
        public string label;
        public GameObject weaponObject;            // match against PlayerWeaponSlots.CurrentWeaponObject
        public TwoBoneIKConstraint defaultIK;      // not aiming, standing
        public TwoBoneIKConstraint aimingIK;       // aiming (any stance)    (null = use defaultIK)
        public TwoBoneIKConstraint crouchingIK;    // not aiming, crouching  (null = use defaultIK)
    }

    [Header("Refs")]
    [SerializeField] private PlayerWeaponSlots weaponSlots;
    [SerializeField] private PlayerAimSettings topDownAimSettings;
    [SerializeField] private PlayerTpsView tpsView;
    [SerializeField] private SwitchCamView switchCamView;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Entries")]
    [SerializeField] private List<WeaponIKEntry> entries = new();

    [Header("Blend")]
    [SerializeField] private float blendSpeed = 12f;

    private Dictionary<TwoBoneIKConstraint, float> currentWeights = new();

    private void Awake()   => ResolveReferences();
    private void OnEnable()
    {
        ResolveReferences();
        ResetAllWeights();
    }

    private void OnDisable() => ResetAllWeights();

    private void LateUpdate()
    {
        GameObject currentWeapon = weaponSlots != null ? weaponSlots.CurrentWeaponObject : null;
        bool isAiming   = IsAimingNow();
        bool isCrouching = playerMovement != null && playerMovement.IsCrouchingNow;

        foreach (var entry in entries)
        {
            if (entry.weaponObject == null) continue;

            bool isActiveWeapon = currentWeapon != null &&
                                  (currentWeapon == entry.weaponObject ||
                                   currentWeapon.transform.IsChildOf(entry.weaponObject.transform) ||
                                   entry.weaponObject.transform.IsChildOf(currentWeapon.transform));

            TwoBoneIKConstraint activeIK = isActiveWeapon
                ? ResolveActiveIK(entry, isAiming, isCrouching)
                : null;

            BlendWeight(entry.defaultIK,   activeIK == entry.defaultIK   ? 1f : 0f);
            BlendWeight(entry.aimingIK,    activeIK == entry.aimingIK    ? 1f : 0f);
            BlendWeight(entry.crouchingIK, activeIK == entry.crouchingIK ? 1f : 0f);
        }
    }

    private TwoBoneIKConstraint ResolveActiveIK(WeaponIKEntry entry, bool isAiming, bool isCrouching)
    {
        if (isAiming)
            return entry.aimingIK != null ? entry.aimingIK : entry.defaultIK;

        if (isCrouching)
            return entry.crouchingIK != null ? entry.crouchingIK : entry.defaultIK;

        return entry.defaultIK;
    }

    private bool IsAimingNow()
    {
        if (switchCamView != null)
        {
            if (switchCamView.IsTopDown    && topDownAimSettings != null) return topDownAimSettings.IsAiming;
            if (switchCamView.IsThirdPerson && tpsView           != null) return tpsView.IsViewAiming;
        }

        if (topDownAimSettings != null && topDownAimSettings.IsAiming) return true;
        if (tpsView != null && tpsView.IsViewAiming) return true;
        return false;
    }

    private void BlendWeight(TwoBoneIKConstraint constraint, float target)
    {
        if (constraint == null) return;

        if (!currentWeights.TryGetValue(constraint, out float current))
            current = constraint.weight;

        float next = Mathf.MoveTowards(current, target, blendSpeed * Time.deltaTime);
        currentWeights[constraint] = next;
        constraint.weight = next;
    }

    private void SetWeightImmediate(TwoBoneIKConstraint constraint, float weight)
    {
        if (constraint == null) return;
        currentWeights[constraint] = weight;
        constraint.weight = weight;
    }

    private void ResetAllWeights()
    {
        foreach (var entry in entries)
        {
            SetWeightImmediate(entry.defaultIK,   0f);
            SetWeightImmediate(entry.aimingIK,    0f);
            SetWeightImmediate(entry.crouchingIK, 0f);
        }
    }

    private void ResolveReferences()
    {
        Transform root = transform.root;

        if (weaponSlots == null)       weaponSlots       = root.GetComponentInChildren<PlayerWeaponSlots>();
        if (topDownAimSettings == null) topDownAimSettings = root.GetComponent<PlayerAimSettings>();
        if (tpsView == null)            tpsView            = root.GetComponent<PlayerTpsView>();
        if (switchCamView == null)      switchCamView      = root.GetComponent<SwitchCamView>();
        if (playerMovement == null)     playerMovement     = root.GetComponent<PlayerMovement>();
    }
}
