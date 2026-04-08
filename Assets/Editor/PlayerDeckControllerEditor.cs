using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerDeckController))]
public class PlayerDeckControllerEditor : Editor
{
    private const string RelicsFolder = "Assets/Scripts/Relics";

    private SerializedProperty startingRelicProperty;
    private readonly List<RelicBase> availableRelics = new List<RelicBase>();
    private string[] relicDisplayOptions = new[] { "None" };

    private void OnEnable()
    {
        startingRelicProperty = serializedObject.FindProperty("startingRelic");
        ReloadAvailableRelics();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.UpdateIfRequiredOrScript();

        DrawPropertiesExcluding(serializedObject, "startingRelic");
        DrawStartingRelicSelector();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawStartingRelicSelector()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Starting Relic", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            int selectedIndex = GetSelectedIndex();
            int nextIndex = EditorGUILayout.Popup(new GUIContent("Relic"), selectedIndex, relicDisplayOptions);
            if (nextIndex != selectedIndex)
            {
                startingRelicProperty.objectReferenceValue = nextIndex <= 0 ? null : availableRelics[nextIndex - 1];
            }

            if (GUILayout.Button("Refresh", GUILayout.Width(64f)))
            {
                ReloadAvailableRelics();
            }
        }

        if (availableRelics.Count == 0)
        {
            EditorGUILayout.HelpBox($"No RelicBase assets were found in {RelicsFolder}.", MessageType.Warning);
            EditorGUILayout.PropertyField(startingRelicProperty);
            return;
        }

        RelicBase selectedRelic = startingRelicProperty.objectReferenceValue as RelicBase;
        if (selectedRelic != null)
        {
            EditorGUILayout.HelpBox($"Current starting relic: {GetRelicDisplayName(selectedRelic)}", MessageType.Info);
        }
    }

    private int GetSelectedIndex()
    {
        RelicBase selectedRelic = startingRelicProperty.objectReferenceValue as RelicBase;
        if (selectedRelic == null)
        {
            return 0;
        }

        for (int i = 0; i < availableRelics.Count; i++)
        {
            if (availableRelics[i] == selectedRelic)
            {
                return i + 1;
            }
        }

        return 0;
    }

    private void ReloadAvailableRelics()
    {
        availableRelics.Clear();

        string[] guids = AssetDatabase.FindAssets("t:RelicBase", new[] { RelicsFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            RelicBase relic = AssetDatabase.LoadAssetAtPath<RelicBase>(path);
            if (relic != null)
            {
                availableRelics.Add(relic);
            }
        }

        availableRelics.Sort((left, right) => string.Compare(GetRelicDisplayName(left), GetRelicDisplayName(right), System.StringComparison.Ordinal));

        relicDisplayOptions = new string[availableRelics.Count + 1];
        relicDisplayOptions[0] = "None";
        for (int i = 0; i < availableRelics.Count; i++)
        {
            relicDisplayOptions[i + 1] = GetRelicDisplayName(availableRelics[i]);
        }
    }

    private static string GetRelicDisplayName(RelicBase relic)
    {
        if (relic == null)
        {
            return "None";
        }

        return string.IsNullOrWhiteSpace(relic.cardName) ? relic.name : $"{relic.cardName} ({relic.name})";
    }
}
