using UnityEngine;

[DisallowMultipleComponent]
public class ForceAimWhileEnabled : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerAimSettings playerAimSettings;

    [Header("Aim Override")]
    [SerializeField] private bool useRotationSpeedOverride = false;
    [SerializeField] private float rotationSpeedOverride = 18f;

    [Header("Apply Timing")]
    [SerializeField] private bool applyEveryFrame = true;

    private void Reset()
    {
        playerMovement = GetComponent<PlayerMovement>();
        playerAimSettings = GetComponent<PlayerAimSettings>();
    }

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerAimSettings == null)
            playerAimSettings = GetComponent<PlayerAimSettings>();
    }

    private void OnEnable()
    {
        ApplyAimOverride();
    }

    private void OnDisable()
    {
        ClearAimOverride();
    }

    private void Update()
    {
        if (!applyEveryFrame)
            return;

        ApplyAimOverride();
    }

    private void ApplyAimOverride()
    {
        if (playerAimSettings != null)
        {
            if (useRotationSpeedOverride)
                playerAimSettings.SetExternalAimOverride(true, rotationSpeedOverride);
            else
                playerAimSettings.SetExternalAimOverride(true);
        }

        if (playerMovement != null && playerMovement.ActiveView != null)
        {
            if (useRotationSpeedOverride)
                playerMovement.ActiveView.SetExternalAimOverride(true, rotationSpeedOverride);
            else
                playerMovement.ActiveView.SetExternalAimOverride(true);
        }
    }

    private void ClearAimOverride()
    {
        if (playerAimSettings != null)
            playerAimSettings.SetExternalAimOverride(false);

        if (playerMovement != null && playerMovement.ActiveView != null)
            playerMovement.ActiveView.SetExternalAimOverride(false);
    }
}