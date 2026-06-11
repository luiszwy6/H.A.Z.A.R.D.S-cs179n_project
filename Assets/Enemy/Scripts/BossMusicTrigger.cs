using UnityEngine;

/// <summary>
/// Attach to the boss. When the boss first spots the player via EnemySensor,
/// tells MusicManager to switch to boss battle music (one-shot).
/// </summary>
[DisallowMultipleComponent]
public class BossMusicTrigger : MonoBehaviour
{
    [SerializeField] private EnemySensor enemySensor;
    [SerializeField] private MusicManager musicManager;

    private bool triggered;

    private void Awake()
    {
        if (enemySensor  == null) enemySensor  = GetComponentInChildren<EnemySensor>();
        if (musicManager == null) musicManager = FindFirstObjectByType<MusicManager>();
    }

    private void Update()
    {
        if (triggered || enemySensor == null || musicManager == null)
            return;

        if (enemySensor.CanSeeTarget)
        {
            triggered = true;
            musicManager.StartBossMusic();
        }
    }
}
