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
    [SerializeField] private AudioClip bossBattleMusic;
    [SerializeField] private AudioClip bossBattleLoopMusic;
    [FormerlySerializedAs("bossPhaseTwoMusic")]
    [SerializeField] private AudioClip bossPhaseLoopMusic;

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
    [Range(0f, 1f)] [SerializeField] private float bossBattleVolume     = 1f;
    [Range(0f, 1f)] [SerializeField] private float bossBattleLoopVolume = 1f;
    [FormerlySerializedAs("bossPhaseTwoVolume")]
    [Range(0f, 1f)] [SerializeField] private float bossPhaseLoopVolume = 1f;

    [Header("Crossfade")]
    [Min(0f)] [SerializeField] private float crossfadeDuration = 1.5f;
    [SerializeField] private AudioClip transitionClip;
    [Range(0f, 1f)] [SerializeField] private float transitionVolume = 1f;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource bulletTimeMusicSource;
    private AudioSource specialAbilityMusicSource;
    private AudioSource waveClearMusicSource;
    private AudioSource bossMusicSource;   // intro, plays once
    private AudioSource bossLoopSource;    // phase 1 loop
    private AudioSource bossPhase2MusicSource; // phase 2 loop
    private AudioSource transitionSource;
    private Coroutine crossfadeRoutine;
    private bool combatActive;
    private bool waveClearActive;
    private bool bossActive;

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

        sourceA                   = CreateSource(loop: true);
        sourceB                   = CreateSource(loop: true);
        bulletTimeMusicSource     = CreateSource(loop: true);
        specialAbilityMusicSource = CreateSource(loop: true);
        waveClearMusicSource      = CreateSource(loop: false);
        bossMusicSource           = CreateSource(loop: false); // intro, one-shot
        bossLoopSource            = CreateSource(loop: true);
        bossPhase2MusicSource     = CreateSource(loop: true);
        transitionSource          = CreateSource(loop: false);
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
        if (!combatActive || waveClearActive || bossActive)
            return;

        UpdateCombatLayerVolumes();
    }

    // ── Boss music ────────────────────────────────────────────────

    public void StartBossMusic()
    {
        if (bossActive) return;
        bossActive = true;
        combatActive = true;

        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        crossfadeRoutine = StartCoroutine(CrossfadeToBossRoutine());
    }

    public void StartBossPhaseTwoMusic()
    {
        if (bossPhaseLoopMusic == null) return;

        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        crossfadeRoutine = StartCoroutine(CrossfadeToBossPhase2Routine());
    }

    public void PlayBossDefeatedMusic()
    {
        bossActive = false;

        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        crossfadeRoutine = StartCoroutine(FadeOutBossRoutine());
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

    private IEnumerator CrossfadeToBossRoutine()
    {
        float duration = Mathf.Max(0.01f, crossfadeDuration);
        float elapsed  = 0f;

        // ── Step 1: fade out all current music, fade in boss intro ──
        float startA      = sourceA.volume;
        float startB      = sourceB.volume;
        float startBullet = bulletTimeMusicSource.volume;
        float startSA     = specialAbilityMusicSource.volume;

        if (bossBattleMusic != null)
        {
            bossMusicSource.clip   = bossBattleMusic;
            bossMusicSource.volume = 0f;
            bossMusicSource.Play();
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            sourceA.volume                   = Mathf.Lerp(startA,      0f, t);
            sourceB.volume                   = Mathf.Lerp(startB,      0f, t);
            bulletTimeMusicSource.volume     = Mathf.Lerp(startBullet, 0f, t);
            specialAbilityMusicSource.volume = Mathf.Lerp(startSA,     0f, t);
            if (bossMusicSource.isPlaying)
                bossMusicSource.volume = Mathf.Lerp(0f, bossBattleVolume, t);
            yield return null;
        }

        sourceA.Stop();
        sourceB.Stop();
        bulletTimeMusicSource.Stop();
        specialAbilityMusicSource.Stop();

        // ── Step 2: wait for intro to end, leaving room for crossfade ──
        if (bossBattleMusic != null)
        {
            float waitTime = Mathf.Max(0f, bossBattleMusic.length - duration);
            float waited   = 0f;
            while (waited < waitTime)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // ── Step 3: crossfade intro → phase-1 loop ──
        if (bossBattleLoopMusic != null)
        {
            bossLoopSource.loop   = true;
            bossLoopSource.clip   = bossBattleLoopMusic;
            bossLoopSource.volume = 0f;
            bossLoopSource.Play();
        }

        float startIntroVol = bossMusicSource.volume;
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            bossMusicSource.volume = Mathf.Lerp(startIntroVol, 0f, t);
            if (bossLoopSource.isPlaying)
                bossLoopSource.volume = Mathf.Lerp(0f, bossBattleLoopVolume, t);
            yield return null;
        }

        bossMusicSource.Stop();
        bossMusicSource.clip = null;
        crossfadeRoutine = null;
    }

    private IEnumerator CrossfadeToBossPhase2Routine()
    {
        float duration   = Mathf.Max(0.01f, crossfadeDuration);
        float elapsed    = 0f;
        float startLoop  = bossLoopSource.volume;
        float startIntro = bossMusicSource.volume; // may still be fading when phase 2 triggers

        if (bossPhaseLoopMusic != null)
        {
            bossPhase2MusicSource.loop   = true;
            bossPhase2MusicSource.clip   = bossPhaseLoopMusic;
            bossPhase2MusicSource.volume = 0f;
            bossPhase2MusicSource.Play();
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            bossLoopSource.volume        = Mathf.Lerp(startLoop,  0f, t);
            bossMusicSource.volume       = Mathf.Lerp(startIntro, 0f, t);
            bossPhase2MusicSource.volume = Mathf.Lerp(0f, bossPhaseLoopVolume, t);
            yield return null;
        }

        bossLoopSource.Stop();
        bossLoopSource.clip = null;
        bossMusicSource.Stop();
        bossMusicSource.clip = null;
        crossfadeRoutine = null;
    }

    private IEnumerator FadeOutBossRoutine()
    {
        float duration    = Mathf.Max(0.01f, crossfadeDuration);
        float elapsed     = 0f;
        float startLoop   = bossLoopSource.volume;
        float startPhase2 = bossPhase2MusicSource.volume;
        float startIntro  = bossMusicSource.volume;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            bossLoopSource.volume        = Mathf.Lerp(startLoop,   0f, t);
            bossPhase2MusicSource.volume = Mathf.Lerp(startPhase2, 0f, t);
            bossMusicSource.volume       = Mathf.Lerp(startIntro,  0f, t);

            yield return null;
        }

        bossLoopSource.Stop();        bossLoopSource.clip        = null;
        bossPhase2MusicSource.Stop(); bossPhase2MusicSource.clip = null;
        bossMusicSource.Stop();       bossMusicSource.clip       = null;

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
