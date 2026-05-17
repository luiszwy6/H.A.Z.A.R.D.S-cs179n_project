using UnityEngine;

[DisallowMultipleComponent]
public class EffectLightController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ParticleSystem particle;
    [SerializeField] private Light targetLight;

    [Header("Light Timing")]
    [SerializeField] private bool enableOnPlay = true;
    [SerializeField] private bool disableWhenParticleStopped = true;
    [SerializeField] private float lightDuration = 0.12f;

    [Header("Intensity")]
    [SerializeField] private float startIntensity = 80f;
    [SerializeField] private bool fadeOut = true;

    [Header("Range")]
    [SerializeField] private bool overrideRange = true;
    [SerializeField] private float startRange = 20f;
    [SerializeField] private bool fadeRangeOut = false;

    [Header("Debug")]
    [SerializeField] private bool logState = false;

    private float timer;
    private bool lightActive;

    private float cachedOriginalIntensity;
    private float cachedOriginalRange;
    private bool cachedOriginalValues;

    private void Reset()
    {
        particle = GetComponentInChildren<ParticleSystem>(true);
        targetLight = GetComponentInChildren<Light>(true);
    }

    private void Awake()
    {
        ResolveReferences();
        CacheOriginalValues();

        if (targetLight != null)
            targetLight.enabled = false;
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (enableOnPlay)
            ActivateLight();
    }

    private void Update()
    {
        if (targetLight == null)
            return;

        if (lightActive)
        {
            timer -= Time.deltaTime;

            float t = lightDuration > 0f
                ? Mathf.Clamp01(timer / lightDuration)
                : 0f;

            if (fadeOut)
                targetLight.intensity = startIntensity * t;

            if (overrideRange && fadeRangeOut)
                targetLight.range = startRange * t;

            if (timer <= 0f)
                DisableLight();
        }

        if (disableWhenParticleStopped && particle != null)
        {
            if (!particle.IsAlive(true) && !lightActive)
                targetLight.enabled = false;
        }
    }

    public void ActivateLight()
    {
        ResolveReferences();

        if (targetLight == null)
            return;

        timer = Mathf.Max(0.01f, lightDuration);
        lightActive = true;

        targetLight.enabled = true;
        targetLight.intensity = startIntensity;

        if (overrideRange)
            targetLight.range = startRange;

        if (logState)
        {
            Debug.Log(
                $"[EffectLightController] Activate Light. Intensity={targetLight.intensity}, Range={targetLight.range}, Object={name}",
                this
            );
        }
    }

    public void DisableLight()
    {
        if (targetLight == null)
            return;

        lightActive = false;
        targetLight.enabled = false;

        if (cachedOriginalValues)
        {
            targetLight.intensity = cachedOriginalIntensity;
            targetLight.range = cachedOriginalRange;
        }
    }

    private void ResolveReferences()
    {
        if (particle == null)
            particle = GetComponentInChildren<ParticleSystem>(true);

        if (targetLight == null)
            targetLight = GetComponentInChildren<Light>(true);
    }

    private void CacheOriginalValues()
    {
        if (cachedOriginalValues)
            return;

        if (targetLight == null)
            return;

        cachedOriginalIntensity = targetLight.intensity;
        cachedOriginalRange = targetLight.range;
        cachedOriginalValues = true;
    }
}