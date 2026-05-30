using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(PlayerInput))]
public class CamoAbility : MonoBehaviour
{
    [Header("Ability Settings")]
    [Min(0.1f)] public float duration = 5f;
    [Min(0f)] public float cooldown = 5f;

    [Header("Refs")]
    [SerializeField] private PlayerStatus playerStatus;
    [SerializeField] private PlayerMeleeAttack playerMeleeAttack;
    [SerializeField] private PlayerGrenadeThrower playerGrenadeThrower;

    [Header("Visuals")]
    [Tooltip("Material used when Camo is active. Should be a transparent/translucent material.")]
    public Material camoMaterial;

    [Tooltip("Renderers that will NOT be replaced with camo material (e.g. laser sights).")]
    public List<Renderer> excludedRenderers = new List<Renderer>();

    [Header("Input")]
    [Tooltip("The name of the Input Action used to trigger Camo.")]
    public string actionName = "Camouflage";

    [Tooltip("The name of the Input Action used for shooting, which will dispel camo.")]
    public string shootActionName = "Shoot";

    public bool IsActive { get; private set; }
    public bool IsInvisibleToEnemies { get; private set; }
    public float CurrentCooldown { get; private set; }

    private PlayerInput playerInput;
    private InputAction camoAction;
    private InputAction shootAction;

    private float activeTimer;
    private Renderer[] playerRenderers;

    private readonly Dictionary<Renderer, Material[]> originalMaterials =
        new Dictionary<Renderer, Material[]>();

    private void Reset()
    {
        if (playerStatus == null)
            playerStatus = GetComponent<PlayerStatus>();

        if (playerMeleeAttack == null)
            playerMeleeAttack = GetComponent<PlayerMeleeAttack>();

        if (playerGrenadeThrower == null)
            playerGrenadeThrower = GetComponent<PlayerGrenadeThrower>();
    }

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        playerRenderers = GetComponentsInChildren<Renderer>(true);
        CurrentCooldown = 0f;

        if (playerStatus == null)
            playerStatus = GetComponent<PlayerStatus>();

        if (playerStatus == null)
            playerStatus = PlayerStatus.Instance;

        if (playerMeleeAttack == null)
            playerMeleeAttack = GetComponent<PlayerMeleeAttack>();

        if (playerGrenadeThrower == null)
            playerGrenadeThrower = GetComponent<PlayerGrenadeThrower>();

        UploadInvisibleStatus(false);
    }

    private void OnEnable()
    {
        if (playerInput != null && playerInput.actions != null)
        {
            camoAction = playerInput.actions[actionName];
            camoAction?.Enable();

            if (camoAction != null)
                camoAction.performed += OnCamoTriggered;

            shootAction = playerInput.actions[shootActionName];
            shootAction?.Enable();

            if (shootAction != null)
                shootAction.performed += OnShootTriggered;
        }

        if (playerMeleeAttack != null)
            playerMeleeAttack.MeleeAttackStarted += OnMeleeAttackStarted;

        if (playerGrenadeThrower != null)
            playerGrenadeThrower.ThrowStarted += OnGrenadeThrowStarted;

        UploadInvisibleStatus(IsInvisibleToEnemies);
    }

    private void OnDisable()
    {
        if (camoAction != null)
            camoAction.performed -= OnCamoTriggered;

        if (shootAction != null)
            shootAction.performed -= OnShootTriggered;

        if (playerMeleeAttack != null)
            playerMeleeAttack.MeleeAttackStarted -= OnMeleeAttackStarted;

        if (playerGrenadeThrower != null)
            playerGrenadeThrower.ThrowStarted -= OnGrenadeThrowStarted;

        camoAction?.Disable();
        shootAction?.Disable();

        if (IsActive)
            DeactivateCamo();
        else
            UploadInvisibleStatus(false);
    }

    private void Update()
    {
        if (CurrentCooldown > 0f)
        {
            CurrentCooldown -= Time.deltaTime;

            if (CurrentCooldown < 0f)
                CurrentCooldown = 0f;
        }

        if (IsActive)
        {
            activeTimer -= Time.deltaTime;

            if (activeTimer <= 0f)
                DeactivateCamo();
        }
    }

    private void OnCamoTriggered(InputAction.CallbackContext ctx)
    {
        if (IsActive || CurrentCooldown > 0f)
            return;

        ActivateCamo();
    }

    private void OnShootTriggered(InputAction.CallbackContext ctx)
    {
        if (IsActive)
            DeactivateCamo();
    }

    private void OnMeleeAttackStarted()
    {
        if (IsActive)
            DeactivateCamo();
    }

    private void OnGrenadeThrowStarted()
    {
        if (IsActive)
            DeactivateCamo();
    }

    private void ActivateCamo()
    {
        IsActive = true;
        IsInvisibleToEnemies = true;
        activeTimer = duration;

        UploadInvisibleStatus(true);
        NotifyEnemySensorsCamoActivated();

        if (camoMaterial != null)
        {
            originalMaterials.Clear();

            foreach (Renderer r in playerRenderers)
            {
                if (r == null || !r.enabled)
                    continue;

                if (excludedRenderers != null && excludedRenderers.Contains(r))
                    continue;

                originalMaterials[r] = r.materials;

                Material[] camoMats = new Material[r.materials.Length];

                for (int i = 0; i < camoMats.Length; i++)
                    camoMats[i] = camoMaterial;

                r.materials = camoMats;
            }
        }
    }

    private void DeactivateCamo()
    {
        IsActive = false;
        IsInvisibleToEnemies = false;
        CurrentCooldown = cooldown;

        UploadInvisibleStatus(false);

        foreach (KeyValuePair<Renderer, Material[]> kvp in originalMaterials)
        {
            if (kvp.Key != null)
                kvp.Key.materials = kvp.Value;
        }

        originalMaterials.Clear();
    }

    private void UploadInvisibleStatus(bool value)
    {
        if (playerStatus != null)
        {
            playerStatus.SetInvisible(value);
            return;
        }

        if (PlayerStatus.Instance != null)
            PlayerStatus.Instance.SetInvisible(value);
    }

    private void NotifyEnemySensorsCamoActivated()
    {
        EnemySensor[] sensors = FindObjectsOfType<EnemySensor>();
        Vector3 camoStartPosition = transform.position;

        for (int i = 0; i < sensors.Length; i++)
        {
            EnemySensor sensor = sensors[i];

            if (sensor == null)
                continue;

            sensor.LockLastKnownPositionForInvisibleTarget(transform, camoStartPosition);
        }
    }
}
