using System;
using System.Collections.Generic;
using UnityEngine;

public class RunMapGenerator
{
    public RunMap Generate(
        int floorCount,
        int minNodes,
        int maxNodes,
        float shopRate,
        float eventRate,
        float eliteRate,
        EncounterPool encounterPool,
        EncounterPool elitePool,
        RunEncounterDefinition bossEncounter,
        ShopInventoryDefinition defaultShop,
        List<RunEventDefinition> eventPool)
    {
        var map = new RunMap();
        int totalFloors = Mathf.Max(1, floorCount);
        for (int floor = 0; floor < totalFloors; floor++)
        {
            int nodeCount = floor == totalFloors - 1 ? 1 : GetRandomNodeCountForFloor(map.Floors, floor, minNodes, maxNodes);

            var floorNodes = new List<MapNodeData>(nodeCount);
            for (int i = 0; i < nodeCount; i++)
            {
                MapNodeType type = DetermineNodeTypeForFloor(floor, floorCount, shopRate, eventRate, eliteRate);
                string nodeId = $"F{floor}_N{i}_{Guid.NewGuid():N}";
                var node = new MapNodeData(nodeId, type, floor);

                if (type == MapNodeType.Battle)
                {
                    node.SetEncounter(encounterPool != null ? encounterPool.GetRandomEncounter() : null);
                }
                else if (type == MapNodeType.EliteBattle)
                {
                    RunEncounterDefinition eliteEncounter = elitePool != null
                        ? elitePool.GetRandomEncounter()
                        : null;
                    node.SetEncounter(eliteEncounter != null ? eliteEncounter : encounterPool?.GetRandomEncounter());
                }
                else if (type == MapNodeType.Boss)
                {
                    node.SetEncounter(bossEncounter != null ? bossEncounter : encounterPool?.GetRandomEncounter());
                }
                else if (type == MapNodeType.Event)
                {
                    node.SetEvent(GetRandomEventDefinition(eventPool));
                }
                else if (type == MapNodeType.Shop)
                {
                    node.SetShop(defaultShop);
                }

                floorNodes.Add(node);
            }
            map.Floors.Add(floorNodes);
        }

        return map;
    }

    private int GetRandomNodeCountForFloor(List<List<MapNodeData>> floors, int floor, int minNodes, int maxNodes)
    {
        int clampedMin = Mathf.Max(1, Mathf.Min(minNodes, maxNodes));
        int clampedMax = Mathf.Max(clampedMin, maxNodes);

        if (floor <= 0 || floors.Count == 0)
        {
            return UnityEngine.Random.Range(clampedMin, clampedMax + 1);
        }

        int previousCount = floors[floors.Count - 1].Count;
        int smoothMin = Mathf.Max(clampedMin, previousCount - 1);
        int smoothMax = Mathf.Min(clampedMax, previousCount + 1);

        if (smoothMin > smoothMax)
        {
            smoothMin = clampedMin;
            smoothMax = clampedMax;
        }

        return UnityEngine.Random.Range(smoothMin, smoothMax + 1);
    }

    private MapNodeType DetermineNodeTypeForFloor(int floor, int totalFloors, float shopRate, float eventRate, float eliteRate)
    {
        if (floor == 0)
            return MapNodeType.Battle;

        if (floor == Mathf.Max(1, totalFloors) - 1)
            return MapNodeType.Boss;

        float roll = UnityEngine.Random.value;
        float clampedShopRate = Mathf.Clamp01(shopRate);
        float clampedEventRate = Mathf.Clamp01(eventRate);
        float clampedEliteRate = Mathf.Clamp01(eliteRate);

        if (roll < clampedShopRate)
            return MapNodeType.Shop;
        if (roll < clampedShopRate + clampedEventRate)
            return MapNodeType.Event;
        if (roll < clampedShopRate + clampedEventRate + clampedEliteRate)
            return MapNodeType.EliteBattle;
        return MapNodeType.Battle;
    }

    private RunEventDefinition GetRandomEventDefinition(List<RunEventDefinition> eventPool)
    {
        if (eventPool == null || eventPool.Count == 0)
            return null;
        int index = UnityEngine.Random.Range(0, eventPool.Count);
        return eventPool[index];
    }
}
