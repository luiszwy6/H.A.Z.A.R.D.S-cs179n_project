using UnityEngine;

public class DebugForceAim : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerAimSettings topDownAimSettings;
    [SerializeField] private PlayerTpsView tpsView;

    [Header("Force Aim")]
    [SerializeField] private bool forceTopDownAim = false;
    [SerializeField] private bool forceTpsAim = false;

    private bool lastForceTopDown;
    private bool lastForceTps;

    private void Awake()
    {
        Transform root = transform.root;
        if (topDownAimSettings == null) topDownAimSettings = root.GetComponent<PlayerAimSettings>();
        if (tpsView == null)            tpsView = root.GetComponent<PlayerTpsView>();
    }

    private void LateUpdate()
    {
        if (forceTopDownAim != lastForceTopDown)
        {
            lastForceTopDown = forceTopDownAim;
            topDownAimSettings?.SetExternalAimOverride(forceTopDownAim);
        }

        if (forceTpsAim != lastForceTps)
        {
            lastForceTps = forceTpsAim;
            tpsView?.SetExternalAimOverride(forceTpsAim);
        }
    }

    private void OnDisable()
    {
        topDownAimSettings?.SetExternalAimOverride(false);
        tpsView?.SetExternalAimOverride(false);
        lastForceTopDown = false;
        lastForceTps = false;
    }
}
