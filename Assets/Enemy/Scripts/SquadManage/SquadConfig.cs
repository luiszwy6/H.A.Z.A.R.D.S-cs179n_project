using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WaveSpawnGroup
{
    public string groupName = "Group";
    [Tooltip("Index into the generator's Squad Configs list.")]
    public int configIndex = 0;
    [Min(1)] public int spawnCount = 1;
    [Tooltip("Spawn points for this group. Empty = use generator defaults.")]
    public List<Transform> spawnPoints = new();
}

[Serializable]
public class WaveRule
{
    public string waveName = "Wave";
    [Min(0f), Tooltip("Seconds to wait after the previous wave is cleared before spawning.")]
    public float delayAfterPreviousClear = 3f;
    [Tooltip("Each group spawns independently at its own location. Add multiple groups to spawn squads at different points in the same wave.")]
    public List<WaveSpawnGroup> groups = new();
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
