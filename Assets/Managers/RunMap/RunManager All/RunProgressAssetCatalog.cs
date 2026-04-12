using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "Run/Run Progress Asset Catalog", fileName = "RunProgressAssetCatalog")]
public class RunProgressAssetCatalog : ScriptableObject
{
    public const string ResourcesLoadPath = "RunProgress/RunProgressAssetCatalog";

#if UNITY_EDITOR
    public const string AssetPath = "Assets/Resources/RunProgress/RunProgressAssetCatalog.asset";
#endif

    [SerializeField] private List<CardBase> cards = new List<CardBase>();
    [SerializeField] private List<RelicBase> relics = new List<RelicBase>();
    [SerializeField] private List<RunEncounterDefinition> encounters = new List<RunEncounterDefinition>();
    [SerializeField] private List<RunEventDefinition> events = new List<RunEventDefinition>();
    [SerializeField] private List<ShopInventoryDefinition> shopInventories = new List<ShopInventoryDefinition>();

    public IReadOnlyList<CardBase> Cards => cards;
    public IReadOnlyList<RelicBase> Relics => relics;
    public IReadOnlyList<RunEncounterDefinition> Encounters => encounters;
    public IReadOnlyList<RunEventDefinition> Events => events;
    public IReadOnlyList<ShopInventoryDefinition> ShopInventories => shopInventories;

#if UNITY_EDITOR
    public static RunProgressAssetCatalog LoadOrCreateEditorAsset()
    {
        RunProgressAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<RunProgressAssetCatalog>(AssetPath);
        if (catalog != null)
        {
            return catalog;
        }

        string directory = Path.GetDirectoryName(AssetPath);
        EnsureFolderPathExists(directory);

        catalog = CreateInstance<RunProgressAssetCatalog>();
        AssetDatabase.CreateAsset(catalog, AssetPath);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    public void RebuildFromProjectAssets()
    {
        cards = LoadAssets<CardBase>();
        relics = LoadAssets<RelicBase>();
        encounters = LoadAssets<RunEncounterDefinition>();
        events = LoadAssets<RunEventDefinition>();
        shopInventories = LoadAssets<ShopInventoryDefinition>();

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    private static List<T> LoadAssets<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        List<T> results = new List<T>(guids.Length);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null || results.Contains(asset))
            {
                continue;
            }

            results.Add(asset);
        }

        return results
            .OrderBy(asset => asset.name, StringComparer.Ordinal)
            .ToList();
    }

    private static void EnsureFolderPathExists(string assetFolderPath)
    {
        if (string.IsNullOrWhiteSpace(assetFolderPath))
        {
            return;
        }

        string normalizedPath = assetFolderPath.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(normalizedPath))
        {
            return;
        }

        string[] parts = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return;
        }

        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
#endif
}

public static class RunProgressAssetResolver
{
    private static bool initialized;
    private static readonly Dictionary<string, CardBase> cardsByCompositeKey = new Dictionary<string, CardBase>(StringComparer.Ordinal);
    private static readonly Dictionary<string, RelicBase> relicsByCompositeKey = new Dictionary<string, RelicBase>(StringComparer.Ordinal);
    private static readonly Dictionary<string, RunEncounterDefinition> encountersById = new Dictionary<string, RunEncounterDefinition>(StringComparer.Ordinal);
    private static readonly Dictionary<string, RunEventDefinition> eventsById = new Dictionary<string, RunEventDefinition>(StringComparer.Ordinal);
    private static readonly Dictionary<string, ShopInventoryDefinition> shopsByName = new Dictionary<string, ShopInventoryDefinition>(StringComparer.Ordinal);
    private static readonly Dictionary<string, CardBase> cardsByName = new Dictionary<string, CardBase>(StringComparer.Ordinal);
    private static readonly Dictionary<string, RelicBase> relicsByName = new Dictionary<string, RelicBase>(StringComparer.Ordinal);

    public static void EnsureReady()
    {
        if (initialized)
        {
            return;
        }

#if UNITY_EDITOR
        RunProgressAssetCatalog editorCatalog = RunProgressAssetCatalog.LoadOrCreateEditorAsset();
        if (editorCatalog != null)
        {
            editorCatalog.RebuildFromProjectAssets();
        }
#endif

        RunProgressAssetCatalog catalog = Resources.Load<RunProgressAssetCatalog>(RunProgressAssetCatalog.ResourcesLoadPath);
        initialized = true;
        ClearCaches();

        if (catalog == null)
        {
            Debug.LogWarning("RunProgressAssetResolver: missing RunProgressAssetCatalog in Resources. Resume may not restore card/relic data.");
            return;
        }

        RegisterCards(catalog.Cards);
        RegisterRelics(catalog.Relics);
        RegisterEncounters(catalog.Encounters);
        RegisterEvents(catalog.Events);
        RegisterShops(catalog.ShopInventories);
    }

