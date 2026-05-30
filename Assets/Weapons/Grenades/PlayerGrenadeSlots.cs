using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public class PlayerGrenadeSlots : MonoBehaviour
{
    public enum GrenadeType
    {
        Frag,
        FlashBang,
        Smoke
    }

    [System.Serializable]
    public class GrenadeThrowPhysics
    {
        [Header("Override")]
        public bool useCustomThrowPhysics = false;

        [Header("Throw Velocity")]
        [Min(0f)] public float fixedThrowSpeed = 13f;
        [Range(-89f, 89f)] public float fixedThrowAngle = 32f;
        public Vector3 angularVelocity = new Vector3(8f, 4f, 2f);

        [Header("Spawn Offset")]
        public bool useCustomSpawnOffset = false;
        [Min(0f)] public float spawnForwardOffset = 0.6f;
        public float spawnUpOffset = 0.08f;
    }

    [System.Serializable]
    public class GrenadeSlot
    {
        public string slotName = "Grenade";
        public GrenadeType grenadeType = GrenadeType.Frag;

        [Header("World Prefab")]
        public GrenadeWorldController worldPrefab;

        [Header("Count")]
        public int count = -1;

        [Header("Animator")]
        public string animatorBoolName = "";

        [Header("Throw Physics")]
        public GrenadeThrowPhysics throwPhysics = new GrenadeThrowPhysics();
    }

    [Header("Slots")]
    [SerializeField] private List<GrenadeSlot> grenadeSlots = new List<GrenadeSlot>();
    [SerializeField] private int startingGrenadeIndex = 0;
    [SerializeField] private bool equipStartingGrenadeOnAwake = true;
    [SerializeField] private bool autoEquipNextWhenEmpty = true;

    [Header("Input")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string changeGrenadeActionName = "ChangeGrenade";

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private bool updateAnimatorGrenadeBools = true;
    [SerializeField] private string fragBoolName = "FragGrenade";
    [SerializeField] private string flashBangBoolName = "FlashBang";
    [SerializeField] private string smokeBoolName = "SmokeGrenade";

    [Header("Debug")]
    [SerializeField] private bool logChange = false;

    private InputAction changeGrenadeAction;
    private int currentGrenadeIndex = -1;

    private int fragBoolHash;
    private int flashBangBoolHash;
    private int smokeBoolHash;

    public int CurrentGrenadeIndex => currentGrenadeIndex;

    public GrenadeSlot CurrentSlot
    {
        get
        {
            if (grenadeSlots == null)
                return null;

            if (currentGrenadeIndex < 0 || currentGrenadeIndex >= grenadeSlots.Count)
                return null;

            return grenadeSlots[currentGrenadeIndex];
        }
    }

    public GrenadeType CurrentGrenadeType
    {
        get
        {
            GrenadeSlot slot = CurrentSlot;
            return slot != null ? slot.grenadeType : GrenadeType.Frag;
        }
    }

    public GrenadeSlot GetSlotByType(GrenadeType type)
    {
        if (grenadeSlots == null)
            return null;

        foreach (var slot in grenadeSlots)
        {
            if (slot != null && slot.grenadeType == type)
                return slot;
        }

        return null;
    }

    public bool HasUsableCurrentGrenade
    {
        get
        {
            GrenadeSlot slot = CurrentSlot;
            return IsUsableSlot(slot);
        }
    }

    private void Reset()
    {
        playerInput = GetComponent<PlayerInput>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        CacheAnimatorHashes();
        CacheInputActions();

        if (equipStartingGrenadeOnAwake)
        {
            int validIndex = GetFirstUsableGrenadeIndex(startingGrenadeIndex);
            EquipGrenade(validIndex, true);
        }
        else
        {
            currentGrenadeIndex = -1;
            SetAllAnimatorGrenadeBoolsFalse();
        }
    }

    private void OnEnable()
    {
        changeGrenadeAction?.Enable();
    }

    private void OnDisable()
    {
        changeGrenadeAction?.Disable();
    }

    private void Update()
    {
        if (changeGrenadeAction != null && changeGrenadeAction.WasPressedThisFrame())
            ChangeGrenade();
    }

    public void ChangeGrenade()
    {
        EquipNextGrenade(+1);
    }

    public void EquipNextGrenade(int direction)
    {
        if (grenadeSlots == null || grenadeSlots.Count == 0)
            return;

        if (direction == 0)
            return;

        int startIndex = currentGrenadeIndex;

        if (startIndex < 0 || startIndex >= grenadeSlots.Count)
            startIndex = GetFirstUsableGrenadeIndex(0);

        if (startIndex < 0)
            return;

        for (int i = 1; i <= grenadeSlots.Count; i++)
        {
            int nextIndex = WrapIndex(startIndex + direction * i);

            if (IsUsableGrenadeIndex(nextIndex))
            {
                EquipGrenade(nextIndex, false);
                return;
            }
        }
    }

    public void EquipGrenade(int index, bool force)
    {
        if (!IsValidGrenadeIndex(index))
            return;

        if (!IsUsableGrenadeIndex(index))
            return;

        if (!force && index == currentGrenadeIndex)
            return;

        currentGrenadeIndex = index;
        UpdateAnimatorGrenadeBools(CurrentSlot);

        if (logChange && CurrentSlot != null)
            Debug.Log($"[PlayerGrenadeSlots] Equipped grenade: {CurrentSlot.slotName}", this);
    }

    public bool TryConsumeCurrentGrenade(out GrenadeSlot consumedSlot)
    {
        consumedSlot = CurrentSlot;

        if (!IsUsableSlot(consumedSlot))
            return false;

        if (consumedSlot.count > 0)
            consumedSlot.count--;

        return true;
    }

    public void RefreshAfterThrow()
    {
        if (autoEquipNextWhenEmpty && !HasUsableCurrentGrenade)
            EquipNextAvailableFromCurrent();

        UpdateAnimatorGrenadeBools(CurrentSlot);
    }

    public int GetCurrentGrenadeCount()
    {
        GrenadeSlot slot = CurrentSlot;

        if (slot == null)
            return 0;

        return slot.count;
    }

    public bool HasAnyUsableGrenade()
    {
        if (grenadeSlots == null)
            return false;

        for (int i = 0; i < grenadeSlots.Count; i++)
        {
            if (IsUsableGrenadeIndex(i))
                return true;
        }

        return false;
    }

    private void EquipNextAvailableFromCurrent()
    {
        if (grenadeSlots == null || grenadeSlots.Count == 0)
        {
            currentGrenadeIndex = -1;
            return;
        }

        int startIndex = currentGrenadeIndex;

        if (startIndex < 0 || startIndex >= grenadeSlots.Count)
            startIndex = 0;

        for (int i = 1; i <= grenadeSlots.Count; i++)
        {
            int nextIndex = WrapIndex(startIndex + i);

            if (IsUsableGrenadeIndex(nextIndex))
            {
                EquipGrenade(nextIndex, true);
                return;
            }
        }

        currentGrenadeIndex = -1;
        SetAllAnimatorGrenadeBoolsFalse();
    }

    private void CacheInputActions()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        if (!string.IsNullOrWhiteSpace(changeGrenadeActionName))
            changeGrenadeAction = playerInput.actions.FindAction(changeGrenadeActionName, false);
    }

    private void CacheAnimatorHashes()
    {
        fragBoolHash = Animator.StringToHash(fragBoolName);
        flashBangBoolHash = Animator.StringToHash(flashBangBoolName);
        smokeBoolHash = Animator.StringToHash(smokeBoolName);
    }

    private void UpdateAnimatorGrenadeBools(GrenadeSlot currentSlot)
    {
        if (!updateAnimatorGrenadeBools || animator == null)
            return;

        SetAllAnimatorGrenadeBoolsFalse();

        if (currentSlot == null)
            return;

        string boolName = GetAnimatorBoolNameForSlot(currentSlot);

        if (!string.IsNullOrWhiteSpace(boolName))
            animator.SetBool(boolName, true);
    }

    private string GetAnimatorBoolNameForSlot(GrenadeSlot slot)
    {
        if (slot == null)
            return "";

        if (!string.IsNullOrWhiteSpace(slot.animatorBoolName))
            return slot.animatorBoolName;

        switch (slot.grenadeType)
        {
            case GrenadeType.Frag:
                return fragBoolName;

            case GrenadeType.FlashBang:
                return flashBangBoolName;

            case GrenadeType.Smoke:
                return smokeBoolName;
        }

        return "";
    }

    private void SetAllAnimatorGrenadeBoolsFalse()
    {
        if (!updateAnimatorGrenadeBools || animator == null)
            return;

        if (!string.IsNullOrWhiteSpace(fragBoolName))
            animator.SetBool(fragBoolHash, false);

        if (!string.IsNullOrWhiteSpace(flashBangBoolName))
            animator.SetBool(flashBangBoolHash, false);

        if (!string.IsNullOrWhiteSpace(smokeBoolName))
            animator.SetBool(smokeBoolHash, false);

        if (grenadeSlots == null)
            return;

        for (int i = 0; i < grenadeSlots.Count; i++)
        {
            GrenadeSlot slot = grenadeSlots[i];

            if (slot == null)
                continue;

            if (string.IsNullOrWhiteSpace(slot.animatorBoolName))
                continue;

            animator.SetBool(slot.animatorBoolName, false);
        }
    }

    private bool IsValidGrenadeIndex(int index)
    {
        if (grenadeSlots == null)
            return false;

        if (index < 0 || index >= grenadeSlots.Count)
            return false;

        GrenadeSlot slot = grenadeSlots[index];

        return slot != null && slot.worldPrefab != null;
    }

    private bool IsUsableGrenadeIndex(int index)
    {
        if (!IsValidGrenadeIndex(index))
            return false;

        return IsUsableSlot(grenadeSlots[index]);
    }

    private bool IsUsableSlot(GrenadeSlot slot)
    {
        if (slot == null)
            return false;

        if (slot.worldPrefab == null)
            return false;

        return slot.count < 0 || slot.count > 0;
    }

    private int GetFirstUsableGrenadeIndex(int preferredIndex)
    {
        if (grenadeSlots == null || grenadeSlots.Count == 0)
            return -1;

        if (IsUsableGrenadeIndex(preferredIndex))
            return preferredIndex;

        for (int i = 0; i < grenadeSlots.Count; i++)
        {
            if (IsUsableGrenadeIndex(i))
                return i;
        }

        return -1;
    }

    private int WrapIndex(int index)
    {
        if (grenadeSlots == null || grenadeSlots.Count == 0)
            return -1;

        int count = grenadeSlots.Count;
        return ((index % count) + count) % count;
    }
}