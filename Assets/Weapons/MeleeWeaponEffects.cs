using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class MeleeWeaponEffects : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] meleeClips;
    [Range(0f, 1f)] [SerializeField] private float meleeVolume = 1f;
    [SerializeField] private bool randomizePitch = true;
    [SerializeField] private float pitchMin = 0.95f;
    [SerializeField] private float pitchMax = 1.05f;

    [Header("Trigger Timing")]
    [SerializeField] private bool useMeleeEffectOffset = false;
    [Min(0f)] [SerializeField] private float meleeEffectOffset = 0f;
    [SerializeField] private bool restartOffsetRoutineWhenTriggered = true;

    [Header("VFX - Particle Systems")]
    [SerializeField] private ParticleSystem[] meleeVfxParticles;
    [SerializeField] private bool restartParticlesWhenTriggered = true;
    [SerializeField] private bool stopParticlesAfterDuration = true;
    [SerializeField] private ParticleSystemStopBehavior particleStopBehavior =
        ParticleSystemStopBehavior.StopEmittingAndClear;

    [Header("VFX - GameObjects")]
    [SerializeField] private GameObject[] meleeVfxObjects;
    [SerializeField] private bool setVfxObjectsActiveOnTrigger = true;
    [SerializeField] private bool disableVfxObjectsAfterDuration = true;

    [Header("VFX Duration")]
    [Min(0f)] [SerializeField] private float vfxDuration = 0.25f;

    private Coroutine vfxRoutine;
    private Coroutine meleeEffectOffsetRoutine;

    private void Reset()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = GetComponentInParent<AudioSource>();
    }

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = GetComponentInParent<AudioSource>();

        SetVfxObjectsActive(false);
    }

    private void OnDisable()
    {
        StopMeleeEffectOffsetRoutineOnly();
        StopVfxRoutineOnly();
        StopVfx();
    }

    public void PlayMeleeEffectFromMeleeInput()
    {
        if (!useMeleeEffectOffset || meleeEffectOffset <= 0f)
        {
            PlayMeleeEffect();
            return;
        }

        if (meleeEffectOffsetRoutine != null)
        {
            if (!restartOffsetRoutineWhenTriggered)
                return;

            StopMeleeEffectOffsetRoutineOnly();
        }

        meleeEffectOffsetRoutine = StartCoroutine(MeleeEffectOffsetRoutine());
    }

    public void PlayMeleeEffect()
    {
        PlayMeleeAudio();
        PlayMeleeVfx();
    }

    public void PlayMeleeAudio()
    {
        if (audioSource == null)
            return;

        AudioClip clip = GetRandomMeleeClip();

        if (clip == null)
            return;

        float oldPitch = audioSource.pitch;

        if (randomizePitch)
        {
            float lo = Mathf.Min(pitchMin, pitchMax);
            float hi = Mathf.Max(pitchMin, pitchMax);
            audioSource.pitch = Random.Range(lo, hi);
        }

        audioSource.PlayOneShot(clip, meleeVolume);
        audioSource.pitch = oldPitch;
    }

    public void PlayMeleeVfx()
    {
        StopVfxRoutineOnly();

        PlayParticleVfx();

        if (setVfxObjectsActiveOnTrigger)
            SetVfxObjectsActive(true);

        if (vfxDuration > 0f &&
            (stopParticlesAfterDuration || disableVfxObjectsAfterDuration))
        {
            vfxRoutine = StartCoroutine(VfxDurationRoutine());
        }
    }

    public void StopVfx()
    {
        StopParticleVfx();

        if (disableVfxObjectsAfterDuration)
            SetVfxObjectsActive(false);
    }

    private IEnumerator MeleeEffectOffsetRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, meleeEffectOffset));

        PlayMeleeEffect();
        meleeEffectOffsetRoutine = null;
    }

    private AudioClip GetRandomMeleeClip()
    {
        if (meleeClips == null || meleeClips.Length == 0)
            return null;

        int index = Random.Range(0, meleeClips.Length);
        return meleeClips[index];
    }

    private void PlayParticleVfx()
    {
        if (meleeVfxParticles == null)
            return;

        for (int i = 0; i < meleeVfxParticles.Length; i++)
        {
            ParticleSystem particle = meleeVfxParticles[i];

            if (particle == null)
                continue;

            if (restartParticlesWhenTriggered)
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            particle.Play(true);
        }
    }

    private void StopParticleVfx()
    {
        if (!stopParticlesAfterDuration)
            return;

        if (meleeVfxParticles == null)
            return;

        for (int i = 0; i < meleeVfxParticles.Length; i++)
        {
            ParticleSystem particle = meleeVfxParticles[i];

            if (particle == null)
                continue;

            particle.Stop(true, particleStopBehavior);
        }
    }

    private void SetVfxObjectsActive(bool active)
    {
        if (meleeVfxObjects == null)
            return;

        for (int i = 0; i < meleeVfxObjects.Length; i++)
        {
            GameObject obj = meleeVfxObjects[i];

            if (obj == null)
                continue;

            obj.SetActive(active);
        }
    }

    private IEnumerator VfxDurationRoutine()
    {
        yield return new WaitForSeconds(vfxDuration);

        StopVfx();
        vfxRoutine = null;
    }

    private void StopVfxRoutineOnly()
    {
        if (vfxRoutine != null)
        {
            StopCoroutine(vfxRoutine);
            vfxRoutine = null;
        }
    }

    private void StopMeleeEffectOffsetRoutineOnly()
    {
        if (meleeEffectOffsetRoutine != null)
        {
            StopCoroutine(meleeEffectOffsetRoutine);
            meleeEffectOffsetRoutine = null;
        }
    }
}