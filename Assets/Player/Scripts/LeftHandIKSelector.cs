using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class LeftHandIKSelector : MonoBehaviour
{
    [System.Serializable]
    public class WeaponIKEntry
    {
        public string label;
        public GameObject weaponObject;             // match against PlayerWeaponSlots.CurrentWeaponObject
        public TwoBoneIKConstraint defaultIK;       // weight when not aiming
        public TwoBoneIKConstraint aimingIK;        // weight when aiming (can be same as defaultIK or null)
    }

    [Header("Refs")]
    [SerializeField] private PlayerWeaponSlots weaponSlots;
    [SerializeField] private PlayerAimSettings topDownAimSettings;  // TopDown aiming
    [SerializeField] private PlayerTpsView tpsView;                 // TPS aiming
    [SerializeField] private SwitchCamView switchCamView;

    [Header("Entries")]
    [SerializeField] private List<WeaponIKEntry> entries = new();

    [Header("Blend")]
    [SerializeField] private float blendSpeed = 12f;

    // cached current weights per constraint
    private Dictionary<TwoBoneIKConstraint, float> currentWeights = new();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        // snap all to 0 on enable
        foreach (var entry in entries)
        {
            SetWeightImmediate(entry.defaultIK, 0f);
            SetWeightImmediate(entry.aimingIK, 0f);
        }
    }

    private void OnDisable()
    {
        foreach (var entry in entries)
        {
            SetWeightImmediate(entry.defaultIK, 0f);
            SetWeightImmediate(entry.aimingIK, 0f);
        }
    }

    private void LateUpdate()
    {
        GameObject currentWeapon = weaponSlots != null ? weaponSlots.CurrentWeaponObject : null;
        bool isAiming = IsAimingNow();

        foreach (var entry in entries)
        {
            if (entry.weaponObject == null) continue;

            bool isActiveWeapon = currentWeapon != null &&
                                  (currentWeapon == entry.weaponObject ||
                                   currentWeapon.transform.IsChildOf(entry.weaponObject.transform) ||
                                   entry.weaponObject.transform.IsChildOf(currentWeapon.transform));

            // determine target weights for this entry
            float targetDefault = 0f;
            float targetAiming  = 0f;

            if (isActiveWeapon)
            {
                bool hasAimIK = entry.aimingIK != null && entry.aimingIK != entry.defaultIK;

                if (hasAimIK)
                {
                    targetDefault = isAiming ? 0f : 1f;
                    targetAiming  = isAiming ? 1f : 0f;
                }
                else
                {
                    targetDefault = 1f;
                }
            }

            BlendWeight(entry.defaultIK, targetDefault);
            BlendWeight(entry.aimingIK,  targetAiming);
        }
    }

    private bool IsAimingNow()
    {
        if (switchCamView != null)
        {
            if (switchCamView.IsTopDown && topDownAimSettings != null)
                return topDownAimSettings.IsAiming;

            if (switchCamView.IsThirdPerson && tpsView != null)
                return tpsView.IsViewAiming;
        }

        // fallback: check both
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

    private void ResolveReferences()
    {
        Transform root = transform.root;

        if (weaponSlots == null)
            weaponSlots = root.GetComponentInChildren<PlayerWeaponSlots>();

        if (topDownAimSettings == null)
            topDownAimSettings = root.GetComponent<PlayerAimSettings>();

        if (tpsView == null)
            tpsView = root.GetComponent<PlayerTpsView>();

        if (switchCamView == null)
            switchCamView = root.GetComponent<SwitchCamView>();
    }
}
