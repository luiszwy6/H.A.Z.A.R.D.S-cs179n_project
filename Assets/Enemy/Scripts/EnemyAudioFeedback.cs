using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class EnemyAudioFeedback : MonoBehaviour
{
    public enum HitPart
    {
        Generic,
        Head,
        Body
    }

    [System.Serializable]
    public class HitboxAudioBinding
    {
        [Header("Hitbox")]
        public Collider hitbox;
        public Transform hitboxTransform;

        [Header("Part")]
        public HitPart part = HitPart.Generic;
    }

    [Header("Bindings")]
    [SerializeField] private HitboxAudioBinding[] hitboxBindings;

    [Header("Ranged Hit Audio")]
    [SerializeField] private AudioClip[] rangedBodyHitClips;
    [SerializeField] private AudioClip[] rangedHeadHitClips;
    [SerializeField] private AudioClip[] rangedFatalHitClips;

    [Header("Melee Hit Audio")]
    [SerializeField] private AudioClip[] meleeNormalHitClips;
    [SerializeField] private AudioClip[] backStabHitClips;
    [SerializeField] private AudioClip[] meleeFatalHitClips;
    [SerializeField] private AudioClip[] backStabFatalHitClips;

    [Header("Volumes")]
    [Range(0f, 1f)] [SerializeField] private float rangedBodyVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float rangedHeadVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float rangedFatalVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float meleeNormalVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float backStabVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float meleeFatalVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float backStabFatalVolume = 1f;

    [Header("Fatal Rules")]
    [SerializeField] private bool preferFatalAudio = true;
    [SerializeField] private bool fallbackToNormalAudioIfFatalMissing = true;

    [Header("Pitch")]
    [SerializeField] private bool randomizePitch = true;
    [SerializeField] private float pitchMin = 0.95f;
    [SerializeField] private float pitchMax = 1.05f;

    [Header("Audio Source")]
    [SerializeField] private bool useTemporaryAudioObject = true;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private bool createAudioSourceIfMissing = true;

    [Header("3D Audio")]
    [Range(0f, 1f)] [SerializeField] private float spatialBlend = 1f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 25f;
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    [Header("Spam Control")]
    [SerializeField] private bool useMinimumInterval = true;
    [SerializeField] private float minimumInterval = 0.03f;

    [Header("Find Child Hitboxes")]
    [SerializeField] private bool includeInactiveColliders = true;
    [SerializeField] private bool triggerCollidersOnly = true;
    [SerializeField] private bool excludeThisObjectColliders = true;
    [SerializeField] private bool clearExistingBindingsBeforeFind = true;
    [SerializeField] private LayerMask findHitboxLayers = ~0;

    [Header("Debug")]
    [SerializeField] private bool logAudio = false;

    private float lastPlayTime = -999f;

    private void Reset()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null && createAudioSourceIfMissing && !useTemporaryAudioObject)
            audioSource = gameObject.AddComponent<AudioSource>();

        ConfigureAudioSource(audioSource);
    }

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null && createAudioSourceIfMissing && !useTemporaryAudioObject)
            audioSource = gameObject.AddComponent<AudioSource>();

        ConfigureAudioSource(audioSource);
        NormalizeBindings();
    }

    private void OnValidate()
    {
        NormalizeBindings();
    }

    [ContextMenu("Find Child Hitboxes")]
    public void FindChildHitboxes()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Find Child Hitbox Audio Bindings");
