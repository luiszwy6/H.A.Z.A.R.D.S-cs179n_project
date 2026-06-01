using UnityEngine;

[DisallowMultipleComponent]
public class BulletTimeSound : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BulletTimeAbility bulletTimeAbility;

    [Header("Clips")]
    [SerializeField] private AudioClip enterClip;
    [SerializeField] private AudioClip exitClip;

    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] private float enterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float exitVolume = 1f;

    private AudioSource loopSource;
    private AudioSource oneShotSource;
    private bool wasActive;

    private void Awake()
    {
        if (bulletTimeAbility == null)
            bulletTimeAbility = FindFirstObjectByType<BulletTimeAbility>();

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
        if (bulletTimeAbility == null)
            return;

        bool isActive = bulletTimeAbility.IsActive;

        if (isActive && !wasActive)
            OnEnterBulletTime();
        else if (!isActive && wasActive)
            OnExitBulletTime();

        wasActive = isActive;
    }

    private void OnEnterBulletTime()
    {
        if (enterClip == null)
            return;

        loopSource.clip = enterClip;
        loopSource.volume = enterVolume;
        loopSource.Play();
    }

    private void OnExitBulletTime()
    {
        loopSource.Stop();

        if (exitClip == null)
            return;

        oneShotSource.PlayOneShot(exitClip, exitVolume);
    }
}
