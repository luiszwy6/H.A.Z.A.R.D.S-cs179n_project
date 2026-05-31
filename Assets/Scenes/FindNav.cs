using UnityEngine;
using Unity.AI.Navigation;

public class FindNavMeshSurfaces : MonoBehaviour
{
    [ContextMenu("Find NavMesh Surfaces")]
    private void FindSurfaces()
    {
#if UNITY_2023_1_OR_NEWER
        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
#else
        NavMeshSurface[] surfaces = Object.FindObjectsOfType<NavMeshSurface>(true);
#endif

        foreach (NavMeshSurface surface in surfaces)
        {
            Debug.Log(
                "Found NavMeshSurface on: " + GetPath(surface.transform),
                surface.gameObject
            );
        }
    }

    private string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}