using UnityEngine;

[DisallowMultipleComponent]
public class SpecialAbilitySound : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AR_SpecialAbility arSpecialAbility;
    [SerializeField] private SG_SpecialAbility sgSpecialAbility;
    [SerializeField] private SRSpecialAbility  srSpecialAbility;

    [Header("Clips")]
    [SerializeField] private bool playEnterClip = true;
    [SerializeField] private AudioClip enterClip;
    [SerializeField] private bool playExitClip = true;
    [SerializeField] private AudioClip exitClip;

    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] private float enterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float exitVolume = 1f;

    private AudioSource loopSource;
    private AudioSource oneShotSource;
    private bool wasActive;

    private void Awake()
    {
        if (arSpecialAbility == null)
            arSpecialAbility = FindFirstObjectByType<AR_SpecialAbility>();

        if (sgSpecialAbility == null)
            sgSpecialAbility = FindFirstObjectByType<SG_SpecialAbility>();

        if (srSpecialAbility == null)
            srSpecialAbility = FindFirstObjectByType<SRSpecialAbility>();

        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.loop = true;
        loopSource.playOnAwake = false;
        loopSource.ignoreListenerPause = true;

        oneShotSource = gameObject.AddComponent<AudioSource>();
        oneShotSource.loop = false;
        oneShotSource.playOnAwake = false;
        oneShotSource.ignoreListenerPause = true;
    }

    private void Update()
    {
        bool isActive = IsSpecialAbilityActive();

        if (isActive && !wasActive)
            OnEnterSpecialAbility();
        else if (!isActive && wasActive)
            OnExitSpecialAbility();

        wasActive = isActive;
    }

    private void OnEnterSpecialAbility()
    {
        if (!playEnterClip || enterClip == null)
            return;

        loopSource.clip = enterClip;
        loopSource.volume = enterVolume;
        loopSource.Play();
    }

    private void OnExitSpecialAbility()
    {
        loopSource.Stop();

        if (!playExitClip || exitClip == null)
            return;

        oneShotSource.PlayOneShot(exitClip, exitVolume);
    }

    private bool IsSpecialAbilityActive()
    {
        return
            arSpecialAbility != null && arSpecialAbility.IsActive ||
            sgSpecialAbility != null && sgSpecialAbility.IsActive ||
            srSpecialAbility != null && srSpecialAbility.IsActive;
    }
}
