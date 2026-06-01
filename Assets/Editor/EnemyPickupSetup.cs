using UnityEditor;
using UnityEngine;
using Pickups;

public static class EnemyPickupSetup
{
    static readonly string[] EnemyPrefabPaths =
    {
        "Assets/Prefabs/Enemy/Enemy_Assault.prefab",
        "Assets/Prefabs/Enemy/Enemy_Shield.prefab",
        "Assets/Prefabs/Enemy/Enemy_Shotgun.prefab",
        "Assets/Prefabs/Enemy/Enemy_Sniper.prefab",
    };

    static readonly (string path, float chance)[] DropTable =
    {
        ("Assets/Pickups/Prefabs/Pickup_HealthKit.prefab", 0.4f),
        ("Assets/Pickups/Prefabs/Pickup_AmmoCase.prefab",  0.4f),
        ("Assets/Pickups/Prefabs/Pickup_Grenade.prefab",   0.2f),
    };

    [MenuItem("Tools/Setup Enemy Pickups")]
    static void Setup()
    {
        foreach (string prefabPath in EnemyPrefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogWarning($"[EnemyPickupSetup] Prefab not found: {prefabPath}");
                continue;
            }

            if (root.GetComponent<PickupDropper>() == null)
                root.AddComponent<PickupDropper>();

            if (root.GetComponent<EnemyDropOnDeath>() == null)
                root.AddComponent<EnemyDropOnDeath>();

            ConfigureDropTable(root.GetComponent<PickupDropper>());

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            Debug.Log($"[EnemyPickupSetup] Configured {prefabPath}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[EnemyPickupSetup] Done — all enemy prefabs configured.");
    }

    static void ConfigureDropTable(PickupDropper dropper)
    {
        var so = new SerializedObject(dropper);
        SerializedProperty table = so.FindProperty("dropTable");
        table.ClearArray();

        int index = 0;
        foreach (var (path, chance) in DropTable)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[EnemyPickupSetup] Pickup prefab not found: {path}");
                continue;
            }

            table.arraySize++;
            SerializedProperty entry = table.GetArrayElementAtIndex(index++);
            entry.FindPropertyRelative("pickupPrefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("dropChance").floatValue = chance;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
