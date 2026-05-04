using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(PlayerInput))]
public class CamoAbility : MonoBehaviour
{
    [Header("Ability Settings")]
    [Min(0.1f)] public float duration = 5f;
    [Min(0f)] public float cooldown = 5f;

    [Header("Visuals")]
    [Tooltip("Material used when Camo is active. Should be a transparent/translucent material.")]
    public Material camoMaterial;

    [Header("Input")]
    [Tooltip("The name of the Input Action used to trigger Camo.")]
    public string actionName = "Camouflage";
    [Tooltip("The name of the Input Action used for shooting, which will dispel camo.")]
    public string shootActionName = "Shoot";

    public bool IsActive { get; private set; }
    public bool IsInvisibleToEnemies { get; private set; }
    public float CurrentCooldown { get; private set; }

    [Header("Stealth Takedown")]
    public bool CanStealthTakedown { get; private set; }
    [Tooltip("Name of the input action to perform stealth takedown.")]
    public string stealthTakedownActionName = "StealthTakeDown";

    private PlayerInput playerInput;
    private InputAction camoAction;
    private InputAction shootAction;
    private InputAction takedownAction;

    private float activeTimer;
    private Renderer[] playerRenderers;
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        playerRenderers = GetComponentsInChildren<Renderer>(true);
        CurrentCooldown = 0f;
    }

    void OnEnable()
    {
        if (playerInput != null)
        {
            camoAction = playerInput.actions[actionName];
            camoAction?.Enable();
            if (camoAction != null)
                camoAction.performed += OnCamoTriggered;

            shootAction = playerInput.actions[shootActionName];
            shootAction?.Enable();
            if (shootAction != null)
                shootAction.performed += OnShootTriggered;

            takedownAction = playerInput.actions[stealthTakedownActionName];
            takedownAction?.Enable();
            if (takedownAction != null)
                takedownAction.performed += OnTakedownTriggered;
        }
    }

    void OnDisable()
    {
        if (camoAction != null)
            camoAction.performed -= OnCamoTriggered;
        if (shootAction != null)
            shootAction.performed -= OnShootTriggered;
        if (takedownAction != null)
            takedownAction.performed -= OnTakedownTriggered;

        camoAction?.Disable();
        shootAction?.Disable();
        takedownAction?.Disable();

        if (IsActive)
            DeactivateCamo();
    }

    void Update()
    {
        if (CurrentCooldown > 0f)
        {
            CurrentCooldown -= Time.deltaTime;
            if (CurrentCooldown < 0f) CurrentCooldown = 0f;
        }

        if (IsActive)
        {
            activeTimer -= Time.deltaTime;
            if (activeTimer <= 0f)
            {
                DeactivateCamo();
            }

            // check distance to enemies to set CanStealthTakedown
        }
    }

    private void OnCamoTriggered(InputAction.CallbackContext ctx)
    {
        if (IsActive || CurrentCooldown > 0f) return;

        ActivateCamo();
    }

    private void OnShootTriggered(InputAction.CallbackContext ctx)
    {
        if (IsActive)
        {
            DeactivateCamo();
        }
    }

    private void OnTakedownTriggered(InputAction.CallbackContext ctx)
    {
        if (IsActive && CanStealthTakedown)
        {
            Debug.Log("stealth takedown");
        }
    }

    private void ActivateCamo()
    {
        IsActive = true;
        IsInvisibleToEnemies = true;
        activeTimer = duration;

        if (camoMaterial != null)
        {
            originalMaterials.Clear();
            foreach (var r in playerRenderers)
            {
                if (r == null || !r.enabled) continue;
                
                originalMaterials[r] = r.materials;
                
                Material[] camoMats = new Material[r.materials.Length];
                for (int i = 0; i < camoMats.Length; i++)
                {
                    camoMats[i] = camoMaterial;
                }
                r.materials = camoMats;
            }
        }
    }

    private void DeactivateCamo()
    {
        IsActive = false;
        IsInvisibleToEnemies = false;
        CurrentCooldown = cooldown;

        // restore original materials
        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.materials = kvp.Value;
            }
        }
        originalMaterials.Clear();
    }
}
