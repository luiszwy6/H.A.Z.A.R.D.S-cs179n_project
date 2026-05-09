using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CoverTrigger))]
public class CoverTriggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CoverTrigger coverTrigger = (CoverTrigger)target;
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Auto Fit To Renderer"))
            {
                Undo.RecordObject(coverTrigger, "Auto Fit Cover Trigger");
                if (coverTrigger.TryAutoFitToRenderer())
                {
                    EditorUtility.SetDirty(coverTrigger);
                }
                else
                {
                    Debug.LogWarning("CoverTrigger: No Renderer found to fit.", coverTrigger);
                }
            }
        }
    }
}
