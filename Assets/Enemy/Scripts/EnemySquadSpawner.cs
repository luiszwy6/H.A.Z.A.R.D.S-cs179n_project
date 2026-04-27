using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemySquadSpawner : MonoBehaviour
{
    [System.Serializable]
    public class SquadDefinition
    {
        public string squadId = "Squad_A";
        public Transform centerPoint;
        public int meleeCount = 3;
        public int rangedCount = 2;
        public float spawnRadius = 6f;
    }

    [Header("Spawn")]
    public SquadDefinition[] squads;
    public bool spawnOnStart = true;
    public float navMeshSampleRadius = 4f;

    [Header("AI Defaults")]
    public float sharedSightRange = 18f;
    public Transform[] sharedPatrolPoints;

    [Header("Role Colors")]
    public Color meleeColor = new Color(0.85f, 0.25f, 0.2f);
    public Color rangedColor = new Color(0.2f, 0.6f, 0.95f);

    [Header("Capsule Scale")]
    public Vector3 meleeScale = new Vector3(0.75f, 0.85f, 0.75f);
    public Vector3 rangedScale = new Vector3(0.72f, 0.82f, 0.72f);

    [Header("Parent")]
    public Transform enemyRoot;

    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnSquads();
        }
    }

    [ContextMenu("Spawn Squads")]
    public void SpawnSquads()
    {
        ClearSpawned();

        SquadDefinition[] activeSquads = squads;
        if (activeSquads == null || activeSquads.Length == 0)
        {
            activeSquads = new[]
            {
                new SquadDefinition
                {
                    squadId = "Squad_A",
                    centerPoint = transform,
                    meleeCount = 3,
                    rangedCount = 2,
                    spawnRadius = 6f
                }
            };
        }

        for (int i = 0; i < activeSquads.Length; i++)
        {
            SquadDefinition def = activeSquads[i];
            if (def == null || string.IsNullOrWhiteSpace(def.squadId))
            {
                continue;
            }

            Vector3 center = def.centerPoint != null ? def.centerPoint.position : transform.position;

            SpawnRoleBatch(def, EnemyAI.EnemyRole.Melee, def.meleeCount, center, meleeColor);
            SpawnRoleBatch(def, EnemyAI.EnemyRole.Ranged, def.rangedCount, center, rangedColor);
        }
    }

    [ContextMenu("Clear Spawned")]
    public void ClearSpawned()
    {
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (spawnedEnemies[i] != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(spawnedEnemies[i]);
                }
                else
                {
                    DestroyImmediate(spawnedEnemies[i]);
                }
            }
        }

        spawnedEnemies.Clear();
    }

    private void SpawnRoleBatch(SquadDefinition def, EnemyAI.EnemyRole role, int count, Vector3 center, Color roleColor)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * Mathf.Max(1f, def.spawnRadius);
            Vector3 desired = center + new Vector3(randomCircle.x, 0f, randomCircle.y);

            Vector3 spawnPosition = ResolveSpawnPosition(desired);
            GameObject enemy = CreateCapsuleEnemy(def.squadId, role, spawnPosition, roleColor);
            if (enemy != null)
            {
                spawnedEnemies.Add(enemy);
            }
        }
    }

    private GameObject CreateCapsuleEnemy(string squadId, EnemyAI.EnemyRole role, Vector3 position, Color roleColor)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = $"Enemy_{squadId}_{role}";
        go.transform.position = position;
        go.transform.SetParent(enemyRoot, true);
        go.transform.localScale = GetScaleForRole(role);

        Renderer rendererRef = go.GetComponent<Renderer>();
        if (rendererRef != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = roleColor;
            rendererRef.sharedMaterial = mat;
        }

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        NavMeshAgent navAgent = go.AddComponent<NavMeshAgent>();
        navAgent.angularSpeed = 540f;
        navAgent.acceleration = 14f;
        navAgent.stoppingDistance = 1.2f;
        navAgent.radius = 0.4f;
        navAgent.height = 1.45f;

        EnemyAI enemyAI = go.AddComponent<EnemyAI>();
        enemyAI.applyRoleDefaultsOnStart = true;
        enemyAI.sightRange = sharedSightRange;
        enemyAI.patrolPoints = sharedPatrolPoints;
        enemyAI.ConfigureForSpawn(squadId, role);

        return go;
    }

    private Vector3 ResolveSpawnPosition(Vector3 desiredPosition)
    {
        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return desiredPosition;
    }

    private Vector3 GetScaleForRole(EnemyAI.EnemyRole role)
    {
        switch (role)
        {
            case EnemyAI.EnemyRole.Ranged:
                return rangedScale;
            default:
                return meleeScale;
        }
    }
}
