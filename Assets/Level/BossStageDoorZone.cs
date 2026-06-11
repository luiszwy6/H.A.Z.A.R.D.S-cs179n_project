using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Put on the child GameObject that has the trigger Collider.
/// Automatically finds BossStageDoor on the parent and forwards player enter/exit.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BossStageDoorZone : MonoBehaviour
{
    private BossStageDoor door;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        // Must find parent before detaching.
        door = GetComponentInParent<BossStageDoor>();

        // Detach from the door so this zone stays fixed while the door slides up.
        // Without this the zone would follow the door and immediately fire OnTriggerExit.
        transform.SetParent(null, worldPositionStays: true);

        if (door == null)
            Debug.LogError("[BossStageDoorZone] No BossStageDoor found in parent.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (door == null) return;
        PlayerInput pi = GetPlayerInput(other);
        if (pi != null) door.OnPlayerEntered(pi);
    }

    private void OnTriggerExit(Collider other)
    {
        if (door == null) return;
        if (GetPlayerInput(other) != null) door.OnPlayerExited();
    }

    private static PlayerInput GetPlayerInput(Collider col)
    {
        GameObject root = col.attachedRigidbody != null
            ? col.attachedRigidbody.gameObject
            : col.gameObject;

        return root.GetComponentInParent<PlayerInput>()
            ?? root.GetComponent<PlayerInput>()
            ?? col.GetComponentInParent<PlayerInput>();
    }
}
