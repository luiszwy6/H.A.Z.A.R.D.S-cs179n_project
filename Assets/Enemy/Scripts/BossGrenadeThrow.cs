using System.Collections;
using UnityEngine;

/// <summary>
/// Periodically throws a grenade toward the player during Boss Phase 2.
///
/// Setup:
///   1. Add this component to the boss GameObject.
///   2. Assign GrenadePrefab (must have GrenadeWorldController).
///   3. Assign ThrowOrigin (e.g. the boss's hand bone transform, or leave null to use self).
///   4. In HeavyAnimator, add a Trigger parameter named "ThrowGrenade" and a state that
///      plays the throw animation. Add an Animation Event on the frame the hand releases
///      that calls BossGrenadeThrow.AnimEvent_ReleaseGrenade() on this component.
///      If you skip the animation event, set ReleaseDelay to the approximate time in
///      seconds after the trigger when the hand releases.
/// </summary>
[DisallowMultipleComponent]
public class BossGrenadeThrow : MonoBehaviour
{
    [Header("Grenade")]
    [SerializeField] private GameObject grenadePrefab;
    [SerializeField] private Transform throwOrigin;

    [Header("Throw Physics")]
    [SerializeField] private float throwSpeed = 12f;
    [Range(5f, 75f)]
    [SerializeField] private float throwAngle = 30f;
    [SerializeField] private Vector3 angularVelocity = new Vector3(8f, 4f, 2f);

    [Header("Interval")]
    [Tooltip("Seconds between grenade throws.")]
    [SerializeField] private float throwInterval = 6f;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [Tooltip("Trigger parameter name in the enemy Animator Controller.")]
    [SerializeField] private string throwTriggerName = "ThrowGrenade";

    [Header("Release Timing")]
    [Tooltip("Use an Animation Event on the throw clip that calls AnimEvent_ReleaseGrenade().\n" +
             "If no event is set up, the grenade releases after ReleaseDelay seconds instead.")]
    [SerializeField] private bool useAnimationEventForRelease = true;
    [Tooltip("Fallback delay (seconds after animation trigger) before grenade releases.")]
    [SerializeField] private float releaseDelay = 0.4f;

    [Header("Refs")]
    [SerializeField] private EnemyHealth enemyHealth;

    public void SetThrowInterval(float interval)
    {
        throwInterval = Mathf.Max(0.5f, interval);
    }

    private float nextThrowTime;
    private bool isThrowing;
    private Vector3 cachedTargetPosition;
    private Coroutine throwRoutine;

    private static readonly int ThrowHash = Animator.StringToHash("ThrowGrenade");

    private void Awake()
    {
        if (animator    == null) animator    = GetComponentInChildren<Animator>(true);
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        enabled = false; // activated by BossPhaseTwo when phase 2 triggers
    }

    private void OnEnable()
    {
        nextThrowTime = Time.time + throwInterval;
    }

    private void Update()
    {
        if (enemyHealth != null && enemyHealth.IsDead) return;
        if (isThrowing) return;
        if (Time.time < nextThrowTime) return;

        Vector3? target = ResolveTargetPosition();
        if (target == null) return;

        cachedTargetPosition = target.Value;
        StartThrow();
    }

    // Called by Animation Event on the throw clip at the release frame.
    public void AnimEvent_ReleaseGrenade()
    {
        if (!isThrowing) return;
        ReleaseGrenade();
    }

    private void StartThrow()
    {
        isThrowing = true;
        nextThrowTime = Time.time + throwInterval;

        TriggerThrowAnimation();

        if (throwRoutine != null) StopCoroutine(throwRoutine);
        throwRoutine = StartCoroutine(ThrowRoutine());
    }

    private IEnumerator ThrowRoutine()
    {
        if (!useAnimationEventForRelease)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, releaseDelay));
            if (isThrowing)
                ReleaseGrenade();
        }
        else
        {
            // Wait for animation event; add a safety timeout so we don't get stuck.
            float timeout = Mathf.Max(releaseDelay + 1f, 2f);
            yield return new WaitForSeconds(timeout);
            if (isThrowing)
                ReleaseGrenade();
        }

        isThrowing = false;
        throwRoutine = null;
    }

    private void ReleaseGrenade()
    {
        isThrowing = false;

        if (grenadePrefab == null) return;

        Vector3 origin = throwOrigin != null ? throwOrigin.position : transform.position + Vector3.up * 1.4f;
        Vector3 velocity = CalculateThrowVelocity(origin, cachedTargetPosition);

        GrenadeWorldController instance = Instantiate(
            grenadePrefab,
            origin,
            Quaternion.LookRotation(new Vector3(velocity.x, 0f, velocity.z).normalized, Vector3.up)
        ).GetComponent<GrenadeWorldController>();

        if (instance != null)
            instance.Launch(gameObject, transform, velocity, angularVelocity);
    }

    private Vector3 CalculateThrowVelocity(Vector3 from, Vector3 to)
    {
        Vector3 horizontal = to - from;
        horizontal.y = 0f;

        Vector3 dir = horizontal.sqrMagnitude > 0.001f
            ? horizontal.normalized
            : transform.forward;

        float angle = Mathf.Clamp(throwAngle, 5f, 75f) * Mathf.Deg2Rad;
        float speed = Mathf.Max(1f, throwSpeed);

        return dir * Mathf.Cos(angle) * speed + Vector3.up * Mathf.Sin(angle) * speed;
    }

    private void TriggerThrowAnimation()
    {
        if (animator == null || string.IsNullOrWhiteSpace(throwTriggerName)) return;
        animator.ResetTrigger(ThrowHash);
        animator.SetTrigger(ThrowHash);
    }

    private Vector3? ResolveTargetPosition()
    {
        PlayerStatus ps = PlayerStatus.Instance;
        if (ps != null) return ps.transform.position;

        // Fallback: find the player tag
        GameObject player = GameObject.FindWithTag("Player");
        return player != null ? player.transform.position : (Vector3?)null;
    }
}
