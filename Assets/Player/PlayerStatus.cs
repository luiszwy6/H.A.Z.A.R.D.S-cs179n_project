using UnityEngine;

public class PlayerStatus : MonoBehaviour
{
    public static PlayerStatus Instance { get; private set; }

    [Header("Cover")]
    [SerializeField] private bool isInCover;
    [SerializeField] private CoverTrigger currentCover;

    [Header("Movement")]
    [SerializeField] private bool isCrouching;
    [SerializeField] private bool isProne;
    [SerializeField] private bool isSliding;
    [SerializeField] private bool isDiving;
    [SerializeField] private bool isRunning;

    [Header("Abilities")]
    [SerializeField] private bool isInvisible;

    [Header("Melee")]
    [SerializeField] private bool isMeleeAttacking;
    [SerializeField] private bool isBackStabbing;

    [Header("Shooting")]
    [SerializeField] private bool isAiming;
    [SerializeField] private bool isRifleShooting;
    [SerializeField] private bool isShotgunShooting;
    [SerializeField] private bool isSniperShooting;
    [SerializeField] private bool isReloading;

    public bool IsInCover => isInCover;
    public CoverTrigger CurrentCover => currentCover;

    public bool IsCrouching => isCrouching;
    public bool IsProne => isProne;
    public bool IsSliding => isSliding;
    public bool IsDiving => isDiving;
    public bool IsRunning => isRunning;

    public bool IsInvisible => isInvisible;

    public bool IsMeleeAttacking => isMeleeAttacking;
    public bool IsBackStabbing => isBackStabbing;

    public bool IsAiming => isAiming;
    public bool IsRifleShooting => isRifleShooting;
    public bool IsShotgunShooting => isShotgunShooting;
    public bool IsSniperShooting => isSniperShooting;
    public bool IsReloading => isReloading;

    public bool IsAnyShooting =>
        isRifleShooting ||
        isShotgunShooting ||
        isSniperShooting;

    public bool IsAnyAttacking =>
        IsAnyShooting ||
        isMeleeAttacking;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple PlayerStatus instances found. Replacing old instance.", this);
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetInCover(bool value, CoverTrigger cover)
    {
        isInCover = value;
        currentCover = value ? cover : null;
    }

    public void SetMovementStatus(
        bool crouching,
        bool prone,
        bool sliding,
        bool diving,
        bool running)
    {
        isCrouching = crouching;
        isProne = prone;
        isSliding = sliding;
        isDiving = diving;
        isRunning = running;
    }

    public void SetInvisible(bool value)
    {
        isInvisible = value;
    }

    public void SetMeleeStatus(bool meleeAttacking, bool backStabbing)
    {
        isMeleeAttacking = meleeAttacking;
        isBackStabbing = meleeAttacking && backStabbing;
    }

    public void ClearMelee()
    {
        isMeleeAttacking = false;
        isBackStabbing = false;
    }

    public void SetAiming(bool value)
    {
        isAiming = value;
    }

    public void SetRifleShooting(bool value)
    {
        isRifleShooting = value;
    }

    public void SetShotgunShooting(bool value)
    {
        isShotgunShooting = value;
    }

    public void SetSniperShooting(bool value)
    {
        isSniperShooting = value;
    }

    public void SetReloading(bool value)
    {
        isReloading = value;
    }

    public void ClearShooting()
    {
        isRifleShooting = false;
        isShotgunShooting = false;
        isSniperShooting = false;
    }
}