using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class MusicManager : MonoBehaviour
{
    [Header("Generator Reference")]
    [SerializeField] private EnemySquadGenerator squadGenerator;

    [Header("Clips")]
    [SerializeField] private AudioClip explorationMusic;
    [SerializeField] private AudioClip combatMusic;

    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] private float explorationVolume = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float combatVolume = 1f;

    [Header("Crossfade")]
    [Min(0f)] [SerializeField] private float crossfadeDuration = 1.5f;
    [SerializeField] private AudioClip transitionClip;
    [Range(0f, 1f)] [SerializeField] private float transitionVolume = 1f;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource transitionSource;
    private Coroutine crossfadeRoutine;

    private void Awake()
    {
        sourceA = gameObject.AddComponent<AudioSource>();
        sourceA.loop = true;
        sourceA.playOnAwake = false;
        sourceA.ignoreListenerPause = true;

        sourceB = gameObject.AddComponent<AudioSource>();
        sourceB.loop = true;
        sourceB.playOnAwake = false;
        sourceB.ignoreListenerPause = true;

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
        PlayImmediate(sourceA, explorationMusic, explorationVolume);
    }

    private void OnCombatStarted()
    {
        CrossfadeTo(sourceB, combatMusic, combatVolume, sourceA);
    }

    private void PlayImmediate(AudioSource source, AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        source.clip = clip;
        source.volume = volume;
        source.Play();
    }

    private void CrossfadeTo(AudioSource incoming, AudioClip clip, float targetVolume, AudioSource outgoing)
    {
        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        crossfadeRoutine = StartCoroutine(CrossfadeRoutine(incoming, clip, targetVolume, outgoing));
    }

    private IEnumerator CrossfadeRoutine(AudioSource incoming, AudioClip clip, float targetVolume, AudioSource outgoing)
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

        if (clip != null)
        {
            incoming.clip = clip;
            incoming.volume = targetVolume;
            incoming.Play();
        }

        crossfadeRoutine = null;
    }
}
