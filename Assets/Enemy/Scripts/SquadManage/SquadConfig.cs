using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WaveRule
{
    public string waveName = "Wave";
    public int configIndex = 0;
    [Min(1)] public int spawnCount = 1;
    [Tooltip("Override spawn points for this wave. Empty = use generator defaults.")]
    public List<Transform> spawnPointOverride = new();
    [Min(0f), Tooltip("Seconds to wait after the previous wave is cleared before spawning.")]
    public float delayAfterPreviousClear = 3f;
}

[Serializable]
public class SquadConfig
{
    public string squadName = "New Squad";
    public List<EnemySpawnEntry> entries = new();
}

[Serializable]
public class EnemySpawnEntry
{
    [Tooltip("Inactive scene GameObject used as the spawn prototype.")]
    public GameObject enemyPrototype;
    public SquadEnemyType enemyType = SquadEnemyType.AR;
    [Min(1)] public int count = 1;
}
