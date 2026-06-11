using UnityEngine;

/// <summary>
/// Drop on any GameObject in the scene. Forces a specific camera view on scene load,
/// overriding whatever startMode is set on SwitchCamView.
/// </summary>
public class SceneStartViewMode : MonoBehaviour
{
    [SerializeField] private SwitchCamView switchCamView;
    [SerializeField] private SwitchCamView.CameraViewMode viewMode = SwitchCamView.CameraViewMode.ThirdPerson;

    private void Awake()
    {
        if (switchCamView == null)
            switchCamView = FindFirstObjectByType<SwitchCamView>();
    }

    private void Start()
    {
        switchCamView?.SetViewMode(viewMode);
    }
}
