using System;
using System.Collections.Generic;
using UnityEngine;

public class RunMapGenerator
{
    private readonly float floorVarianceChance;

    private readonly int earlyMin;
    private readonly int earlyMax;
    private readonly int midMin;
    private readonly int midMax;
    private readonly int lateMin;
    private readonly int lateMax;

    public RunMapGenerator(
        float floorVarianceChance = 0.2f,
        int earlyMin = 2,
        int earlyMax = 3,
        int midMin = 3,
        int midMax = 5,
        int lateMin = 3,
        int lateMax = 4)
    {
        this.floorVarianceChance = Mathf.Clamp01(floorVarianceChance);
        this.earlyMin = Mathf.Max(1, Mathf.Min(earlyMin, earlyMax));
        this.earlyMax = Mathf.Max(this.earlyMin, earlyMax);
        this.midMin = Mathf.Max(1, Mathf.Min(midMin, midMax));
        this.midMax = Mathf.Max(this.midMin, midMax);
        this.lateMin = Mathf.Max(1, Mathf.Min(lateMin, lateMax));
        this.lateMax = Mathf.Max(this.lateMin, lateMax);
    }

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
            int nodeCount = floor == totalFloors - 1
                ? 1
                : GetRandomNodeCountForFloor(map.Floors, floor, minNodes, maxNodes, floorVarianceChance, totalFloors);


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

    private int GetRandomNodeCountForFloor(
        List<List<MapNodeData>> floors,
        int floor,
        int minNodes,
        int maxNodes,
        float floorVarianceChance,
        int totalFloors)
    {
        int clampedMin = Mathf.Max(1, Mathf.Min(minNodes, maxNodes));
        int clampedMax = Mathf.Max(clampedMin, maxNodes);

        int lastFloor = Mathf.Max(0, totalFloors - 1);
        int baseMin;
        int baseMax;
        if (floor == lastFloor - 1)
        {
            baseMin = Mathf.Max(3, lateMin);
            baseMax = Mathf.Max(baseMin, lateMax);
        }
        else if (floor <= 2)
        {
            baseMin = earlyMin;
            baseMax = Mathf.Max(baseMin, earlyMax);
        }
        else
        {
            baseMin = midMin;
            baseMax = Mathf.Max(baseMin, midMax);
        }

        baseMin = Mathf.Clamp(baseMin, clampedMin, clampedMax);
        baseMax = Mathf.Clamp(baseMax, baseMin, clampedMax);

        bool allowVariance = UnityEngine.Random.value < Mathf.Clamp01(floorVarianceChance);
        int varianceMin = baseMin;
        int varianceMax = baseMax;

        if (allowVariance)
        {
            int expandedMin = baseMin - 1;
            int expandedMax = baseMax + 1;

            if (floor == lastFloor - 1)
            {
                expandedMin = Mathf.Max(3, expandedMin);
            }

            varianceMin = Mathf.Clamp(expandedMin, clampedMin, clampedMax);
            varianceMax = Mathf.Clamp(expandedMax, varianceMin, clampedMax);
        }

        int choice = UnityEngine.Random.Range(varianceMin, varianceMax + 1);
        return Mathf.Clamp(choice, clampedMin, clampedMax);
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
