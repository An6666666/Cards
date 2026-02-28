using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject dialogue database: maps a key to multiple dialogue lines for reuse across scenes.
/// </summary>
[CreateAssetMenu(fileName = "GuideDialogueDatabase", menuName = "Guide/Dialogue Database")]
public class GuideDialogueDatabase : ScriptableObject
{
    private static readonly char[] KeyTrimChars = { ' ', '\t', '\r', '\n', '\u200B', '\uFEFF' };

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
        if (string.IsNullOrWhiteSpace(key))
            return null;

        string normalizedKey = NormalizeKey(key);
        if (string.IsNullOrEmpty(normalizedKey))
            return null;

        InitializeIfNeeded();
        if (lookup.TryGetValue(normalizedKey, out List<string> lines) && lines != null && lines.Count > 0)
            return lines;

        // Fast-enter play mode can keep stale lookup; force one rebuild on miss.
        RebuildLookup();
        return lookup.TryGetValue(normalizedKey, out lines) ? lines : null;
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
            return;

        RebuildLookupInternal();
    }

    private void RebuildLookup()
    {
        initialized = false;
        RebuildLookupInternal();
    }

    private void RebuildLookupInternal()
    {
        lookup.Clear();
        foreach (DialogueEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            string normalizedKey = NormalizeKey(entry.key);
            if (string.IsNullOrEmpty(normalizedKey))
                continue;

            if (lookup.ContainsKey(normalizedKey))
                continue;

            lookup.Add(normalizedKey, entry.lines ?? new List<string>());
        }

        initialized = true;
    }

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        return key.Trim(KeyTrimChars);
    }

    private void OnValidate()
    {
        initialized = false;
    }

    private void OnEnable()
    {
        initialized = false;
    }
}
