using System.Collections;
using UnityEngine;

namespace ASGS.Grenade
{
    public class Grenade_Smoke_wPin : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float delay = 2f;
        [SerializeField] private float smokeSpawnDelay = 1f;
        [SerializeField] private float smokeDuration = 30f;
        [SerializeField] private float grenadeDestroyDelayAfterSmoke = 15f;

        [Header("Refs")]
        [SerializeField] private GameObject SmokeGrenadePrefab;
        [SerializeField] private GameObject grenade;
        [SerializeField] private GameObject SmokeIgnition;
        [SerializeField] private GameObject smokePrefab;
        [SerializeField] private GameObject smokeObject;
        [SerializeField] private GameObject pinPrefab;
        [SerializeField] private GameObject SmokeCover;
        [SerializeField] private Rigidbody SmokePinRB;
        [SerializeField] private Rigidbody HandleRB;

        [Header("Handle")]
        [SerializeField] private float IgniteThrust = 5f;

        [Header("Cleanup")]
        [SerializeField] private bool hideGrenadeVisualAfterSmokeSpawn = true;
        [SerializeField] private bool destroyThisObjectOnEnd = true;
        [SerializeField] private bool destroySmokeObjectOnEnd = true;

        [Header("Vision Block")]
        [SerializeField] private bool blockEnemyVision = true;
        [SerializeField] private float visionBlockRadius = 6f;
        [SerializeField] private float visionBlockDelay = 3f;

        [Header("Debug / Inspector Control")]
        [SerializeField] private bool debugArmNow = false;
        [SerializeField] private bool debugIgniteNow = false;
        [SerializeField] private bool debugSmokeNow = false;
        [SerializeField] private bool autoResetDebugToggles = true;

        [Header("Debug")]
        [SerializeField] private bool logState = false;

        private float counter = 0f;
        private bool hasArmed = false;
        private bool hasIgnited = false;
        private bool hasSpawnedSmoke = false;
        private bool hasEnded = false;
        private global::SmokeVisionBlocker smokeVisionBlocker;

        private void Awake()
        {
            if (SmokeIgnition != null)
                SmokeIgnition.SetActive(false);
        }

        private void Update()
        {
            if (debugArmNow)
            {
                if (autoResetDebugToggles)
                    debugArmNow = false;

                ArmGrenade();
            }

            if (debugIgniteNow)
            {
                if (autoResetDebugToggles)
                    debugIgniteNow = false;

                IgniteNow();
            }

            if (debugSmokeNow)
            {
                if (autoResetDebugToggles)
                    debugSmokeNow = false;

                SmokescreenNow();
            }
        }

        private void OnTriggerExit(Collider col)
        {
            if (col.gameObject.CompareTag("Grenade_Pin"))
                ArmGrenade();
        }

        [ContextMenu("Debug Arm Smoke Grenade")]
        public void ArmGrenade()
        {
            if (hasArmed)
                return;

            hasArmed = true;
            counter++;

            if (counter != 1)
                return;

            Invoke(nameof(releaseHandle), Mathf.Max(0f, delay - 1f));
            Invoke(nameof(Ignite), Mathf.Max(0f, delay));
            Invoke(nameof(Smokescreen), Mathf.Max(0f, delay + smokeSpawnDelay));

            if (SmokePinRB != null)
                SmokePinRB.isKinematic = false;

            if (logState)
                Debug.Log($"[Grenade_Smoke_wPin] Armed: {name}", this);
        }

        [ContextMenu("Debug Ignite Now")]
        public void IgniteNow()
        {
            CancelInvoke(nameof(Ignite));
            Ignite();
        }

        [ContextMenu("Debug Smoke Now")]
        public void SmokescreenNow()
        {
            CancelInvoke(nameof(Smokescreen));
            Smokescreen();
        }

        private void releaseHandle()
        {
            if (HandleRB == null)
                return;

            HandleRB.isKinematic = false;
            HandleRB.AddForce(IgniteThrust * transform.up, ForceMode.Impulse);
            HandleRB.AddForce(transform.forward * IgniteThrust / 4f, ForceMode.Impulse);

            if (logState)
                Debug.Log($"[Grenade_Smoke_wPin] Released handle: {name}", this);
        }

        private void Ignite()
        {
            if (hasIgnited)
                return;

            hasIgnited = true;

            if (SmokeIgnition != null)
                SmokeIgnition.SetActive(true);

            if (SmokeCover != null)
                SmokeCover.SetActive(false);

            if (logState)
                Debug.Log($"[Grenade_Smoke_wPin] Ignited: {name}", this);
        }

        private void Smokescreen()
        {
            if (hasSpawnedSmoke)
                return;

            hasSpawnedSmoke = true;

            if (!hasIgnited)
                Ignite();

            if (smokePrefab != null)
                smokeObject = Instantiate(smokePrefab, transform.position, Quaternion.identity);

            if (blockEnemyVision && smokeObject != null)
            {
                smokeVisionBlocker =
                    smokeObject.GetComponent<global::SmokeVisionBlocker>();

                if (smokeVisionBlocker == null)
                    smokeVisionBlocker = smokeObject.AddComponent<global::SmokeVisionBlocker>();

                smokeVisionBlocker.SetRadius(visionBlockRadius);
                smokeVisionBlocker.SetBlockingEnabled(false);
                Invoke(nameof(EnableSmokeVisionBlock), Mathf.Max(0f, visionBlockDelay));
            }

            if (hideGrenadeVisualAfterSmokeSpawn)
                HideGrenadeVisual();

            float endDelay = Mathf.Max(0f, smokeDuration);
            Invoke(nameof(End), endDelay);

            if (grenadeDestroyDelayAfterSmoke > 0f)
                DestroyGrenadeMeshAfterDelay(grenadeDestroyDelayAfterSmoke);

            if (logState)
                Debug.Log($"[Grenade_Smoke_wPin] Smoke spawned: {name}", this);
        }

        private void EnableSmokeVisionBlock()
        {
            if (hasEnded)
                return;

            if (smokeVisionBlocker == null)
                return;

            smokeVisionBlocker.SetBlockingEnabled(true);
        }

        private void DestroyGrenadeMeshAfterDelay(float delayTime)
        {
            if (grenade != null)
                Destroy(grenade, Mathf.Max(0f, delayTime));
        }

        private void HideGrenadeVisual()
        {
            if (grenade != null)
            {
                grenade.SetActive(false);
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                if (smokeObject != null && renderers[i].transform.IsChildOf(smokeObject.transform))
                    continue;

                renderers[i].enabled = false;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null)
                    continue;

                colliders[i].enabled = false;
            }
        }

        private void End()
        {
            if (hasEnded)
                return;

            hasEnded = true;
            CancelInvoke(nameof(EnableSmokeVisionBlock));

            if (smokeVisionBlocker != null)
                smokeVisionBlocker.SetBlockingEnabled(false);

            if (destroySmokeObjectOnEnd && smokeObject != null)
                Destroy(smokeObject);

            if (destroyThisObjectOnEnd)
            {
                if (SmokeGrenadePrefab != null)
                    Destroy(SmokeGrenadePrefab);
                else
                    Destroy(gameObject);
            }

            if (logState)
                Debug.Log($"[Grenade_Smoke_wPin] Ended: {name}", this);
        }
    }
}
