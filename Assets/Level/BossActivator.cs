using UnityEngine;

/// <summary>
/// Put on an always-active GameObject in the boss room (NOT on the boss itself).
/// Activates the boss when BossStageDoor is opened.
/// Boss should start with its root GameObject set inactive in the scene.
/// </summary>
public class BossActivator : MonoBehaviour
{
    [Tooltip("The boss root GameObject. Should start inactive in the scene.")]
    [SerializeField] private GameObject bossRoot;

    [Tooltip("Auto-found in scene if left empty.")]
    [SerializeField] private BossStageDoor door;

    private void Awake()
    {
        if (door == null)
            door = FindFirstObjectByType<BossStageDoor>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        if (door != null) door.OnDoorOpened += ActivateBoss;
    }

    private void OnDisable()
    {
        if (door != null) door.OnDoorOpened -= ActivateBoss;
    }

    private void ActivateBoss()
    {
        if (bossRoot != null)
            bossRoot.SetActive(true);
    }
}
