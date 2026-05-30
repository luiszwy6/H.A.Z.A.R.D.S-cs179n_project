using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    public enum PlayerBodyPart
    {
        Generic,
        Head,
        Body,
        Limb
    }

    [System.Serializable]
    public class PlayerHitboxBinding
    {
        [Header("Hitbox")]
        public Collider hitbox;
        public Transform hitboxTransform;

        [Header("Body Part")]
        public PlayerBodyPart bodyPart = PlayerBodyPart.Generic;

        [Header("Damage")]
        public float damageMultiplier = 1f;
    }

    [Header("Health")]
    [SerializeField] private float baseHealth = 100f;
    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private bool initializeHealthOnAwake = true;

    [Header("Armor")]
    [Range(0, 2)]
    [SerializeField] private int armorLevel = 0;

    [Header("Hitboxes")]
    [SerializeField] private PlayerHitboxBinding[] hitboxes;
    [SerializeField] private bool requireRegisteredHitbox = true;

    [Header("Find Child Hitboxes")]
    [SerializeField] private bool includeInactiveColliders = true;
    [SerializeField] private bool triggerCollidersOnly = true;
    [SerializeField] private bool excludeThisObjectColliders = true;
    [SerializeField] private bool clearExistingHitboxesBeforeFind = true;
    [SerializeField] private LayerMask findHitboxLayers = ~0;

    [Header("Death")]
    [SerializeField] private bool disableHitboxesOnDeath = true;
    [SerializeField] private bool destroyOnDeath = false;
    [SerializeField] private float destroyDelay = 3f;

    [Header("Animator Death Bool")]
    [SerializeField] private bool setAnimatorIsDeadOnDeath = true;
    [SerializeField] private bool resetAnimatorIsDeadOnAwake = true;
    [SerializeField] private bool applyIsDeadToChildAnimators = true;
    [SerializeField] private string isDeadBoolName = "IsDead";

    [Header("Animator Death Trigger")]
    [SerializeField] private bool triggerAnimatorDeathOnDeath = true;
    [SerializeField] private bool resetDeathTriggerOnAwake = true;
    [SerializeField] private bool applyDeathTriggerToChildAnimators = true;
    [SerializeField] private string deathTriggerName = "DieTrigger";

    [Header("Death Component Disable")]
    [SerializeField] private bool disableBehavioursOnDeath = true;
    [SerializeField] private bool keepAnimatorEnabled = true;
    [SerializeField] private bool includeChildBehaviours = true;
    [Tooltip("These components will stay enabled after death.")]
    [SerializeField] private Behaviour[] behavioursToKeepEnabled;
    [Tooltip("These components will be disabled even if not found by the scan.")]
    [SerializeField] private Behaviour[] extraBehavioursToDisable;

    [Header("Events")]
    public UnityEvent onDamaged;
    public UnityEvent onDeath;

    [Header("Debug")]
    [SerializeField] private bool logDamage = false;
    [SerializeField] private bool logDeath = false;
    [SerializeField] private bool logAnimatorDeathParameter = false;

    private bool isDead;
    private int isDeadBoolHash;
    private int deathTriggerHash;

    public float BaseHealth => baseHealth;
    public float CurrentHealth => currentHealth;
    public int CurrentArmorLevel => Mathf.Clamp(armorLevel, 0, 2);
    public bool IsDead => isDead;

    public void SetArmorLevel(int value)
    {
        armorLevel = Mathf.Clamp(value, 0, 2);
    }
    public PlayerHitboxBinding[] Hitboxes => hitboxes;

    private void Awake()
    {
        isDeadBoolHash = Animator.StringToHash(isDeadBoolName);
        deathTriggerHash = Animator.StringToHash(deathTriggerName);

        baseHealth = Mathf.Max(1f, baseHealth);

        if (initializeHealthOnAwake)
            currentHealth = baseHealth;
        else
            currentHealth = Mathf.Clamp(currentHealth, 0f, baseHealth);

        if (resetDeathTriggerOnAwake)
            ResetAnimatorDeathTrigger();

        if (resetAnimatorIsDeadOnAwake)
            SetAnimatorIsDead(false);
    }

    [ContextMenu("Find Child Hitboxes")]
    public void FindChildHitboxes()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Find Child Hitboxes");
