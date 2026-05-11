using UnityEngine;

[DisallowMultipleComponent]
public class PlayerFpsFacingDriver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SwitchCamView switchCamView;
    [SerializeField] private PlayerFpsView fpsView;
    [SerializeField] private Transform actorRoot;

    [Header("Facing")]
    [SerializeField] private bool onlyWorkInFirstPerson = true;
    [SerializeField] private bool useSmoothRotation = false;
    [SerializeField] private float rotationSpeed = 30f;

    private void Reset()
    {
        switchCamView = GetComponent<SwitchCamView>();
        fpsView = GetComponent<PlayerFpsView>();
        actorRoot = transform;
    }

    private void Awake()
    {
        if (switchCamView == null)
            switchCamView = GetComponent<SwitchCamView>();

        if (fpsView == null)
            fpsView = GetComponent<PlayerFpsView>();

        if (actorRoot == null)
            actorRoot = transform;
    }

    private void LateUpdate()
    {
        if (actorRoot == null || fpsView == null)
            return;

        if (onlyWorkInFirstPerson)
        {
            if (switchCamView == null)
                return;

            if (!switchCamView.IsFirstPerson)
                return;
        }

        Quaternion targetRotation = Quaternion.Euler(0f, fpsView.Yaw, 0f);

        if (!useSmoothRotation)
        {
            actorRoot.rotation = targetRotation;
            return;
        }

        actorRoot.rotation = Quaternion.Slerp(
            actorRoot.rotation,
            targetRotation,
            1f - Mathf.Exp(-rotationSpeed * Time.deltaTime)
        );
    }
}