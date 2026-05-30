using UnityEngine;

[DisallowMultipleComponent]
public class EnemyCoverContactRelay : MonoBehaviour
{
    [SerializeField] private EnemyCoverController controller;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponentInParent<EnemyCoverController>();
    }

    public void SetController(EnemyCoverController value)
    {
        controller = value;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (controller != null)
            controller.HandleCoverTriggerEnter(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (controller != null)
            controller.HandleCoverTriggerStay(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (controller != null)
            controller.HandleCoverTriggerExit(other);
    }
}
