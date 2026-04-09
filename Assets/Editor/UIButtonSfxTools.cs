using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class UIButtonSfxTools
{
    [MenuItem("Tools/UI/Button SFX/Add To Selected Buttons And Children", false, 2000)]
    private static void AddToSelectedButtonsAndChildren()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        int inspectedButtons = 0;
        int addedComponents = 0;

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            if (selectedObjects[i] == null) continue;

            Button[] buttons = selectedObjects[i].GetComponentsInChildren<Button>(true);
            for (int buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
            {
                Button button = buttons[buttonIndex];
                if (button == null) continue;

                inspectedButtons++;
                if (button.GetComponent<UIButtonSfxPlayer>() != null) continue;

                Undo.AddComponent<UIButtonSfxPlayer>(button.gameObject);
                addedComponents++;
            }
        }

        Debug.Log($"[UIButtonSfxTools] Checked {inspectedButtons} button(s), added {addedComponents} UIButtonSfxPlayer component(s).");
    }

    [MenuItem("Tools/UI/Button SFX/Add To Selected Buttons And Children", true)]
    private static bool ValidateAddToSelectedButtonsAndChildren()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            if (selectedObjects[i] != null && selectedObjects[i].GetComponentInChildren<Button>(true) != null)
            {
                return true;
            }
        }

        return false;
    }

    [MenuItem("CONTEXT/Button/Add Button SFX")]
    private static void AddButtonSfxFromContext(MenuCommand command)
    {
        if (command.context is not Button button) return;
        if (button.GetComponent<UIButtonSfxPlayer>() != null) return;

        Undo.AddComponent<UIButtonSfxPlayer>(button.gameObject);
    }
}