#endif

        Collider[] foundColliders = GetComponentsInChildren<Collider>(includeInactiveColliders);
        List<PlayerHitboxBinding> result = new List<PlayerHitboxBinding>();

        if (!clearExistingHitboxesBeforeFind && hitboxes != null)
        {
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i] == null)
                    continue;

                result.Add(hitboxes[i]);
            }
        }

        for (int i = 0; i < foundColliders.Length; i++)
        {
            Collider col = foundColliders[i];

            if (!ShouldUseCollider(col))
                continue;

            PlayerHitboxBinding existing = FindHitboxInList(result, col);

            if (existing != null)
            {
                existing.hitbox = col;
                existing.hitboxTransform = col.transform;

                if (existing.bodyPart == PlayerBodyPart.Generic)
                    existing.bodyPart = GuessBodyPartFromName(col.name);

                continue;
            }

            PlayerHitboxBinding binding = new PlayerHitboxBinding
            {
                hitbox = col,
                hitboxTransform = col.transform,
                bodyPart = GuessBodyPartFromName(col.name),
                damageMultiplier = 1f
            };

            result.Add(binding);
        }

        hitboxes = result.ToArray();

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public bool TryApplyBulletDamage(
        RaycastHit hit,
        float baseDamage,
        int armorPierceLevel,
        float knockbackValue,
        out float appliedDamage,
        out bool triggeredKnockback)
    {
        appliedDamage = 0f;
        triggeredKnockback = false;

        if (isDead)
            return false;

        if (hit.collider == null)
            return false;

        PlayerHitboxBinding binding = FindHitbox(hit.collider);

        if (requireRegisteredHitbox && binding == null)
            return false;

        PlayerBodyPart bodyPart = binding != null
            ? binding.bodyPart
            : PlayerBodyPart.Generic;

        float damageMultiplier = binding != null
            ? Mathf.Max(0f, binding.damageMultiplier)
            : 1f;

        int targetArmorLevel = Mathf.Clamp(armorLevel, 0, 2);
        int pierceLevel = Mathf.Clamp(armorPierceLevel, 0, 2);

        float armorDamageMultiplier = ResolveArmorDamageMultiplier(pierceLevel, targetArmorLevel);

        float finalDamage =
            Mathf.Max(0f, baseDamage) *
            damageMultiplier *
            armorDamageMultiplier;

        appliedDamage = finalDamage;

        if (finalDamage > 0f)
            TakeDamage(finalDamage);

        if (logDamage)
        {
            string hitboxName = hit.collider != null ? hit.collider.name : "None";

            Debug.Log(
                $"[PlayerHealth] Hit={hitboxName}, BodyPart={bodyPart}, BaseDamage={baseDamage}, ArmorPierce={pierceLevel}, ArmorLevel={targetArmorLevel}, ArmorMult={armorDamageMultiplier}, DamageMult={damageMultiplier}, Applied={finalDamage}, HP={currentHealth}/{baseHealth}",
                this
            );
        }

        return true;
    }

    public void TakeDamage(float amount)
    {
        if (isDead)
            return;

        float damage = Mathf.Max(0f, amount);

        if (damage <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);

        onDamaged?.Invoke();

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (isDead)
            return;

        currentHealth = Mathf.Clamp(
            currentHealth + Mathf.Max(0f, amount),
            0f,
            baseHealth
        );
    }

    public void Kill()
    {
        if (isDead)
            return;

        currentHealth = 0f;
        Die();
    }

    public void Revive(float healthValue = -1f)
    {
        isDead = false;

        if (healthValue < 0f)
            currentHealth = baseHealth;
        else
            currentHealth = Mathf.Clamp(healthValue, 1f, baseHealth);

        if (disableHitboxesOnDeath)
            SetHitboxesEnabled(true);

        ResetAnimatorDeathTrigger();
        SetAnimatorIsDead(false);
    }

    private float ResolveArmorDamageMultiplier(int pierceLevel, int targetArmorLevel)
    {
        if (pierceLevel > targetArmorLevel)
            return 1f;

        if (pierceLevel == targetArmorLevel)
            return 0.7f;

        return 0.35f;
    }

    private PlayerBodyPart GuessBodyPartFromName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return PlayerBodyPart.Generic;

        string lower = objectName.ToLowerInvariant();

        if (lower.Contains("head")) return PlayerBodyPart.Head;
        if (lower.Contains("neck")) return PlayerBodyPart.Head;
        if (lower.Contains("skull")) return PlayerBodyPart.Head;
        if (lower.Contains("face")) return PlayerBodyPart.Head;

        if (lower.Contains("chest")) return PlayerBodyPart.Body;
        if (lower.Contains("torso")) return PlayerBodyPart.Body;
        if (lower.Contains("spine")) return PlayerBodyPart.Body;
        if (lower.Contains("body")) return PlayerBodyPart.Body;
        if (lower.Contains("abdomen")) return PlayerBodyPart.Body;
        if (lower.Contains("pelvis")) return PlayerBodyPart.Body;
        if (lower.Contains("hips")) return PlayerBodyPart.Body;
        if (lower.Contains("hip")) return PlayerBodyPart.Body;

        if (lower.Contains("arm")) return PlayerBodyPart.Limb;
        if (lower.Contains("hand")) return PlayerBodyPart.Limb;
        if (lower.Contains("leg")) return PlayerBodyPart.Limb;
        if (lower.Contains("foot")) return PlayerBodyPart.Limb;
        if (lower.Contains("thigh")) return PlayerBodyPart.Limb;
        if (lower.Contains("calf")) return PlayerBodyPart.Limb;
        if (lower.Contains("shin")) return PlayerBodyPart.Limb;

        return PlayerBodyPart.Generic;
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        currentHealth = 0f;

        if (triggerAnimatorDeathOnDeath)
            TriggerAnimatorDeath();

        if (setAnimatorIsDeadOnDeath)
            SetAnimatorIsDead(true);

        if (disableHitboxesOnDeath)
            SetHitboxesEnabled(false);

        if (disableBehavioursOnDeath)
            DisableDeathBehaviours();

        onDeath?.Invoke();

        if (logDeath)
            Debug.Log("[PlayerHealth] Player died.", this);

        if (destroyOnDeath)
            Destroy(gameObject, Mathf.Max(0f, destroyDelay));
    }

    private void DisableDeathBehaviours()
    {
        System.Collections.Generic.HashSet<Behaviour> keepEnabled =
            new System.Collections.Generic.HashSet<Behaviour>();

        keepEnabled.Add(this);

        if (keepAnimatorEnabled)
        {
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                    keepEnabled.Add(animators[i]);
            }
        }

        if (behavioursToKeepEnabled != null)
        {
            for (int i = 0; i < behavioursToKeepEnabled.Length; i++)
            {
                if (behavioursToKeepEnabled[i] != null)
                    keepEnabled.Add(behavioursToKeepEnabled[i]);
            }
        }

        Behaviour[] behaviours = includeChildBehaviours
            ? GetComponentsInChildren<Behaviour>(true)
            : GetComponents<Behaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];

            if (behaviour == null || keepEnabled.Contains(behaviour))
                continue;

            behaviour.enabled = false;
        }

        if (extraBehavioursToDisable != null)
        {
            for (int i = 0; i < extraBehavioursToDisable.Length; i++)
            {
                Behaviour behaviour = extraBehavioursToDisable[i];

                if (behaviour == null || keepEnabled.Contains(behaviour))
                    continue;

                behaviour.enabled = false;
            }
        }
    }

    private void TriggerAnimatorDeath()
    {
        if (string.IsNullOrWhiteSpace(deathTriggerName))
            return;

        deathTriggerHash = Animator.StringToHash(deathTriggerName);

        Animator animator = GetComponent<Animator>();

        if (animator != null)
            SetAnimatorTriggerIfExists(animator, deathTriggerHash);

        if (!applyDeathTriggerToChildAnimators)
            return;

        Animator[] childAnimators = GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < childAnimators.Length; i++)
        {
            Animator childAnimator = childAnimators[i];

            if (childAnimator == null)
                continue;

            if (childAnimator == animator)
                continue;

            SetAnimatorTriggerIfExists(childAnimator, deathTriggerHash);
        }
    }

    private void ResetAnimatorDeathTrigger()
    {
        if (string.IsNullOrWhiteSpace(deathTriggerName))
            return;

        deathTriggerHash = Animator.StringToHash(deathTriggerName);

        Animator animator = GetComponent<Animator>();

        if (animator != null)
            ResetAnimatorTriggerIfExists(animator, deathTriggerHash);

        if (!applyDeathTriggerToChildAnimators)
            return;

        Animator[] childAnimators = GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < childAnimators.Length; i++)
        {
            Animator childAnimator = childAnimators[i];

            if (childAnimator == null)
                continue;

            if (childAnimator == animator)
                continue;

            ResetAnimatorTriggerIfExists(childAnimator, deathTriggerHash);
        }
    }

    private void SetAnimatorIsDead(bool value)
    {
        if (string.IsNullOrWhiteSpace(isDeadBoolName))
            return;

        isDeadBoolHash = Animator.StringToHash(isDeadBoolName);

        Animator animator = GetComponent<Animator>();

        if (animator != null)
            SetAnimatorBoolIfExists(animator, isDeadBoolHash, value);

        if (!applyIsDeadToChildAnimators)
            return;

        Animator[] childAnimators = GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < childAnimators.Length; i++)
        {
            Animator childAnimator = childAnimators[i];

            if (childAnimator == null)
                continue;

            if (childAnimator == animator)
                continue;

            SetAnimatorBoolIfExists(childAnimator, isDeadBoolHash, value);
        }
    }

    private void SetAnimatorTriggerIfExists(Animator animator, int parameterHash)
    {
        if (animator == null)
            return;

        if (animator.runtimeAnimatorController == null)
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.nameHash != parameterHash)
                continue;

            if (parameter.type != AnimatorControllerParameterType.Trigger)
                return;

            animator.ResetTrigger(parameterHash);
            animator.SetTrigger(parameterHash);

            if (logAnimatorDeathParameter)
            {
                Debug.Log(
                    $"[PlayerHealth] Animator trigger {deathTriggerName} set on {animator.gameObject.name}.",
                    animator
                );
            }

            return;
        }
    }

    private void ResetAnimatorTriggerIfExists(Animator animator, int parameterHash)
    {
        if (animator == null)
            return;

        if (animator.runtimeAnimatorController == null)
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.nameHash != parameterHash)
                continue;

            if (parameter.type != AnimatorControllerParameterType.Trigger)
                return;

            animator.ResetTrigger(parameterHash);

            if (logAnimatorDeathParameter)
            {
                Debug.Log(
                    $"[PlayerHealth] Animator trigger {deathTriggerName} reset on {animator.gameObject.name}.",
                    animator
                );
            }

            return;
        }
    }

    private void SetAnimatorBoolIfExists(Animator animator, int parameterHash, bool value)
    {
        if (animator == null)
            return;

        if (animator.runtimeAnimatorController == null)
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.nameHash != parameterHash)
                continue;

            if (parameter.type != AnimatorControllerParameterType.Bool)
                return;

            animator.SetBool(parameterHash, value);

            if (logAnimatorDeathParameter)
            {
                Debug.Log(
                    $"[PlayerHealth] Animator bool {isDeadBoolName} set to {value} on {animator.gameObject.name}.",
                    animator
                );
            }

            return;
        }
    }

    private void SetHitboxesEnabled(bool enabled)
    {
        if (hitboxes == null)
            return;

        for (int i = 0; i < hitboxes.Length; i++)
        {
            if (hitboxes[i] == null || hitboxes[i].hitbox == null)
                continue;

            hitboxes[i].hitbox.enabled = enabled;
        }
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

    private PlayerHitboxBinding FindHitbox(Collider hitCollider)
    {
        if (hitboxes == null || hitCollider == null)
            return null;

        for (int i = 0; i < hitboxes.Length; i++)
        {
            PlayerHitboxBinding binding = hitboxes[i];

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

    private PlayerHitboxBinding FindHitboxInList(List<PlayerHitboxBinding> list, Collider col)
    {
        if (list == null || col == null)
            return null;

        for (int i = 0; i < list.Count; i++)
        {
            PlayerHitboxBinding binding = list[i];

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
}