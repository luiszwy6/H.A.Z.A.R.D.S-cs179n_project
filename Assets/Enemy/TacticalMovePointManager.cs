using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TacticalMovePointManager : MonoBehaviour
{
    public static TacticalMovePointManager Instance { get; private set; }

    [Header("Runtime")]
    [SerializeField] private bool dontDestroyOnLoad = false;

    private readonly Dictionary<Transform, Transform> ownerToPoint = new Dictionary<Transform, Transform>();

    public Transform Root => transform;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple TacticalMovePointManager instances found. Destroying duplicate.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static TacticalMovePointManager GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        GameObject existing = GameObject.Find("TacticalMovePointManager");

        if (existing != null)
        {
            TacticalMovePointManager existingManager = existing.GetComponent<TacticalMovePointManager>();

            if (existingManager != null)
                return existingManager;

            return existing.AddComponent<TacticalMovePointManager>();
        }

        GameObject managerObject = new GameObject("TacticalMovePointManager");
        return managerObject.AddComponent<TacticalMovePointManager>();
    }

    public Transform RegisterPoint(Transform owner, Transform existingPoint = null)
    {
        if (owner == null)
            return null;

        if (ownerToPoint.TryGetValue(owner, out Transform registeredPoint) && registeredPoint != null)
            return registeredPoint;

        Transform point = existingPoint;

        if (point == null)
        {
            GameObject pointObject = new GameObject($"{owner.name}_TacticalMovePoint");
            point = pointObject.transform;
        }

        point.SetParent(transform, true);
        point.position = owner.position;
        point.rotation = Quaternion.identity;
        point.gameObject.SetActive(true);

        ownerToPoint[owner] = point;

        return point;
    }

    public void UnregisterPoint(Transform owner, bool destroyPoint)
    {
        if (owner == null)
            return;

        if (!ownerToPoint.TryGetValue(owner, out Transform point))
            return;

        ownerToPoint.Remove(owner);

        if (destroyPoint && point != null)
            Destroy(point.gameObject);
    }

    public bool TryGetPoint(Transform owner, out Transform point)
    {
        point = null;

        if (owner == null)
            return false;

        return ownerToPoint.TryGetValue(owner, out point) && point != null;
    }
}