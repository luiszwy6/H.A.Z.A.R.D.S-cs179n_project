using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyCoverController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EnemyStatus enemyStatus;
    [SerializeField] private EnemyAnimatorParameterDriver animatorDriver;

    [Header("Cover Enter")]
    [SerializeField] private bool autoEnterCover = true;
    [SerializeField] private bool installChildColliderRelays = true;
    [SerializeField] private float enemyRadius = 0.45f;

    [Header("Cover Type Behavior")]
    [SerializeField] private bool highCoverRotatesToCover = true;
    [SerializeField] private bool lowCoverForcesCrouch = true;

    private readonly List<CoverTrigger> nearbyCovers = new List<CoverTrigger>();

    private CoverTrigger currentCover;

    private bool isInCover;
    private bool lowCoverCrouchApplied;

    private bool highCoverRotationApplied;

    public bool IsInCover => isInCover;
    public bool IsMovingToCover => false;
    public CoverTrigger CurrentCover => currentCover;

    private void Awake()
    {
        if (enemyStatus == null)
            enemyStatus = GetComponent<EnemyStatus>();

        if (animatorDriver == null)
            animatorDriver = GetComponent<EnemyAnimatorParameterDriver>();

        if (installChildColliderRelays)
            InstallChildColliderRelays();
    }

    private void OnDisable()
    {
        ExitCover();
        nearbyCovers.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleCoverTriggerEnter(other);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleCoverTriggerStay(other);
    }

    private void OnTriggerExit(Collider other)
    {
        HandleCoverTriggerExit(other);
    }

    public void HandleCoverTriggerEnter(Collider other)
    {
        CoverTrigger cover = GetCoverFromCollider(other);

        if (cover == null)
            return;

        RegisterNearbyCover(cover);

        if (autoEnterCover && !isInCover)
            EnterCover(cover);
    }

    public void HandleCoverTriggerStay(Collider other)
    {
        CoverTrigger cover = GetCoverFromCollider(other);

        if (cover == null)
            return;

        RegisterNearbyCover(cover);

        if (autoEnterCover && !isInCover)
            EnterCover(cover);
    }

    public void HandleCoverTriggerExit(Collider other)
    {
        CoverTrigger cover = GetCoverFromCollider(other);

        if (cover == null)
            return;

        nearbyCovers.Remove(cover);

        if (cover == currentCover)
            ExitCover();
    }

    private CoverTrigger GetCoverFromCollider(Collider other)
    {
        if (other == null)
            return null;

        return other.GetComponentInParent<CoverTrigger>();
    }

    private void InstallChildColliderRelays()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (col == null)
                continue;

            if (col.GetComponentInParent<CoverTrigger>() != null)
                continue;

            EnemyCoverContactRelay relay = col.GetComponent<EnemyCoverContactRelay>();

            if (relay == null)
                relay = col.gameObject.AddComponent<EnemyCoverContactRelay>();

            relay.SetController(this);
        }
    }

    private void RegisterNearbyCover(CoverTrigger cover)
    {
        if (cover == null)
            return;

        if (!nearbyCovers.Contains(cover))
            nearbyCovers.Add(cover);
    }

    private void EnterCover(CoverTrigger cover)
    {
        if (cover == null)
            return;

        if (currentCover == cover && isInCover)
            return;

        if (!cover.Reserve(gameObject))
            return;

        bool foundPose = cover.TryGetCoverPose(
            transform.position,
            enemyRadius,
            out Vector3 ignoredPosition,
            out Quaternion targetRotation
        );

        if (!foundPose)
        {
            cover.Release(gameObject);
            return;
        }

        if (currentCover != null && currentCover != cover)
            currentCover.Release(gameObject);

        ReleaseLowCoverCrouch();
        ReleaseHighCoverRotation();

        currentCover = cover;

        bool rotateToCover = cover.coverType == CoverType.High && highCoverRotatesToCover;
        bool forceCrouch = cover.coverType == CoverType.Low && lowCoverForcesCrouch;

        isInCover = true;

        if (forceCrouch)
            ApplyLowCoverCrouch();

        if (rotateToCover)
            ApplyHighCoverRotation(targetRotation);

        if (enemyStatus != null)
            enemyStatus.SetInCover(true, currentCover);
    }

    private void ExitCover()
    {
        ReleaseLowCoverCrouch();
        ReleaseHighCoverRotation();

        if (currentCover != null)
            currentCover.Release(gameObject);

        currentCover = null;
        isInCover = false;

        if (enemyStatus != null)
            enemyStatus.SetInCover(false, null);
    }

    private void ApplyLowCoverCrouch()
    {
        if (lowCoverCrouchApplied)
            return;

        if (animatorDriver == null)
            return;

        animatorDriver.SetExternalCrouchOverride(true);
        lowCoverCrouchApplied = true;
    }

    private void ReleaseLowCoverCrouch()
    {
        if (!lowCoverCrouchApplied)
            return;

        if (animatorDriver != null)
            animatorDriver.SetExternalCrouchOverride(false);

        lowCoverCrouchApplied = false;
    }

    private void ApplyHighCoverRotation(Quaternion targetRotation)
    {
        transform.rotation = targetRotation;
        highCoverRotationApplied = true;
    }

    private void ReleaseHighCoverRotation()
    {
        if (!highCoverRotationApplied)
            return;

        highCoverRotationApplied = false;
    }
}
