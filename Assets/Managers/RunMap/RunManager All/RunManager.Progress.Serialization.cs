using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class RunManager
{
    private static RunProgressSnapshotData BuildSnapshotData(PlayerRunSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return null;
        }

        RunProgressSnapshotData data = new RunProgressSnapshotData
        {
            maxHP = snapshot.maxHP,
            currentHP = snapshot.currentHP,
            gold = snapshot.gold
        };

        AddAssetReferences(snapshot.deck, data.deck);
        AddAssetReferences(snapshot.relics, data.relics);
        AddAssetReferences(snapshot.exhaustPile, data.exhaustPile);
        return data;
    }

    private static void AddAssetReferences<T>(IEnumerable<T> source, List<RunProgressAssetReference> target)
        where T : UnityEngine.Object
    {
        if (source == null || target == null)
        {
            return;
        }

        foreach (T asset in source)
        {
            RunProgressAssetReference reference = BuildAssetReference(asset);
            if (reference != null)
            {
                target.Add(reference);
            }
        }
    }

    private static RunProgressAssetReference BuildAssetReference(UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return null;
        }

        return new RunProgressAssetReference
        {
            assetName = NormalizeRuntimeAssetName(asset.name),
            typeName = asset.GetType().FullName
        };
    }

    private static string NormalizeRuntimeAssetName(string assetName)
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

    private static PlayerRunSnapshot ResolveSnapshot(RunProgressSnapshotData data)
    {
        if (data == null)
        {
            return null;
        }

        RunProgressAssetResolver.EnsureReady();

        PlayerRunSnapshot snapshot = new PlayerRunSnapshot
        {
            maxHP = data.maxHP,
            currentHP = data.currentHP,
            gold = data.gold,
            deck = ResolveCards(data.deck),
            relics = ResolveRelics(data.relics),
            exhaustPile = ResolveCards(data.exhaustPile)
        };

        int expectedAssetCount = CountReferences(data.deck) + CountReferences(data.relics) + CountReferences(data.exhaustPile);
        int resolvedAssetCount = snapshot.deck.Count + snapshot.relics.Count + snapshot.exhaustPile.Count;
        if (expectedAssetCount > 0 && resolvedAssetCount == 0)
        {
            Debug.LogWarning("RunProgress: saved snapshot had asset references, but none could be resolved. Skipping this snapshot to avoid wiping player progress.");
            return null;
        }

        return snapshot;
    }

    private static int CountReferences(IEnumerable<RunProgressAssetReference> references)
    {
        if (references == null)
        {
            return 0;
        }

        int count = 0;
        foreach (RunProgressAssetReference reference in references)
        {
            if (reference != null)
            {
                count++;
            }
        }

        return count;
    }

    private static List<CardBase> ResolveCards(IEnumerable<RunProgressAssetReference> references)
    {
        List<CardBase> results = new List<CardBase>();
        if (references == null)
        {
            return results;
        }

        foreach (RunProgressAssetReference reference in references)
        {
            if (reference == null)
            {
                continue;
            }

            CardBase template = RunProgressAssetResolver.ResolveCard(reference.assetName, reference.typeName);
            if (PlayerRunSnapshot.ShouldPersistCard(template))
            {
                results.Add(UnityEngine.Object.Instantiate(template));
            }
        }

        return results;
    }

    private static List<RelicBase> ResolveRelics(IEnumerable<RunProgressAssetReference> references)
    {
        List<RelicBase> results = new List<RelicBase>();
        if (references == null)
        {
            return results;
        }

        foreach (RunProgressAssetReference reference in references)
        {
            if (reference == null)
            {
                continue;
            }

            RelicBase template = RunProgressAssetResolver.ResolveRelic(reference.assetName, reference.typeName);
            if (template != null)
            {
                results.Add(UnityEngine.Object.Instantiate(template));
            }
        }

        return results;
    }

    private static List<CardBase> ResolveCardTemplates(IEnumerable<RunProgressAssetReference> references)
    {
        List<CardBase> results = new List<CardBase>();
        if (references == null)
        {
            return results;
        }

        foreach (RunProgressAssetReference reference in references)
        {
            if (reference == null)
            {
                continue;
            }

            CardBase template = RunProgressAssetResolver.ResolveCard(reference.assetName, reference.typeName);
            if (template != null)
            {
                results.Add(template);
            }
        }

        return results;
    }

    private static List<RelicBase> ResolveRelicTemplates(IEnumerable<RunProgressAssetReference> references)
    {
        List<RelicBase> results = new List<RelicBase>();
        if (references == null)
        {
            return results;
        }

        foreach (RunProgressAssetReference reference in references)
        {
            if (reference == null)
            {
                continue;
            }

            RelicBase template = RunProgressAssetResolver.ResolveRelic(reference.assetName, reference.typeName);
            if (template != null)
            {
                results.Add(template);
            }
        }

        return results;
    }

    private Dictionary<string, MapNodeData> RestoreMapNodes(IEnumerable<RunProgressNodeData> nodeDataList)
    {
        mapFloors.Clear();

        Dictionary<string, MapNodeData> nodesById = new Dictionary<string, MapNodeData>(StringComparer.Ordinal);
        if (nodeDataList == null)
        {
            return nodesById;
        }

        List<RunProgressNodeData> orderedNodes = nodeDataList
            .Where(nodeData => nodeData != null && !string.IsNullOrWhiteSpace(nodeData.nodeId))
            .OrderBy(nodeData => nodeData.floorIndex)
            .ThenBy(nodeData => nodeData.nodeId, StringComparer.Ordinal)
            .ToList();

        for (int i = 0; i < orderedNodes.Count; i++)
        {
            RunProgressNodeData nodeData = orderedNodes[i];
            while (mapFloors.Count <= nodeData.floorIndex)
            {
                mapFloors.Add(new List<MapNodeData>());
            }

            MapNodeType nodeType = Enum.IsDefined(typeof(MapNodeType), nodeData.nodeType)
                ? (MapNodeType)nodeData.nodeType
                : MapNodeType.Battle;

            MapNodeData node = new MapNodeData(nodeData.nodeId, nodeType, Mathf.Max(0, nodeData.floorIndex));
            if (nodeData.isCompleted)
            {
                node.MarkCompleted();
            }

            node.SetEncounter(RunProgressAssetResolver.ResolveEncounter(nodeData.encounterId));
            node.SetEvent(RunProgressAssetResolver.ResolveEvent(nodeData.eventId));
            node.SetShop(RunProgressAssetResolver.ResolveShop(nodeData.shopName));
            node.SetShopOfferState(
                nodeData.shopOffersGenerated,
                ResolveCardTemplates(nodeData.shopCardOffers),
                ResolveRelicTemplates(nodeData.shopRelicOffers));

            nodesById[node.NodeId] = node;
            mapFloors[node.FloorIndex].Add(node);
        }

        for (int i = 0; i < orderedNodes.Count; i++)
        {
            RunProgressNodeData nodeData = orderedNodes[i];
            if (!nodesById.TryGetValue(nodeData.nodeId, out MapNodeData node))
            {
                continue;
            }

            node.ClearNextNodes();
            if (nodeData.nextNodeIds == null)
            {
                continue;
            }

            for (int nextIndex = 0; nextIndex < nodeData.nextNodeIds.Count; nextIndex++)
            {
                string nextId = nodeData.nextNodeIds[nextIndex];
                if (!string.IsNullOrWhiteSpace(nextId) && nodesById.TryGetValue(nextId, out MapNodeData nextNode))
                {
                    node.AddNextNode(nextNode);
                }
            }
        }

        return nodesById;
    }

    private static MapNodeData ResolveNodeById(Dictionary<string, MapNodeData> nodesById, string nodeId)
    {
        if (nodesById == null || string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        nodesById.TryGetValue(nodeId, out MapNodeData node);
        return node;
    }
}
