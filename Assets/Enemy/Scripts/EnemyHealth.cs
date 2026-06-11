using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AI;
using FIMSpace.FProceduralAnimation;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour
{
    public enum DeathRagdollSwitchMode
    {
        None,
        Fall,
        Sleep
    }

    public enum DeathComponentShutdownMode
    {
        Custom,
        DisableAllBehaviours,
        DisableAllExceptRagdollAndAnimator
    }

    [System.Serializable]
    public class EnemyHitboxBinding
    {
        [Header("Hitbox")]
        public Collider hitbox;
        public Transform hitboxTransform;

        [Header("Body Part")]
        public EnemyBodyPart bodyPart = EnemyBodyPart.Generic;

        [Header("Damage")]
        public float damageMultiplier = 1f;
    }

    [System.Serializable]
    public class DeathRagdollSwitchOption
    {
        [Header("Switch Mode")]
        public DeathRagdollSwitchMode mode = DeathRagdollSwitchMode.Fall;

        [Header("Weight")]
        [Min(0f)] public float weight = 1f;

        [Header("Sleep Settings")]
        [Min(0f)] public float sleepDisableMecanimAfter = 2.5f;
    }

    public enum EnemyType { Soldier, Zombie }

    [Header("Enemy Type")]
    [SerializeField] private EnemyType enemyType = EnemyType.Soldier;

    [Header("Health")]
    [SerializeField] private float base_Health = 100f;
    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private bool initializeHealthOnAwake = true;

    [Header("Armor")]
    [Range(0, 2)]
    [SerializeField] private int ArmorLevel = 0;

    [Header("Hitboxes")]
    [SerializeField] private EnemyHitboxBinding[] hitboxes;
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

    [Header("Animator Death Parameter")]
    [SerializeField] private bool setAnimatorIsDeadOnDeath = true;
    [SerializeField] private bool resetAnimatorIsDeadOnAwake = true;
    [SerializeField] private bool applyIsDeadToChildAnimators = true;
    [SerializeField] private string isDeadBoolName = "IsDead";
    [SerializeField] private bool fireAnimatorDeadTriggerOnDeath = true;
    [SerializeField] private string deadTriggerName = "Dead";

    [Header("Death Component Shutdown")]
    [SerializeField] private bool shutdownComponentsOnDeath = true;

    [SerializeField] private DeathComponentShutdownMode componentShutdownMode =
        DeathComponentShutdownMode.DisableAllExceptRagdollAndAnimator;

    [Tooltip("True = only disable Behaviour components on this root object. False = also search children.")]
    [SerializeField] private bool shutdownOnlyRootObjectComponents = false;

    [SerializeField] private bool includeInactiveBehavioursForShutdown = true;

    [Tooltip("Delay shutdown so RA2 can receive its death switch first.")]
    [SerializeField] private bool delayComponentShutdownOnDeath = false;

    [Min(0f)]
    [SerializeField] private float componentShutdownDelay = 0.1f;

    [Header("Death Ragdoll Protection")]
    [SerializeField] private bool hardProtectAnimatorOnDeath = true;
    [SerializeField] private bool hardProtectRagdollAnimatorOnDeath = true;
    [SerializeField] private bool lockEnemyRagdollGetUpOnDeath = true;
    [SerializeField] private bool enforceDeathRagdollBlendAfterDeath = true;

    [Tooltip("0 = forever while this EnemyHealth is enabled.")]
    [Min(0f)]
    [SerializeField] private float enforceDeathRagdollBlendDuration = 0f;

    [Tooltip("RA2 Sleep calls DisableMecanimAfter internally. Enable this to convert Sleep to Fall when Animator is protected.")]
    [SerializeField] private bool convertSleepToFallWhenProtectingAnimator = true;

    [Header("Death Ragdoll Muscles")]
    [SerializeField] private bool setRagdollMusclesPowerOnDeath = true;

    [Range(0f, 1f)]
    [SerializeField] private float deathRagdollMusclesPower = 0f;

    [SerializeField] private bool applyMusclesPowerToChildRagdollAnimators = true;
    [SerializeField] private bool enforceDeathRagdollMusclesPowerAfterDeath = true;

    [Header("Force Disable Child Behaviours")]
    [Tooltip("Disables all non-Animator Behaviours on child objects unconditionally, regardless of shutdown mode settings.")]
    [SerializeField] private bool forceDisableChildBehavioursOnDeath = true;

    [Header("Death Weapon GameObject Disable")]
    [SerializeField] private bool disableWeaponGameObjectsOnDeath = true;

    [Header("Custom Shutdown Types")]
    [SerializeField] private bool disableAnimatorOnDeath = false;
    [SerializeField] private bool disableNavMeshAgentOnDeath = true;
    [SerializeField] private bool disableRagdollAnimatorOnDeath = false;
    [SerializeField] private bool disableBehaviorGraphAgentOnDeath = true;
    [SerializeField] private bool disableEnemySensorOnDeath = true;
    [SerializeField] private bool disableWeaponShootersOnDeath = true;
    [SerializeField] private bool disableOtherBehavioursOnDeath = true;

    [Header("NavMeshAgent Shutdown")]
    [SerializeField] private bool stopNavMeshAgentBeforeDisable = true;

    [Header("Shutdown Exceptions")]
    [Tooltip("These behaviours will stay enabled even after death.")]
    [SerializeField] private Behaviour[] extraBehavioursToKeepEnabled;

    [Tooltip("These behaviours will be disabled even if they are not found by the normal scan.")]
    [SerializeField] private Behaviour[] extraBehavioursToDisable;

    [Header("Death Ragdoll")]
    [SerializeField] private RagdollAnimator2 deathRagdollComponent;
    [SerializeField] private bool keepDeathRagdollEnabledOnAwake = true;
    [SerializeField] private bool setInitialRagdollBlendOnAwake = true;
    [Range(0f, 1f)] [SerializeField] private float initialRagdollBlend = 0f;
    [SerializeField] private bool activateDeathRagdollOnDeath = true;
    [Range(0f, 1f)] [SerializeField] private float deathRagdollBlend = 1f;

    [Header("Death Ragdoll Switch Options")]
    [SerializeField] private bool randomizeDeathRagdollSwitchMode = true;
    [SerializeField] private DeathRagdollSwitchOption[] deathRagdollSwitchOptions =
    {
        new DeathRagdollSwitchOption
        {
            mode = DeathRagdollSwitchMode.Fall,
            weight = 1f,
            sleepDisableMecanimAfter = 2.5f
        }
    };

    [SerializeField] private DeathRagdollSwitchMode fallbackDeathRagdollSwitchMode =
        DeathRagdollSwitchMode.Fall;

    [Min(0f)]
    [SerializeField] private float fallbackSleepDisableMecanimAfter = 2.5f;

    [Header("Events")]
    public UnityEvent onDamaged;
    public UnityEvent onDeath;

    [Header("Damage Reveal")]
    [SerializeField] private EnemySensor enemySensor;
    [SerializeField] private bool revealPlayerOnDamageWhilePlayerShooting = true;
    [SerializeField] private bool revealPlayerOnAnyDamage = false;
    [Min(0f)] [SerializeField] private float damageRevealDuration = 2f;

    [Header("Debug")]
    [SerializeField] private bool logDamage = false;
    [SerializeField] private bool logDeath = false;
    [SerializeField] private bool logDeathRagdollSwitch = false;
    [SerializeField] private bool logDeathComponentShutdown = false;
    [SerializeField] private bool logDeathRagdollProtection = false;
    [SerializeField] private bool logAnimatorDeathParameter = false;
    [SerializeField] private bool logDeathRagdollMusclesPower = false;

    private bool isDead;
    private int isDeadBoolHash;
    private Coroutine shutdownComponentsRoutine;
    private Coroutine enforceDeathRagdollBlendRoutine;

    private static readonly string[] RagdollMusclesPowerMemberNames =
    {
        "MusclesPower",
        "MusclePower",
        "musclesPower",
        "musclePower",
        "MusclesPowerMultiplier",
        "MusclePowerMultiplier",
        "musclesPowerMultiplier",
        "musclePowerMultiplier",
        "MusclesStrength",
        "MuscleStrength",
        "musclesStrength",
        "muscleStrength",
        "MusclesWeight",
        "MuscleWeight",
        "musclesWeight",
        "muscleWeight"
    };

    public static event System.Action<EnemyHealth> OnAnyEnemyDied;

    public float BaseHealth => base_Health;
    public float CurrentHealth => currentHealth;
    public int CurrentArmorLevel => Mathf.Clamp(ArmorLevel, 0, 2);
    public bool IsDead => isDead;
    public EnemyType Type => enemyType;

    private void Reset()
    {
        if (deathRagdollComponent == null)
            deathRagdollComponent = GetComponent<RagdollAnimator2>();
    }

    private void Awake()
    {
        isDeadBoolHash = Animator.StringToHash(isDeadBoolName);

        base_Health = Mathf.Max(1f, base_Health);

        if (initializeHealthOnAwake)
            currentHealth = base_Health;
        else
            currentHealth = Mathf.Clamp(currentHealth, 0f, base_Health);

        if (deathRagdollComponent == null)
            deathRagdollComponent = GetComponent<RagdollAnimator2>();

        if (enemySensor == null)
            enemySensor = GetComponent<EnemySensor>();

        if (deathRagdollComponent != null)
        {
            if (keepDeathRagdollEnabledOnAwake)
                deathRagdollComponent.enabled = true;

            if (setInitialRagdollBlendOnAwake)
                deathRagdollComponent.RagdollBlend = Mathf.Clamp01(initialRagdollBlend);
        }

        if (resetAnimatorIsDeadOnAwake)
            SetAnimatorIsDead(false);
    }

    private void OnDisable()
    {
        if (shutdownComponentsRoutine != null)
        {
            StopCoroutine(shutdownComponentsRoutine);
            shutdownComponentsRoutine = null;
        }

        if (enforceDeathRagdollBlendRoutine != null)
        {
            StopCoroutine(enforceDeathRagdollBlendRoutine);
            enforceDeathRagdollBlendRoutine = null;
        }
    }

    [ContextMenu("Find Child Hitboxes")]
    public void FindChildHitboxes()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Find Child Hitboxes");
#endif

        Collider[] foundColliders = GetComponentsInChildren<Collider>(includeInactiveColliders);
        List<EnemyHitboxBinding> result = new List<EnemyHitboxBinding>();

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

            EnemyHitboxBinding existing = FindHitboxInList(result, col);

            if (existing != null)
            {
                existing.hitbox = col;
                existing.hitboxTransform = col.transform;

                if (existing.bodyPart == EnemyBodyPart.Generic)
                    existing.bodyPart = GuessBodyPartFromName(col.name);

                continue;
            }

            EnemyHitboxBinding binding = new EnemyHitboxBinding
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
        float Base_dmg,
        int ArmoPierLevel,
        float knockbackValue,
        out float appliedDamage,
        out bool triggeredKnockback)
    {
        return TryApplyBulletDamage(
            hit,
            Base_dmg,
            ArmoPierLevel,
            knockbackValue,
            null,
            out appliedDamage,
            out triggeredKnockback
        );
    }

    public bool TryApplyBulletDamage(
        RaycastHit hit,
        float Base_dmg,
        int ArmoPierLevel,
        float knockbackValue,
        WeaponDamagePartOverride[] partOverrides,
        out float appliedDamage,
        out bool triggeredKnockback)
    {
        appliedDamage = 0f;
        triggeredKnockback = false;

        if (isDead)
            return false;

        if (hit.collider == null)
            return false;

        EnemyHitboxBinding binding = FindHitbox(hit.collider);

        if (requireRegisteredHitbox && binding == null)
            return false;

        EnemyBodyPart bodyPart = binding != null
            ? binding.bodyPart
            : EnemyBodyPart.Generic;

        float damageMultiplier = binding != null
            ? Mathf.Max(0f, binding.damageMultiplier)
            : 1f;

        int armorLevel = Mathf.Clamp(ArmorLevel, 0, 2);
        int pierceLevel = Mathf.Clamp(ArmoPierLevel, 0, 2);

        WeaponDamagePartOverride partOverride = FindPartOverride(bodyPart, partOverrides);

        if (partOverride != null)
        {
            if (partOverride.overrideDamageMultiplier)
                damageMultiplier = Mathf.Max(0f, partOverride.damageMultiplier);

            if (partOverride.overrideArmorPierceLevel)
                pierceLevel = Mathf.Clamp(partOverride.armorPierceLevel, 0, 2);
        }

        float armorDamageMultiplier = ResolveArmorDamageMultiplier(pierceLevel, armorLevel);

        float finalDamage =
            Mathf.Max(0f, Base_dmg) *
            damageMultiplier *
            armorDamageMultiplier;

        appliedDamage = finalDamage;

        if (finalDamage > 0f)
            TakeDamage(finalDamage);

        if (logDamage)
        {
            string hitboxName = hit.collider != null ? hit.collider.name : "None";
            bool hasPartOverride = partOverride != null;

            Debug.Log(
                $"[EnemyHealth] Hit={hitboxName}, BodyPart={bodyPart}, Base_dmg={Base_dmg}, ArmoPierLevel={pierceLevel}, ArmorLevel={armorLevel}, ArmorMult={armorDamageMultiplier}, DamageMult={damageMultiplier}, HasPartOverride={hasPartOverride}, Applied={finalDamage}, HP={currentHealth}/{base_Health}",
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

        TryRevealPlayerFromDamage();

        onDamaged?.Invoke();

        if (currentHealth <= 0f)
            Die();
    }

    private void TryRevealPlayerFromDamage()
    {
        if (enemySensor == null)
            enemySensor = GetComponent<EnemySensor>();

        if (enemySensor == null)
            return;

        if (revealPlayerOnAnyDamage)
        {
            enemySensor.RevealTargetFromDamage(damageRevealDuration);
            return;
        }

        if (!revealPlayerOnDamageWhilePlayerShooting)
            return;

        PlayerStatus playerStatus = PlayerStatus.Instance;

        if (playerStatus == null || !playerStatus.IsAnyShooting)
            return;

        enemySensor.RevealTargetFromDamage(damageRevealDuration);
    }

    public void Heal(float amount)
    {
        if (isDead)
            return;

        currentHealth = Mathf.Clamp(
            currentHealth + Mathf.Max(0f, amount),
            0f,
            base_Health
        );
    }

    public void Kill()
    {
        if (isDead)
            return;

        currentHealth = 0f;
        Die();
    }

    private float ResolveArmorDamageMultiplier(int pierceLevel, int armorLevel)
    {
        if (pierceLevel > armorLevel)
            return 1f;

        if (pierceLevel == armorLevel)
            return 0.7f;

        return 0.35f;
    }

    private WeaponDamagePartOverride FindPartOverride(
        EnemyBodyPart bodyPart,
        WeaponDamagePartOverride[] overrides)
    {
        if (overrides == null)
            return null;

        for (int i = 0; i < overrides.Length; i++)
        {
            WeaponDamagePartOverride partOverride = overrides[i];

            if (partOverride == null)
                continue;

            if (partOverride.bodyPart == bodyPart)
                return partOverride;
        }

        return null;
    }

    private EnemyBodyPart GuessBodyPartFromName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return EnemyBodyPart.Generic;

        string lower = objectName.ToLowerInvariant();

        if (lower.Contains("head")) return EnemyBodyPart.Head;
        if (lower.Contains("neck")) return EnemyBodyPart.Head;
        if (lower.Contains("skull")) return EnemyBodyPart.Head;
        if (lower.Contains("face")) return EnemyBodyPart.Head;

        if (lower.Contains("chest")) return EnemyBodyPart.Body;
        if (lower.Contains("torso")) return EnemyBodyPart.Body;
        if (lower.Contains("spine")) return EnemyBodyPart.Body;
        if (lower.Contains("body")) return EnemyBodyPart.Body;
        if (lower.Contains("abdomen")) return EnemyBodyPart.Body;
        if (lower.Contains("pelvis")) return EnemyBodyPart.Body;
        if (lower.Contains("hips")) return EnemyBodyPart.Body;
        if (lower.Contains("hip")) return EnemyBodyPart.Body;

        if (lower.Contains("arm")) return EnemyBodyPart.Limb;
        if (lower.Contains("hand")) return EnemyBodyPart.Limb;
        if (lower.Contains("leg")) return EnemyBodyPart.Limb;
        if (lower.Contains("foot")) return EnemyBodyPart.Limb;
        if (lower.Contains("thigh")) return EnemyBodyPart.Limb;
        if (lower.Contains("calf")) return EnemyBodyPart.Limb;
        if (lower.Contains("shin")) return EnemyBodyPart.Limb;

        return EnemyBodyPart.Generic;
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        currentHealth = 0f;

        SquadMember squadMember = GetComponent<SquadMember>();
        if (squadMember != null)
            squadMember.MarkDead();

        if (setAnimatorIsDeadOnDeath)
            SetAnimatorIsDead(true);

        if (fireAnimatorDeadTriggerOnDeath)
            FireAnimatorDeadTrigger();

        if (disableHitboxesOnDeath)
            SetHitboxesEnabled(false);

        onDeath?.Invoke();
        OnAnyEnemyDied?.Invoke(this);

        if (disableWeaponGameObjectsOnDeath)
            DisableWeaponGameObjects();

        if (forceDisableChildBehavioursOnDeath)
            ForceDisableChildBehaviours();

        if (activateDeathRagdollOnDeath)
            ActivateDeathRagdoll();

        if (shutdownComponentsOnDeath)
            RequestShutdownDeathComponents();

        if (logDeath)
            Debug.Log("[EnemyHealth] Enemy died.", this);

        if (destroyOnDeath)
            Destroy(gameObject, Mathf.Max(0f, destroyDelay));
    }

    private void ActivateDeathRagdoll()
    {
        if (deathRagdollComponent == null)
            return;

        if (!deathRagdollComponent.enabled)
            deathRagdollComponent.enabled = true;

        deathRagdollComponent.RagdollBlend = Mathf.Clamp01(deathRagdollBlend);

        if (setRagdollMusclesPowerOnDeath)
            ApplyDeathRagdollMusclesPower();

        DeathRagdollSwitchOption selectedOption = SelectDeathRagdollSwitchOption();

        ApplyDeathRagdollSwitchOption(selectedOption);

        if (lockEnemyRagdollGetUpOnDeath)
            LockEnemyRagdollGetUpComponents();

        if (enforceDeathRagdollBlendAfterDeath || enforceDeathRagdollMusclesPowerAfterDeath)
            StartEnforceDeathRagdollBlend();
    }

    private void RequestShutdownDeathComponents()
    {
        if (delayComponentShutdownOnDeath && componentShutdownDelay > 0f)
        {
            if (shutdownComponentsRoutine != null)
                StopCoroutine(shutdownComponentsRoutine);

            shutdownComponentsRoutine = StartCoroutine(ShutdownDeathComponentsAfterDelay());
            return;
        }

        ShutdownDeathComponents();
    }

    private IEnumerator ShutdownDeathComponentsAfterDelay()
    {
        yield return new WaitForSeconds(componentShutdownDelay);

        ShutdownDeathComponents();

        shutdownComponentsRoutine = null;
    }

    private void ShutdownDeathComponents()
    {
        HashSet<Behaviour> keepEnabled = BuildKeepEnabledSet();

        Behaviour[] behaviours = shutdownOnlyRootObjectComponents
            ? GetComponents<Behaviour>()
            : GetComponentsInChildren<Behaviour>(includeInactiveBehavioursForShutdown);

        // Stop the behavior tree first so action nodes cannot call NavMeshAgent methods
        // on a disabled agent during the same frame.
        if (disableBehaviorGraphAgentOnDeath)
        {
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour == null || keepEnabled.Contains(behaviour)) continue;
                if (!IsBehaviorGraphAgent(behaviour)) continue;
                TryEndBehaviorGraphAgent(behaviour);
                behaviour.enabled = false;
            }
        }

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];

            if (behaviour == null)
                continue;

            if (keepEnabled.Contains(behaviour))
                continue;

            if (!ShouldDisableBehaviourOnDeath(behaviour))
                continue;

            // Already stopped in pre-pass above
            if (IsBehaviorGraphAgent(behaviour) && !behaviour.enabled)
                continue;

            DisableBehaviourOnDeath(behaviour, false);
        }

        if (extraBehavioursToDisable != null)
        {
            for (int i = 0; i < extraBehavioursToDisable.Length; i++)
            {
                Behaviour behaviour = extraBehavioursToDisable[i];

                if (behaviour == null)
                    continue;

                if (keepEnabled.Contains(behaviour))
                    continue;

                DisableBehaviourOnDeath(behaviour, true);
            }
        }
    }

    private HashSet<Behaviour> BuildKeepEnabledSet()
    {
        HashSet<Behaviour> keepEnabled = new HashSet<Behaviour>();

        keepEnabled.Add(this);

        if (hardProtectRagdollAnimatorOnDeath)
            AddAllRagdollAnimatorsToKeepSet(keepEnabled);

        if (hardProtectAnimatorOnDeath)
            AddAllAnimatorsToKeepSet(keepEnabled);

        if (componentShutdownMode == DeathComponentShutdownMode.DisableAllExceptRagdollAndAnimator)
        {
            AddAllRagdollAnimatorsToKeepSet(keepEnabled);
            AddAllAnimatorsToKeepSet(keepEnabled);
        }

        if (componentShutdownMode == DeathComponentShutdownMode.Custom)
        {
            if (!disableRagdollAnimatorOnDeath)
                AddAllRagdollAnimatorsToKeepSet(keepEnabled);

            if (!disableAnimatorOnDeath)
                AddAllAnimatorsToKeepSet(keepEnabled);
        }

        if (extraBehavioursToKeepEnabled != null)
        {
            for (int i = 0; i < extraBehavioursToKeepEnabled.Length; i++)
            {
                if (extraBehavioursToKeepEnabled[i] == null)
                    continue;

                keepEnabled.Add(extraBehavioursToKeepEnabled[i]);
            }
        }

        return keepEnabled;
    }

    private void AddAllAnimatorsToKeepSet(HashSet<Behaviour> keepEnabled)
    {
        if (keepEnabled == null)
            return;

        Animator animator = GetComponent<Animator>();
        if (animator != null)
            keepEnabled.Add(animator);

        Animator[] childAnimators = GetComponentsInChildren<Animator>(includeInactiveBehavioursForShutdown);
        for (int i = 0; i < childAnimators.Length; i++)
        {
            if (childAnimators[i] != null)
                keepEnabled.Add(childAnimators[i]);
        }
    }

    private void AddAllRagdollAnimatorsToKeepSet(HashSet<Behaviour> keepEnabled)
    {
        if (keepEnabled == null)
            return;

        if (deathRagdollComponent != null)
            keepEnabled.Add(deathRagdollComponent);

        RagdollAnimator2[] ragdolls =
            GetComponentsInChildren<RagdollAnimator2>(includeInactiveBehavioursForShutdown);

        for (int i = 0; i < ragdolls.Length; i++)
        {
            if (ragdolls[i] != null)
                keepEnabled.Add(ragdolls[i]);
        }
    }

    private bool ShouldDisableBehaviourOnDeath(Behaviour behaviour)
    {
        if (behaviour == null)
            return false;

        if (behaviour == this)
            return false;

        if (hardProtectAnimatorOnDeath && behaviour is Animator)
            return false;

        if (hardProtectRagdollAnimatorOnDeath && behaviour is RagdollAnimator2)
            return false;

        switch (componentShutdownMode)
        {
            case DeathComponentShutdownMode.DisableAllBehaviours:
                return true;

            case DeathComponentShutdownMode.DisableAllExceptRagdollAndAnimator:
                if (behaviour is Animator)
                    return false;

                if (behaviour is RagdollAnimator2)
                    return false;

                return true;

            case DeathComponentShutdownMode.Custom:
            default:
                return ShouldDisableBehaviourOnDeathCustom(behaviour);
        }
    }

    private bool ShouldDisableBehaviourOnDeathCustom(Behaviour behaviour)
    {
        if (behaviour == null)
            return false;

        if (behaviour == this)
            return false;

        if (behaviour is Animator)
            return disableAnimatorOnDeath;

        if (behaviour is NavMeshAgent)
            return disableNavMeshAgentOnDeath;

        if (behaviour is RagdollAnimator2)
            return disableRagdollAnimatorOnDeath;

        if (IsBehaviorGraphAgent(behaviour))
            return disableBehaviorGraphAgentOnDeath;

        if (behaviour is EnemySensor)
            return disableEnemySensorOnDeath;

        if (behaviour is EnemyWeaponShooter || behaviour is EnemyMeleeAttacker)
            return disableWeaponShootersOnDeath;

        return disableOtherBehavioursOnDeath;
    }

    private void DisableWeaponGameObjects()
    {
        EnemyWeaponShooter[] shooters = GetComponentsInChildren<EnemyWeaponShooter>(true);

        for (int i = 0; i < shooters.Length; i++)
        {
            if (shooters[i] != null)
                shooters[i].gameObject.SetActive(false);
        }

        EnemyMeleeAttacker[] meleeAttackers = GetComponentsInChildren<EnemyMeleeAttacker>(true);

        for (int i = 0; i < meleeAttackers.Length; i++)
        {
            if (meleeAttackers[i] != null)
                meleeAttackers[i].gameObject.SetActive(false);
        }
    }

    private void ForceDisableChildBehaviours()
    {
        Behaviour[] childBehaviours = GetComponentsInChildren<Behaviour>(true);

        for (int i = 0; i < childBehaviours.Length; i++)
        {
            Behaviour behaviour = childBehaviours[i];

            if (behaviour == null)
                continue;

            if (behaviour.transform == transform)
                continue;

            if (behaviour is Animator)
                continue;

            if (behaviour is RagdollAnimator2)
                continue;

            if (behaviour is EnemyWeaponShooter weaponShooter)
                weaponShooter.ForceClearRuntimeState();

            if (behaviour is EnemyMeleeAttacker meleeAttacker)
                meleeAttacker.ForceClearRuntimeState();

            behaviour.enabled = false;
        }
    }

    private void DisableBehaviourOnDeath(Behaviour behaviour, bool forced)
    {
        if (behaviour == null)
            return;

        if (behaviour is NavMeshAgent agent && stopNavMeshAgentBeforeDisable)
            StopNavMeshAgent(agent);

        if (IsBehaviorGraphAgent(behaviour))
            TryEndBehaviorGraphAgent(behaviour);

        if (behaviour is EnemyWeaponShooter weaponShooter)
            weaponShooter.ForceClearRuntimeState();

        if (behaviour is EnemyMeleeAttacker meleeAttacker)
            meleeAttacker.ForceClearRuntimeState();

        behaviour.enabled = false;

        if (logDeathComponentShutdown)
        {
            string prefix = forced ? "Force disabled" : "Disabled";

            Debug.Log(
                $"[EnemyHealth] {prefix} component on death: {behaviour.GetType().Name} on {behaviour.gameObject.name}",
                behaviour
            );
        }
    }

    private void StopNavMeshAgent(NavMeshAgent agent)
    {
        if (agent == null)
            return;

        if (!agent.enabled)
            return;

        if (!agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.ResetPath();
    }

    private bool IsBehaviorGraphAgent(Behaviour behaviour)
    {
        if (behaviour == null)
            return false;

        System.Type type = behaviour.GetType();

        while (type != null)
        {
            if (type.Name == "BehaviorGraphAgent")
                return true;

            if (type.FullName == "Unity.Behavior.BehaviorGraphAgent")
                return true;

            type = type.BaseType;
        }

        return false;
    }

    private void TryEndBehaviorGraphAgent(Behaviour behaviour)
    {
        if (behaviour == null)
            return;

        System.Type type = behaviour.GetType();
        System.Reflection.MethodInfo endMethod = type.GetMethod(
            "End",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic
        );

        if (endMethod == null)
            return;

        endMethod.Invoke(behaviour, null);
    }

    private void LockEnemyRagdollGetUpComponents()
    {
        EnemyRagdollGetUp[] getUpComponents =
            GetComponentsInChildren<EnemyRagdollGetUp>(true);

        for (int i = 0; i < getUpComponents.Length; i++)
        {
            if (getUpComponents[i] == null)
                continue;

            getUpComponents[i].LockAsDeathRagdoll();
        }
    }

    private void StartEnforceDeathRagdollBlend()
    {
        if (enforceDeathRagdollBlendRoutine != null)
            StopCoroutine(enforceDeathRagdollBlendRoutine);

        enforceDeathRagdollBlendRoutine = StartCoroutine(EnforceDeathRagdollBlendRoutine());
    }

    private IEnumerator EnforceDeathRagdollBlendRoutine()
    {
        float timer = 0f;
        float targetBlend = Mathf.Clamp01(deathRagdollBlend);

        while (isDead && deathRagdollComponent != null)
        {
            if (!deathRagdollComponent.enabled)
                deathRagdollComponent.enabled = true;

            if (enforceDeathRagdollBlendAfterDeath)
                deathRagdollComponent.RagdollBlend = targetBlend;

            if (enforceDeathRagdollMusclesPowerAfterDeath)
                ApplyDeathRagdollMusclesPower();

            if (enforceDeathRagdollBlendDuration > 0f)
            {
                timer += Time.deltaTime;

                if (timer >= enforceDeathRagdollBlendDuration)
                    break;
            }

            yield return null;
        }

        enforceDeathRagdollBlendRoutine = null;
    }

    private void ApplyDeathRagdollMusclesPower()
    {
        float targetPower = Mathf.Clamp01(deathRagdollMusclesPower);
        bool anyApplied = false;

        if (deathRagdollComponent != null)
            anyApplied |= TrySetRagdollMusclesPower(deathRagdollComponent, targetPower);

        if (applyMusclesPowerToChildRagdollAnimators)
        {
            RagdollAnimator2[] ragdolls = GetComponentsInChildren<RagdollAnimator2>(true);

            for (int i = 0; i < ragdolls.Length; i++)
            {
                RagdollAnimator2 ragdoll = ragdolls[i];

                if (ragdoll == null)
                    continue;

                if (ragdoll == deathRagdollComponent)
                    continue;

                anyApplied |= TrySetRagdollMusclesPower(ragdoll, targetPower);
            }
        }

        if (logDeathRagdollMusclesPower)
        {
            if (anyApplied)
            {
                Debug.Log(
                    $"[EnemyHealth] Death ragdoll muscles power set to {targetPower}.",
                    this
                );
            }
            else
            {
                Debug.LogWarning(
                    "[EnemyHealth] Failed to set death ragdoll muscles power. RA2 member name may be different in this plugin version.",
                    this
                );
            }
        }
    }

    private bool TrySetRagdollMusclesPower(RagdollAnimator2 ragdoll, float value)
    {
        if (ragdoll == null)
            return false;

        bool applied = false;

        applied |= TrySetFloatMember(ragdoll, value, RagdollMusclesPowerMemberNames);

        if (ragdoll.Handler != null)
            applied |= TrySetFloatMember(ragdoll.Handler, value, RagdollMusclesPowerMemberNames);

        if (ragdoll.Settings != null)
            applied |= TrySetFloatMember(ragdoll.Settings, value, RagdollMusclesPowerMemberNames);

        if (ragdoll.Actions != null)
            applied |= TrySetFloatMember(ragdoll.Actions, value, RagdollMusclesPowerMemberNames);

        return applied;
    }

    private bool TrySetFloatMember(object target, float value, string[] memberNames)
    {
        if (target == null || memberNames == null)
            return false;

        bool applied = false;
        Type type = target.GetType();

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        for (int i = 0; i < memberNames.Length; i++)
        {
            string memberName = memberNames[i];

            PropertyInfo property = type.GetProperty(memberName, flags);

            if (property != null && property.CanWrite && IsSupportedNumericType(property.PropertyType))
            {
                property.SetValue(target, ConvertFloatToType(value, property.PropertyType));
                applied = true;
            }

            FieldInfo field = type.GetField(memberName, flags);

            if (field != null && IsSupportedNumericType(field.FieldType))
            {
                field.SetValue(target, ConvertFloatToType(value, field.FieldType));
                applied = true;
            }
        }

        return applied;
    }

    private bool IsSupportedNumericType(Type type)
    {
        return type == typeof(float) ||
               type == typeof(double) ||
               type == typeof(int);
    }

    private object ConvertFloatToType(float value, Type targetType)
    {
        if (targetType == typeof(float))
            return value;

        if (targetType == typeof(double))
            return (double)value;

        if (targetType == typeof(int))
            return Mathf.RoundToInt(value);

        return value;
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

    private void FireAnimatorDeadTrigger()
    {
        if (string.IsNullOrWhiteSpace(deadTriggerName))
            return;

        int hash = Animator.StringToHash(deadTriggerName);

        Animator animator = GetComponent<Animator>();
        if (animator != null)
            FireTriggerIfExists(animator, hash);

        if (!applyIsDeadToChildAnimators)
            return;

        Animator[] childAnimators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < childAnimators.Length; i++)
        {
            if (childAnimators[i] == null || childAnimators[i] == animator)
                continue;

            FireTriggerIfExists(childAnimators[i], hash);
        }
    }

    private void FireTriggerIfExists(Animator animator, int triggerHash)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == triggerHash &&
                parameters[i].type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(triggerHash);
                return;
            }
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
                    $"[EnemyHealth] Animator bool {isDeadBoolName} set to {value} on {animator.gameObject.name}.",
                    animator
                );
            }

            return;
        }
    }

    private DeathRagdollSwitchOption SelectDeathRagdollSwitchOption()
    {
        if (!randomizeDeathRagdollSwitchMode)
            return CreateFallbackDeathRagdollSwitchOption();

        if (deathRagdollSwitchOptions == null || deathRagdollSwitchOptions.Length == 0)
            return CreateFallbackDeathRagdollSwitchOption();

        float totalWeight = 0f;

        for (int i = 0; i < deathRagdollSwitchOptions.Length; i++)
        {
            DeathRagdollSwitchOption option = deathRagdollSwitchOptions[i];

            if (option == null)
                continue;

            totalWeight += Mathf.Max(0f, option.weight);
        }

        if (totalWeight <= 0f)
            return CreateFallbackDeathRagdollSwitchOption();

        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        for (int i = 0; i < deathRagdollSwitchOptions.Length; i++)
        {
            DeathRagdollSwitchOption option = deathRagdollSwitchOptions[i];

            if (option == null)
                continue;

            float weight = Mathf.Max(0f, option.weight);

            if (weight <= 0f)
                continue;

            currentWeight += weight;

            if (randomValue <= currentWeight)
                return option;
        }

        return CreateFallbackDeathRagdollSwitchOption();
    }

    private DeathRagdollSwitchOption CreateFallbackDeathRagdollSwitchOption()
    {
        return new DeathRagdollSwitchOption
        {
            mode = fallbackDeathRagdollSwitchMode,
            weight = 1f,
            sleepDisableMecanimAfter = fallbackSleepDisableMecanimAfter
        };
    }

    private void ApplyDeathRagdollSwitchOption(DeathRagdollSwitchOption option)
    {
        if (deathRagdollComponent == null)
            return;

        DeathRagdollSwitchMode mode = option != null
            ? option.mode
            : fallbackDeathRagdollSwitchMode;

        float sleepDisableMecanimAfter = option != null
            ? Mathf.Max(0f, option.sleepDisableMecanimAfter)
            : Mathf.Max(0f, fallbackSleepDisableMecanimAfter);

        if (convertSleepToFallWhenProtectingAnimator &&
            hardProtectAnimatorOnDeath &&
            mode == DeathRagdollSwitchMode.Sleep)
        {
            mode = DeathRagdollSwitchMode.Fall;

            if (logDeathRagdollProtection)
            {
                Debug.Log(
                    "[EnemyHealth] Converted death ragdoll Sleep to Fall because Animator is hard protected.",
                    this
                );
            }
        }

        if (logDeathRagdollSwitch)
        {
            Debug.Log(
                $"[EnemyHealth] Death ragdoll switch mode selected: {mode}",
                this
            );
        }

        switch (mode)
        {
            case DeathRagdollSwitchMode.Fall:
                deathRagdollComponent.RA2Event_SwitchToFall();
                break;

            case DeathRagdollSwitchMode.Sleep:
                deathRagdollComponent.RA2Event_SwitchToSleep(sleepDisableMecanimAfter);
                break;

            case DeathRagdollSwitchMode.None:
                break;
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

    private EnemyHitboxBinding FindHitbox(Collider hitCollider)
    {
        if (hitboxes == null || hitCollider == null)
            return null;

        for (int i = 0; i < hitboxes.Length; i++)
        {
            EnemyHitboxBinding binding = hitboxes[i];

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

    private EnemyHitboxBinding FindHitboxInList(List<EnemyHitboxBinding> list, Collider col)
    {
        if (list == null || col == null)
            return null;

        for (int i = 0; i < list.Count; i++)
        {
            EnemyHitboxBinding binding = list[i];

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