#endif

        Collider[] colliders = GetComponentsInChildren<Collider>(includeInactiveColliders);
        List<HitboxAudioBinding> result = new List<HitboxAudioBinding>();

        if (!clearExistingBindingsBeforeFind && hitboxBindings != null)
        {
            for (int i = 0; i < hitboxBindings.Length; i++)
            {
                if (hitboxBindings[i] != null)
                    result.Add(hitboxBindings[i]);
            }
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (!ShouldUseCollider(col))
                continue;

            HitboxAudioBinding existing = FindBindingInList(result, col);

            if (existing != null)
            {
                existing.hitbox = col;
                existing.hitboxTransform = col.transform;
                continue;
            }

            HitboxAudioBinding binding = new HitboxAudioBinding
            {
                hitbox = col,
                hitboxTransform = col.transform,
                part = GuessPartFromName(col.name)
            };

            result.Add(binding);
        }

        hitboxBindings = result.ToArray();

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public void PlayRangedHit(Collider hitCollider, Vector3 hitPoint, float appliedDamage, bool fatal)
    {
        if (appliedDamage <= 0f)
            return;

        if (fatal && preferFatalAudio && HasClips(rangedFatalHitClips))
        {
            PlayClipGroup(rangedFatalHitClips, rangedFatalVolume, hitPoint, "Ranged Fatal");
            return;
        }

        HitPart part = ResolvePart(hitCollider);

        if (part == HitPart.Head)
        {
            if (HasClips(rangedHeadHitClips))
            {
                PlayClipGroup(rangedHeadHitClips, rangedHeadVolume, hitPoint, "Ranged Head");
                return;
            }
        }

        if (HasClips(rangedBodyHitClips))
        {
            PlayClipGroup(rangedBodyHitClips, rangedBodyVolume, hitPoint, "Ranged Body");
            return;
        }

        if (fatal && fallbackToNormalAudioIfFatalMissing && HasClips(rangedFatalHitClips))
            PlayClipGroup(rangedFatalHitClips, rangedFatalVolume, hitPoint, "Ranged Fatal Fallback");
    }

    public void PlayMeleeHit(Collider hitCollider, Vector3 hitPoint, float appliedDamage, MeleeDamage.MeleeDamageMode meleeMode, bool fatal)
    {
        if (appliedDamage <= 0f)
            return;

        if (fatal && preferFatalAudio)
        {
            if (meleeMode == MeleeDamage.MeleeDamageMode.BackStab && HasClips(backStabFatalHitClips))
            {
                PlayClipGroup(backStabFatalHitClips, backStabFatalVolume, hitPoint, "BackStab Fatal");
                return;
            }

            if (HasClips(meleeFatalHitClips))
            {
                PlayClipGroup(meleeFatalHitClips, meleeFatalVolume, hitPoint, "Melee Fatal");
                return;
            }
        }

        if (meleeMode == MeleeDamage.MeleeDamageMode.BackStab)
        {
            if (HasClips(backStabHitClips))
            {
                PlayClipGroup(backStabHitClips, backStabVolume, hitPoint, "BackStab");
                return;
            }

            if (fatal && fallbackToNormalAudioIfFatalMissing && HasClips(backStabFatalHitClips))
            {
                PlayClipGroup(backStabFatalHitClips, backStabFatalVolume, hitPoint, "BackStab Fatal Fallback");
                return;
            }
        }

        if (HasClips(meleeNormalHitClips))
        {
            PlayClipGroup(meleeNormalHitClips, meleeNormalVolume, hitPoint, "Melee Normal");
            return;
        }

        if (fatal && fallbackToNormalAudioIfFatalMissing && HasClips(meleeFatalHitClips))
            PlayClipGroup(meleeFatalHitClips, meleeFatalVolume, hitPoint, "Melee Fatal Fallback");
    }

    private bool HasClips(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return false;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                return true;
        }

        return false;
    }

    private void PlayClipGroup(AudioClip[] clips, float volume, Vector3 position, string label)
    {
        if (!HasClips(clips))
            return;

        if (useMinimumInterval && Time.time < lastPlayTime + Mathf.Max(0f, minimumInterval))
            return;

        AudioClip clip = null;

        for (int i = 0; i < 12; i++)
        {
            clip = clips[Random.Range(0, clips.Length)];

            if (clip != null)
                break;
        }

        if (clip == null)
            return;

        float pitch = randomizePitch
            ? Random.Range(Mathf.Min(pitchMin, pitchMax), Mathf.Max(pitchMin, pitchMax))
            : 1f;

        pitch = Mathf.Max(0.01f, pitch);

        if (useTemporaryAudioObject)
            PlayTemporaryAudio(clip, volume, pitch, position);
        else
            PlayOnAudioSource(clip, volume, pitch);

        lastPlayTime = Time.time;

        if (logAudio)
            Debug.Log($"[EnemyAudioFeedback] Played {label}. Clip={clip.name}", this);
    }

    private void PlayTemporaryAudio(AudioClip clip, float volume, float pitch, Vector3 position)
    {
        GameObject audioObject = new GameObject($"EnemyHitAudio_{clip.name}");
        audioObject.transform.position = position;

        AudioSource source = audioObject.AddComponent<AudioSource>();
        ConfigureAudioSource(source);

        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.Play();

        Destroy(audioObject, clip.length / Mathf.Max(0.01f, pitch) + 0.1f);
    }

    private void PlayOnAudioSource(AudioClip clip, float volume, float pitch)
    {
        if (audioSource == null)
            return;

        ConfigureAudioSource(audioSource);

        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume);
    }

    private void ConfigureAudioSource(AudioSource source)
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = rolloffMode;
    }

    private HitPart ResolvePart(Collider hitCollider)
    {
        HitboxAudioBinding binding = FindBinding(hitCollider);

        if (binding != null)
            return binding.part;

        if (hitCollider != null)
            return GuessPartFromName(hitCollider.name);

        return HitPart.Generic;
    }

    private HitPart GuessPartFromName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return HitPart.Generic;

        string lower = objectName.ToLowerInvariant();

        if (lower.Contains("head")) return HitPart.Head;
        if (lower.Contains("neck")) return HitPart.Head;
        if (lower.Contains("face")) return HitPart.Head;
        if (lower.Contains("skull")) return HitPart.Head;

        if (lower.Contains("body")) return HitPart.Body;
        if (lower.Contains("torso")) return HitPart.Body;
        if (lower.Contains("chest")) return HitPart.Body;
        if (lower.Contains("spine")) return HitPart.Body;
        if (lower.Contains("pelvis")) return HitPart.Body;
        if (lower.Contains("hips")) return HitPart.Body;
        if (lower.Contains("arm")) return HitPart.Body;
        if (lower.Contains("hand")) return HitPart.Body;
        if (lower.Contains("leg")) return HitPart.Body;
        if (lower.Contains("foot")) return HitPart.Body;

        return HitPart.Generic;
    }

    private void NormalizeBindings()
    {
        if (hitboxBindings == null)
            return;

        for (int i = 0; i < hitboxBindings.Length; i++)
        {
            HitboxAudioBinding binding = hitboxBindings[i];

            if (binding == null)
                continue;

            if (binding.hitbox != null && binding.hitboxTransform == null)
                binding.hitboxTransform = binding.hitbox.transform;

            if (binding.hitbox == null && binding.hitboxTransform != null)
                binding.hitbox = binding.hitboxTransform.GetComponent<Collider>();
        }
    }

    private HitboxAudioBinding FindBinding(Collider hitCollider)
    {
        if (hitboxBindings == null || hitCollider == null)
            return null;

        for (int i = 0; i < hitboxBindings.Length; i++)
        {
            HitboxAudioBinding binding = hitboxBindings[i];

            if (binding == null)
                continue;

            if (binding.hitbox == hitCollider)
                return binding;

            if (binding.hitbox != null && binding.hitbox.gameObject == hitCollider.gameObject)
                return binding;

            if (binding.hitboxTransform != null && binding.hitboxTransform == hitCollider.transform)
                return binding;
        }

        return null;
    }

    private HitboxAudioBinding FindBindingInList(List<HitboxAudioBinding> list, Collider col)
    {
        if (list == null || col == null)
            return null;

        for (int i = 0; i < list.Count; i++)
        {
            HitboxAudioBinding binding = list[i];

            if (binding == null)
                continue;

            if (binding.hitbox == col)
                return binding;

            if (binding.hitbox != null && binding.hitbox.gameObject == col.gameObject)
                return binding;

            if (binding.hitboxTransform != null && binding.hitboxTransform == col.transform)
                return binding;
        }

        return null;
    }

    private bool ShouldUseCollider(Collider col)
    {
        if (col == null)
            return false;

        if (excludeThisObjectColliders && col.transform == transform)
            return false;

        if (triggerCollidersOnly && !col.isTrigger)
            return false;

        if (!IsLayerIncluded(findHitboxLayers, col.gameObject.layer))
            return false;

        return true;
    }

    private bool IsLayerIncluded(LayerMask mask, int layer)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}