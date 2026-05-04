using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public class PlayerWeaponSlots : MonoBehaviour
{
    public enum WeaponType
    {
        AssaultRifle,
        ShotGun,
        Sniper,
        Pistol
    }

    [System.Serializable]
    public class WeaponSlot
    {
        public string slotName = "Weapon";
        public WeaponType weaponType = WeaponType.AssaultRifle;
        public GameObject weaponObject;

        [Header("Animator")]
        [Tooltip("Optional. If empty, PlayerWeaponSlots will use the global bool name based on Weapon Type.")]
        public string animatorBoolName = "";

        [HideInInspector] public ARShootSettings arShootSettings;
        [HideInInspector] public SG_ShootSettings sgShootSettings;

        [HideInInspector] public MuzzlePointSettings muzzlePointSettings;
        [HideInInspector] public PlayerCrossHairSettings crossHairSettings;
        [HideInInspector] public WeaponAmmoSettings ammoSettings;
        [HideInInspector] public WeaponEffects weaponEffects;
    }

    [System.Serializable]
    public class SwitchRigWeightEntry
    {
        [Header("Rig")]
        public Rig rig;

        [Header("During Switching")]
        [Range(0f, 1f)] public float switchingWeight = 0f;

        [Header("After Switching")]
        public bool restoreOriginalWeight = true;
        [Range(0f, 1f)] public float restoreWeight = 1f;
    }

    [Header("Weapon Slots")]
    [SerializeField] private List<WeaponSlot> weaponSlots = new List<WeaponSlot>();
    [SerializeField] private int startingWeaponIndex = 0;
    [SerializeField] private bool activateStartingWeaponOnAwake = true;

    [Header("Input")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string changeWeaponUpActionName = "ChangeWeaponUp";
    [SerializeField] private string changeWeaponDownActionName = "ChangeWeaponDown";

    [Header("Direct Slot Input")]
    [SerializeField] private string slot1ActionName = "Slot 1";
    [SerializeField] private string slot2ActionName = "Slot 2";
    [SerializeField] private string slot3ActionName = "Slot 3";
    [SerializeField] private string slot4ActionName = "Slot 4";
    [SerializeField] private string slot5ActionName = "Slot 5";

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Animator Weapon Bools")]
    [SerializeField] private bool updateAnimatorWeaponBools = true;
    [SerializeField] private string assaultRifleBoolName = "AssaultRifle";
    [SerializeField] private string shotGunBoolName = "ShotGun";
    [SerializeField] private string sniperBoolName = "Sniper";
    [SerializeField] private string pistolBoolName = "Pistol";

    [Header("Animator Weapon Switch")]
    [SerializeField] private bool triggerWeaponSwitchOnChange = true;
    [SerializeField] private string weaponSwitchTriggerName = "WeaponSwitch";
    [SerializeField] private bool resetWeaponSwitchTriggerBeforeSet = true;

    [Header("Switching State")]
    [SerializeField] private bool useSwitchingState = true;
    [SerializeField] private string isSwitchingBoolName = "IsSwitching";
    [Min(0f)] [SerializeField] private float switchingDuration = 0.35f;
    [SerializeField] private bool blockSwitchWhileSwitching = true;

    [Header("Rig Weights During Switching")]
    [SerializeField] private List<SwitchRigWeightEntry> switchRigWeights = new List<SwitchRigWeightEntry>();

    [Header("Switch Rules")]
    [Tooltip("If true, switching weapon will cancel the current weapon reload instead of blocking the switch.")]
    [SerializeField] private bool cancelReloadOnSwitch = true;

    [Tooltip("Only used when Cancel Reload On Switch is false.")]
    [SerializeField] private bool blockSwitchWhileReloading = true;

    [SerializeField] private bool blockSwitchWhileShootingLocked = true;

    private InputAction changeWeaponUpAction;
    private InputAction changeWeaponDownAction;

    private InputAction slot1Action;
    private InputAction slot2Action;
    private InputAction slot3Action;
    private InputAction slot4Action;
    private InputAction slot5Action;

    private int currentWeaponIndex = -1;
    private bool hasEquippedOnce;

    private int assaultRifleBoolHash;
    private int shotGunBoolHash;
    private int sniperBoolHash;
    private int pistolBoolHash;
    private int weaponSwitchTriggerHash;
    private int isSwitchingBoolHash;

    private Coroutine switchingRoutine;
    private bool isSwitching;
    private readonly Dictionary<int, float> originalRigWeights = new Dictionary<int, float>();

    public int CurrentWeaponIndex => currentWeaponIndex;
    public bool IsSwitching => isSwitching;

    public GameObject CurrentWeaponObject
    {
        get
        {
            WeaponSlot slot = GetCurrentSlot();
            return slot != null ? slot.weaponObject : null;
        }
    }

    public ARShootSettings CurrentARShootSettings
    {
        get
        {
            WeaponSlot slot = GetCurrentSlot();
            return slot != null ? slot.arShootSettings : null;
        }
    }

    public SG_ShootSettings CurrentSGShootSettings
    {
        get
        {
            WeaponSlot slot = GetCurrentSlot();
            return slot != null ? slot.sgShootSettings : null;
        }
    }

    public MuzzlePointSettings CurrentMuzzlePointSettings
    {
        get
        {
            WeaponSlot slot = GetCurrentSlot();
            return slot != null ? slot.muzzlePointSettings : null;
        }
    }

    public PlayerCrossHairSettings CurrentCrossHairSettings
    {
        get
        {
            WeaponSlot slot = GetCurrentSlot();
            return slot != null ? slot.crossHairSettings : null;
        }
    }

    public WeaponAmmoSettings CurrentAmmoSettings
    {
        get
        {
            WeaponSlot slot = GetCurrentSlot();
            return slot != null ? slot.ammoSettings : null;
        }
    }

    private void Reset()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnValidate()
    {
        switchingDuration = Mathf.Max(0f, switchingDuration);
    }

    private void Awake()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        CacheAnimatorHashes();
        CacheWeaponSlots();
        CacheInputActions();

        if (activateStartingWeaponOnAwake)
        {
            int validIndex = GetFirstValidWeaponIndex(startingWeaponIndex);
            EquipWeapon(validIndex, true);
        }
        else
        {
            SetAllAnimatorWeaponBoolsFalse();
            SetAnimatorSwitchingBool(false);
        }
    }

    private void OnEnable()
    {
        changeWeaponUpAction?.Enable();
        changeWeaponDownAction?.Enable();

        slot1Action?.Enable();
        slot2Action?.Enable();
        slot3Action?.Enable();
        slot4Action?.Enable();
        slot5Action?.Enable();
    }

    private void OnDisable()
    {
        changeWeaponUpAction?.Disable();
        changeWeaponDownAction?.Disable();

        slot1Action?.Disable();
        slot2Action?.Disable();
        slot3Action?.Disable();
        slot4Action?.Disable();
        slot5Action?.Disable();

        StopSwitchingRoutineAndRestore();
    }

    private void Update()
    {
        if (changeWeaponUpAction != null && changeWeaponUpAction.WasPressedThisFrame())
            ChangeWeaponUp();

        if (changeWeaponDownAction != null && changeWeaponDownAction.WasPressedThisFrame())
            ChangeWeaponDown();

        if (slot1Action != null && slot1Action.WasPressedThisFrame())
            EquipWeaponBySlotNumber(1);

        if (slot2Action != null && slot2Action.WasPressedThisFrame())
            EquipWeaponBySlotNumber(2);

        if (slot3Action != null && slot3Action.WasPressedThisFrame())
            EquipWeaponBySlotNumber(3);

        if (slot4Action != null && slot4Action.WasPressedThisFrame())
            EquipWeaponBySlotNumber(4);

        if (slot5Action != null && slot5Action.WasPressedThisFrame())
            EquipWeaponBySlotNumber(5);
    }

    public void ChangeWeaponUp()
    {
        if (!CanSwitchWeapon())
            return;

        EquipNextWeapon(+1);
    }

    public void ChangeWeaponDown()
    {
        if (!CanSwitchWeapon())
            return;

        EquipNextWeapon(-1);
    }

    public void EquipWeaponBySlotNumber(int slotNumber)
    {
        int index = slotNumber - 1;
        EquipWeapon(index, false);
    }

    public void EquipNextWeapon(int direction)
    {
        if (weaponSlots == null || weaponSlots.Count == 0)
            return;

        if (direction == 0)
            return;

        int startIndex = currentWeaponIndex;

        if (startIndex < 0 || startIndex >= weaponSlots.Count)
            startIndex = GetFirstValidWeaponIndex(0);

        if (startIndex < 0)
            return;

        for (int i = 1; i <= weaponSlots.Count; i++)
        {
            int nextIndex = WrapIndex(startIndex + direction * i);

            if (IsValidWeaponIndex(nextIndex))
            {
                EquipWeapon(nextIndex, false);
                return;
            }
        }
    }

    public void EquipWeapon(int index, bool force)
    {
        if (!IsValidWeaponIndex(index))
            return;

        bool isDifferentWeapon = index != currentWeaponIndex;

        if (!force && !isDifferentWeapon)
            return;

        if (!force && !CanSwitchWeapon())
            return;

        WeaponSlot previousSlot = GetCurrentSlot();

        if (hasEquippedOnce && isDifferentWeapon && previousSlot != null)
        {
            ClearShootRuntimeState(previousSlot);

            if (cancelReloadOnSwitch)
                CancelReloadRuntimeState(previousSlot);
        }

        for (int i = 0; i < weaponSlots.Count; i++)
        {
            if (i == index)
                continue;

            WeaponSlot slot = weaponSlots[i];

            if (slot == null || slot.weaponObject == null)
                continue;

            if (slot.weaponObject.activeSelf)
                slot.weaponObject.SetActive(false);
        }

        WeaponSlot targetSlot = weaponSlots[index];

        if (targetSlot.weaponObject != null && !targetSlot.weaponObject.activeSelf)
            targetSlot.weaponObject.SetActive(true);

        currentWeaponIndex = index;

        WeaponSlot current = GetCurrentSlot();

        if (current != null)
        {
            ClearShootRuntimeState(current);
            CancelReloadRuntimeState(current);
            UpdateAnimatorWeaponBools(current);
        }

        if (hasEquippedOnce && isDifferentWeapon)
            StartSwitchingState();

        hasEquippedOnce = true;
    }

    public bool CanSwitchWeapon()
    {
        if (blockSwitchWhileSwitching && isSwitching)
            return false;

        WeaponSlot current = GetCurrentSlot();

        if (current == null)
            return true;

        if (current.ammoSettings != null && current.ammoSettings.IsReloading)
        {
            if (cancelReloadOnSwitch)
                return true;

            if (blockSwitchWhileReloading)
                return false;
        }

        if (blockSwitchWhileShootingLocked)
        {
            if (current.arShootSettings != null && current.arShootSettings.externalShootLock)
                return false;

            if (current.sgShootSettings != null && current.sgShootSettings.externalShootLock)
                return false;
        }

        return true;
    }

    private void StartSwitchingState()
    {
        if (triggerWeaponSwitchOnChange)
            TriggerWeaponSwitch();

        if (!useSwitchingState)
            return;

        if (switchingRoutine != null)
        {
            StopCoroutine(switchingRoutine);
            switchingRoutine = null;
            RestoreSwitchRigWeights();
        }

        switchingRoutine = StartCoroutine(SwitchingRoutine());
    }

    private IEnumerator SwitchingRoutine()
    {
        isSwitching = true;
        SetAnimatorSwitchingBool(true);
        ApplySwitchRigWeights();

        if (switchingDuration > 0f)
            yield return new WaitForSeconds(switchingDuration);
        else
            yield return null;

        RestoreSwitchRigWeights();
        SetAnimatorSwitchingBool(false);
        isSwitching = false;
        switchingRoutine = null;
    }

    private void StopSwitchingRoutineAndRestore()
    {
        if (switchingRoutine != null)
        {
            StopCoroutine(switchingRoutine);
            switchingRoutine = null;
        }

        RestoreSwitchRigWeights();
        SetAnimatorSwitchingBool(false);
        isSwitching = false;
    }

    private void ApplySwitchRigWeights()
    {
        originalRigWeights.Clear();

        if (switchRigWeights == null)
            return;

        for (int i = 0; i < switchRigWeights.Count; i++)
        {
            SwitchRigWeightEntry entry = switchRigWeights[i];

            if (entry == null || entry.rig == null)
                continue;

            int id = entry.rig.GetInstanceID();

            if (!originalRigWeights.ContainsKey(id))
                originalRigWeights.Add(id, entry.rig.weight);

            entry.rig.weight = entry.switchingWeight;
        }
    }

    private void RestoreSwitchRigWeights()
    {
        if (switchRigWeights == null)
        {
            originalRigWeights.Clear();
            return;
        }

        for (int i = 0; i < switchRigWeights.Count; i++)
        {
            SwitchRigWeightEntry entry = switchRigWeights[i];

            if (entry == null || entry.rig == null)
                continue;

            int id = entry.rig.GetInstanceID();

            if (entry.restoreOriginalWeight && originalRigWeights.TryGetValue(id, out float originalWeight))
                entry.rig.weight = originalWeight;
            else
                entry.rig.weight = entry.restoreWeight;
        }

        originalRigWeights.Clear();
    }

    private void SetAnimatorSwitchingBool(bool value)
    {
        if (animator == null)
            return;

        if (string.IsNullOrWhiteSpace(isSwitchingBoolName))
            return;

        animator.SetBool(isSwitchingBoolHash, value);
    }

    private void CacheInputActions()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        changeWeaponUpAction = FindActionOrNull(changeWeaponUpActionName);
        changeWeaponDownAction = FindActionOrNull(changeWeaponDownActionName);

        slot1Action = FindActionOrNull(slot1ActionName);
        slot2Action = FindActionOrNull(slot2ActionName);
        slot3Action = FindActionOrNull(slot3ActionName);
        slot4Action = FindActionOrNull(slot4ActionName);
        slot5Action = FindActionOrNull(slot5ActionName);
    }

    private InputAction FindActionOrNull(string actionName)
    {
        if (playerInput == null || playerInput.actions == null)
            return null;

        if (string.IsNullOrWhiteSpace(actionName))
            return null;

        return playerInput.actions.FindAction(actionName, false);
    }

    private void CacheAnimatorHashes()
    {
        assaultRifleBoolHash = Animator.StringToHash(assaultRifleBoolName);
        shotGunBoolHash = Animator.StringToHash(shotGunBoolName);
        sniperBoolHash = Animator.StringToHash(sniperBoolName);
        pistolBoolHash = Animator.StringToHash(pistolBoolName);
        weaponSwitchTriggerHash = Animator.StringToHash(weaponSwitchTriggerName);
        isSwitchingBoolHash = Animator.StringToHash(isSwitchingBoolName);
    }

    private void UpdateAnimatorWeaponBools(WeaponSlot currentSlot)
    {
        if (!updateAnimatorWeaponBools || animator == null || currentSlot == null)
            return;

        SetAllAnimatorWeaponBoolsFalse();

        string boolName = GetAnimatorBoolNameForSlot(currentSlot);

        if (!string.IsNullOrWhiteSpace(boolName))
            animator.SetBool(boolName, true);
    }

    private string GetAnimatorBoolNameForSlot(WeaponSlot slot)
    {
        if (slot == null)
            return "";

        if (!string.IsNullOrWhiteSpace(slot.animatorBoolName))
            return slot.animatorBoolName;

        switch (slot.weaponType)
        {
            case WeaponType.AssaultRifle:
                return assaultRifleBoolName;

            case WeaponType.ShotGun:
                return shotGunBoolName;

            case WeaponType.Sniper:
                return sniperBoolName;

            case WeaponType.Pistol:
                return pistolBoolName;
        }

        return "";
    }

    private void SetAllAnimatorWeaponBoolsFalse()
    {
        if (!updateAnimatorWeaponBools || animator == null)
            return;

        animator.SetBool(assaultRifleBoolHash, false);
        animator.SetBool(shotGunBoolHash, false);
        animator.SetBool(sniperBoolHash, false);
        animator.SetBool(pistolBoolHash, false);

        if (weaponSlots == null)
            return;

        for (int i = 0; i < weaponSlots.Count; i++)
        {
            WeaponSlot slot = weaponSlots[i];

            if (slot == null)
                continue;

            if (string.IsNullOrWhiteSpace(slot.animatorBoolName))
                continue;

            animator.SetBool(slot.animatorBoolName, false);
        }
    }

    private void TriggerWeaponSwitch()
    {
        if (animator == null)
            return;

        if (string.IsNullOrWhiteSpace(weaponSwitchTriggerName))
            return;

        if (resetWeaponSwitchTriggerBeforeSet)
            animator.ResetTrigger(weaponSwitchTriggerHash);

        animator.SetTrigger(weaponSwitchTriggerHash);
    }

    private void CacheWeaponSlots()
    {
        if (weaponSlots == null)
            return;

        for (int i = 0; i < weaponSlots.Count; i++)
        {
            WeaponSlot slot = weaponSlots[i];

            if (slot == null || slot.weaponObject == null)
                continue;

            slot.arShootSettings = slot.weaponObject.GetComponentInChildren<ARShootSettings>(true);
            slot.sgShootSettings = slot.weaponObject.GetComponentInChildren<SG_ShootSettings>(true);

            slot.muzzlePointSettings = slot.weaponObject.GetComponentInChildren<MuzzlePointSettings>(true);
            slot.crossHairSettings = slot.weaponObject.GetComponentInChildren<PlayerCrossHairSettings>(true);
            slot.ammoSettings = slot.weaponObject.GetComponentInChildren<WeaponAmmoSettings>(true);
            slot.weaponEffects = slot.weaponObject.GetComponentInChildren<WeaponEffects>(true);
        }
    }

    private void ClearShootRuntimeState(WeaponSlot slot)
    {
        if (slot == null)
            return;

        if (slot.arShootSettings != null)
            slot.arShootSettings.ForceClearRuntimeState();

        if (slot.sgShootSettings != null)
            slot.sgShootSettings.ForceClearRuntimeState();
    }

    private void CancelReloadRuntimeState(WeaponSlot slot)
    {
        if (slot == null)
            return;

        if (slot.ammoSettings != null)
            slot.ammoSettings.CancelReload();
    }

    private WeaponSlot GetCurrentSlot()
    {
        if (weaponSlots == null)
            return null;

        if (currentWeaponIndex < 0 || currentWeaponIndex >= weaponSlots.Count)
            return null;

        return weaponSlots[currentWeaponIndex];
    }

    private int GetFirstValidWeaponIndex(int preferredIndex)
    {
        if (weaponSlots == null || weaponSlots.Count == 0)
            return -1;

        if (IsValidWeaponIndex(preferredIndex))
            return preferredIndex;

        for (int i = 0; i < weaponSlots.Count; i++)
        {
            if (IsValidWeaponIndex(i))
                return i;
        }

        return -1;
    }

    private bool IsValidWeaponIndex(int index)
    {
        if (weaponSlots == null)
            return false;

        if (index < 0 || index >= weaponSlots.Count)
            return false;

        WeaponSlot slot = weaponSlots[index];

        return slot != null && slot.weaponObject != null;
    }

    private int WrapIndex(int index)
    {
        if (weaponSlots == null || weaponSlots.Count == 0)
            return -1;

        int count = weaponSlots.Count;
        return ((index % count) + count) % count;
    }
}