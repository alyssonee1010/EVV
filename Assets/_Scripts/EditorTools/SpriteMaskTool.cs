using UnityEngine;
using UnityEditor;

public class SpriteMaskTool : EditorWindow
{
    // This creates a top menu shortcut: Tools > Sprite Mask Utilities > Set Visible Inside Mask
    [MenuItem("Tools/Sprite Mask Utilities/Set Visible Inside Mask")]
    private static void SetVisibleInside()
    {
        ApplyMaskInteraction(SpriteMaskInteraction.VisibleInsideMask);
    }

    // This creates a second shortcut: Tools > Sprite Mask Utilities > Set Visible Outside Mask
    [MenuItem("Tools/Sprite Mask Utilities/Set Visible Outside Mask")]
    private static void SetVisibleOutside()
    {
        ApplyMaskInteraction(SpriteMaskInteraction.VisibleOutsideMask);
    }

    // This creates a third shortcut: Tools > Sprite Mask Utilities > Set None
    [MenuItem("Tools/Sprite Mask Utilities/Set None")]
    private static void SetNone()
    {
        ApplyMaskInteraction(SpriteMaskInteraction.None);
    }

    private static void ApplyMaskInteraction(SpriteMaskInteraction targetInteraction)
    {
        // Get the active GameObject selected in the Hierarchy
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null)
        {
            Debug.LogWarning("SpriteMaskTool: Please select a GameObject in the Hierarchy first!");
            return;
        }

        // Find all SpriteRenderers in the selected object and its children
        SpriteRenderer[] renderers = selectedObject.GetComponentsInChildren<SpriteRenderer>(true);
        
        int count = 0;
        foreach (SpriteRenderer sr in renderers)
        {
            // Allows you to use Ctrl+Z to undo the changes
            Undo.RecordObject(sr, "Change Sprite Mask Interaction");
            
            sr.maskInteraction = targetInteraction;
            count++;
        }

        Debug.Log($"SpriteMaskTool: Successfully set Mask Interaction to {targetInteraction} on {count} SpriteRenderers under '{selectedObject.name}'.");
    }
}
