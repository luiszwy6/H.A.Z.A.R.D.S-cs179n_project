using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class GrenadeWorldController : MonoBehaviour
{
    public static readonly List<GrenadeWorldController> ActiveGrenades =
        new List<GrenadeWorldController>();

    [Header("Refs")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform effectRoot;
    [SerializeField] private GameObject visualRoot;

    [Header("Type")]
    [SerializeField] private PlayerGrenadeSlots.GrenadeType grenadeType =
        PlayerGrenadeSlots.GrenadeType.Frag;

    [Header("Existing Prefab Grenade Script")]
    [SerializeField] private bool triggerExistingGrenadeScriptOnLaunch = true;
    [SerializeField] private string existingGrenadeArmMethodName = "ArmGrenade";
    [SerializeField] private bool broadcastArmMethodToChildren = true;
    [SerializeField] private bool disableControllerFuseWhenUsingExistingScript = true;

    [Header("Fuse")]
    [SerializeField] private bool armOnLaunch = true;
    [SerializeField] private bool useFuse = true;
    [SerializeField] private float fuseTime = 3f;

    [Header("Impact")]
    [SerializeField] private bool activateOnImpact = false;
    [SerializeField] private LayerMask impactLayers = ~0;
    [SerializeField] private float minimumImpactSpeed = 2f;

    [Header("Owner Collision Ignore")]
    [SerializeField] private bool ignoreOwnerCollisionOnLaunch = true;
    [SerializeField] private bool restoreOwnerCollisionAfterDelay = true;
    [SerializeField] private float ownerCollisionIgnoreDuration = 0.35f;
    [SerializeField] private bool includeInactiveOwnerColliders = true;

    [Header("Activation")]
    [SerializeField] private bool detachEffectRootOnActivate = true;
    [SerializeField] private bool hideVisualOnActivate = true;
    [SerializeField] private bool disableCollidersOnActivate = true;
    [SerializeField] private bool freezeRigidbodyOnActivate = true;

    [Header("Cleanup")]
    [SerializeField] private bool destroyGrenadeObjectAfterActivate = true;
    [SerializeField] private float destroyDelay = 0.05f;
    [SerializeField] private bool destroyDetachedEffectRoot = false;
    [SerializeField] private float detachedEffectRootDestroyDelay = 10f;

    [Header("Debug")]
    [SerializeField] private bool logState = false;
    [SerializeField] private bool logExistingGrenadeScriptTrigger = true;
    [SerializeField] private bool logOwnerCollisionIgnore = false;

    private GrenadeWorldEffect[] effects;
    private Collider[] colliders;

    private GameObject owner;
    private Transform ownerRoot;

    private bool launched;
    private bool armed;
    private bool activated;
    private float fuseTimer;

    private Coroutine restoreOwnerCollisionRoutine;
    private bool ownerCollisionIgnored;

    public Rigidbody Rigidbody => rb;
    public GameObject Owner => owner;
    public Transform OwnerRoot => ownerRoot;
    public bool Launched => launched;
    public bool Armed => armed;
    public bool Activated => activated;
    public PlayerGrenadeSlots.GrenadeType GrenadeType => grenadeType;
    public bool IsActiveThreat => isActiveAndEnabled && launched && !activated;

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
        visualRoot = gameObject;
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (visualRoot == null)
            visualRoot = gameObject;

        CacheRuntimeComponents();
        SetHeldPhysicsState();
    }

    private void OnDisable()
    {
        ActiveGrenades.Remove(this);

        StopRestoreOwnerCollisionRoutineOnly();

        if (ownerCollisionIgnored)
            SetOwnerCollisionIgnored(false);
    }

    private void Update()
    {
        if (!armed || activated)
            return;

        if (!useFuse)
            return;

        fuseTimer -= Time.deltaTime;

        if (fuseTimer <= 0f)
            Activate();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!activateOnImpact)
            return;

        if (!armed || activated)
            return;

        if (collision == null || collision.collider == null)
            return;

        if (!IsLayerIncluded(impactLayers, collision.collider.gameObject.layer))
            return;

        if (rb != null && rb.linearVelocity.magnitude < minimumImpactSpeed)
            return;

        Activate();
    }

    public void Launch(
        GameObject newOwner,
        Transform newOwnerRoot,
        Vector3 velocity,
        Vector3 angularVelocity)
    {
        owner = newOwner;
        ownerRoot = newOwnerRoot;
        launched = true;

        CacheRuntimeComponents();
        SetWorldPhysicsState();

        if (ignoreOwnerCollisionOnLaunch)
            SetOwnerCollisionIgnored(true);

        if (rb != null)
        {
            rb.linearVelocity = velocity;
            rb.angularVelocity = angularVelocity;
        }

        GrenadeEffectContext context = BuildContext();

        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i] == null)
                continue;

            effects[i].OnGrenadeLaunched(context);
        }

        bool usedExistingGrenadeScript = false;

        if (triggerExistingGrenadeScriptOnLaunch)
            usedExistingGrenadeScript = TriggerExistingGrenadeArmMethod();

        if (armOnLaunch && !(usedExistingGrenadeScript && disableControllerFuseWhenUsingExistingScript))
            Arm();

        if (logState)
        {
            Debug.Log(
                $"[GrenadeWorldController] Launched: {name}, UsedExistingGrenadeScript={usedExistingGrenadeScript}",
                this
            );
        }
    }

    public void SetGrenadeType(PlayerGrenadeSlots.GrenadeType type)
    {
        grenadeType = type;
    }

    public void Arm()
    {
        if (activated)
            return;

        armed = true;
        fuseTimer = Mathf.Max(0f, fuseTime);

        if (logState)
            Debug.Log($"[GrenadeWorldController] Armed by controller: {name}", this);
    }

    public void Activate()
    {
        if (activated)
            return;

        activated = true;
        armed = false;
        ActiveGrenades.Remove(this);

        StopRestoreOwnerCollisionRoutineOnly();

        CacheRuntimeComponents();

        Transform detachedRoot = null;

        if (effectRoot != null && detachEffectRootOnActivate)
        {
            detachedRoot = effectRoot;
            detachedRoot.SetParent(null, true);
        }

        GrenadeEffectContext context = BuildContext();

        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i] == null)
                continue;

            effects[i].ActivateEffect(context);
        }

        if (hideVisualOnActivate && visualRoot != null)
            visualRoot.SetActive(false);

        if (disableCollidersOnActivate)
            SetCollidersEnabled(false);

        if (freezeRigidbodyOnActivate && rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (detachedRoot != null && destroyDetachedEffectRoot)
            Destroy(detachedRoot.gameObject, Mathf.Max(0f, detachedEffectRootDestroyDelay));

        if (destroyGrenadeObjectAfterActivate)
            Destroy(gameObject, Mathf.Max(0f, destroyDelay));

        if (logState)
            Debug.Log($"[GrenadeWorldController] Activated by controller: {name}", this);
    }

    public void ForceActivate()
    {
        Activate();
    }

    private bool TriggerExistingGrenadeArmMethod()
    {
        if (string.IsNullOrWhiteSpace(existingGrenadeArmMethodName))
            return false;

        if (broadcastArmMethodToChildren)
        {
            BroadcastMessage(
                existingGrenadeArmMethodName,
                SendMessageOptions.DontRequireReceiver
            );
        }
        else
        {
            SendMessage(
                existingGrenadeArmMethodName,
                SendMessageOptions.DontRequireReceiver
            );
        }

        if (logExistingGrenadeScriptTrigger)
        {
            Debug.Log(
                $"[GrenadeWorldController] Called existing grenade method: {existingGrenadeArmMethodName} on {name}",
                this
            );
        }

        return true;
    }

    private GrenadeEffectContext BuildContext()
    {
        return new GrenadeEffectContext
        {
            Grenade = this,
            Owner = owner,
            OwnerRoot = ownerRoot,
            Position = transform.position,
            Velocity = rb != null ? rb.linearVelocity : Vector3.zero
        };
    }

    private void CacheRuntimeComponents()
    {
        effects = effectRoot != null
            ? effectRoot.GetComponentsInChildren<GrenadeWorldEffect>(true)
            : GetComponentsInChildren<GrenadeWorldEffect>(true);

        colliders = GetComponentsInChildren<Collider>(true);
    }

    private void SetHeldPhysicsState()
    {
        if (rb == null)
            return;

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void SetWorldPhysicsState()
    {
        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.useGravity = true;

        if (!ActiveGrenades.Contains(this))
            ActiveGrenades.Add(this);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
                continue;

            colliders[i].enabled = enabled;
        }
    }

    private void SetOwnerCollisionIgnored(bool ignore)
    {
        if (ownerRoot == null)
            return;

        if (colliders == null || colliders.Length == 0)
            CacheRuntimeComponents();

        Collider[] ownerColliders = ownerRoot.GetComponentsInChildren<Collider>(includeInactiveOwnerColliders);

        if (ownerColliders == null || ownerColliders.Length == 0)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider grenadeCollider = colliders[i];

            if (grenadeCollider == null)
                continue;

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider ownerCollider = ownerColliders[j];

                if (ownerCollider == null)
                    continue;

                if (ownerCollider == grenadeCollider)
                    continue;

                Physics.IgnoreCollision(grenadeCollider, ownerCollider, ignore);
            }
        }

        ownerCollisionIgnored = ignore;

        if (ignore && restoreOwnerCollisionAfterDelay)
        {
            StopRestoreOwnerCollisionRoutineOnly();
            restoreOwnerCollisionRoutine = StartCoroutine(RestoreOwnerCollisionRoutine());
        }

        if (logOwnerCollisionIgnore)
            Debug.Log($"[GrenadeWorldController] Owner collision ignored={ignore}, Owner={ownerRoot.name}", this);
    }

    private IEnumerator RestoreOwnerCollisionRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, ownerCollisionIgnoreDuration));

        if (!activated)
            SetOwnerCollisionIgnored(false);

        restoreOwnerCollisionRoutine = null;
    }

    private void StopRestoreOwnerCollisionRoutineOnly()
    {
        if (restoreOwnerCollisionRoutine == null)
            return;

        StopCoroutine(restoreOwnerCollisionRoutine);
        restoreOwnerCollisionRoutine = null;
    }

    private bool IsLayerIncluded(LayerMask mask, int layer)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
