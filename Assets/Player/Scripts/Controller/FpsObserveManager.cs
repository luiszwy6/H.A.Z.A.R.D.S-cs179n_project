using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class FpsObserveManager : MonoBehaviour
{
    [SerializeField] private SwitchCamView switchCamView;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Input Action Names")]
    [SerializeField] private string observeActionName = "Observe";
    [SerializeField] private string moveActionName    = "Move";
    [SerializeField] private string shootActionName   = "Shoot";

    [SerializeField] private float moveInputThreshold = 0.1f;

    private PlayerInput playerInput;
    private InputAction observeAction;
    private InputAction moveAction;
    private InputAction shootAction;

    private bool _isObserving;
    private SwitchCamView.CameraViewMode _storedMode;

    private void Awake()
    {
        playerInput = GetComponentInChildren<PlayerInput>(true);
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        if (switchCamView == null)
            switchCamView = GetComponent<SwitchCamView>();
        CacheInputActions();
    }

    private void Update()
    {
        if (switchCamView == null) return;

        bool observeHeld = observeAction != null && observeAction.IsPressed();
        bool isMoving    = moveAction    != null && moveAction.ReadValue<Vector2>().sqrMagnitude > moveInputThreshold * moveInputThreshold;
        bool isShooting  = shootAction   != null && shootAction.IsPressed();

        bool shouldObserve = observeHeld && !isMoving && !isShooting;

        if (shouldObserve && !_isObserving)
        {
            _storedMode = switchCamView.CurrentMode;
            switchCamView.SetViewMode(SwitchCamView.CameraViewMode.FirstPerson, false);
            _isObserving = true;
        }
        else if (!shouldObserve && _isObserving)
        {
            switchCamView.SetViewMode(_storedMode, false);
            _isObserving = false;
        }
    }

    private void CacheInputActions()
    {
        if (playerInput == null || playerInput.actions == null) return;
        observeAction = playerInput.actions.FindAction(observeActionName, false);
        moveAction    = playerInput.actions.FindAction(moveActionName,    false);
        shootAction   = playerInput.actions.FindAction(shootActionName,   false);
    }
}
