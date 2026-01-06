using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject dialogue database: maps a key to multiple dialogue lines for reuse across scenes.
/// </summary>
[CreateAssetMenu(fileName = "GuideDialogueDatabase", menuName = "Guide/Dialogue Database")]
public class GuideDialogueDatabase : ScriptableObject
{
    [System.Serializable]
    public class DialogueEntry
    {
        public string key;
        [TextArea(2, 4)]
        public List<string> lines = new List<string>();
    }

    [SerializeField] private List<DialogueEntry> entries = new List<DialogueEntry>();
    private readonly Dictionary<string, List<string>> lookup = new Dictionary<string, List<string>>();
    private bool initialized;

    public IReadOnlyList<string> GetLines(string key)
    {
        InitializeIfNeeded();
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return lookup.TryGetValue(key, out List<string> lines) ? lines : null;
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
            return;

        lookup.Clear();
        foreach (DialogueEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            if (lookup.ContainsKey(entry.key))
                continue;

            lookup.Add(entry.key, entry.lines ?? new List<string>());
        }

        initialized = true;
    }
}