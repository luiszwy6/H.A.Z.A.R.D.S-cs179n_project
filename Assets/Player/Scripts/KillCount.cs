using System;
using UnityEngine;

[DisallowMultipleComponent]
public class KillCount : MonoBehaviour
{
    [Header("Settings")]
    [Min(1)] [SerializeField] private int requiredKills = 12;

    [Header("Debug")]
    [SerializeField] private int currentKills;

    public event Action OnKillsChanged;
    public event Action OnCharged;

    public int CurrentKills => currentKills;
    public int RequiredKills => requiredKills;
    public bool IsCharged => currentKills >= requiredKills;
    public float ChargeRatio => requiredKills > 0 ? Mathf.Clamp01((float)currentKills / requiredKills) : 1f;

    private void OnEnable()
    {
        EnemyHealth.OnAnyEnemyDied += OnEnemyDied;
    }

    private void OnDisable()
    {
        EnemyHealth.OnAnyEnemyDied -= OnEnemyDied;
    }

    public void ResetKills()
    {
        currentKills = 0;
        OnKillsChanged?.Invoke();
    }

    private void OnEnemyDied()
    {
        if (IsCharged)
            return;

        currentKills++;

        OnKillsChanged?.Invoke();

        if (IsCharged)
            OnCharged?.Invoke();
    }
}
