using UnityEngine;

public class PlayerFireTrailTest : MonoBehaviour
{
    [SerializeField] private GameObject firePrefab;
    [SerializeField] private float spawnInterval = 0.5f;
    [SerializeField] private float fireDuration = 10f;
    [SerializeField] private float minMoveDistance = 0f;

    private float timer;
    private Vector3 lastPosition;

    private void Start()
    {
        lastPosition = transform.position;
    }

    private void Update()
    {
        if (firePrefab == null) return;

        timer += Time.deltaTime;
        if (timer < spawnInterval) return;
        timer = 0f;

        float moved = Vector3.Distance(transform.position, lastPosition);
        lastPosition = transform.position;

        if (moved < minMoveDistance) return;

        GameObject fire = Instantiate(firePrefab, transform.position, Quaternion.identity);
        BossFireZone zone = fire.GetComponent<BossFireZone>();
        if (zone != null)
        {
            zone.SetLifetime(fireDuration);
            zone.ignoreTarget = gameObject;
        }
    }
}
