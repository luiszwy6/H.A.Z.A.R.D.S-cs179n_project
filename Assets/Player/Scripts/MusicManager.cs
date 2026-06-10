using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class MusicManager : MonoBehaviour
{
    [Header("Generator Reference")]
    [SerializeField] private EnemySquadGenerator squadGenerator;

    [Header("Clips")]
    [SerializeField] private AudioClip explorationMusic;
    [SerializeField] private AudioClip combatMusic;
    [FormerlySerializedAs("specialCombatMusic")]
    [SerializeField] private AudioClip bulletTimeMusic;
    [SerializeField] private AudioClip specialAbilityMusic;

    [Header("Ability References")]
    [SerializeField] private BulletTimeAbility bulletTimeAbility;
    [SerializeField] private AR_SpecialAbility arSpecialAbility;
    [SerializeField] private SG_SpecialAbility sgSpecialAbility;

    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] private float explorationVolume = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float combatVolume = 1f;
    [FormerlySerializedAs("specialCombatVolume")]
    [Range(0f, 1f)] [SerializeField] private float bulletTimeVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float specialAbilityVolume = 1f;

    [Header("Crossfade")]
    [Min(0f)] [SerializeField] private float crossfadeDuration = 1.5f;
    [SerializeField] private AudioClip transitionClip;
    [Range(0f, 1f)] [SerializeField] private float transitionVolume = 1f;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource bulletTimeMusicSource;
    private AudioSource specialAbilityMusicSource;
    private AudioSource transitionSource;
    private Coroutine crossfadeRoutine;
    private bool combatActive;

    private void Awake()
    {
        if (bulletTimeAbility == null)
            bulletTimeAbility = FindFirstSceneObject<BulletTimeAbility>();

        if (arSpecialAbility == null)
            arSpecialAbility = FindFirstSceneObject<AR_SpecialAbility>();

        if (sgSpecialAbility == null)
            sgSpecialAbility = FindFirstSceneObject<SG_SpecialAbility>();

        sourceA = gameObject.AddComponent<AudioSource>();
        sourceA.loop = true;
        sourceA.playOnAwake = false;
        sourceA.ignoreListenerPause = true;

        sourceB = gameObject.AddComponent<AudioSource>();
        sourceB.loop = true;
        sourceB.playOnAwake = false;
        sourceB.ignoreListenerPause = true;

        bulletTimeMusicSource = gameObject.AddComponent<AudioSource>();
        bulletTimeMusicSource.loop = true;
        bulletTimeMusicSource.playOnAwake = false;
        bulletTimeMusicSource.ignoreListenerPause = true;

        specialAbilityMusicSource = gameObject.AddComponent<AudioSource>();
        specialAbilityMusicSource.loop = true;
        specialAbilityMusicSource.playOnAwake = false;
        specialAbilityMusicSource.ignoreListenerPause = true;

        transitionSource = gameObject.AddComponent<AudioSource>();
        transitionSource.loop = false;
        transitionSource.playOnAwake = false;
        transitionSource.ignoreListenerPause = true;
    }

    private void OnEnable()
    {
        if (squadGenerator != null)
            squadGenerator.OnStartedGenerating += OnCombatStarted;
    }

    private void OnDisable()
    {
        if (squadGenerator != null)
            squadGenerator.OnStartedGenerating -= OnCombatStarted;
    }

    private void Start()
    {
        combatActive = false;
        PlayImmediate(sourceA, explorationMusic, explorationVolume);
    }

    private void Update()
    {
        if (!combatActive)
            return;

        UpdateCombatLayerVolumes();
    }

    private void OnCombatStarted()
    {
        CrossfadeToCombat(sourceA);
    }

    private void PlayImmediate(AudioSource source, AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        source.clip = clip;
        source.volume = volume;
        source.Play();
    }

    private void CrossfadeToCombat(AudioSource outgoing)
    {
        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        crossfadeRoutine = StartCoroutine(CrossfadeToCombatRoutine(outgoing));
    }

    private IEnumerator CrossfadeToCombatRoutine(AudioSource outgoing)
    {
        float duration = Mathf.Max(0.01f, crossfadeDuration);
        float transitionDelay = transitionClip != null
            ? Mathf.Max(0f, duration - transitionClip.length)
            : float.MaxValue;

        float startOutVolume = outgoing.volume;
        float elapsed = 0f;
        bool transitionPlayed = false;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            outgoing.volume = Mathf.Lerp(startOutVolume, 0f, t);

            if (!transitionPlayed && elapsed >= transitionDelay)
            {
                transitionSource.PlayOneShot(transitionClip, transitionVolume);
                transitionPlayed = true;
            }

            yield return null;
        }

        outgoing.Stop();
        outgoing.clip = null;

        combatActive = true;

        bool useBulletTimeMusic = IsBulletTimeMusicRequested();
        bool useSpecialAbilityMusic = IsSpecialAbilityMusicRequested() && !useBulletTimeMusic;
        bool useSpecialMusic = useBulletTimeMusic || useSpecialAbilityMusic;

        if (combatMusic != null)
        {
            sourceB.clip = combatMusic;
            sourceB.volume = useSpecialMusic ? 0f : combatVolume;
            sourceB.Play();
        }

        if (bulletTimeMusic != null)
        {
            bulletTimeMusicSource.clip = bulletTimeMusic;
            bulletTimeMusicSource.volume = useBulletTimeMusic ? bulletTimeVolume : 0f;
            bulletTimeMusicSource.Play();
        }

        if (specialAbilityMusic != null)
        {
            specialAbilityMusicSource.clip = specialAbilityMusic;
            specialAbilityMusicSource.volume = useSpecialAbilityMusic ? specialAbilityVolume : 0f;
            specialAbilityMusicSource.Play();
        }

        crossfadeRoutine = null;
    }

    private void UpdateCombatLayerVolumes()
    {
        bool useBulletTimeMusic = IsBulletTimeMusicRequested();
        bool useSpecialAbilityMusic = IsSpecialAbilityMusicRequested() && !useBulletTimeMusic;
        bool useSpecialMusic = useBulletTimeMusic || useSpecialAbilityMusic;

        if (sourceB.isPlaying)
            sourceB.volume = useSpecialMusic ? 0f : combatVolume;

        if (bulletTimeMusicSource.isPlaying)
            bulletTimeMusicSource.volume = useBulletTimeMusic ? bulletTimeVolume : 0f;

        if (specialAbilityMusicSource.isPlaying)
            specialAbilityMusicSource.volume = useSpecialAbilityMusic ? specialAbilityVolume : 0f;
    }

    private bool IsBulletTimeMusicRequested()
    {
        return bulletTimeAbility != null && bulletTimeAbility.IsActive;
    }

    private bool IsSpecialAbilityMusicRequested()
    {
        return
            arSpecialAbility != null && arSpecialAbility.IsActive ||
            sgSpecialAbility != null && sgSpecialAbility.IsActive;
    }

    private static T FindFirstSceneObject<T>() where T : Object
    {
        T[] objects = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return objects.Length > 0 ? objects[0] : null;
    }
}
