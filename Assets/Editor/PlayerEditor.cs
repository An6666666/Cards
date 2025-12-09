using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(Player))]
public class PlayerEditor : Editor
{
    private SerializedProperty deckProperty;
    private ReorderableList deckList;

    private void OnEnable()
    {
        RebuildList();
    }

    private void OnDisable()
    {
        deckList = null;
        deckProperty = null;
    }

    private void RebuildList()
    {
        deckProperty = serializedObject.FindProperty("deck");
        if (deckProperty == null)
        {
            deckList = null;
            return;
        }

        deckList = new ReorderableList(serializedObject, deckProperty, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Deck"),
            elementHeight = EditorGUIUtility.singleLineHeight + 6f
        };

        deckList.drawElementCallback = (rect, index, active, focused) =>
        {
            // The serialized property can be rebuilt after domain reloads or undo/redo.
            var listProperty = deckList?.serializedProperty;
            if (listProperty == null || index >= listProperty.arraySize)
            {
                return;
            }

            SerializedProperty element = listProperty.GetArrayElementAtIndex(index);
            if (element.propertyType != SerializedPropertyType.ObjectReference)
            {
                return;
            }

            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;

            Object reference = element.objectReferenceValue;
            if (reference != null && !(reference is CardBase))
            {
                element.objectReferenceValue = null;
            }

            EditorGUI.ObjectField(rect, element, GUIContent.none);
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.UpdateIfRequiredOrScript();

        if (deckList == null)
        {
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            return;
        }

        if (deckList.serializedProperty != deckProperty)
        {
            RebuildList();
        }

        DrawPropertiesExcluding(serializedObject, "deck");
        deckList.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }
}