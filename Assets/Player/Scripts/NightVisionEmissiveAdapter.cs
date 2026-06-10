using UnityEngine;

public class NightVisionEmissiveAdapter : MonoBehaviour
{
    [Header("Material Swap")]
    [SerializeField] private Material nightVisionMaterial;
    [SerializeField] private int materialIndex = 0;

    [Header("Light")]
    [SerializeField] private bool affectLight = true;
    [SerializeField] private Color nightVisionLightColor = Color.white;

    private Renderer cachedRenderer;
    private Light[] cachedLights;

    private Material originalMaterial;
    private Color[] originalLightColors;

    private void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        cachedLights   = GetComponents<Light>();

        if (cachedRenderer != null)
        {
            Material[] shared = cachedRenderer.sharedMaterials;
            if (materialIndex < shared.Length)
                originalMaterial = shared[materialIndex];
        }

        if (affectLight && cachedLights.Length > 0)
        {
            originalLightColors = new Color[cachedLights.Length];
            for (int i = 0; i < cachedLights.Length; i++)
                originalLightColors[i] = cachedLights[i].color;
        }
    }

    private void OnEnable()
    {
        NightVisionEvents.OnNightVisionChanged += Apply;
        Apply(NightVisionEvents.IsActive);
    }

    private void OnDisable()
    {
        NightVisionEvents.OnNightVisionChanged -= Apply;
        Apply(false);
    }

    private void Apply(bool nvActive)
    {
        if (cachedRenderer != null && nightVisionMaterial != null)
        {
            Material[] mats = cachedRenderer.sharedMaterials;
            if (materialIndex < mats.Length)
            {
                mats[materialIndex] = nvActive ? nightVisionMaterial : originalMaterial;
                cachedRenderer.sharedMaterials = mats;
            }
        }

        if (affectLight && cachedLights != null && originalLightColors != null)
        {
            for (int i = 0; i < cachedLights.Length; i++)
            {
                if (cachedLights[i] == null) continue;
                cachedLights[i].color = nvActive ? nightVisionLightColor : originalLightColors[i];
            }
        }
    }
}
