using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class HitPartBloodEffect : MonoBehaviour
{
    public enum SpawnPointMode
    {
        HitPoint,
        ColliderCenter,
        ColliderTransform,
        SpawnOverride
    }

    [System.Serializable]
    public class HurtboxBloodBinding
    {
        [Header("Hurtbox")]
        public Collider hurtbox;
        public Transform hurtboxTransform;

        [Header("Blood Prefab")]
        public GameObject bloodPrefab;
        public Transform spawnOverride;

        [Header("Spawn")]
        public SpawnPointMode spawnPointMode = SpawnPointMode.HitPoint;
        public bool alignToHitNormal = true;
        public Vector3 rotationOffsetEuler;
        public bool parentToSpawnOverride = false;

        [Header("Scale")]
        public bool overrideScale = false;
        public float scale = 1f;
    }

    [Header("Bindings")]
    [SerializeField] private HurtboxBloodBinding[] bindings;

    [Header("Default Blood")]
    [SerializeField] private GameObject defaultBloodPrefab;
    [SerializeField] private SpawnPointMode defaultSpawnPointMode = SpawnPointMode.HitPoint;
    [SerializeField] private bool defaultAlignToHitNormal = true;
    [SerializeField] private Vector3 defaultRotationOffsetEuler;
    [SerializeField] private float defaultScale = 1f;

    [Header("Find Child Colliders")]
    [SerializeField] private bool includeInactiveColliders = true;
    [SerializeField] private bool triggerCollidersOnly = true;
    [SerializeField] private bool excludeThisObjectColliders = true;
    [SerializeField] private bool clearExistingBindingsBeforeFind = true;
    [SerializeField] private LayerMask findColliderLayers = ~0;

    [Header("Spawn Control")]
    [SerializeField] private bool spawnOnlyIfDamageApplied = true;
    [SerializeField] private bool detachFromEnemy = true;

    [Header("Cleanup")]
    [SerializeField] private bool autoDestroySpawnedEffect = false;
    [SerializeField] private float destroyDelay = 3f;

    [Header("Debug")]
    [SerializeField] private bool logBlood = false;

    [ContextMenu("Find Child Hurtbox Colliders")]
    public void FindChildHurtboxColliders()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Find Child Blood Hurtbox Colliders");
#endif

        Collider[] foundColliders = GetComponentsInChildren<Collider>(includeInactiveColliders);
        List<HurtboxBloodBinding> result = new List<HurtboxBloodBinding>();

        if (!clearExistingBindingsBeforeFind && bindings != null)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i] != null)
                    result.Add(bindings[i]);
            }
        }

        for (int i = 0; i < foundColliders.Length; i++)
        {
            Collider col = foundColliders[i];

            if (!ShouldUseCollider(col))
                continue;

            HurtboxBloodBinding existing = FindBindingInList(result, col);

            if (existing != null)
            {
                existing.hurtbox = col;
                existing.hurtboxTransform = col.transform;
                continue;
            }

            HurtboxBloodBinding binding = new HurtboxBloodBinding
            {
                hurtbox = col,
                hurtboxTransform = col.transform,
                bloodPrefab = defaultBloodPrefab,
                spawnPointMode = defaultSpawnPointMode,
                alignToHitNormal = defaultAlignToHitNormal,
                rotationOffsetEuler = defaultRotationOffsetEuler,
                scale = defaultScale
            };

            result.Add(binding);
        }

        bindings = result.ToArray();

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public void PlayHit(RaycastHit hit)
    {
        if (hit.collider == null)
            return;

        PlayHurtbox(hit.collider, hit.point, hit.normal);
    }

    public void PlayHurtbox(Collider hitHurtbox)
    {
        if (hitHurtbox == null)
            return;

        Vector3 point = hitHurtbox.bounds.center;
        Vector3 normal = -transform.forward;

        PlayHurtbox(hitHurtbox, point, normal);
    }

    public void PlayHurtbox(Collider hitHurtbox, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hitHurtbox == null)
            return;

        HurtboxBloodBinding binding = FindBinding(hitHurtbox);

        GameObject prefab = binding != null && binding.bloodPrefab != null
            ? binding.bloodPrefab
            : defaultBloodPrefab;

        if (prefab == null)
            return;

        Vector3 spawnPosition = ResolveSpawnPosition(binding, hitHurtbox, hitPoint);
        Quaternion spawnRotation = ResolveSpawnRotation(binding, hitNormal);

        Transform parent = null;

        if (!detachFromEnemy && binding != null && binding.parentToSpawnOverride)
            parent = binding.spawnOverride;

        GameObject instance = Instantiate(prefab, spawnPosition, spawnRotation, parent);

        float scale = ResolveScale(binding);
        instance.transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);

        if (autoDestroySpawnedEffect)
            Destroy(instance, Mathf.Max(0.01f, destroyDelay));

        if (logBlood)
        {
            Debug.Log(
                $"[HitPartBloodEffect] Spawned blood. Hurtbox={hitHurtbox.name}, Prefab={prefab.name}, Position={spawnPosition}",
                this
            );
        }
    }

    private Vector3 ResolveSpawnPosition(
        HurtboxBloodBinding binding,
        Collider hitHurtbox,
        Vector3 hitPoint)
    {
        SpawnPointMode mode = binding != null
            ? binding.spawnPointMode
            : defaultSpawnPointMode;

        if (mode == SpawnPointMode.SpawnOverride &&
            binding != null &&
            binding.spawnOverride != null)
        {
            return binding.spawnOverride.position;
        }

        switch (mode)
        {
            case SpawnPointMode.ColliderCenter:
                return hitHurtbox.bounds.center;

            case SpawnPointMode.ColliderTransform:
                return hitHurtbox.transform.position;

            case SpawnPointMode.HitPoint:
            default:
                return hitPoint;
        }
    }

    private Quaternion ResolveSpawnRotation(
        HurtboxBloodBinding binding,
        Vector3 hitNormal)
    {
        bool align = binding != null
            ? binding.alignToHitNormal
            : defaultAlignToHitNormal;

        Vector3 offset = binding != null
            ? binding.rotationOffsetEuler
            : defaultRotationOffsetEuler;

        Quaternion baseRotation = transform.rotation;

        if (align && hitNormal.sqrMagnitude > 0.0001f)
            baseRotation = Quaternion.LookRotation(hitNormal.normalized, Vector3.up);

        return baseRotation * Quaternion.Euler(offset);
    }

    private float ResolveScale(HurtboxBloodBinding binding)
    {
        if (binding != null && binding.overrideScale)
            return binding.scale;

        return defaultScale;
    }

    private HurtboxBloodBinding FindBinding(Collider hitHurtbox)
    {
        if (bindings == null || hitHurtbox == null)
            return null;

        for (int i = 0; i < bindings.Length; i++)
        {
            HurtboxBloodBinding b = bindings[i];

            if (b == null)
                continue;

            if (b.hurtbox == hitHurtbox)
                return b;

            if (b.hurtbox != null && b.hurtbox.gameObject == hitHurtbox.gameObject)
                return b;

            if (b.hurtboxTransform != null && b.hurtboxTransform == hitHurtbox.transform)
                return b;
        }

        return null;
    }

    private HurtboxBloodBinding FindBindingInList(List<HurtboxBloodBinding> list, Collider col)
    {
        if (list == null || col == null)
            return null;

        for (int i = 0; i < list.Count; i++)
        {
            HurtboxBloodBinding b = list[i];

            if (b == null)
                continue;

            if (b.hurtbox == col)
                return b;

            if (b.hurtbox != null && b.hurtbox.gameObject == col.gameObject)
                return b;

            if (b.hurtboxTransform != null && b.hurtboxTransform == col.transform)
                return b;
        }

        return null;
    }

    private bool ShouldUseCollider(Collider col)
    {
        if (col == null)
            return false;

        if (excludeThisObjectColliders && col.transform == transform)
            return false;

        if (triggerCollidersOnly && !col.isTrigger)
            return false;

        if (!IsLayerIncluded(findColliderLayers, col.gameObject.layer))
            return false;

        return true;
    }

    private bool IsLayerIncluded(LayerMask mask, int layer)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}