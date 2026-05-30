using System.Collections;
using UnityEngine;
using UnityEngine.AI;

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
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private EnemyStatus enemyStatus;

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
    [SerializeField] private bool setAnimatorFlashStunBool = true;
    [SerializeField] private string isStunByFlashBoolName = "IsStunByFlash";

    [Header("Flash Bang Stun")]
    [SerializeField] private bool overrideFlashBangStatusDuration = false;
    [Min(0f)] [SerializeField] private float flashBangStatusDuration = 3f;

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

    [Header("Movement Lock")]
    [SerializeField] private bool stopMovementWhileStunned = true;
    [SerializeField] private bool resetPathWhenStunned = false;
    [SerializeField] private bool forceZeroVelocityWhenStunned = true;
    [SerializeField] private bool resumeMovementWhenStunEnds = true;

    [Header("Debug")]
    [SerializeField] private bool logStun = false;
    [SerializeField] private bool logRejectedHitbox = false;
    [SerializeField] private bool logCooldownRejected = false;
    [SerializeField] private bool logMissingAnimatorParameter = false;

    private bool isStunned;
    private bool isFlashBangStun;
    private float stunEndTime;
    private float flashBangStunEndTime;
    private float nextAllowedStunTime;
    private StunHitPart currentStunPart = StunHitPart.Generic;
    private Coroutine stunRoutine;
    private Coroutine flashBangStunRoutine;
    private bool shootLockAddedByThis;
    private bool cachedAgentStopped;
    private bool hasCachedAgentStopped;

    private int isStunBoolHash;
    private int isStunByFlashBoolHash;

    public bool IsStunned => isStunned;
    public bool IsFlashBangStun => isFlashBangStun;
    public float StunRemaining => isStunned ? Mathf.Max(0f, stunEndTime - Time.time) : 0f;
    public float FlashBangStunRemaining => isFlashBangStun ? Mathf.Max(0f, flashBangStunEndTime - Time.time) : 0f;
    public float StunCooldownRemaining => Mathf.Max(0f, nextAllowedStunTime - Time.time);
    public bool IsInStunCooldown => useStunCooldown && Time.time < nextAllowedStunTime;
    public StunHitPart CurrentStunPart => currentStunPart;

    private void Reset()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        animator = GetComponentInChildren<Animator>();
        shootLockController = GetComponent<EnemyShootLockController>();
        agent = GetComponent<NavMeshAgent>();
        enemyStatus = GetComponent<EnemyStatus>();
    }

    private void Awake()
    {
        if (enemyHealth == null)
            enemyHealth = GetComponent<EnemyHealth>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (shootLockController == null)
            shootLockController = GetComponent<EnemyShootLockController>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (enemyStatus == null)
            enemyStatus = GetComponent<EnemyStatus>();

        isStunBoolHash = Animator.StringToHash(isStunBoolName);
        isStunByFlashBoolHash = Animator.StringToHash(isStunByFlashBoolName);
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

        if (flashBangStunRoutine != null)
        {
            StopCoroutine(flashBangStunRoutine);
            flashBangStunRoutine = null;
        }

        ClearStunRuntime(false);
        ClearFlashBangStunStatus();
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
            true,
            false
        );
    }

    public void ForceFlashBangStun(float duration)
    {
        float statusDuration = overrideFlashBangStatusDuration
            ? flashBangStatusDuration
            : duration;

        ForceFlashBangStun(duration, statusDuration);
    }

    public void ForceFlashBangStun(float duration, float flashStatusDuration)
    {
        if (!forceStunIgnoresCooldown && IsInStunCooldown)
        {
            if (logCooldownRejected)
            {
                Debug.Log(
                    $"[EnemyStunReceiver] Flash stun rejected by cooldown. Remaining={StunCooldownRemaining:F2}s",
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
            true,
            true,
            flashStatusDuration
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

        ApplyStun(binding, duration, part, triggerName, forced, false);
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

        ApplyStun(binding, duration, part, triggerName, forced, false);
    }

    private void ApplyStun(
        StunHitboxBinding binding,
        float duration,
        StunHitPart part,
        string triggerName,
        bool forced,
        bool flashBangStun,
        float flashStatusDuration = 0f)
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
        StopMovement();

        SetAnimatorStunBool(true);

        if (flashBangStun)
            StartFlashBangStunStatus(flashStatusDuration);

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
                $"[EnemyStunReceiver] Stun triggered. Hitbox={hitboxName}, Part={part}, Duration={duration}, Trigger={triggerName}, Forced={forced}, FlashBang={flashBangStun}, NextAllowed={nextAllowedStunTime:F2}",
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

        RestoreMovement();

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

    private void StartFlashBangStunStatus(float duration)
    {
        duration = Mathf.Max(0.01f, duration);

        isFlashBangStun = true;
        flashBangStunEndTime = Time.time + duration;

        SetAnimatorFlashStunBool(true);
        SetEnemyFlashBangStunStatus(true);

        if (flashBangStunRoutine != null)
            StopCoroutine(flashBangStunRoutine);

        flashBangStunRoutine = StartCoroutine(FlashBangStunStatusRoutine());
    }

    private IEnumerator FlashBangStunStatusRoutine()
    {
        while (isFlashBangStun && Time.time < flashBangStunEndTime)
            yield return null;

        ClearFlashBangStunStatus();
        flashBangStunRoutine = null;
    }

    private void ClearFlashBangStunStatus()
    {
        isFlashBangStun = false;
        flashBangStunEndTime = 0f;

        SetAnimatorFlashStunBool(false);
        SetEnemyFlashBangStunStatus(false);
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

    private void StopMovement()
    {
        if (!stopMovementWhileStunned)
            return;

        if (agent == null)
            return;

        if (!agent.enabled || !agent.isOnNavMesh)
            return;

        if (!hasCachedAgentStopped)
        {
            cachedAgentStopped = agent.isStopped;
            hasCachedAgentStopped = true;
        }

        agent.isStopped = true;

        if (resetPathWhenStunned)
            agent.ResetPath();

        if (forceZeroVelocityWhenStunned)
            agent.velocity = Vector3.zero;
    }

    private void RestoreMovement()
    {
        if (!hasCachedAgentStopped)
            return;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (resumeMovementWhenStunEnds)
                agent.isStopped = false;
            else
                agent.isStopped = cachedAgentStopped;
        }

        hasCachedAgentStopped = false;
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

    private void SetAnimatorFlashStunBool(bool value)
    {
        if (!setAnimatorFlashStunBool)
            return;

        if (animator == null)
            return;

        if (string.IsNullOrWhiteSpace(isStunByFlashBoolName))
            return;

        SetAnimatorBoolIfExists(animator, isStunByFlashBoolHash, value);
    }

    private void SetEnemyFlashBangStunStatus(bool value)
    {
        if (enemyStatus == null)
            enemyStatus = GetComponent<EnemyStatus>();

        if (enemyStatus != null)
        {
            enemyStatus.SetFlashBangStun(value);

            if (value)
                enemyStatus.SetEscapingFromGrenade(false);
        }
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
