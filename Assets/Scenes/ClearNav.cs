#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AI;
using UnityEditor.SceneManagement;
using UnityEngine;

public class ClearNav : MonoBehaviour
{
    [ContextMenu("Clear Baked NavMesh")]
    void Clear()
    {
        NavMeshBuilder.ClearAllNavMeshes();
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("Baked NavMesh cleared — save the scene to persist.");
    }
}
#endif