    public static CardBase ResolveCard(string assetName, string typeName)
    {
        EnsureReady();
        string normalizedName = NormalizeAssetName(assetName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(typeName) &&
            cardsByCompositeKey.TryGetValue(BuildCompositeKey(normalizedName, typeName), out CardBase exact))
        {
            return exact;
        }

        cardsByName.TryGetValue(normalizedName, out CardBase byName);
        return byName;
    }

    public static RelicBase ResolveRelic(string assetName, string typeName)
    {
        EnsureReady();
        string normalizedName = NormalizeAssetName(assetName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(typeName) &&
            relicsByCompositeKey.TryGetValue(BuildCompositeKey(normalizedName, typeName), out RelicBase exact))
        {
            return exact;
        }

        relicsByName.TryGetValue(normalizedName, out RelicBase byName);
        return byName;
    }

    public static RunEncounterDefinition ResolveEncounter(string encounterId)
    {
        EnsureReady();
        if (string.IsNullOrWhiteSpace(encounterId))
        {
            return null;
        }

        encountersById.TryGetValue(encounterId, out RunEncounterDefinition definition);
        return definition;
    }

    public static RunEventDefinition ResolveEvent(string eventId)
    {
        EnsureReady();
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return null;
        }

        eventsById.TryGetValue(eventId, out RunEventDefinition definition);
        return definition;
    }

    public static ShopInventoryDefinition ResolveShop(string shopName)
    {
        EnsureReady();
        if (string.IsNullOrWhiteSpace(shopName))
        {
            return null;
        }

        shopsByName.TryGetValue(shopName, out ShopInventoryDefinition definition);
        return definition;
    }

    private static void ClearCaches()
    {
        cardsByCompositeKey.Clear();
        relicsByCompositeKey.Clear();
        encountersById.Clear();
        eventsById.Clear();
        shopsByName.Clear();
        cardsByName.Clear();
        relicsByName.Clear();
    }

    private static void RegisterCards(IReadOnlyList<CardBase> cards)
    {
        if (cards == null)
        {
            return;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            CardBase card = cards[i];
            if (card == null)
            {
                continue;
            }

            string normalizedName = NormalizeAssetName(card.name);
            string compositeKey = BuildCompositeKey(normalizedName, card.GetType().FullName);
            if (!cardsByCompositeKey.ContainsKey(compositeKey))
            {
                cardsByCompositeKey.Add(compositeKey, card);
            }

            if (!cardsByName.ContainsKey(normalizedName))
            {
                cardsByName.Add(normalizedName, card);
            }
        }
    }

    private static void RegisterRelics(IReadOnlyList<RelicBase> relics)
    {
        if (relics == null)
        {
            return;
        }

        for (int i = 0; i < relics.Count; i++)
        {
            RelicBase relic = relics[i];
            if (relic == null)
            {
                continue;
            }

            string normalizedName = NormalizeAssetName(relic.name);
            string compositeKey = BuildCompositeKey(normalizedName, relic.GetType().FullName);
            if (!relicsByCompositeKey.ContainsKey(compositeKey))
            {
                relicsByCompositeKey.Add(compositeKey, relic);
            }

            if (!relicsByName.ContainsKey(normalizedName))
            {
                relicsByName.Add(normalizedName, relic);
            }
        }
    }

    private static void RegisterEncounters(IReadOnlyList<RunEncounterDefinition> encounters)
    {
        if (encounters == null)
        {
            return;
        }

        for (int i = 0; i < encounters.Count; i++)
        {
            RunEncounterDefinition encounter = encounters[i];
            if (encounter == null)
            {
                continue;
            }

            string encounterId = encounter.EncounterId;
            if (!string.IsNullOrWhiteSpace(encounterId) && !encountersById.ContainsKey(encounterId))
            {
                encountersById.Add(encounterId, encounter);
            }
        }
    }

    private static void RegisterEvents(IReadOnlyList<RunEventDefinition> events)
    {
        if (events == null)
        {
            return;
        }

        for (int i = 0; i < events.Count; i++)
        {
            RunEventDefinition definition = events[i];
            if (definition == null)
            {
                continue;
            }

            string eventId = definition.EventId;
            if (!string.IsNullOrWhiteSpace(eventId) && !eventsById.ContainsKey(eventId))
            {
                eventsById.Add(eventId, definition);
            }
        }
    }

    private static void RegisterShops(IReadOnlyList<ShopInventoryDefinition> shops)
    {
        if (shops == null)
        {
            return;
        }

        for (int i = 0; i < shops.Count; i++)
        {
            ShopInventoryDefinition definition = shops[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.name) || shopsByName.ContainsKey(definition.name))
            {
                continue;
            }

            shopsByName.Add(definition.name, definition);
        }
    }

    private static string BuildCompositeKey(string assetName, string typeName)
    {
        return $"{typeName}::{assetName}";
    }

    private static string NormalizeAssetName(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return string.Empty;
        }

        string normalized = assetName.Trim();
        const string cloneSuffix = "(Clone)";

        while (normalized.EndsWith(cloneSuffix, StringComparison.Ordinal))
        {
            normalized = normalized.Substring(0, normalized.Length - cloneSuffix.Length).TrimEnd();
        }

        return normalized;
    }
}
