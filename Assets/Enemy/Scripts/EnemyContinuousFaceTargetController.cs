using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class EnemyContinuousFaceTargetController : MonoBehaviour
{
    [Header("Default")]
    [SerializeField] private Transform target;
    [SerializeField] private bool usePlayerStatusIfTargetMissing = true;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private bool disableAgentRotationWhileActive = true;
    [SerializeField] private bool restoreAgentRotationOnStop = false;
    [SerializeField] private bool setAimingWhileActive = false;
    [SerializeField] private bool aiming = true;
    [SerializeField] private bool clearAimingOnStop = false;

    private NavMeshAgent agent;
    private EnemyAnimatorParameterDriver animatorDriver;
    private bool isFacing;
    private bool cachedAgentUpdateRotation;
    private bool hasCachedAgentRotation;

    public bool IsFacing => isFacing;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animatorDriver = GetComponent<EnemyAnimatorParameterDriver>();
    }

    private void LateUpdate()
    {
        if (!isFacing)
            return;

        Transform faceTarget = ResolveTarget();

        if (faceTarget == null)
            return;

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent != null && disableAgentRotationWhileActive)
            agent.updateRotation = false;

        if (setAimingWhileActive)
            SetAiming(aiming);

        Vector3 direction = faceTarget.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Mathf.Max(0f, rotationSpeed) * Time.deltaTime
        );
    }

    private void OnDisable()
    {
        StopFacing();
    }

    public void StartFacing(
        Transform newTarget,
        float newRotationSpeed,
        bool newUsePlayerStatusIfTargetMissing,
        bool newDisableAgentRotationWhileActive,
        bool newRestoreAgentRotationOnStop,
        bool newSetAimingWhileActive,
        bool newAiming,
        bool newClearAimingOnStop)
    {
        target = newTarget;
        rotationSpeed = newRotationSpeed;
        usePlayerStatusIfTargetMissing = newUsePlayerStatusIfTargetMissing;
        disableAgentRotationWhileActive = newDisableAgentRotationWhileActive;
        restoreAgentRotationOnStop = newRestoreAgentRotationOnStop;
        setAimingWhileActive = newSetAimingWhileActive;
        aiming = newAiming;
        clearAimingOnStop = newClearAimingOnStop;

        isFacing = true;

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent != null && disableAgentRotationWhileActive)
        {
            if (!hasCachedAgentRotation)
            {
                cachedAgentUpdateRotation = agent.updateRotation;
                hasCachedAgentRotation = true;
            }

            agent.updateRotation = false;
        }

        if (setAimingWhileActive)
            SetAiming(aiming);
    }

    public void StopFacing()
    {
        isFacing = false;

        if (agent != null &&
            restoreAgentRotationOnStop &&
            hasCachedAgentRotation)
        {
            agent.updateRotation = cachedAgentUpdateRotation;
        }

        if (clearAimingOnStop)
            SetAiming(false);

        hasCachedAgentRotation = false;
    }

    private void SetAiming(bool value)
    {
        if (animatorDriver == null)
            animatorDriver = GetComponent<EnemyAnimatorParameterDriver>();

        if (animatorDriver != null)
            animatorDriver.SetAiming(value);
    }

    private Transform ResolveTarget()
    {
        if (target != null)
            return target;

        if (!usePlayerStatusIfTargetMissing)
            return null;

        PlayerStatus playerStatus = PlayerStatus.Instance;
        return playerStatus != null ? playerStatus.transform : null;
    }
}
