using UnityEngine;

namespace ASGS.Grenade
{
    public class Grenade_Flashbang_wPin : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float delay = 2.5f;
        [SerializeField] private float endDelay = 4f;

        [Header("Refs")]
        [SerializeField] private GameObject flashbangRoot;
        [SerializeField] private GameObject grenadeVisualRoot;
        [SerializeField] private GameObject pinPrefab;
        [SerializeField] private Rigidbody pinRB;
        [SerializeField] private Rigidbody handleRB;

        [Header("Effect Test")]
        [SerializeField] private GameObject explosionEffectPrefab;
        [SerializeField] private GameObject explosionObject;

        [Header("Enemy Stun")]
        [SerializeField] private bool stunEnemiesOnFlash = true;
        [SerializeField] private float enemyStunRadius = 8f;
        [SerializeField] private float enemyStunDuration = 3f;
        [SerializeField] private float enemyFlashBangStatusDuration = 3f;
        [SerializeField] private LayerMask enemyStunMask = ~0;
        [SerializeField] private QueryTriggerInteraction enemyStunTriggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] private bool requireLineOfSightToStun = false;
        [SerializeField] private LayerMask stunObstructionMask = ~0;

        [Header("Spawned Effect Position")]
        [SerializeField] private bool useIdentityRotationForSpawnedEffect = true;
        [SerializeField] private Vector3 effectWorldOffset = new Vector3(0f, 1.2f, 0f);

        [Header("Spawned Light Position")]
        [SerializeField] private bool forceLightsToEffectSpawnPoint = true;
        [SerializeField] private Vector3 lightWorldOffset = new Vector3(0f, 0.2f, 0f);

        [Header("Spawned Effect Override")]
        [SerializeField] private bool overrideSpawnedEffectScale = false;
        [SerializeField] private float spawnedEffectScale = 1f;

        [SerializeField] private bool overrideSpawnedLights = false;
        [SerializeField] private float spawnedLightRange = 25f;
        [SerializeField] private float spawnedLightIntensity = 120f;
        [SerializeField] private bool forceEnableSpawnedLights = true;
        [SerializeField] private bool disableSpawnedLightShadows = true;
        [SerializeField] private bool forceActivateEffectLightControllers = true;

        [Header("Particle Override")]
        [SerializeField] private bool playSpawnedParticleSystems = true;
        [SerializeField] private bool clearParticlesBeforePlay = false;

        [Header("Handle")]
        [SerializeField] private float igniteThrust = 5f;

        [Header("Cleanup")]
        [SerializeField] private bool hideVisualOnFlash = true;
        [SerializeField] private bool disableCollidersOnFlash = true;
        [SerializeField] private bool freezeRigidbodyOnFlash = true;
        [SerializeField] private bool destroyEffectOnEnd = true;
        [SerializeField] private bool destroyFlashbangRootOnEnd = true;

        [Header("Debug / Inspector Control")]
        [SerializeField] private bool debugArmNow = false;
        [SerializeField] private bool debugFlashNow = false;
        [SerializeField] private bool autoResetDebugToggles = true;

        [Header("Debug")]
        [SerializeField] private bool logState = false;
        [SerializeField] private bool logSpawnedEffectLightValues = true;

        private float counter = 0f;
        private bool hasArmed = false;
        private bool hasFlashed = false;
        private bool hasEnded = false;

        private Rigidbody rootRB;
        private Collider[] cachedColliders;

        private void Reset()
        {
            flashbangRoot = gameObject;
            grenadeVisualRoot = null;
            rootRB = GetComponent<Rigidbody>();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (debugArmNow)
            {
                if (autoResetDebugToggles)
                    debugArmNow = false;

                ArmGrenade();
            }

            if (debugFlashNow)
            {
                if (autoResetDebugToggles)
                    debugFlashNow = false;

                FlashNow();
            }
        }

        private void OnTriggerExit(Collider col)
        {
            if (col.gameObject.CompareTag("Grenade_Pin"))
                ArmGrenade();
        }

        [ContextMenu("Debug Arm Flashbang")]
        public void ArmGrenade()
        {
            if (hasArmed)
                return;

            hasArmed = true;
            counter++;

            if (counter != 1)
                return;

            Invoke(nameof(ReleaseHandle), Mathf.Max(0f, delay - 1f));
            Invoke(nameof(Flash), Mathf.Max(0f, delay));

            if (pinRB != null)
                pinRB.isKinematic = false;

            if (logState)
                Debug.Log($"[Grenade_Flashbang_wPin] Armed: {name}", this);
        }

        [ContextMenu("Debug Flash Now")]
        public void FlashNow()
        {
            CancelInvoke(nameof(ReleaseHandle));
            CancelInvoke(nameof(Flash));
            Flash();
        }

        private void ReleaseHandle()
        {
            if (handleRB == null)
                return;

            handleRB.isKinematic = false;
            handleRB.AddForce(igniteThrust * transform.up, ForceMode.Impulse);
            handleRB.AddForce(transform.forward * igniteThrust / 4f, ForceMode.Impulse);

            if (logState)
                Debug.Log($"[Grenade_Flashbang_wPin] Released handle: {name}", this);
        }

        private void Flash()
        {
            if (hasFlashed)
                return;

            hasFlashed = true;

            ResolveReferences();

            Vector3 flashPosition = transform.position + effectWorldOffset;
            Quaternion effectRotation = useIdentityRotationForSpawnedEffect
                ? Quaternion.identity
                : transform.rotation;

            if (explosionEffectPrefab != null)
            {
                explosionObject = Instantiate(
                    explosionEffectPrefab,
                    flashPosition,
                    effectRotation
                );

                ConfigureSpawnedEffect(explosionObject, flashPosition);
            }

            if (hideVisualOnFlash)
                HideVisual();

            if (disableCollidersOnFlash)
                SetCollidersEnabled(false);

            if (freezeRigidbodyOnFlash && rootRB != null)
            {
                rootRB.linearVelocity = Vector3.zero;
                rootRB.angularVelocity = Vector3.zero;
                rootRB.isKinematic = true;
                rootRB.useGravity = false;
            }

            StunEnemies(flashPosition);

            Invoke(nameof(End), Mathf.Max(0f, endDelay));

            if (logState)
                Debug.Log($"[Grenade_Flashbang_wPin] Flashed: {name}, FlashPosition={flashPosition}", this);
        }

        private void StunEnemies(Vector3 flashPosition)
        {
            if (!stunEnemiesOnFlash)
                return;

            float radius = Mathf.Max(0f, enemyStunRadius);

            if (radius <= 0f)
                return;

            Collider[] hits = Physics.OverlapSphere(
                flashPosition,
                radius,
                enemyStunMask,
                enemyStunTriggerInteraction
            );

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];

                if (hit == null)
                    continue;

                global::EnemyStunReceiver stunReceiver =
                    hit.GetComponentInParent<global::EnemyStunReceiver>();

                if (stunReceiver == null)
                    continue;

                if (requireLineOfSightToStun &&
                    IsStunLineBlocked(flashPosition, stunReceiver))
                {
                    continue;
                }

                stunReceiver.ForceFlashBangStun(
                    enemyStunDuration,
                    enemyFlashBangStatusDuration
                );
            }
        }

        private bool IsStunLineBlocked(
            Vector3 flashPosition,
            global::EnemyStunReceiver stunReceiver)
        {
            if (stunReceiver == null)
                return true;

            Vector3 targetPosition = stunReceiver.transform.position + Vector3.up * 1.2f;
            Vector3 toTarget = targetPosition - flashPosition;
            float distance = toTarget.magnitude;

            if (distance <= 0.01f)
                return false;

            RaycastHit[] hits = Physics.RaycastAll(
                flashPosition,
                toTarget / distance,
                distance,
                stunObstructionMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].transform;

                if (hitTransform == null)
                    continue;

                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                    continue;

                if (hitTransform == stunReceiver.transform ||
                    hitTransform.IsChildOf(stunReceiver.transform))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void ConfigureSpawnedEffect(GameObject spawnedEffect, Vector3 flashPosition)
        {
            if (spawnedEffect == null)
                return;

            if (overrideSpawnedEffectScale)
                spawnedEffect.transform.localScale = Vector3.one * Mathf.Max(0.01f, spawnedEffectScale);

            if (playSpawnedParticleSystems)
            {
                ParticleSystem[] particles = spawnedEffect.GetComponentsInChildren<ParticleSystem>(true);

                for (int i = 0; i < particles.Length; i++)
                {
                    ParticleSystem particle = particles[i];

                    if (particle == null)
                        continue;

                    if (clearParticlesBeforePlay)
                        particle.Clear(true);

                    particle.Play(true);
                }
            }

            Light[] lights = spawnedEffect.GetComponentsInChildren<Light>(true);

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];

                if (light == null)
                    continue;

                if (forceLightsToEffectSpawnPoint)
                    light.transform.position = flashPosition + lightWorldOffset;

                if (forceEnableSpawnedLights)
                    light.enabled = true;

                if (overrideSpawnedLights)
                {
                    light.range = Mathf.Max(0f, spawnedLightRange);
                    light.intensity = Mathf.Max(0f, spawnedLightIntensity);
                }

                if (disableSpawnedLightShadows)
                    light.shadows = LightShadows.None;

                if (logSpawnedEffectLightValues)
                {
                    Debug.Log(
                        $"[Grenade_Flashbang_wPin] Spawned light configured. Light={light.name}, Position={light.transform.position}, Range={light.range}, Intensity={light.intensity}, LossyScale={light.transform.lossyScale}",
                        light
                    );
                }
            }

            if (forceActivateEffectLightControllers)
            {
                spawnedEffect.BroadcastMessage(
                    "ActivateLight",
                    SendMessageOptions.DontRequireReceiver
                );
            }
        }

        private void End()
        {
            if (hasEnded)
                return;

            hasEnded = true;

            if (destroyEffectOnEnd && explosionObject != null)
                Destroy(explosionObject);

            if (destroyFlashbangRootOnEnd)
            {
                if (flashbangRoot != null)
                    Destroy(flashbangRoot);
                else
                    Destroy(gameObject);
            }

            if (logState)
                Debug.Log($"[Grenade_Flashbang_wPin] Ended: {name}", this);
        }

        private void HideVisual()
        {
            if (grenadeVisualRoot != null && grenadeVisualRoot != gameObject)
            {
                grenadeVisualRoot.SetActive(false);
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                if (explosionObject != null && renderers[i].transform.IsChildOf(explosionObject.transform))
                    continue;

                renderers[i].enabled = false;
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (cachedColliders == null || cachedColliders.Length == 0)
                cachedColliders = GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < cachedColliders.Length; i++)
            {
                if (cachedColliders[i] == null)
                    continue;

                cachedColliders[i].enabled = enabled;
            }
        }

        private void ResolveReferences()
        {
            if (flashbangRoot == null)
                flashbangRoot = gameObject;

            if (rootRB == null)
                rootRB = GetComponent<Rigidbody>();

            if (cachedColliders == null || cachedColliders.Length == 0)
                cachedColliders = GetComponentsInChildren<Collider>(true);
        }
    }
}
