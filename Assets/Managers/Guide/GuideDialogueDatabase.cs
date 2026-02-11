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
    private readonly Dictionary<string, List<string>> lookup = new Dictionary<string, List<string>>(System.StringComparer.Ordinal);
    private bool initialized;

    public IReadOnlyList<string> GetLines(string key)
    {
        InitializeIfNeeded();
        if (string.IsNullOrWhiteSpace(key))
        return null;

        string trimmedKey = key.Trim();
        return lookup.TryGetValue(trimmedKey, out List<string> lines) ? lines : null;
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

            string trimmedKey = entry.key.Trim();
            if (string.IsNullOrEmpty(trimmedKey))
            continue;

            if (lookup.ContainsKey(trimmedKey))
            continue;

            lookup.Add(trimmedKey, entry.lines ?? new List<string>());
        }

        initialized = true;
    }
}