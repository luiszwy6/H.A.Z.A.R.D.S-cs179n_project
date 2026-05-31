#if UNITY_EDITOR
using UnityEditor.AI;
using UnityEngine;

public class ClearNav : MonoBehaviour
{
    [ContextMenu("Clear Baked NavMesh")]
    void Clear()
    {
        NavMeshBuilder.ClearAllNavMeshes();
        Debug.Log("Baked NavMesh cleared — save the scene to persist.");
    }
}
#endif
