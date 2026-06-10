using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SniperScopeFOV : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private SwitchCamView switchCamView;
    [SerializeField] private PlayerTpsView tpsView;
    [SerializeField] private SRShootSettings sniperShootSettings;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera tpsAimCamera;

    [Header("FOV")]
    [SerializeField] private float normalFOV = 40f;
    [SerializeField] private List<float> scopeFOVLevels = new List<float> { 25f, 15f };
    [SerializeField] private float transitionSpeed = 8f;

    [Header("Input")]
    [SerializeField] private string scopeActionName = "Scope";

    private InputAction scopeAction;
    private int scopeIndex = -1; // -1 = normal, 0..n-1 = scope levels
    private float currentFOV;
    private float targetFOV;

    private void Awake()
    {
        ResolveReferences();
        currentFOV = normalFOV;
        targetFOV  = normalFOV;
    }

    private void OnEnable()
    {
        ResolveReferences();

        scopeAction = playerInput != null
            ? playerInput.actions?.FindAction(scopeActionName, false)
            : null;

        if (scopeAction != null)
            scopeAction.Enable();

        scopeIndex = -1;
        targetFOV  = normalFOV;
    }

    private void OnDisable()
    {
        if (scopeAction != null)
            scopeAction.Disable();

        scopeIndex = -1;
        targetFOV  = normalFOV;
        // snap immediately on disable
        currentFOV = normalFOV;
        WriteFOV(currentFOV);
    }

    private void Update()
    {
        if (!IsConditionMet())
        {
            if (scopeIndex != -1)
            {
                scopeIndex = -1;
                targetFOV  = normalFOV;
            }
        }
        else if (scopeAction != null && scopeAction.WasPressedThisFrame())
        {
            // cycle: -1 → 0 → 1 → ... → n-1 → -1
            if (scopeFOVLevels != null && scopeFOVLevels.Count > 0)
            {
                scopeIndex = scopeIndex + 1 >= scopeFOVLevels.Count ? -1 : scopeIndex + 1;
                targetFOV  = scopeIndex == -1 ? normalFOV : scopeFOVLevels[scopeIndex];
            }
        }

        // smooth transition every frame
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * transitionSpeed);
        WriteFOV(currentFOV);
    }

    private void WriteFOV(float fov)
    {
        if (tpsAimCamera == null) return;
        tpsAimCamera.Lens.FieldOfView = fov;
    }

    private bool IsConditionMet() =>
        switchCamView != null && switchCamView.IsThirdPerson &&
        tpsView != null && tpsView.IsViewAiming &&
        IsSniperEquipped();

    private bool IsSniperEquipped() =>
        sniperShootSettings != null &&
        sniperShootSettings.enabled &&
        sniperShootSettings.gameObject.activeInHierarchy;

    private void ResolveReferences()
    {
        Transform root = transform.root;

        if (playerInput == null)
            playerInput = root.GetComponent<PlayerInput>();

        if (switchCamView == null)
            switchCamView = root.GetComponent<SwitchCamView>();

        if (tpsView == null)
            tpsView = root.GetComponent<PlayerTpsView>();
    }
}
