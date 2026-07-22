using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace ASGS.Grenade
{
    public class Grenade_Low_YieldwPin : MonoBehaviour
    {
        public float delay = 4;

        [FormerlySerializedAs("damage")]
        [SerializeField] private float baseDamage = 100f;

        public GameObject grenade;
        public GameObject explosionObject;
        public GameObject pinPrefab;
        public Rigidbody pinRB;
        public GameObject explosionEffectLowYield;
        public Rigidbody HandleRB;
        public float IgniteThrust = 5;

        [Header("Hitbox Damage")]
        [SerializeField] private bool applyEnemyDamage = true;
        [SerializeField] private bool applyPlayerDamage = true;
        [SerializeField] private LayerMask damageMask = ~0;
        [SerializeField] private QueryTriggerInteraction damageTriggerInteraction = QueryTriggerInteraction.Collide;
        [SerializeField] private bool useHitboxClosestPoint = true;

        [Header("Damage Radius")]
        [FormerlySerializedAs("innerFalloffRadius")]
        [Min(0f)]
        [SerializeField] private float innerFullDamageRadius = 1.5f;

        [FormerlySerializedAs("fullDamageRadius")]
        [Min(0f)]
        [SerializeField] private float middleDamageRadius = 3f;

        [FormerlySerializedAs("BlastRadiusLY")]
        [Min(0f)]
        [SerializeField] private float maxDamageRadius = 5f;

        [FormerlySerializedAs("innerRadiusDamagePercent")]
        [Range(0f, 1f)]
        [SerializeField] private float middleRadiusDamagePercent = 0.6f;

        [FormerlySerializedAs("minDamagePercent")]
        [Range(0f, 1f)]
        [SerializeField] private float outerRadiusDamagePercent = 0.15f;

        [Header("Armor")]
        [SerializeField] private bool useArmorReduction = true;

        [Range(0, 2)]
        [SerializeField] private int armorPierceLevel = 1;

        [Header("Line Of Sight")]
        [SerializeField] private bool requireLineOfSight = false;
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        [Header("Explosion Impact")]
        [SerializeField] private GrenadeExplosionImpact explosionImpact;
        [SerializeField] private bool activateExplosionImpact = true;

        [Header("Cleanup")]
        [SerializeField] private bool destroyGrenadeMeshObject = true;
        [SerializeField] private bool destroyThisObjectOnEnd = true;
        [SerializeField] private float endDelay = 4f;

        [Header("Sound")]
        [SerializeField] private AudioClip explosionSound;
        [Range(0f, 1f)] [SerializeField] private float explosionSoundVolume = 1f;

        [Header("Debug / Inspector Control")]
        [SerializeField] private bool debugArmNow = false;
        [SerializeField] private bool debugExplodeNow = false;
        [SerializeField] private bool autoResetDebugToggles = true;

        [Header("Debug")]
        [SerializeField] private bool logExplosionDamage = false;
        [SerializeField] private bool logExplosionCandidates = false;

        [Header("Gizmos")]
        [SerializeField] private bool drawDamageGizmos = true;
        [SerializeField] private bool drawDamageGizmosOnlyWhenSelected = true;
        [SerializeField] private Color innerFullDamageRadiusColor = new Color(1f, 0f, 0f, 0.35f);
        [SerializeField] private Color middleDamageRadiusColor = new Color(1f, 0.35f, 0f, 0.32f);
        [SerializeField] private Color maxDamageRadiusColor = new Color(1f, 0.75f, 0f, 0.28f);

        [Header("Runtime Radius Debug")]
        [SerializeField] private bool drawDamageRadiusInPlayMode = true;
        [SerializeField] private bool drawRuntimeRadiusOnlyBeforeExplosion = false;
        [SerializeField] private int runtimeCircleSegments = 96;
        [SerializeField] private float runtimeDrawHeightOffset = 0.03f;

        private float counter = 0;
        private bool hasExploded = false;
        private bool hasArmed = false;

        private readonly Dictionary<EnemyHealth, DamageCandidate<EnemyHealth>> enemyCandidates =
            new Dictionary<EnemyHealth, DamageCandidate<EnemyHealth>>();

        private readonly Dictionary<PlayerHealth, DamageCandidate<PlayerHealth>> playerCandidates =
            new Dictionary<PlayerHealth, DamageCandidate<PlayerHealth>>();

        private struct DamageCandidate<T>
        {
            public T health;
            public Collider hitbox;
            public Vector3 damagePoint;
            public float distance;
            public float damagePercent;
        }

        void Update()
        {
            if (debugArmNow)
            {
                if (autoResetDebugToggles)
                    debugArmNow = false;

                ArmGrenade();
            }

            if (debugExplodeNow)
            {
                if (autoResetDebugToggles)
                    debugExplodeNow = false;

                ExplodeNow();
            }

            if (drawDamageRadiusInPlayMode && Application.isPlaying)
            {
                if (!drawRuntimeRadiusOnlyBeforeExplosion || !hasExploded)
                    DrawDamageRadiusRuntime();
            }
        }

        void OnTriggerExit(Collider col)
        {
            if (col.gameObject.tag == "Grenade_Pin")
                ArmGrenade();
        }

        [ContextMenu("Debug Arm Grenade")]
        public void ArmGrenade()
        {
            if (hasArmed)
                return;

            hasArmed = true;
            counter++;

            if (counter == 1)
            {
                Invoke(nameof(releaseHandle), Mathf.Max(0f, delay - 1f));
                Invoke(nameof(Explode), Mathf.Max(0f, delay));

                if (pinRB != null)
                    pinRB.isKinematic = false;
            }
        }

        [ContextMenu("Debug Explode Now")]
        public void ExplodeNow()
        {
            CancelInvoke(nameof(releaseHandle));
            CancelInvoke(nameof(Explode));
            Explode();
        }

        void releaseHandle()
        {
            if (HandleRB == null)
                return;

            HandleRB.isKinematic = false;
            HandleRB.AddForce(IgniteThrust * transform.up, ForceMode.Impulse);
            HandleRB.AddForce(transform.forward * IgniteThrust / 4, ForceMode.Impulse);
        }

        void Explode()
        {
            if (hasExploded)
                return;

            hasExploded = true;

            Vector3 explosionPosition = transform.position;

            if (explosionEffectLowYield != null)
                explosionObject = Instantiate(explosionEffectLowYield, explosionPosition, transform.rotation);

            if (explosionSound != null)
                AudioSource.PlayClipAtPoint(explosionSound, explosionPosition, explosionSoundVolume);

            enemyCandidates.Clear();
            playerCandidates.Clear();

            Collider[] colliders = Physics.OverlapSphere(
                explosionPosition,
                Mathf.Max(0.01f, GetResolvedMaxDamageRadius()),
                damageMask,
                damageTriggerInteraction
            );

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider hitbox = colliders[i];

                if (hitbox == null)
                    continue;

                if (applyEnemyDamage)
                    TryRegisterEnemyHitboxCandidate(hitbox, explosionPosition);

                if (applyPlayerDamage)
                    TryRegisterPlayerHitboxCandidate(hitbox, explosionPosition);
            }

            ApplyEnemyHitboxDamage();
            ApplyPlayerHitboxDamage();

            if (activateExplosionImpact)
            {
                if (explosionImpact == null)
                    explosionImpact = GetComponentInChildren<GrenadeExplosionImpact>(true);

                if (explosionImpact != null)
                    explosionImpact.Activate(explosionPosition);
            }

            HideGrenadeVisualAfterExplosion();

            Invoke(nameof(End), Mathf.Max(0f, endDelay));
        }

        private void TryRegisterEnemyHitboxCandidate(Collider hitbox, Vector3 explosionPosition)
        {
            EnemyHealth enemyHealth = hitbox.GetComponentInParent<EnemyHealth>();

            if (enemyHealth == null)
                return;

            if (enemyHealth.IsDead)
                return;

            // Skip the grenade's owner so enemies don't damage themselves.
            GrenadeWorldController gwc = GetComponent<GrenadeWorldController>();
            if (gwc != null && gwc.Owner != null)
            {
                EnemyHealth ownerHealth = gwc.Owner.GetComponentInParent<EnemyHealth>();
                if (ownerHealth != null && ownerHealth == enemyHealth)
                    return;
            }

            Vector3 damagePoint = ResolveHitboxDamagePoint(hitbox, explosionPosition);

            if (!TryResolveDamagePercent(explosionPosition, damagePoint, out float distance, out float damagePercent))
                return;

            if (requireLineOfSight && !HasLineOfSightToCollider(explosionPosition, damagePoint, hitbox))
                return;

            DamageCandidate<EnemyHealth> candidate = new DamageCandidate<EnemyHealth>
            {
                health = enemyHealth,
                hitbox = hitbox,
                damagePoint = damagePoint,
                distance = distance,
                damagePercent = damagePercent
            };

            if (!enemyCandidates.TryGetValue(enemyHealth, out DamageCandidate<EnemyHealth> existing))
                enemyCandidates.Add(enemyHealth, candidate);
            else if (candidate.distance < existing.distance)
                enemyCandidates[enemyHealth] = candidate;

            if (logExplosionCandidates)
            {
                Debug.Log(
                    $"[Grenade_Low_YieldwPin] Enemy candidate. Enemy={enemyHealth.name}, Hitbox={hitbox.name}, Distance={distance:F2}, DamagePercent={damagePercent:F2}",
                    this
                );
            }
        }

        private void TryRegisterPlayerHitboxCandidate(Collider hitbox, Vector3 explosionPosition)
        {
            PlayerHealth playerHealth = hitbox.GetComponentInParent<PlayerHealth>();

            if (playerHealth == null)
                return;

            if (playerHealth.IsDead)
                return;

            Vector3 damagePoint = ResolveHitboxDamagePoint(hitbox, explosionPosition);

            if (!TryResolveDamagePercent(explosionPosition, damagePoint, out float distance, out float damagePercent))
                return;

            if (requireLineOfSight && !HasLineOfSightToCollider(explosionPosition, damagePoint, hitbox))
                return;

            DamageCandidate<PlayerHealth> candidate = new DamageCandidate<PlayerHealth>
            {
                health = playerHealth,
                hitbox = hitbox,
                damagePoint = damagePoint,
                distance = distance,
                damagePercent = damagePercent
            };

            if (!playerCandidates.TryGetValue(playerHealth, out DamageCandidate<PlayerHealth> existing))
                playerCandidates.Add(playerHealth, candidate);
            else if (candidate.distance < existing.distance)
                playerCandidates[playerHealth] = candidate;

            if (logExplosionCandidates)
            {
                Debug.Log(
                    $"[Grenade_Low_YieldwPin] Player candidate. Player={playerHealth.name}, Hitbox={hitbox.name}, Distance={distance:F2}, DamagePercent={damagePercent:F2}",
                    this
                );
            }
        }

        private void ApplyEnemyHitboxDamage()
        {
            foreach (KeyValuePair<EnemyHealth, DamageCandidate<EnemyHealth>> pair in enemyCandidates)
            {
                EnemyHealth enemyHealth = pair.Key;
                DamageCandidate<EnemyHealth> candidate = pair.Value;

                if (enemyHealth == null)
                    continue;

                if (enemyHealth.IsDead)
                    continue;

                float finalDamage = CalculateFinalDamage(candidate.damagePercent, enemyHealth.CurrentArmorLevel);

                if (finalDamage <= 0f)
                    continue;

                enemyHealth.TakeDamage(finalDamage);

                if (logExplosionDamage)
                {
                    Debug.Log(
                        $"[Grenade_Low_YieldwPin] Enemy hitbox damage. Enemy={enemyHealth.name}, Hitbox={candidate.hitbox.name}, Distance={candidate.distance:F2}, DamagePercent={candidate.damagePercent:F2}, Armor={enemyHealth.CurrentArmorLevel}, Damage={finalDamage:F1}, HP={enemyHealth.CurrentHealth}/{enemyHealth.BaseHealth}",
                        this
                    );
                }
            }
        }

        private void ApplyPlayerHitboxDamage()
        {
            foreach (KeyValuePair<PlayerHealth, DamageCandidate<PlayerHealth>> pair in playerCandidates)
            {
                PlayerHealth playerHealth = pair.Key;
                DamageCandidate<PlayerHealth> candidate = pair.Value;

                if (playerHealth == null)
                    continue;

                if (playerHealth.IsDead)
                    continue;

                float finalDamage = CalculateFinalDamage(candidate.damagePercent, playerHealth.CurrentArmorLevel);

                if (finalDamage <= 0f)
                    continue;

                playerHealth.TakeDamage(finalDamage);

                if (logExplosionDamage)
                {
                    Debug.Log(
                        $"[Grenade_Low_YieldwPin] Player hitbox damage. Player={playerHealth.name}, Hitbox={candidate.hitbox.name}, Distance={candidate.distance:F2}, DamagePercent={candidate.damagePercent:F2}, Armor={playerHealth.CurrentArmorLevel}, Damage={finalDamage:F1}, HP={playerHealth.CurrentHealth}/{playerHealth.BaseHealth}",
                        this
                    );
                }
            }
        }

        private Vector3 ResolveHitboxDamagePoint(Collider hitbox, Vector3 explosionPosition)
        {
            if (hitbox == null)
                return explosionPosition;

            if (useHitboxClosestPoint)
                return hitbox.ClosestPoint(explosionPosition);

            return hitbox.bounds.center;
        }

        private bool TryResolveDamagePercent(
            Vector3 explosionPosition,
            Vector3 targetPoint,
            out float distance,
            out float damagePercent)
        {
            float innerRadius = GetResolvedInnerFullDamageRadius();
            float middleRadius = GetResolvedMiddleDamageRadius();
            float outerRadius = GetResolvedMaxDamageRadius();

            distance = Vector3.Distance(explosionPosition, targetPoint);

            if (outerRadius <= 0.001f)
            {
                damagePercent = 0f;
                return false;
            }

            if (distance > outerRadius)
            {
                damagePercent = 0f;
                return false;
            }

            if (distance <= innerRadius)
            {
                damagePercent = 1f;
                return true;
            }

            if (distance <= middleRadius)
            {
                float falloffRange = Mathf.Max(0.001f, middleRadius - innerRadius);
                float t = Mathf.Clamp01((distance - innerRadius) / falloffRange);

                damagePercent = Mathf.Lerp(
                    1f,
                    Mathf.Clamp01(middleRadiusDamagePercent),
                    t
                );

                damagePercent = Mathf.Clamp01(damagePercent);
                return damagePercent > 0f;
            }

            float outerFalloffRange = Mathf.Max(0.001f, outerRadius - middleRadius);
            float outerT = Mathf.Clamp01((distance - middleRadius) / outerFalloffRange);

            damagePercent = Mathf.Lerp(
                Mathf.Clamp01(middleRadiusDamagePercent),
                Mathf.Clamp01(outerRadiusDamagePercent),
                outerT
            );

            damagePercent = Mathf.Clamp01(damagePercent);
            return damagePercent > 0f;
        }

        private float CalculateFinalDamage(float damagePercent, int armorLevel)
        {
            float rawDamage = Mathf.Max(0f, baseDamage) * Mathf.Clamp01(damagePercent);

            if (!useArmorReduction)
                return rawDamage;

            float armorMultiplier = ResolveArmorDamageMultiplier(
                Mathf.Clamp(armorPierceLevel, 0, 2),
                Mathf.Clamp(armorLevel, 0, 2)
            );

            return rawDamage * armorMultiplier;
        }

        private float ResolveArmorDamageMultiplier(int pierceLevel, int armorLevel)
        {
            if (pierceLevel > armorLevel)
                return 1f;

            if (pierceLevel == armorLevel)
                return 0.7f;

            return 0.35f;
        }

        private float GetResolvedInnerFullDamageRadius()
        {
            float outer = Mathf.Max(0f, maxDamageRadius);
            float middle = Mathf.Clamp(middleDamageRadius, 0f, outer);
            return Mathf.Clamp(innerFullDamageRadius, 0f, middle);
        }

        private float GetResolvedMiddleDamageRadius()
        {
            float outer = Mathf.Max(0f, maxDamageRadius);
            return Mathf.Clamp(middleDamageRadius, 0f, outer);
        }

        private float GetResolvedMaxDamageRadius()
        {
            return Mathf.Max(0f, maxDamageRadius);
        }

        private bool HasLineOfSightToCollider(Vector3 explosionPosition, Vector3 targetPoint, Collider targetCollider)
        {
            if (targetCollider == null)
                return false;

            Vector3 direction = targetPoint - explosionPosition;
            float distance = direction.magnitude;

            if (distance <= 0.01f)
                return true;

            direction.Normalize();

            RaycastHit[] hits = Physics.RaycastAll(
                explosionPosition,
                direction,
                distance,
                lineOfSightMask,
                QueryTriggerInteraction.Ignore
            );

            if (hits == null || hits.Length == 0)
                return true;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;

                if (hitCollider == null)
                    continue;

                if (hitCollider == targetCollider)
                    return true;

                if (hitCollider.transform == targetCollider.transform)
                    return true;

                if (hitCollider.transform.IsChildOf(targetCollider.transform))
                    return true;

                if (targetCollider.transform.IsChildOf(hitCollider.transform))
                    return true;

                EnemyHealth targetEnemy = targetCollider.GetComponentInParent<EnemyHealth>();
                EnemyHealth hitEnemy = hitCollider.GetComponentInParent<EnemyHealth>();

                if (targetEnemy != null && targetEnemy == hitEnemy)
                    return true;

                PlayerHealth targetPlayer = targetCollider.GetComponentInParent<PlayerHealth>();
                PlayerHealth hitPlayer = hitCollider.GetComponentInParent<PlayerHealth>();

                if (targetPlayer != null && targetPlayer == hitPlayer)
                    return true;

                return false;
            }

            return true;
        }

        private void HideGrenadeVisualAfterExplosion()
        {
            if (!destroyGrenadeMeshObject)
                return;

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

                if (explosionObject != null && renderers[i].transform.IsChildOf(explosionObject.transform))
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

        void End()
        {
            if (explosionObject != null)
                Destroy(explosionObject);

            if (destroyThisObjectOnEnd)
                Destroy(gameObject);
        }

        private void OnDrawGizmos()
        {
            if (!drawDamageGizmos)
                return;

            if (drawDamageGizmosOnlyWhenSelected)
                return;

            DrawDamageGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDamageGizmos)
                return;

            DrawDamageGizmos();
        }

        private void DrawDamageGizmos()
        {
            Vector3 center = transform.position;

            float innerRadius = GetResolvedInnerFullDamageRadius();
            float middleRadius = GetResolvedMiddleDamageRadius();
            float outerRadius = GetResolvedMaxDamageRadius();

            if (outerRadius > 0f)
            {
                Gizmos.color = maxDamageRadiusColor;
                Gizmos.DrawWireSphere(center, outerRadius);
            }

            if (middleRadius > 0f)
            {
                Gizmos.color = middleDamageRadiusColor;
                Gizmos.DrawWireSphere(center, middleRadius);
            }

            if (innerRadius > 0f)
            {
                Gizmos.color = innerFullDamageRadiusColor;
                Gizmos.DrawWireSphere(center, innerRadius);
            }
        }

        private void DrawDamageRadiusRuntime()
        {
            Vector3 center = transform.position + Vector3.up * runtimeDrawHeightOffset;

            float innerRadius = GetResolvedInnerFullDamageRadius();
            float middleRadius = GetResolvedMiddleDamageRadius();
            float outerRadius = GetResolvedMaxDamageRadius();

            if (outerRadius > 0f)
                DrawRuntimeCircle(center, outerRadius, maxDamageRadiusColor);

            if (middleRadius > 0f)
                DrawRuntimeCircle(center, middleRadius, middleDamageRadiusColor);

            if (innerRadius > 0f)
                DrawRuntimeCircle(center, innerRadius, innerFullDamageRadiusColor);
        }

        private void DrawRuntimeCircle(Vector3 center, float radius, Color color)
        {
            int segments = Mathf.Max(12, runtimeCircleSegments);
            float step = Mathf.PI * 2f / segments;

            Vector3 previous = center + new Vector3(Mathf.Cos(0f), 0f, Mathf.Sin(0f)) * radius;

            for (int i = 1; i <= segments; i++)
            {
                float angle = step * i;
                Vector3 current = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                Debug.DrawLine(previous, current, color, 0f, false);
                previous = current;
            }
        }
    }
}