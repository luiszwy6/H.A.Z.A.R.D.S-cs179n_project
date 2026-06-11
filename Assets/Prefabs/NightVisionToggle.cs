using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class NightVisionToggle : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Volume volume;

    [Header("Profiles")]
    [SerializeField] private VolumeProfile normalProfile;
    [SerializeField] private VolumeProfile nightVisionProfile;

    [Header("Night Vision Light (Child)")]
    [SerializeField] private Light nvDirectionalLight;
    [SerializeField] private bool autoFindChildLight = true;

    [Header("Input")]
    [SerializeField] private string nightVisionActionName = "NightVision";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip nvOnClip;
    [SerializeField] private AudioClip nvOffClip;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Header("High Priority Overrides (NV < BT < SA)")]
    [Tooltip("Drag in the player BulletTimeAbility. NV is forced off while any of these are active.")]
    [SerializeField] private BulletTimeAbility bulletTimeAbility;
    [SerializeField] private AR_SpecialAbility arSpecialAbility;
    [SerializeField] private SG_SpecialAbility sgSpecialAbility;
    [SerializeField] private SRSpecialAbility  srSpecialAbility;

    private InputAction nightVisionAction;
    private bool enabledNV;
    private bool isBlockedByHighPriority;

    private void Awake()
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (audioSource  == null) audioSource  = GetComponent<AudioSource>();

        if (autoFindChildLight && nvDirectionalLight == null)
        {
            foreach (var l in GetComponentsInChildren<Light>(true))
            {
                if (l.name.Contains("NV")) { nvDirectionalLight = l; break; }
            }
        }

        // Auto-find ability refs if not assigned in Inspector
        if (bulletTimeAbility == null) bulletTimeAbility = FindFirst<BulletTimeAbility>();
        if (arSpecialAbility  == null) arSpecialAbility  = FindFirst<AR_SpecialAbility>();
        if (sgSpecialAbility  == null) sgSpecialAbility  = FindFirst<SG_SpecialAbility>();
        if (srSpecialAbility  == null) srSpecialAbility  = FindFirst<SRSpecialAbility>();

        if (playerInput == null || volume == null)
        {
            Debug.LogError("[NightVisionToggle] Missing PlayerInput or Volume reference.");
            enabled = false;
            return;
        }

        nightVisionAction = playerInput.actions[nightVisionActionName];
        if (nightVisionAction == null)
        {
            Debug.LogError($"[NightVisionToggle] Action '{nightVisionActionName}' not found.");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (nightVisionAction == null) return;
        nightVisionAction.performed += OnNightVisionPerformed;
        nightVisionAction.Enable();
    }

    private void OnDisable()
    {
        if (nightVisionAction == null) return;
        nightVisionAction.performed -= OnNightVisionPerformed;
        nightVisionAction.Disable();
    }

    private void Start()
    {
        ApplyNightVision(playSound: false);
    }

    private void Update()
    {
        bool highPriority = IsAnyHighPriorityActive();

        if (highPriority == isBlockedByHighPriority)
            return;

        isBlockedByHighPriority = highPriority;

        if (highPriority && enabledNV)
        {
            // Force full NV toggle-off: volume + light + events + sound
            enabledNV = false;
            ApplyNightVision(playSound: true);
        }
    }

    private void OnNightVisionPerformed(InputAction.CallbackContext _)
    {
        if (isBlockedByHighPriority)
            return;

        enabledNV = !enabledNV;
        ApplyNightVision(playSound: true);
    }

    private void ApplyNightVision(bool playSound)
    {
        volume.profile = enabledNV ? nightVisionProfile : normalProfile;

        NightVisionEvents.SetActive(enabledNV);

        if (nvDirectionalLight != null)
            nvDirectionalLight.enabled = enabledNV;

        if (!playSound || audioSource == null) return;

        AudioClip clip = enabledNV ? nvOnClip : nvOffClip;
        if (clip != null)
            audioSource.PlayOneShot(clip, sfxVolume);
    }

    private bool IsAnyHighPriorityActive()
    {
        return
            (bulletTimeAbility != null && bulletTimeAbility.IsActive) ||
            (arSpecialAbility  != null && arSpecialAbility.IsActive)  ||
            (sgSpecialAbility  != null && sgSpecialAbility.IsActive)  ||
            (srSpecialAbility  != null && srSpecialAbility.IsActive);
    }

    private static T FindFirst<T>() where T : Object
    {
        T[] arr = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return arr.Length > 0 ? arr[0] : null;
    }
}
