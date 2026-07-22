using UnityEngine;

[DisallowMultipleComponent]
public class BossFireZone : MonoBehaviour
{
    [SerializeField] private float lifetime = 10f;
    [SerializeField] private float damagePerSecond = 10f;

    public void SetLifetime(float value)
    {
        lifetime = value;
    }

    private void Start()
    {
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc) sc.isTrigger = true;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            child.gameObject.SetActive(true);

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
            ps.Play();

        Destroy(gameObject, lifetime);
    }

    [Header("Ignore")]
    [Tooltip("Set at runtime to skip damaging this GameObject (e.g. the spawner during testing).")]
    public GameObject ignoreTarget;

    private void OnTriggerStay(Collider other)
    {
        if (ignoreTarget != null && other.transform.IsChildOf(ignoreTarget.transform))
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health != null)
            health.TakeDamage(damagePerSecond * Time.deltaTime);
    }
}
