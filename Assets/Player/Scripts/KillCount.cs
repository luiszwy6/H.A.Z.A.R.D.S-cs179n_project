using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class KillCount : MonoBehaviour
{
    [Serializable]
    public class KillTypeEntry
    {
        public EnemyHealth.EnemyType enemyType;
        [Min(0)] public int killValue = 1;
    }

    [Header("Settings")]
    [Min(1)] [SerializeField] private int requiredKills = 12;

    [Header("Kill Values Per Type")]
    [SerializeField] private List<KillTypeEntry> killValues = new List<KillTypeEntry>
    {
        new KillTypeEntry { enemyType = EnemyHealth.EnemyType.Soldier, killValue = 2 },
        new KillTypeEntry { enemyType = EnemyHealth.EnemyType.Zombie,  killValue = 1 },
    };

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

    private void OnEnemyDied(EnemyHealth enemy)
    {
        if (IsCharged)
            return;

        int value = GetKillValue(enemy.Type);

        if (value <= 0)
            return;

        currentKills += value;

        OnKillsChanged?.Invoke();

        if (IsCharged)
            OnCharged?.Invoke();
    }

    private int GetKillValue(EnemyHealth.EnemyType type)
    {
        for (int i = 0; i < killValues.Count; i++)
        {
            if (killValues[i].enemyType == type)
                return killValues[i].killValue;
        }

        return 1;
    }
}
