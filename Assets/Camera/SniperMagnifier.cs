using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SniperMagnifierToggle : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject magnifierUIRoot;
    [SerializeField] private Camera magnifierCamera;
    [SerializeField] private PlayerAimSettings aimSettings;

    [Header("Input")]
    [SerializeField] private bool showOnlyWhileAiming = true;

    [Header("Camera Zoom")]
    [SerializeField] private bool overrideOrthographicSize = true;
    [SerializeField] private float magnifierOrthographicSize = 2f;

    private float originalOrthographicSize;
    private bool hasOriginalSize;

    private void Awake()
    {
        if (magnifierCamera != null)
        {
            originalOrthographicSize = magnifierCamera.orthographicSize;
            hasOriginalSize = true;
        }

        ApplyVisible(false);
    }

    private void Update()
    {
        bool shouldShow = true;

        if (showOnlyWhileAiming)
        {
            shouldShow =
                aimSettings != null &&
                (aimSettings.IsAimInputHeld || aimSettings.IsAiming || aimSettings.IsAimHeld);
        }

        ApplyVisible(shouldShow);
    }

    private void ApplyVisible(bool visible)
    {
        if (magnifierUIRoot != null && magnifierUIRoot.activeSelf != visible)
            magnifierUIRoot.SetActive(visible);

        if (magnifierCamera != null && magnifierCamera.gameObject.activeSelf != visible)
            magnifierCamera.gameObject.SetActive(visible);

        if (visible && magnifierCamera != null && overrideOrthographicSize)
            magnifierCamera.orthographicSize = Mathf.Max(0.01f, magnifierOrthographicSize);

        if (!visible && magnifierCamera != null && hasOriginalSize)
            magnifierCamera.orthographicSize = originalOrthographicSize;
    }
}