using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
public class PlayerUpperBodyAimRig : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private SwitchCamView switchCamView;
    [SerializeField] private List<Rig> aimRigs = new List<Rig>();
    [SerializeField] private Transform aimTarget;
    [SerializeField] private Transform fallbackForwardSource;

    [Header("View Gate")]
    [SerializeField] private bool onlyWorkInThirdPersonView = true;

    [Header("Aim Target")]
    [SerializeField] private bool aimOnlyWhenViewAiming = true;
    [SerializeField] private float fallbackDistance = 40f;
    [SerializeField] private float targetFollowSpeed = 30f;

    [Header("Rig Weight")]
    [Range(0f, 1f)] [SerializeField] private float maxWeight = 1f;
    [SerializeField] private float weightInSpeed = 12f;
    [SerializeField] private float weightOutSpeed = 18f;

    private float currentWeight;

    private void Reset()
    {
        playerMovement = GetComponent<PlayerMovement>();
        switchCamView = GetComponent<SwitchCamView>();

        if (fallbackForwardSource == null)
            fallbackForwardSource = transform;
    }

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (switchCamView == null)
            switchCamView = GetComponent<SwitchCamView>();

        if (fallbackForwardSource == null)
            fallbackForwardSource = transform;

        currentWeight = GetInitialRigWeight();
    }

    private void OnDisable()
    {
        currentWeight = 0f;
        ApplyWeightToRigs(0f);
    }

    private void LateUpdate()
    {
        if (!IsViewAllowed())
        {
            FadeOutRigs();
            return;
        }

        UpdateAimTarget();
        UpdateRigWeight();
    }

    private bool IsViewAllowed()
    {
        if (!onlyWorkInThirdPersonView)
            return true;

        if (switchCamView == null)
            return false;

        return switchCamView.IsThirdPerson;
    }

    private void UpdateAimTarget()
    {
        if (aimTarget == null)
            return;

        Vector3 targetPoint = GetFallbackAimPoint();
        PlayerMovementViewBase activeView = playerMovement != null ? playerMovement.ActiveView : null;

        if (activeView != null)
        {
            if (activeView.TryGetMuzzleAimPoint(out Vector3 viewAimPoint))
            {
                targetPoint = viewAimPoint;
            }
            else if (activeView.HasViewAimPoint)
            {
                targetPoint = activeView.ViewAimPoint;
            }
        }

        float t = 1f - Mathf.Exp(-targetFollowSpeed * Time.deltaTime);
        aimTarget.position = Vector3.Lerp(aimTarget.position, targetPoint, t);
    }

    private void UpdateRigWeight()
    {
        PlayerMovementViewBase activeView = playerMovement != null ? playerMovement.ActiveView : null;

        bool shouldAim = !aimOnlyWhenViewAiming ||
                         (activeView != null && activeView.IsViewAiming);

        float targetWeight = shouldAim ? maxWeight : 0f;
        MoveRigWeightToward(targetWeight);
    }

    private void FadeOutRigs()
    {
        MoveRigWeightToward(0f);
    }

    private void MoveRigWeightToward(float targetWeight)
    {
        float speed = targetWeight > currentWeight ? weightInSpeed : weightOutSpeed;

        currentWeight = Mathf.MoveTowards(
            currentWeight,
            targetWeight,
            speed * Time.deltaTime
        );

        ApplyWeightToRigs(currentWeight);
    }

    private void ApplyWeightToRigs(float weight)
    {
        if (aimRigs == null)
            return;

        for (int i = 0; i < aimRigs.Count; i++)
        {
            if (aimRigs[i] == null)
                continue;

            aimRigs[i].weight = weight;
        }
    }

    private float GetInitialRigWeight()
    {
        if (aimRigs == null)
            return 0f;

        for (int i = 0; i < aimRigs.Count; i++)
        {
            if (aimRigs[i] == null)
                continue;

            return aimRigs[i].weight;
        }

        return 0f;
    }

    private Vector3 GetFallbackAimPoint()
    {
        Transform source = fallbackForwardSource != null ? fallbackForwardSource : transform;

        Vector3 forward = source.forward;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        return source.position + forward.normalized * fallbackDistance;
    }
}