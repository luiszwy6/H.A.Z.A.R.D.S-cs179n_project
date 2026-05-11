using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class TpsAimPitchOveride : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerTpsView playerTpsView;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private SwitchCamView switchCamView;

    [Header("Input")]
    [SerializeField] private string aimActionName = "Aim";

    [Header("Pitch Clamp Override")]
    [SerializeField] private float overrideMinPitch = -20f;
    [SerializeField] private float overrideMaxPitch = 45f;
    [SerializeField] private bool clampCurrentPitchImmediately = true;

    [Header("Gate")]
    [SerializeField] private bool onlyWorkInThirdPersonView = true;
    [SerializeField] private bool requireRealAimInput = true;
    [SerializeField] private bool requireThisCameraActive = true;

    private InputAction aimAction;

    private FieldInfo minPitchField;
    private FieldInfo maxPitchField;
    private FieldInfo pitchField;

    private bool hasCachedOriginalClamp;
    private float originalMinPitch;
    private float originalMaxPitch;

    private bool overrideApplied;

    private void Reset()
    {
        if (playerTpsView == null)
            playerTpsView = FindObjectOfType<PlayerTpsView>();

        if (playerInput == null)
            playerInput = FindObjectOfType<PlayerInput>();

        if (switchCamView == null)
            switchCamView = FindObjectOfType<SwitchCamView>();
    }

    private void Awake()
    {
        if (playerTpsView == null)
            playerTpsView = FindObjectOfType<PlayerTpsView>();

        if (playerInput == null)
            playerInput = FindObjectOfType<PlayerInput>();

        if (switchCamView == null)
            switchCamView = FindObjectOfType<SwitchCamView>();

        CacheInputAction();
        CachePitchFields();
        CacheOriginalClamp();
    }

    private void OnEnable()
    {
        CacheInputAction();
        CachePitchFields();
        CacheOriginalClamp();
    }

    private void OnDisable()
    {
        RestoreOriginalClamp();
    }

    private void LateUpdate()
    {
        bool shouldApply = ShouldApplyOverride();

        if (shouldApply)
            ApplyPitchOverride();
        else
            RestoreOriginalClamp();
    }

    private void CacheInputAction()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        if (string.IsNullOrWhiteSpace(aimActionName))
            return;

        aimAction = playerInput.actions.FindAction(aimActionName, false);
    }

    private void CachePitchFields()
    {
        minPitchField = null;
        maxPitchField = null;
        pitchField = null;

        if (playerTpsView == null)
            return;

        System.Type type = playerTpsView.GetType();

        minPitchField = type.GetField(
            "minPitch",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        maxPitchField = type.GetField(
            "maxPitch",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        pitchField = type.GetField(
            "pitch",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
    }

    private void CacheOriginalClamp()
    {
        if (hasCachedOriginalClamp)
            return;

        if (playerTpsView == null)
            return;

        if (minPitchField == null || maxPitchField == null)
            return;

        object minValue = minPitchField.GetValue(playerTpsView);
        object maxValue = maxPitchField.GetValue(playerTpsView);

        if (minValue is float min && maxValue is float max)
        {
            originalMinPitch = min;
            originalMaxPitch = max;
            hasCachedOriginalClamp = true;
        }
    }

    private bool ShouldApplyOverride()
    {
        if (playerTpsView == null)
            return false;

        if (onlyWorkInThirdPersonView)
        {
            if (switchCamView == null)
                return false;

            if (!switchCamView.IsThirdPerson)
                return false;
        }

        if (requireRealAimInput)
        {
            if (aimAction == null)
                return false;

            if (!aimAction.IsPressed())
                return false;
        }

        if (requireThisCameraActive && !gameObject.activeInHierarchy)
            return false;

        return true;
    }

    private void ApplyPitchOverride()
    {
        if (playerTpsView == null)
            return;

        if (minPitchField == null || maxPitchField == null)
            return;

        CacheOriginalClamp();

        minPitchField.SetValue(playerTpsView, overrideMinPitch);
        maxPitchField.SetValue(playerTpsView, overrideMaxPitch);

        if (clampCurrentPitchImmediately)
            ClampCurrentPitch();

        overrideApplied = true;
    }

    private void RestoreOriginalClamp()
    {
        if (!overrideApplied)
            return;

        if (!hasCachedOriginalClamp)
            return;

        if (playerTpsView == null)
            return;

        if (minPitchField == null || maxPitchField == null)
            return;

        minPitchField.SetValue(playerTpsView, originalMinPitch);
        maxPitchField.SetValue(playerTpsView, originalMaxPitch);

        if (clampCurrentPitchImmediately)
            ClampCurrentPitch();

        overrideApplied = false;
    }

    private void ClampCurrentPitch()
    {
        if (playerTpsView == null)
            return;

        if (pitchField == null)
            return;

        object value = pitchField.GetValue(playerTpsView);

        if (value is not float currentPitch)
            return;

        float min = overrideApplied ? overrideMinPitch : originalMinPitch;
        float max = overrideApplied ? overrideMaxPitch : originalMaxPitch;

        float clampedPitch = Mathf.Clamp(currentPitch, min, max);
        pitchField.SetValue(playerTpsView, clampedPitch);
    }
}   