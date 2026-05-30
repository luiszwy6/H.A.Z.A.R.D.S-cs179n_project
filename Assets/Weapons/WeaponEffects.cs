using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponEffects : MonoBehaviour
{
    [System.Serializable]
    private class WeightedGunshotClip
    {
        public AudioClip clip;

        [Min(0f)]
        public float weight = 1f;

        [Range(0f, 2f)]
        public float volumeMultiplier = 1f;
    }

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Fallback clip. Used only if Gunshot Clips list has no valid clips.")]
    [SerializeField] private AudioClip gunshotClip;

    [SerializeField] private List<WeightedGunshotClip> gunshotClips = new List<WeightedGunshotClip>();

    [Range(0f, 1f)]
    [SerializeField] private float gunshotVolume = 1f;

    [SerializeField] private bool randomizePitch = true;
    [SerializeField] private float pitchMin = 0.95f;
    [SerializeField] private float pitchMax = 1.05f;

    [Header("VFX")]
    [SerializeField] private ParticleSystem muzzleFlash;

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

        if (muzzleFlash != null)
        {
            var main = muzzleFlash.main;
            main.useUnscaledTime = true;
        }
    }

    public void PlayGunshot()
    {
        if (muzzleFlash != null)
        {
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlash.Play(true);
        }

        PlayRandomGunshotAudio();
    }

    private void PlayRandomGunshotAudio()
    {
        if (audioSource == null)
            return;

        AudioClip selectedClip = null;
        float selectedVolumeMultiplier = 1f;

        if (TryGetRandomGunshotClip(out WeightedGunshotClip selectedEntry))
        {
            selectedClip = selectedEntry.clip;
            selectedVolumeMultiplier = selectedEntry.volumeMultiplier;
        }
        else
        {
            selectedClip = gunshotClip;
            selectedVolumeMultiplier = 1f;
        }

        if (selectedClip == null)
            return;

        float oldPitch = audioSource.pitch;

        if (randomizePitch)
        {
            float lo = Mathf.Min(pitchMin, pitchMax);
            float hi = Mathf.Max(pitchMin, pitchMax);
            audioSource.pitch = Random.Range(lo, hi);
        }

        float finalVolume = gunshotVolume * Mathf.Max(0f, selectedVolumeMultiplier);
        audioSource.PlayOneShot(selectedClip, finalVolume);

        audioSource.pitch = oldPitch;
    }

    private bool TryGetRandomGunshotClip(out WeightedGunshotClip selectedEntry)
    {
        selectedEntry = null;

        if (gunshotClips == null || gunshotClips.Count == 0)
            return false;

        float totalWeight = 0f;

        for (int i = 0; i < gunshotClips.Count; i++)
        {
            WeightedGunshotClip entry = gunshotClips[i];

            if (entry == null)
                continue;

            if (entry.clip == null)
                continue;

            if (entry.weight <= 0f)
                continue;

            totalWeight += entry.weight;
        }

        if (totalWeight <= 0f)
            return false;

        float randomValue = Random.Range(0f, totalWeight);
        float runningWeight = 0f;

        for (int i = 0; i < gunshotClips.Count; i++)
        {
            WeightedGunshotClip entry = gunshotClips[i];

            if (entry == null)
                continue;

            if (entry.clip == null)
                continue;

            if (entry.weight <= 0f)
                continue;

            runningWeight += entry.weight;

            if (randomValue <= runningWeight)
            {
                selectedEntry = entry;
                return true;
            }
        }

        return false;
    }
}