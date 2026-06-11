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
    [SerializeField] private AudioClip waveClearMusic;

    [Header("Ability References")]
    [SerializeField] private BulletTimeAbility bulletTimeAbility;
    [SerializeField] private AR_SpecialAbility arSpecialAbility;
    [SerializeField] private SG_SpecialAbility sgSpecialAbility;
    [SerializeField] private SRSpecialAbility  srSpecialAbility;

    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] private float explorationVolume    = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float combatVolume         = 1f;
    [FormerlySerializedAs("specialCombatVolume")]
    [Range(0f, 1f)] [SerializeField] private float bulletTimeVolume     = 1f;
    [Range(0f, 1f)] [SerializeField] private float specialAbilityVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float waveClearVolume      = 1f;

    [Header("Crossfade")]
    [Min(0f)] [SerializeField] private float crossfadeDuration = 1.5f;
    [SerializeField] private AudioClip transitionClip;
    [Range(0f, 1f)] [SerializeField] private float transitionVolume = 1f;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource bulletTimeMusicSource;
    private AudioSource specialAbilityMusicSource;
    private AudioSource waveClearMusicSource;
    private AudioSource transitionSource;
    private Coroutine crossfadeRoutine;
    private bool combatActive;
    private bool waveClearActive;

    private void Awake()
    {
        if (bulletTimeAbility == null)
            bulletTimeAbility = FindFirstSceneObject<BulletTimeAbility>();

        if (arSpecialAbility == null)
            arSpecialAbility = FindFirstSceneObject<AR_SpecialAbility>();

        if (sgSpecialAbility == null)
            sgSpecialAbility = FindFirstSceneObject<SG_SpecialAbility>();

        if (srSpecialAbility == null)
            srSpecialAbility = FindFirstSceneObject<SRSpecialAbility>();

        sourceA = CreateSource(loop: true);
        sourceB = CreateSource(loop: true);
        bulletTimeMusicSource    = CreateSource(loop: true);
        specialAbilityMusicSource = CreateSource(loop: true);
        waveClearMusicSource     = CreateSource(loop: false);
        transitionSource         = CreateSource(loop: false);
    }

    private AudioSource CreateSource(bool loop)
    {
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.loop = loop;
        src.playOnAwake = false;
        src.ignoreListenerPause = true;
        return src;
    }

    private void OnEnable()
    {
        if (squadGenerator == null)
            return;

        squadGenerator.OnStartedGenerating += OnCombatStarted;
        squadGenerator.OnWaveCleared       += OnWaveCleared;
        squadGenerator.OnWaveSpawned       += OnWaveSpawned;
    }

    private void OnDisable()
    {
        if (squadGenerator == null)
            return;

        squadGenerator.OnStartedGenerating -= OnCombatStarted;
        squadGenerator.OnWaveCleared       -= OnWaveCleared;
        squadGenerator.OnWaveSpawned       -= OnWaveSpawned;
    }

    private void Start()
    {
        combatActive = false;
        PlayImmediate(sourceA, explorationMusic, explorationVolume);
    }

    private void Update()
    {
        if (!combatActive || waveClearActive)
            return;

        UpdateCombatLayerVolumes();
    }

    // ── Combat start ─────────────────────────────────────────────

    private void OnCombatStarted()
    {
        CrossfadeToCombat(sourceA);
    }

    // ── Wave clear / next wave ────────────────────────────────────

    private void OnWaveCleared(int _, float __)
    {
        if (!combatActive)
            return;

        waveClearActive = true;

        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        crossfadeRoutine = StartCoroutine(CrossfadeToWaveClearRoutine());
    }

    private void OnWaveSpawned(int _)
    {
        if (!combatActive || !waveClearActive)
            return;

        waveClearActive = false;

        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        crossfadeRoutine = StartCoroutine(CrossfadeBackToCombatRoutine());
    }

    // ── Playback helpers ─────────────────────────────────────────

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

    // ── Coroutines ───────────────────────────────────────────────

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

        bool useBulletTime     = IsBulletTimeMusicRequested();
        bool useSpecialAbility = IsSpecialAbilityMusicRequested() && !useBulletTime;
        bool useSpecial        = useBulletTime || useSpecialAbility;

        if (combatMusic != null)
        {
            sourceB.clip   = combatMusic;
            sourceB.volume = useSpecial ? 0f : combatVolume;
            sourceB.Play();
        }

        if (bulletTimeMusic != null)
        {
            bulletTimeMusicSource.clip   = bulletTimeMusic;
            bulletTimeMusicSource.volume = useBulletTime ? bulletTimeVolume : 0f;
            bulletTimeMusicSource.Play();
        }

        if (specialAbilityMusic != null)
        {
            specialAbilityMusicSource.clip   = specialAbilityMusic;
            specialAbilityMusicSource.volume = useSpecialAbility ? specialAbilityVolume : 0f;
            specialAbilityMusicSource.Play();
        }

        crossfadeRoutine = null;
    }

    private IEnumerator CrossfadeToWaveClearRoutine()
    {
        float duration = Mathf.Max(0.01f, crossfadeDuration);
        float elapsed  = 0f;

        float startCombatVol   = sourceB.volume;
        float startBulletVol   = bulletTimeMusicSource.volume;
        float startSpecialVol  = specialAbilityMusicSource.volume;

        if (waveClearMusic != null)
        {
            waveClearMusicSource.clip   = waveClearMusic;
            waveClearMusicSource.volume = 0f;
            waveClearMusicSource.Play();
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            sourceB.volume                = Mathf.Lerp(startCombatVol,  0f, t);
            bulletTimeMusicSource.volume  = Mathf.Lerp(startBulletVol,  0f, t);
            specialAbilityMusicSource.volume = Mathf.Lerp(startSpecialVol, 0f, t);

            if (waveClearMusicSource.isPlaying)
                waveClearMusicSource.volume = Mathf.Lerp(0f, waveClearVolume, t);

            yield return null;
        }

        sourceB.volume                   = 0f;
        bulletTimeMusicSource.volume     = 0f;
        specialAbilityMusicSource.volume = 0f;

        crossfadeRoutine = null;
    }

    private IEnumerator CrossfadeBackToCombatRoutine()
    {
        float duration = Mathf.Max(0.01f, crossfadeDuration);
        float elapsed  = 0f;

        float startWaveClearVol = waveClearMusicSource.volume;

        bool useBulletTime     = IsBulletTimeMusicRequested();
        bool useSpecialAbility = IsSpecialAbilityMusicRequested() && !useBulletTime;
        bool useSpecial        = useBulletTime || useSpecialAbility;

        float targetCombatVol  = useSpecial ? 0f : combatVolume;
        float targetBulletVol  = useBulletTime ? bulletTimeVolume : 0f;
        float targetSpecialVol = useSpecialAbility ? specialAbilityVolume : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            waveClearMusicSource.volume      = Mathf.Lerp(startWaveClearVol, 0f, t);
            sourceB.volume                   = Mathf.Lerp(0f, targetCombatVol,  t);
            bulletTimeMusicSource.volume     = Mathf.Lerp(0f, targetBulletVol,  t);
            specialAbilityMusicSource.volume = Mathf.Lerp(0f, targetSpecialVol, t);

            yield return null;
        }

        waveClearMusicSource.Stop();
        waveClearMusicSource.clip = null;

        crossfadeRoutine = null;
    }

    // ── Volume layer logic ────────────────────────────────────────

    private void UpdateCombatLayerVolumes()
    {
        bool useBulletTime     = IsBulletTimeMusicRequested();
        bool useSpecialAbility = IsSpecialAbilityMusicRequested() && !useBulletTime;
        bool useSpecial        = useBulletTime || useSpecialAbility;

        if (sourceB.isPlaying)
            sourceB.volume = useSpecial ? 0f : combatVolume;

        if (bulletTimeMusicSource.isPlaying)
            bulletTimeMusicSource.volume = useBulletTime ? bulletTimeVolume : 0f;

        if (specialAbilityMusicSource.isPlaying)
            specialAbilityMusicSource.volume = useSpecialAbility ? specialAbilityVolume : 0f;
    }

    private bool IsBulletTimeMusicRequested()
    {
        return bulletTimeAbility != null && bulletTimeAbility.IsActive;
    }

    private bool IsSpecialAbilityMusicRequested()
    {
        return
            arSpecialAbility != null && arSpecialAbility.IsActive ||
            sgSpecialAbility != null && sgSpecialAbility.IsActive ||
            srSpecialAbility != null && srSpecialAbility.IsActive;
    }

    private static T FindFirstSceneObject<T>() where T : Object
    {
        T[] objects = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return objects.Length > 0 ? objects[0] : null;
    }
}
