using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyStunReceiver : MonoBehaviour
{
    public enum StunHitPart
    {
        Generic,
        Head,
        Body,
        Back
    }

    public enum StunCooldownStartMode
    {
        OnStunStart,
        OnStunEnd
    }

    [System.Serializable]
    public class StunHitboxBinding
    {
        [Header("Stun Hitbox")]
        public Collider stunHitbox;
        public Transform stunHitboxTransform;

        [Header("Part")]
        public StunHitPart hitPart = StunHitPart.Generic;

        [Header("Duration")]
        public bool overrideStunDuration = false;
        [Min(0f)] public float stunDuration = 1f;

        [Header("Animator Trigger")]
        public bool overrideAnimatorTrigger = false;
        public string animatorTriggerName = "Stun";
    }

    [Header("Refs")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyShootLockController shootLockController;

    [Header("Manual Stun Hitboxes")]
    [Tooltip("Only colliders registered here can trigger stun.")]
    [SerializeField] private StunHitboxBinding[] stunHitboxes;

    [Header("Default Stun")]
    [Min(0f)] [SerializeField] private float defaultStunDuration = 1f;
    [SerializeField] private bool ignoreIfDead = true;
    [SerializeField] private bool refreshDurationIfAlreadyStunned = true;

    [Header("Stun Cooldown")]
    [SerializeField] private bool useStunCooldown = true;
    [Min(0f)] [SerializeField] private float stunCooldown = 0.75f;
    [SerializeField] private StunCooldownStartMode cooldownStartMode = StunCooldownStartMode.OnStunStart;
    [SerializeField] private bool forceStunIgnoresCooldown = true;

    [Header("Animator")]
    [SerializeField] private bool setAnimatorIsStunBool = true;
    [SerializeField] private string isStunBoolName = "IsStun";

    [SerializeField] private bool triggerAnimatorStun = true;
    [SerializeField] private bool usePartSpecificTriggers = true;
    [SerializeField] private string defaultStunTriggerName = "Stun";
    [SerializeField] private string headStunTriggerName = "HeadStun";
    [SerializeField] private string bodyStunTriggerName = "BodyStun";
    [SerializeField] private string backStunTriggerName = "BackStun";

    [Header("Confirmed Back Trigger")]
    [SerializeField] private bool confirmedBackTriggerRequiresBodyHitPart = true;

    [Header("Shoot Lock")]
    [SerializeField] private bool addShootLockWhileStunned = true;
    [SerializeField] private bool removeShootLockWhenStunEnds = true;

    [Header("Debug")]
    [SerializeField] private bool logStun = false;
    [SerializeField] private bool logRejectedHitbox = false;
    [SerializeField] private bool logCooldownRejected = false;
    [SerializeField] private bool logMissingAnimatorParameter = false;

    private bool isStunned;
    private float stunEndTime;
    private float nextAllowedStunTime;
    private StunHitPart currentStunPart = StunHitPart.Generic;
    private Coroutine stunRoutine;
    private bool shootLockAddedByThis;

    private int isStunBoolHash;

    public bool IsStunned => isStunned;
    public float StunRemaining => isStunned ? Mathf.Max(0f, stunEndTime - Time.time) : 0f;
    public float StunCooldownRemaining => Mathf.Max(0f, nextAllowedStunTime - Time.time);
    public bool IsInStunCooldown => useStunCooldown && Time.time < nextAllowedStunTime;
    public StunHitPart CurrentStunPart => currentStunPart;

    private void Reset()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        animator = GetComponentInChildren<Animator>();
        shootLockController = GetComponent<EnemyShootLockController>();
    }

    private void Awake()
    {
        if (enemyHealth == null)
            enemyHealth = GetComponent<EnemyHealth>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (shootLockController == null)
            shootLockController = GetComponent<EnemyShootLockController>();

        isStunBoolHash = Animator.StringToHash(isStunBoolName);
        NormalizeManualBindings();
    }

    private void OnValidate()
    {
        NormalizeManualBindings();
    }

    private void OnDisable()
    {
        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
            stunRoutine = null;
        }

        ClearStunRuntime(false);
    }

    public bool TryApplyStunFromHit(Collider hitCollider)
    {
        if (hitCollider == null)
            return false;

        if (ignoreIfDead && enemyHealth != null && enemyHealth.IsDead)
            return false;

        StunHitboxBinding binding = FindBinding(hitCollider);

        if (binding == null)
        {
            if (logRejectedHitbox)
            {
                Debug.Log(
                    $"[EnemyStunReceiver] Rejected stun hitbox: {hitCollider.name}. This collider is not manually registered.",
                    this
                );
            }

            return false;
        }

        if (IsInStunCooldown)
        {
            if (logCooldownRejected)
            {
                Debug.Log(
                    $"[EnemyStunReceiver] Stun rejected by cooldown. Remaining={StunCooldownRemaining:F2}s, Hitbox={hitCollider.name}",
                    this
                );
            }

            return false;
        }

        ApplyStun(binding, false);
        return true;
    }

    public bool IsConfirmedBackTriggerBodyHit(Collider hitCollider)
    {
        if (hitCollider == null)
            return false;

        StunHitboxBinding binding = FindBinding(hitCollider);

        if (binding == null)
            return false;

        if (confirmedBackTriggerRequiresBodyHitPart && binding.hitPart != StunHitPart.Body)
            return false;

        return true;
    }

    public bool TryApplyConfirmedBackTriggerStun(Collider hitCollider)
    {
        if (hitCollider == null)
            return false;

        if (ignoreIfDead && enemyHealth != null && enemyHealth.IsDead)
            return false;

        StunHitboxBinding binding = FindBinding(hitCollider);

        if (binding == null)
        {
            if (logRejectedHitbox)
            {
                Debug.Log(
                    $"[EnemyStunReceiver] Rejected confirmed back trigger body hitbox: {hitCollider.name}. This collider is not manually registered.",
                    this
                );
            }

            return false;
        }

        if (confirmedBackTriggerRequiresBodyHitPart && binding.hitPart != StunHitPart.Body)
        {
            if (logRejectedHitbox)
            {
                Debug.Log(
                    $"[EnemyStunReceiver] Rejected confirmed back trigger. Hitbox={hitCollider.name}, Part={binding.hitPart}, Required=Body",
                    this
                );
            }

            return false;
        }

        if (IsInStunCooldown)
        {
            if (logCooldownRejected)
            {
                Debug.Log(
                    $"[EnemyStunReceiver] Confirmed back trigger stun rejected by cooldown. Remaining={StunCooldownRemaining:F2}s, Hitbox={hitCollider.name}",
                    this
                );
            }

            return false;
        }

        ApplyStunWithTriggerOverride(binding, false, backStunTriggerName);
        return true;
    }

    public void ForceStun(float duration)
    {
        if (!forceStunIgnoresCooldown && IsInStunCooldown)
        {
            if (logCooldownRejected)
            {
                Debug.Log(
                    $"[EnemyStunReceiver] ForceStun rejected by cooldown. Remaining={StunCooldownRemaining:F2}s",
                    this
                );
            }

            return;
        }

        ApplyStun(
            null,
            Mathf.Max(0f, duration),
            StunHitPart.Generic,
            defaultStunTriggerName,
            true
        );
    }

    public void ClearStun()
    {
        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
            stunRoutine = null;
        }

        ClearStunRuntime(true);
    }

    public void ClearCooldown()
    {
        nextAllowedStunTime = 0f;
    }

    private void NormalizeManualBindings()
    {
        if (stunHitboxes == null)
            return;

        for (int i = 0; i < stunHitboxes.Length; i++)
        {
            StunHitboxBinding binding = stunHitboxes[i];

            if (binding == null)
                continue;

            if (binding.stunHitbox != null && binding.stunHitboxTransform == null)
                binding.stunHitboxTransform = binding.stunHitbox.transform;

            if (binding.stunHitbox == null && binding.stunHitboxTransform != null)
                binding.stunHitbox = binding.stunHitboxTransform.GetComponent<Collider>();
        }
    }

    private void ApplyStun(StunHitboxBinding binding, bool forced)
    {
        float duration = binding != null && binding.overrideStunDuration
            ? binding.stunDuration
            : defaultStunDuration;

        StunHitPart part = binding != null
            ? binding.hitPart
            : StunHitPart.Generic;

        string triggerName = ResolveTriggerName(binding, part);

        ApplyStun(binding, duration, part, triggerName, forced);
    }

    private void ApplyStunWithTriggerOverride(
        StunHitboxBinding binding,
        bool forced,
        string triggerOverride)
    {
        float duration = binding != null && binding.overrideStunDuration
            ? binding.stunDuration
            : defaultStunDuration;

        StunHitPart part = binding != null
            ? binding.hitPart
            : StunHitPart.Generic;

        string triggerName = !string.IsNullOrWhiteSpace(triggerOverride)
            ? triggerOverride
            : ResolveTriggerName(binding, part);

        ApplyStun(binding, duration, part, triggerName, forced);
    }

    private void ApplyStun(
        StunHitboxBinding binding,
        float duration,
        StunHitPart part,
        string triggerName,
        bool forced)
    {
        duration = Mathf.Max(0.01f, duration);

        if (isStunned && !refreshDurationIfAlreadyStunned)
            return;

        currentStunPart = part;
        isStunned = true;
        stunEndTime = Time.time + duration;

        if (useStunCooldown && cooldownStartMode == StunCooldownStartMode.OnStunStart)
            StartStunCooldown();

        AddShootLock();

        SetAnimatorStunBool(true);

        if (triggerAnimatorStun)
            SetAnimatorTriggerIfExists(triggerName);

        if (stunRoutine != null)
            StopCoroutine(stunRoutine);

        stunRoutine = StartCoroutine(StunRoutine());

        if (logStun)
        {
            string hitboxName = binding != null && binding.stunHitbox != null
                ? binding.stunHitbox.name
                : "Forced";

            Debug.Log(
                $"[EnemyStunReceiver] Stun triggered. Hitbox={hitboxName}, Part={part}, Duration={duration}, Trigger={triggerName}, Forced={forced}, NextAllowed={nextAllowedStunTime:F2}",
                this
            );
        }
    }

    private IEnumerator StunRoutine()
    {
        while (isStunned && Time.time < stunEndTime)
            yield return null;

        ClearStunRuntime(true);
        stunRoutine = null;
    }

    private void ClearStunRuntime(bool removeShootLock)
    {
        bool wasStunned = isStunned;

        isStunned = false;
        currentStunPart = StunHitPart.Generic;
        stunEndTime = 0f;

        SetAnimatorStunBool(false);

        if (removeShootLock)
            RemoveShootLock();

        if (wasStunned &&
            useStunCooldown &&
            cooldownStartMode == StunCooldownStartMode.OnStunEnd)
        {
            StartStunCooldown();
        }
    }

    private void StartStunCooldown()
    {
        nextAllowedStunTime = Time.time + Mathf.Max(0f, stunCooldown);
    }

    private string ResolveTriggerName(StunHitboxBinding binding, StunHitPart part)
    {
        if (binding != null &&
            binding.overrideAnimatorTrigger &&
            !string.IsNullOrWhiteSpace(binding.animatorTriggerName))
        {
            return binding.animatorTriggerName;
        }

        if (!usePartSpecificTriggers)
            return defaultStunTriggerName;

        switch (part)
        {
            case StunHitPart.Head:
                return headStunTriggerName;

            case StunHitPart.Body:
                return bodyStunTriggerName;

            case StunHitPart.Back:
                return backStunTriggerName;

            case StunHitPart.Generic:
            default:
                return defaultStunTriggerName;
        }
    }

    private void AddShootLock()
    {
        if (!addShootLockWhileStunned)
            return;

        if (shootLockController == null)
            return;

        if (shootLockAddedByThis)
            return;

        shootLockController.AddShootLock();
        shootLockAddedByThis = true;
    }

    private void RemoveShootLock()
    {
        if (!removeShootLockWhenStunEnds)
            return;

        if (!shootLockAddedByThis)
            return;

        if (shootLockController != null)
            shootLockController.RemoveShootLock();

        shootLockAddedByThis = false;
    }

    private void SetAnimatorStunBool(bool value)
    {
        if (!setAnimatorIsStunBool)
            return;

        if (animator == null)
            return;

        if (string.IsNullOrWhiteSpace(isStunBoolName))
            return;

        SetAnimatorBoolIfExists(animator, isStunBoolHash, value);
    }

    private void SetAnimatorTriggerIfExists(string triggerName)
    {
        if (animator == null)
            return;

        if (string.IsNullOrWhiteSpace(triggerName))
            return;

        int triggerHash = Animator.StringToHash(triggerName);
        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter p = parameters[i];

            if (p.nameHash != triggerHash)
                continue;

            if (p.type != AnimatorControllerParameterType.Trigger)
            {
                if (logMissingAnimatorParameter)
                {
                    Debug.LogWarning(
                        $"[EnemyStunReceiver] Animator parameter '{triggerName}' exists but is not a Trigger.",
                        animator
                    );
                }

                return;
            }

            animator.ResetTrigger(triggerHash);
            animator.SetTrigger(triggerHash);
            return;
        }

        if (logMissingAnimatorParameter)
        {
            Debug.LogWarning(
                $"[EnemyStunReceiver] Animator Trigger not found: {triggerName}",
                animator
            );
        }
    }

    private void SetAnimatorBoolIfExists(Animator targetAnimator, int parameterHash, bool value)
    {
        if (targetAnimator == null)
            return;

        AnimatorControllerParameter[] parameters = targetAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter p = parameters[i];

            if (p.nameHash != parameterHash)
                continue;

            if (p.type != AnimatorControllerParameterType.Bool)
            {
                if (logMissingAnimatorParameter)
                {
                    Debug.LogWarning(
                        $"[EnemyStunReceiver] Animator parameter '{isStunBoolName}' exists but is not a Bool.",
                        targetAnimator
                    );
                }

                return;
            }

            targetAnimator.SetBool(parameterHash, value);
            return;
        }

        if (logMissingAnimatorParameter)
        {
            Debug.LogWarning(
                $"[EnemyStunReceiver] Animator Bool not found: {isStunBoolName}",
                targetAnimator
            );
        }
    }

    private StunHitboxBinding FindBinding(Collider hitCollider)
    {
        if (stunHitboxes == null || hitCollider == null)
            return null;

        for (int i = 0; i < stunHitboxes.Length; i++)
        {
            StunHitboxBinding b = stunHitboxes[i];

            if (b == null)
                continue;

            if (b.stunHitbox == hitCollider)
                return b;

            if (b.stunHitboxTransform != null &&
                b.stunHitboxTransform == hitCollider.transform)
            {
                return b;
            }
        }

        return null;
    }
}