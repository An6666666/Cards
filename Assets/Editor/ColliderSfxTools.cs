using UnityEditor;
using UnityEngine;

public static class ColliderSfxTools
{
    [MenuItem("Tools/Audio/Collider SFX/Add To Selected Colliders And Children", false, 2100)]
    private static void AddToSelectedCollidersAndChildren()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        int inspectedObjects = 0;
        int addedComponents = 0;

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            GameObject selected = selectedObjects[i];
            if (selected == null) continue;

            Transform[] transforms = selected.GetComponentsInChildren<Transform>(true);
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                GameObject current = transforms[transformIndex].gameObject;
                if (!HasSupportedCollider(current))
                {
                    continue;
                }

                inspectedObjects++;
                if (current.GetComponent<ColliderSfxPlayer>() != null)
                {
                    continue;
                }

                Undo.AddComponent<ColliderSfxPlayer>(current);
                addedComponents++;
            }
        }

        Debug.Log($"[ColliderSfxTools] Checked {inspectedObjects} collider object(s), added {addedComponents} ColliderSfxPlayer component(s).");
    }

    [MenuItem("Tools/Audio/Collider SFX/Add To Selected Colliders And Children", true)]
    private static bool ValidateAddToSelectedCollidersAndChildren()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            GameObject selected = selectedObjects[i];
            if (selected == null) continue;

            Transform[] transforms = selected.GetComponentsInChildren<Transform>(true);
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                if (HasSupportedCollider(transforms[transformIndex].gameObject))
                {
                    return true;
                }
            }
        }

        return false;
    }

    [MenuItem("CONTEXT/Collider/Add Collider SFX")]
    private static void AddColliderSfxFromContext(MenuCommand command)
    {
        if (command.context is not Collider collider) return;
        AddComponentIfMissing(collider.gameObject);
    }

    [MenuItem("CONTEXT/Collider2D/Add Collider SFX")]
    private static void AddCollider2DSfxFromContext(MenuCommand command)
    {
        if (command.context is not Collider2D collider) return;
        AddComponentIfMissing(collider.gameObject);
    }

    private static void AddComponentIfMissing(GameObject target)
    {
        if (target == null || target.GetComponent<ColliderSfxPlayer>() != null)
        {
            return;
        }

        Undo.AddComponent<ColliderSfxPlayer>(target);
    }

    private static bool HasSupportedCollider(GameObject target)
    {
        return target != null &&
               (target.GetComponent<Collider>() != null || target.GetComponent<Collider2D>() != null);
    }
}
