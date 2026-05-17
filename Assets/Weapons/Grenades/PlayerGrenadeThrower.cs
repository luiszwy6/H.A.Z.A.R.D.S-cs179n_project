using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public class PlayerGrenadeThrower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerAimSettings playerAimSettings;
    [SerializeField] private PlayerWeaponSlots playerWeaponSlots;
    [SerializeField] private PlayerGrenadeSlots grenadeSlots;

    [Header("Throw Origin")]
    [SerializeField] private Transform throwOrigin;
    [SerializeField] private Transform fallbackForwardSource;

    [Header("Spawn Safety")]
    [SerializeField] private bool offsetSpawnFromThrowOrigin = true;
    [SerializeField] private float spawnForwardOffset = 0.6f;
    [SerializeField] private float spawnUpOffset = 0.08f;
    [SerializeField] private bool useThrowVelocityDirectionForSpawnOffset = true;

    [Header("Input")]
    [SerializeField] private string throwActionName = "Throw";

    [Header("Animator")]
    [SerializeField] private string throwTriggerName = "ThrowGrenade";
    [SerializeField] private string isThrowingBoolName = "IsThrowingGrenade";
    [SerializeField] private string isAimingBoolName = "IsAiming";
    [SerializeField] private bool resetThrowTriggerBeforeSet = true;
    [SerializeField] private bool forceAimingAnimatorFalseWhileThrowing = true;

    [Header("Animator Events")]
    [SerializeField] private bool useAnimatorEventForRelease = true;
    [SerializeField] private bool useAnimatorEventForEnd = true;
    [SerializeField] private bool releaseIfEndEventBeforeRelease = true;

    [Header("Aim Suppression By Throw Events")]
    [SerializeField] private bool forceExitAimingOnReleaseEvent = true;
    [SerializeField] private bool keepAimingSuppressedUntilEndEvent = true;

    [Header("Safety Fallback")]
    [SerializeField] private bool useMissingEventFallback = false;
    [SerializeField] private float fallbackReleaseDelay = 0.35f;
    [SerializeField] private float fallbackEndDelay = 1.2f;

    [Header("Throw Facing")]
    [SerializeField] private bool turnTowardMouseWhenNotAiming = true;
    [SerializeField] private bool updateMouseDirectionWhileThrowing = false;
    [SerializeField] private bool snapToThrowDirectionOnRelease = true;
    [SerializeField] private Transform turnRootOverride;
    [SerializeField] private float throwTurnSpeedDegrees = 720f;
    [SerializeField] private float throwTurnSnapAngle = 1f;

    [Header("Default Throw Physics")]
    [SerializeField] private float fixedThrowSpeed = 13f;
    [SerializeField] private float fixedThrowAngle = 32f;
    [SerializeField] private Vector3 angularVelocity = new Vector3(8f, 4f, 2f);

    [Header("Cooldown")]
    [SerializeField] private bool useThrowCooldown = true;
    [SerializeField] private float throwCooldown = 0.8f;

    [Header("Weapon Visibility")]
    [SerializeField] private bool hideCurrentWeaponDuringThrow = true;
    [SerializeField] private bool restoreWeaponVisibilityAfterThrow = true;

    [Header("Weapon Switch Lock / Restore")]
    [SerializeField] private bool lockWeaponSwitchWhileThrowing = true;
    [SerializeField] private bool restoreWeaponOnThrowEnd = true;

    [Header("Rig")]
    [SerializeField] private List<Rig> throwDisableRigs = new List<Rig>();

    [Tooltip("Legacy single rig slot. Keep this if an old prefab already has one rig assigned here.")]
    [SerializeField] private Rig throwDisableRig;

    [SerializeField] private bool includeLegacySingleRig = true;
    [SerializeField] private float throwRigWeight = 0f;
    [SerializeField] private bool restoreThrowRigAfterThrow = true;

    [Header("Locks")]
    [SerializeField] private bool blockWhenMovementLocked = true;
    [SerializeField] private bool blockWhenDiving = true;
    [SerializeField] private bool blockWhenSliding = true;
    [SerializeField] private bool blockWhenRecoveryLocked = true;

    [Tooltip("If true, aiming is also suppressed immediately when throw starts. Default false keeps suppression controlled by Animator Events.")]
    [SerializeField] private bool cancelAimOnThrow = false;

    [SerializeField] private bool cancelShootingOnThrow = true;
    [SerializeField] private bool cancelReloadOnThrow = true;

    [Header("Debug")]
    [SerializeField] private bool logThrow = false;
    [SerializeField] private bool drawThrowDirection = false;
    [SerializeField] private bool drawSpawnOffset = true;
    [SerializeField] private bool drawFacingDirection = false;
    [SerializeField] private float debugDrawDuration = 1f;

    private InputAction throwAction;

    private int throwTriggerHash;
    private int isThrowingBoolHash;
    private int isAimingBoolHash;

    private bool isThrowing;
    private bool releasedThisThrow;
    private bool aimSuppressionActive;
    private float nextThrowAllowedTime = -999f;

    private GameObject cachedCurrentWeaponObject;
    private bool cachedCurrentWeaponWasActive;
    private bool hasCachedCurrentWeaponObject;

    private int cachedWeaponIndexBeforeThrow = -1;
    private bool hasCachedWeaponIndexBeforeThrow;

    private readonly Dictionary<Rig, float> cachedThrowRigWeights = new Dictionary<Rig, float>();

    private Coroutine fallbackRoutine;

    private bool cachedWasAiming;
    private bool turnDuringThisThrow;
    private bool hasThrowFacingDirection;
    private Vector3 throwFacingDirection;

    public bool IsThrowing => isThrowing;
    public bool ReleasedThisThrow => releasedThisThrow;

    private void Reset()
    {
        playerInput = GetComponent<PlayerInput>();
        animator = GetComponentInChildren<Animator>();
        playerMovement = GetComponent<PlayerMovement>();
        playerAimSettings = GetComponent<PlayerAimSettings>();
        playerWeaponSlots = GetComponent<PlayerWeaponSlots>();
        grenadeSlots = GetComponent<PlayerGrenadeSlots>();
        fallbackForwardSource = transform;

        if (throwOrigin == null)
            throwOrigin = transform;
    }

    private void Awake()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerAimSettings == null)
            playerAimSettings = GetComponent<PlayerAimSettings>();

        if (playerWeaponSlots == null)
            playerWeaponSlots = GetComponent<PlayerWeaponSlots>();

        if (grenadeSlots == null)
            grenadeSlots = GetComponent<PlayerGrenadeSlots>();

        if (fallbackForwardSource == null)
            fallbackForwardSource = transform;

        if (throwOrigin == null)
            throwOrigin = transform;

        throwTriggerHash = Animator.StringToHash(throwTriggerName);
        isThrowingBoolHash = Animator.StringToHash(isThrowingBoolName);
        isAimingBoolHash = Animator.StringToHash(isAimingBoolName);

        CacheInputAction();
    }

    private void OnEnable()
    {
        CacheInputAction();
        throwAction?.Enable();
    }

    private void OnDisable()
    {
        throwAction?.Disable();

        if (fallbackRoutine != null)
        {
            StopCoroutine(fallbackRoutine);
            fallbackRoutine = null;
        }

        ForceEndThrowRuntime();
    }

    private void Update()
    {
        if (throwAction != null && throwAction.WasPressedThisFrame())
            TryStartThrow();

        if (isThrowing && forceAimingAnimatorFalseWhileThrowing)
            SetAnimatorAimingBool(false);

        if (isThrowing && aimSuppressionActive && keepAimingSuppressedUntilEndEvent)
            ForceExitAimingForThrowFrame();
    }

    private void LateUpdate()
    {
        if (!isThrowing)
            return;

        if (forceAimingAnimatorFalseWhileThrowing)
            SetAnimatorAimingBool(false);

        if (aimSuppressionActive && keepAimingSuppressedUntilEndEvent)
            ForceExitAimingForThrowFrame();

        if (!turnDuringThisThrow)
            return;

        if (updateMouseDirectionWhileThrowing)
            CacheThrowFacingDirection();

        ApplyThrowFacingTurn();
    }

    public bool TryStartThrow()
    {
        if (!CanStartThrow())
            return false;

        cachedWasAiming = IsPlayerAiming();

        turnDuringThisThrow =
            turnTowardMouseWhenNotAiming &&
            !cachedWasAiming;

        isThrowing = true;
        releasedThisThrow = false;
        aimSuppressionActive = false;

        CacheWeaponBeforeThrow();
        SetWeaponSwitchLock(true);

        CacheThrowFacingDirection();

        SetAnimatorThrowingBool(true);
        SetAnimatorAimingBool(false);

        if (cancelAimOnThrow)
            BeginAimSuppressionForThrow();

        if (useThrowCooldown)
            nextThrowAllowedTime = Time.time + Mathf.Max(0f, throwCooldown);

        if (cancelShootingOnThrow)
            CancelShootingRuntime();

        if (cancelReloadOnThrow)
            CancelReloadRuntime();

        ApplyThrowRigWeight();
        ApplyWeaponVisibility();

        TriggerThrowAnimation();

        if (turnDuringThisThrow)
            ApplyThrowFacingTurn();

        if (fallbackRoutine != null)
            StopCoroutine(fallbackRoutine);

        if (useMissingEventFallback)
            fallbackRoutine = StartCoroutine(MissingEventFallbackRoutine());

        if (drawThrowDirection)
        {
            PlayerGrenadeSlots.GrenadeSlot previewSlot = grenadeSlots != null ? grenadeSlots.CurrentSlot : null;
            Vector3 origin = ResolveThrowOrigin();
            Vector3 velocity = ResolveFixedThrowVelocity(previewSlot);
            Debug.DrawRay(origin, velocity, Color.yellow, debugDrawDuration, false);
        }

        if (drawFacingDirection && hasThrowFacingDirection)
        {
            Vector3 origin = ResolveTurnRoot().position + Vector3.up * 1.2f;
            Debug.DrawRay(origin, throwFacingDirection * 2f, Color.cyan, debugDrawDuration, false);
        }

        if (logThrow)
        {
            PlayerGrenadeSlots.GrenadeSlot previewSlot = grenadeSlots != null ? grenadeSlots.CurrentSlot : null;

            Debug.Log(
                $"[PlayerGrenadeThrower] Throw started. WasAiming={cachedWasAiming}, TurnDuringThrow={turnDuringThisThrow}, IsThrowingGrenade=true, HasFacingDir={hasThrowFacingDirection}, CachedWeaponIndex={cachedWeaponIndexBeforeThrow}, Grenade={(previewSlot != null ? previewSlot.slotName : "None")}, Speed={ResolveThrowSpeed(previewSlot)}, Angle={ResolveThrowAngle(previewSlot)}",
                this
            );
        }

        return true;
    }

    public void ReleaseGrenade()
    {
        if (!isThrowing)
            return;

        if (releasedThisThrow)
            return;

        if (forceExitAimingOnReleaseEvent)
            BeginAimSuppressionForThrow();

        ReleaseGrenadeNow();
    }

    public void ThrowGrenade()
    {
        ReleaseGrenade();
    }

    public void AnimEvent_ReleaseGrenade()
    {
        ReleaseGrenade();
    }

    public void AnimEvent_ThrowGrenade()
    {
        ReleaseGrenade();
    }

    public void EndGrenadeThrow()
    {
        if (!isThrowing)
            return;

        if (!releasedThisThrow && releaseIfEndEventBeforeRelease)
        {
            if (forceExitAimingOnReleaseEvent)
                BeginAimSuppressionForThrow();

            ReleaseGrenadeNow();
        }

        ForceEndThrowRuntime();
    }

    public void FinishGrenadeThrow()
    {
        EndGrenadeThrow();
    }

    public void AnimEvent_EndGrenadeThrow()
    {
        EndGrenadeThrow();
    }

    public void AnimEvent_FinishGrenadeThrow()
    {
        EndGrenadeThrow();
    }

    private IEnumerator MissingEventFallbackRoutine()
    {
        if (!useAnimatorEventForRelease)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, fallbackReleaseDelay));

            if (isThrowing && !releasedThisThrow)
                ReleaseGrenade();
        }

        if (!useAnimatorEventForEnd)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, fallbackEndDelay));

            if (isThrowing)
                EndGrenadeThrow();
        }

        fallbackRoutine = null;
    }

    private void ReleaseGrenadeNow()
    {
        if (!isThrowing)
            return;

        if (releasedThisThrow)
            return;

        if (grenadeSlots == null)
            return;

        if (!grenadeSlots.TryConsumeCurrentGrenade(out PlayerGrenadeSlots.GrenadeSlot slot))
            return;

        if (slot == null || slot.worldPrefab == null)
            return;

        if (snapToThrowDirectionOnRelease && hasThrowFacingDirection)
            SnapToThrowFacingDirection();

        releasedThisThrow = true;

        Vector3 throwOriginPosition = ResolveThrowOrigin();
        Vector3 velocity = ResolveFixedThrowVelocity(slot);
        Vector3 spawnPosition = ResolveSafeSpawnPosition(throwOriginPosition, velocity, slot);

        Quaternion rotation = ResolveSpawnRotation(velocity);
        GrenadeWorldController instance = Instantiate(slot.worldPrefab, spawnPosition, rotation);

        instance.Launch(
            gameObject,
            transform,
            velocity,
            ResolveAngularVelocity(slot)
        );

        if (drawThrowDirection)
            Debug.DrawRay(spawnPosition, velocity, Color.green, debugDrawDuration, false);

        if (drawSpawnOffset)
            Debug.DrawLine(throwOriginPosition, spawnPosition, Color.magenta, debugDrawDuration, false);

        if (logThrow)
        {
            Debug.Log(
                $"[PlayerGrenadeThrower] Released grenade={slot.slotName}, WasAiming={cachedWasAiming}, TurnDuringThrow={turnDuringThisThrow}, AimSuppressionActive={aimSuppressionActive}, Origin={throwOriginPosition}, Spawn={spawnPosition}, Velocity={velocity}, Speed={ResolveThrowSpeed(slot)}, Angle={ResolveThrowAngle(slot)}, Angular={ResolveAngularVelocity(slot)}",
                this
            );
        }
    }

    private bool CanStartThrow()
    {
        if (isThrowing)
            return false;

        if (useThrowCooldown && Time.time < nextThrowAllowedTime)
            return false;

        if (grenadeSlots == null || !grenadeSlots.HasUsableCurrentGrenade)
            return false;

        if (playerMovement != null)
        {
            if (blockWhenMovementLocked && playerMovement.externalMovementLock)
                return false;

            if (blockWhenDiving && playerMovement.IsDivingNow)
                return false;

            if (blockWhenSliding && playerMovement.IsSlidingNow)
                return false;

            if (blockWhenRecoveryLocked && playerMovement.IsRecoveryLockedNow)
                return false;
        }

        return true;
    }

    private Vector3 ResolveThrowOrigin()
    {
        if (throwOrigin != null)
            return throwOrigin.position;

        return transform.position + Vector3.up * 1.2f;
    }

    private Vector3 ResolveFixedThrowVelocity(PlayerGrenadeSlots.GrenadeSlot slot)
    {
        Vector3 horizontalDir = ResolveThrowHorizontalDirection();
        horizontalDir.y = 0f;

        if (horizontalDir.sqrMagnitude <= 0.0001f)
            horizontalDir = transform.forward;

        horizontalDir.y = 0f;

        if (horizontalDir.sqrMagnitude <= 0.0001f)
            horizontalDir = Vector3.forward;

        horizontalDir.Normalize();

        float speed = ResolveThrowSpeed(slot);
        float angle = Mathf.Clamp(ResolveThrowAngle(slot), -89f, 89f) * Mathf.Deg2Rad;

        float horizontalSpeed = Mathf.Cos(angle) * speed;
        float verticalSpeed = Mathf.Sin(angle) * speed;

        return horizontalDir * horizontalSpeed + Vector3.up * verticalSpeed;
    }

    private float ResolveThrowSpeed(PlayerGrenadeSlots.GrenadeSlot slot)
    {
        if (slot != null &&
            slot.throwPhysics != null &&
            slot.throwPhysics.useCustomThrowPhysics)
        {
            return Mathf.Max(0f, slot.throwPhysics.fixedThrowSpeed);
        }

        return Mathf.Max(0f, fixedThrowSpeed);
    }

    private float ResolveThrowAngle(PlayerGrenadeSlots.GrenadeSlot slot)
    {
        if (slot != null &&
            slot.throwPhysics != null &&
            slot.throwPhysics.useCustomThrowPhysics)
        {
            return Mathf.Clamp(slot.throwPhysics.fixedThrowAngle, -89f, 89f);
        }

        return Mathf.Clamp(fixedThrowAngle, -89f, 89f);
    }

    private Vector3 ResolveAngularVelocity(PlayerGrenadeSlots.GrenadeSlot slot)
    {
        if (slot != null &&
            slot.throwPhysics != null &&
            slot.throwPhysics.useCustomThrowPhysics)
        {
            return slot.throwPhysics.angularVelocity;
        }

        return angularVelocity;
    }

    private float ResolveSpawnForwardOffset(PlayerGrenadeSlots.GrenadeSlot slot)
    {
        if (slot != null &&
            slot.throwPhysics != null &&
            slot.throwPhysics.useCustomThrowPhysics &&
            slot.throwPhysics.useCustomSpawnOffset)
        {
            return slot.throwPhysics.spawnForwardOffset;
        }

        return spawnForwardOffset;
    }

    private float ResolveSpawnUpOffset(PlayerGrenadeSlots.GrenadeSlot slot)
    {
        if (slot != null &&
            slot.throwPhysics != null &&
            slot.throwPhysics.useCustomThrowPhysics &&
            slot.throwPhysics.useCustomSpawnOffset)
        {
            return slot.throwPhysics.spawnUpOffset;
        }

        return spawnUpOffset;
    }

    private Vector3 ResolveThrowHorizontalDirection()
    {
        if (hasThrowFacingDirection)
            return throwFacingDirection;

        Transform turnRoot = ResolveTurnRoot();

        Vector3 forward = turnRoot.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.0001f)
            return forward.normalized;

        Transform source = fallbackForwardSource != null ? fallbackForwardSource : transform;

        Vector3 dir = source.forward;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.0001f)
            return dir.normalized;

        dir = transform.forward;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.0001f)
            return dir.normalized;

        return Vector3.forward;
    }

    private Vector3 ResolveSafeSpawnPosition(
        Vector3 origin,
        Vector3 velocity,
        PlayerGrenadeSlots.GrenadeSlot slot)
    {
        if (!offsetSpawnFromThrowOrigin)
            return origin;

        Vector3 dir = Vector3.zero;

        if (useThrowVelocityDirectionForSpawnOffset && velocity.sqrMagnitude > 0.001f)
        {
            dir = velocity;
            dir.y = 0f;
        }

        if (dir.sqrMagnitude <= 0.001f)
            dir = ResolveThrowHorizontalDirection();

        if (dir.sqrMagnitude <= 0.001f)
        {
            Transform source = fallbackForwardSource != null ? fallbackForwardSource : transform;
            dir = source.forward;
            dir.y = 0f;
        }

        if (dir.sqrMagnitude <= 0.001f)
        {
            dir = transform.forward;
            dir.y = 0f;
        }

        if (dir.sqrMagnitude <= 0.001f)
            dir = Vector3.forward;

        dir.Normalize();

        return origin +
               dir * Mathf.Max(0f, ResolveSpawnForwardOffset(slot)) +
               Vector3.up * ResolveSpawnUpOffset(slot);
    }

    private Quaternion ResolveSpawnRotation(Vector3 velocity)
    {
        Vector3 forward = velocity;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.001f)
            forward = ResolveThrowHorizontalDirection();

        if (forward.sqrMagnitude <= 0.001f)
        {
            Transform source = fallbackForwardSource != null ? fallbackForwardSource : transform;
            forward = source.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude <= 0.001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude <= 0.001f)
            forward = Vector3.forward;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private bool IsPlayerAiming()
    {
        return playerAimSettings != null && playerAimSettings.IsAiming;
    }

    private void CacheThrowFacingDirection()
    {
        hasThrowFacingDirection = false;
        throwFacingDirection = Vector3.zero;

        if (turnDuringThisThrow)
        {
            if (TryResolveMouseYawDirection(out Vector3 mouseDir))
            {
                throwFacingDirection = mouseDir;
                hasThrowFacingDirection = true;
                return;
            }
        }

        Transform turnRoot = ResolveTurnRoot();

        Vector3 forward = turnRoot.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.0001f)
        {
            throwFacingDirection = forward.normalized;
            hasThrowFacingDirection = true;
            return;
        }

        Transform source = fallbackForwardSource != null ? fallbackForwardSource : transform;

        forward = source.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.0001f)
        {
            throwFacingDirection = forward.normalized;
            hasThrowFacingDirection = true;
            return;
        }

        throwFacingDirection = Vector3.forward;
        hasThrowFacingDirection = true;
    }

    private bool TryResolveMouseYawDirection(out Vector3 dir)
    {
        dir = Vector3.zero;

        Transform turnRoot = ResolveTurnRoot();
        Vector3 origin = turnRoot.position;

        if (playerAimSettings != null && playerAimSettings.HasMouseAimPoint)
        {
            dir = playerAimSettings.MouseAimPoint - origin;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
                return true;
            }
        }

        if (playerMovement != null && playerMovement.ActiveView != null)
        {
            Vector3 viewDir = playerMovement.ActiveView.ViewAimWorldDir;
            viewDir.y = 0f;

            if (viewDir.sqrMagnitude > 0.0001f)
            {
                dir = viewDir.normalized;
                return true;
            }
        }

        Camera cam = null;

        if (playerAimSettings != null && playerAimSettings.aimCamera != null)
            cam = playerAimSettings.aimCamera;
        else
            cam = Camera.main;

        if (cam != null && Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(mousePos);

            LayerMask mask = playerAimSettings != null ? playerAimSettings.mouseAimLayers : ~0;
            float maxDistance = playerAimSettings != null ? playerAimSettings.mouseAimDistance : 100f;

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, mask, QueryTriggerInteraction.Ignore))
            {
                dir = hit.point - origin;
                dir.y = 0f;

                if (dir.sqrMagnitude > 0.0001f)
                {
                    dir.Normalize();
                    return true;
                }
            }

            Plane groundPlane = new Plane(Vector3.up, origin);

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 point = ray.GetPoint(enter);
                dir = point - origin;
                dir.y = 0f;

                if (dir.sqrMagnitude > 0.0001f)
                {
                    dir.Normalize();
                    return true;
                }
            }
        }

        return false;
    }

    private void ApplyThrowFacingTurn()
    {
        if (!hasThrowFacingDirection)
            return;

        Vector3 targetForward = throwFacingDirection;
        targetForward.y = 0f;

        if (targetForward.sqrMagnitude <= 0.0001f)
            return;

        targetForward.Normalize();

        Transform turnRoot = ResolveTurnRoot();

        Vector3 currentForward = turnRoot.forward;
        currentForward.y = 0f;

        if (currentForward.sqrMagnitude <= 0.0001f)
            currentForward = Vector3.forward;

        currentForward.Normalize();

        Quaternion targetRotation = Quaternion.LookRotation(targetForward, Vector3.up);

        float angle = Vector3.Angle(currentForward, targetForward);

        if (angle <= throwTurnSnapAngle)
        {
            turnRoot.rotation = targetRotation;
            return;
        }

        float maxDegrees = Mathf.Max(0f, throwTurnSpeedDegrees) * Time.deltaTime;

        turnRoot.rotation = Quaternion.RotateTowards(
            turnRoot.rotation,
            targetRotation,
            maxDegrees
        );
    }

    private void SnapToThrowFacingDirection()
    {
        if (!hasThrowFacingDirection)
            return;

        Vector3 targetForward = throwFacingDirection;
        targetForward.y = 0f;

        if (targetForward.sqrMagnitude <= 0.0001f)
            return;

        targetForward.Normalize();

        Transform turnRoot = ResolveTurnRoot();
        turnRoot.rotation = Quaternion.LookRotation(targetForward, Vector3.up);
    }

    private Transform ResolveTurnRoot()
    {
        if (turnRootOverride != null)
            return turnRootOverride;

        if (playerMovement != null)
            return playerMovement.transform;

        return transform;
    }

    private void BeginAimSuppressionForThrow()
    {
        aimSuppressionActive = true;
        ForceExitAimingForThrowFrame();
    }

    private void ForceExitAimingForThrowFrame()
    {
        if (playerMovement != null)
            playerMovement.CancelAimAndRequireRepress();
        else if (playerAimSettings != null)
            playerAimSettings.CancelAimAndRequireRepress();

        SetAnimatorAimingBool(false);
    }

    private void CacheWeaponBeforeThrow()
    {
        hasCachedWeaponIndexBeforeThrow = false;
        cachedWeaponIndexBeforeThrow = -1;

        if (playerWeaponSlots == null)
            return;

        int index = playerWeaponSlots.CurrentWeaponIndex;

        if (index < 0)
            return;

        cachedWeaponIndexBeforeThrow = index;
        hasCachedWeaponIndexBeforeThrow = true;
    }

    private void RestoreWeaponBeforeThrow()
    {
        if (!restoreWeaponOnThrowEnd)
        {
            ClearCachedWeaponBeforeThrow();
            return;
        }

        if (!hasCachedWeaponIndexBeforeThrow)
            return;

        if (playerWeaponSlots != null)
            playerWeaponSlots.ForceRestoreWeaponSilently(cachedWeaponIndexBeforeThrow);

        ClearCachedWeaponBeforeThrow();
    }

    private void ClearCachedWeaponBeforeThrow()
    {
        cachedWeaponIndexBeforeThrow = -1;
        hasCachedWeaponIndexBeforeThrow = false;
    }

    private void SetWeaponSwitchLock(bool locked)
    {
        if (!lockWeaponSwitchWhileThrowing)
            return;

        if (playerWeaponSlots == null)
            return;

        playerWeaponSlots.SetExternalSwitchLock(locked);
    }

    private void CacheInputAction()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        if (!string.IsNullOrWhiteSpace(throwActionName))
            throwAction = playerInput.actions.FindAction(throwActionName, false);
    }

    private void TriggerThrowAnimation()
    {
        if (animator == null)
            return;

        if (string.IsNullOrWhiteSpace(throwTriggerName))
            return;

        if (resetThrowTriggerBeforeSet)
            animator.ResetTrigger(throwTriggerHash);

        animator.SetTrigger(throwTriggerHash);
    }

    private void SetAnimatorThrowingBool(bool value)
    {
        if (animator == null)
            return;

        if (string.IsNullOrWhiteSpace(isThrowingBoolName))
            return;

        animator.SetBool(isThrowingBoolHash, value);
    }

    private void SetAnimatorAimingBool(bool value)
    {
        if (animator == null)
            return;

        if (string.IsNullOrWhiteSpace(isAimingBoolName))
            return;

        animator.SetBool(isAimingBoolHash, value);
    }

    private void ApplyWeaponVisibility()
    {
        if (!hideCurrentWeaponDuringThrow)
            return;

        if (playerWeaponSlots == null || playerWeaponSlots.CurrentWeaponObject == null)
            return;

        cachedCurrentWeaponObject = playerWeaponSlots.CurrentWeaponObject;
        cachedCurrentWeaponWasActive = cachedCurrentWeaponObject.activeSelf;
        hasCachedCurrentWeaponObject = true;

        cachedCurrentWeaponObject.SetActive(false);
    }

    private void RestoreWeaponVisibility()
    {
        if (!restoreWeaponVisibilityAfterThrow)
            return;

        if (!hasCachedCurrentWeaponObject)
            return;

        if (cachedCurrentWeaponObject != null)
            cachedCurrentWeaponObject.SetActive(cachedCurrentWeaponWasActive);

        cachedCurrentWeaponObject = null;
        cachedCurrentWeaponWasActive = false;
        hasCachedCurrentWeaponObject = false;
    }

    private void ApplyThrowRigWeight()
    {
        cachedThrowRigWeights.Clear();

        if (throwDisableRigs != null)
        {
            for (int i = 0; i < throwDisableRigs.Count; i++)
                ApplySingleThrowRigWeight(throwDisableRigs[i]);
        }

        if (includeLegacySingleRig)
            ApplySingleThrowRigWeight(throwDisableRig);
    }

    private void ApplySingleThrowRigWeight(Rig rig)
    {
        if (rig == null)
            return;

        if (!cachedThrowRigWeights.ContainsKey(rig))
            cachedThrowRigWeights.Add(rig, rig.weight);

        rig.weight = throwRigWeight;
    }

    private void RestoreThrowRigWeight()
    {
        if (!restoreThrowRigAfterThrow)
        {
            cachedThrowRigWeights.Clear();
            return;
        }

        foreach (KeyValuePair<Rig, float> pair in cachedThrowRigWeights)
        {
            Rig rig = pair.Key;

            if (rig == null)
                continue;

            rig.weight = pair.Value;
        }

        cachedThrowRigWeights.Clear();
    }

    private void CancelShootingRuntime()
    {
        if (playerWeaponSlots == null)
            return;

        if (playerWeaponSlots.CurrentARShootSettings != null)
            playerWeaponSlots.CurrentARShootSettings.ForceClearRuntimeState();

        if (playerWeaponSlots.CurrentSGShootSettings != null)
            playerWeaponSlots.CurrentSGShootSettings.ForceClearRuntimeState();
    }

    private void CancelReloadRuntime()
    {
        if (playerWeaponSlots == null)
            return;

        if (playerWeaponSlots.CurrentAmmoSettings != null)
            playerWeaponSlots.CurrentAmmoSettings.CancelReload();
    }

    private void ForceEndThrowRuntime()
    {
        if (fallbackRoutine != null)
        {
            StopCoroutine(fallbackRoutine);
            fallbackRoutine = null;
        }

        bool wasThrowing = isThrowing || hasCachedWeaponIndexBeforeThrow;

        isThrowing = false;
        releasedThisThrow = false;
        aimSuppressionActive = false;
        cachedWasAiming = false;
        turnDuringThisThrow = false;
        hasThrowFacingDirection = false;
        throwFacingDirection = Vector3.zero;

        SetAnimatorThrowingBool(false);
        RestoreThrowRigWeight();
        RestoreWeaponVisibility();

        if (wasThrowing)
            RestoreWeaponBeforeThrow();
        else
            ClearCachedWeaponBeforeThrow();

        SetWeaponSwitchLock(false);

        if (grenadeSlots != null)
            grenadeSlots.RefreshAfterThrow();
    }
}