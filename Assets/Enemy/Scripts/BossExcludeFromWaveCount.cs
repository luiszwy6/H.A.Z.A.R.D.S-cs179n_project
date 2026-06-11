using UnityEngine;

/// <summary>
/// Attach to a boss GameObject. EnemySquadGenerator will skip wave-clear checks
/// when this enemy dies, so the boss does not interfere with wave counting.
/// </summary>
public class BossExcludeFromWaveCount : MonoBehaviour { }
