using System.Collections.Generic;
using UnityEngine;

public class TotalAmmoSetter : MonoBehaviour
{
    public enum AmmoType
    {
        AssaultRifle,
        Pistol,
        Shotgun,
        Sniper
    }

    [System.Serializable]
    public class AmmoEntry
    {
        public AmmoType ammoType;
        [Min(0)] public int amount;
    }

    [Header("Total Ammo Pool")]
    [SerializeField] private List<AmmoEntry> ammoEntries = new List<AmmoEntry>();

    private readonly Dictionary<AmmoType, AmmoEntry> ammoLookup = new Dictionary<AmmoType, AmmoEntry>();

    private void Awake()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        ClampEntries();
        RebuildLookup();
    }

    private void ClampEntries()
    {
        if (ammoEntries == null) return;

        for (int i = 0; i < ammoEntries.Count; i++)
        {
            if (ammoEntries[i] == null) continue;
            ammoEntries[i].amount = Mathf.Max(0, ammoEntries[i].amount);
        }
    }

    private void RebuildLookup()
    {
        ammoLookup.Clear();

        if (ammoEntries == null)
            return;

        for (int i = 0; i < ammoEntries.Count; i++)
        {
            AmmoEntry entry = ammoEntries[i];
            if (entry == null) continue;

            entry.amount = Mathf.Max(0, entry.amount);
            ammoLookup[entry.ammoType] = entry;
        }
    }

    private AmmoEntry GetOrCreateEntry(AmmoType ammoType)
    {
        if (ammoLookup.TryGetValue(ammoType, out AmmoEntry entry))
            return entry;

        entry = new AmmoEntry
        {
            ammoType = ammoType,
            amount = 0
        };

        ammoEntries.Add(entry);
        ammoLookup[ammoType] = entry;
        return entry;
    }

    public int GetAmmoCount(AmmoType ammoType)
    {
        if (!ammoLookup.TryGetValue(ammoType, out AmmoEntry entry))
            return 0;

        return Mathf.Max(0, entry.amount);
    }

    public bool HasAmmo(AmmoType ammoType, int minAmount = 1)
    {
        return GetAmmoCount(ammoType) >= Mathf.Max(0, minAmount);
    }

    public void AddAmmo(AmmoType ammoType, int amount)
    {
        if (amount <= 0)
            return;

        AmmoEntry entry = GetOrCreateEntry(ammoType);
        entry.amount += amount;
    }

    public int ConsumeAmmo(AmmoType ammoType, int amount)
    {
        if (amount <= 0)
            return 0;

        AmmoEntry entry = GetOrCreateEntry(ammoType);

        int consumed = Mathf.Min(entry.amount, amount);
        entry.amount -= consumed;
        return consumed;
    }
}