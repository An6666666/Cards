// Default NodeTypeConstraints:
// eliteMinFloorOffset = 2
// eliteMaxFloorOffsetFromBoss = 2
// shopMinFloorOffset = 2
// shopMaxFloorOffsetFromBoss = 3
// eventMinFloorOffset = 1
// eventMaxFloorOffsetFromBoss = 2
// restMinFloorOffset = 2
// restMaxFloorOffsetFromBoss = 1
// eliteMinGap = 3
// shopMinGap = 3
// restMinGap = 2
// forbidConsecutiveElite = true
// forbidConsecutiveShop = true
// forbidConsecutiveRest = true
// maxConsecutiveEvents = 2
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RunMapGenerator
{
    private readonly RunMapGenerationFeatureFlags featureFlags;
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
        int lateMax = 4,
        RunMapGenerationFeatureFlags featureFlags = null)
    {
        this.floorVarianceChance = Mathf.Clamp01(floorVarianceChance);
        this.earlyMin = Mathf.Max(1, Mathf.Min(earlyMin, earlyMax));
        this.earlyMax = Mathf.Max(this.earlyMin, earlyMax);
        this.midMin = Mathf.Max(1, Mathf.Min(midMin, midMax));
        this.midMax = Mathf.Max(this.midMin, midMax);
        this.lateMin = Mathf.Max(1, Mathf.Min(lateMin, lateMax));
        this.lateMax = Mathf.Max(this.lateMin, lateMax);
        this.featureFlags = featureFlags ?? RunMapGenerationFeatureFlags.Default;
    }

    public RunMap Generate(
        int floorCount,
        int minNodes,
        int maxNodes,
        EncounterPool encounterPool,
        EncounterPool elitePool,
        RunEncounterDefinition bossEncounter,
        ShopInventoryDefinition defaultShop,
        List<RunEventDefinition> eventPool)
    {
        var map = new RunMap();
        int totalFloors = Mathf.Max(1, floorCount);
        List<int> floorNodeCounts = BuildFloorNodeCounts(totalFloors, minNodes, maxNodes);

        for (int floor = 0; floor < totalFloors; floor++)
        {
            var floorNodes = new List<MapNodeData>(floorNodeCounts[floor]);
            for (int i = 0; i < floorNodeCounts[floor]; i++)
            {
                MapNodeType type = floor == totalFloors - 1 ? MapNodeType.Boss : MapNodeType.Battle;
                string nodeId = $"F{floor}_N{i}_{Guid.NewGuid():N}";
                var node = new MapNodeData(nodeId, type, floor);

                floorNodes.Add(node);
            }

            map.Floors.Add(floorNodes);
        }

        // 實際的節點類型與內容會在連線建立後，由 AllocateSlotsAcrossMapAfterConnections 決定
        return map;
    }
    public RunMap Generate(
        int floorCount,
        int minNodes,
        int maxNodes,
        EncounterPool encounterPool,
        EncounterPool elitePool,
        RunEncounterDefinition bossEncounter,
        ShopInventoryDefinition defaultShop,
        List<RunEventDefinition> eventPool,
        SlotAllocationSettings settings)
    {
        // 舊呼叫點可能會想直接帶入 slot 設定，這裡保持介面兼容，只回傳基礎地圖
        return Generate(
            floorCount,
            minNodes,
            maxNodes,
            encounterPool,
            elitePool,
            bossEncounter,
            defaultShop,
            eventPool);
    }

    public void AllocateSlotsAcrossMapAfterConnections(
        RunMap map,
        EncounterPool encounterPool,
        EncounterPool elitePool,
        RunEncounterDefinition bossEncounter,
        ShopInventoryDefinition defaultShop,
        List<RunEventDefinition> eventPool,
        SlotAllocationSettings settings)
    {
        if (map == null || map.Floors == null || map.Floors.Count == 0)
            return;

        int totalFloors = map.Floors.Count;
        SlotAllocationSettings clamped = settings.GetClamped();
        NodeTypeConstraints constraints = clamped.TypeConstraints;
        List<NodeSlot> availableSlots = CollectConfigurableSlots(map);
        Dictionary<MapNodeData, List<MapNodeData>> predecessors = BuildPredecessorMap(map);
        var slotContext = new SlotAssignmentContext(map, constraints, availableSlots, predecessors);

        var allocator = new RunMapSlotAllocator();
        var fixedCounts = allocator.ApplyFixedFloorRules(slotContext, clamped.FixedFloorRules);
        int remainingSlots = slotContext.AvailableSlots.Count;

        int shopSlots = Mathf.Min(allocator.GetSlotCount(clamped.ShopMin, clamped.ShopMax, remainingSlots), remainingSlots);
        if (fixedCounts.TryGetValue(MapNodeType.Shop, out int fixedShops))
            shopSlots = Mathf.Max(0, shopSlots - fixedShops);
        remainingSlots -= shopSlots;

        int eliteSlots = Mathf.Min(allocator.GetSlotCount(clamped.EliteMin, clamped.EliteMax, remainingSlots), remainingSlots);
        if (fixedCounts.TryGetValue(MapNodeType.EliteBattle, out int fixedElites))
            eliteSlots = Mathf.Max(0, eliteSlots - fixedElites);
        remainingSlots -= eliteSlots;

        int restSlots = Mathf.Min(allocator.GetSlotCount(clamped.RestMin, clamped.RestMax, remainingSlots), remainingSlots);
        if (fixedCounts.TryGetValue(MapNodeType.Rest, out int fixedRests))
            restSlots = Mathf.Max(0, restSlots - fixedRests);
        remainingSlots -= restSlots;

        int eventSlots = Mathf.Clamp(
            Mathf.RoundToInt(remainingSlots * UnityEngine.Random.Range(clamped.EventRatioMin, clamped.EventRatioMax)),
            0,
            remainingSlots);
        if (fixedCounts.TryGetValue(MapNodeType.Event, out int fixedEvents))
            eventSlots = Mathf.Max(0, eventSlots - fixedEvents);
        remainingSlots -= eventSlots;

        allocator.AllocateSlots(slotContext, shopSlots, eliteSlots, restSlots, eventSlots);

        ApplyNodePayloads(map, encounterPool, elitePool, bossEncounter, defaultShop, eventPool);
        ValidateFixedFloorRules(map, clamped.FixedFloorRules);
    }

    public void AllocateSlotsAcrossMapAfterConnections(
        RunMap map,
        EncounterPool encounterPool,
        EncounterPool elitePool,
        RunEncounterDefinition bossEncounter,
        ShopInventoryDefinition defaultShop,
        List<RunEventDefinition> eventPool)
    {
        // 提供舊版呼叫點的相容性：使用預設的《殺戮尖塔》風格名額配置
        AllocateSlotsAcrossMapAfterConnections(
            map,
            encounterPool,
            elitePool,
            bossEncounter,
            defaultShop,
            eventPool,
            SlotAllocationSettings.Default);
    }

    private List<NodeSlot> CollectConfigurableSlots(RunMap map)
    {
        var incomingCounts = ComputeIncomingCounts(map);
        var slots = new List<NodeSlot>();

        for (int floor = 0; floor < map.Floors.Count; floor++)
        {
            List<MapNodeData> nodes = map.Floors[floor];
            for (int column = 0; column < nodes.Count; column++)
            {
                MapNodeData node = nodes[column];
                if (node.NodeType == MapNodeType.Boss)
                    continue;

                int incoming = incomingCounts.TryGetValue(node, out int count) ? count : 0;
                slots.Add(new NodeSlot(node, floor, column, nodes.Count, incoming, map.Floors.Count));
            }
        }

        return slots;
    }

    private Dictionary<MapNodeData, List<MapNodeData>> BuildPredecessorMap(RunMap map)
    {
        var predecessors = new Dictionary<MapNodeData, List<MapNodeData>>();
        if (map == null || map.Floors == null)
            return predecessors;

        for (int floor = 0; floor < map.Floors.Count - 1; floor++)
        {
            foreach (MapNodeData node in map.Floors[floor])
            {
                if (node?.NextNodes == null)
                    continue;

                foreach (MapNodeData next in node.NextNodes)
                {
                    if (next == null)
                        continue;

                    if (!predecessors.TryGetValue(next, out var parents))
                    {
                        parents = new List<MapNodeData>();
                        predecessors[next] = parents;
                    }

                    if (!parents.Contains(node))
                        parents.Add(node);
                }
            }
        }

        return predecessors;
    }

    private Dictionary<MapNodeData, int> ComputeIncomingCounts(RunMap map)
    {
        var incomingCounts = new Dictionary<MapNodeData, int>();
        for (int floor = 0; floor < map.Floors.Count - 1; floor++)
        {
            foreach (MapNodeData node in map.Floors[floor])
            {
                foreach (MapNodeData next in node.NextNodes)
                {
                    if (!incomingCounts.ContainsKey(next))
                        incomingCounts[next] = 0;
                    incomingCounts[next]++;
                }
            }
        }

        return incomingCounts;
    }

    private void ApplyNodePayloads(
        RunMap map,
        EncounterPool encounterPool,
        EncounterPool elitePool,
        RunEncounterDefinition bossEncounter,
        ShopInventoryDefinition defaultShop,
        List<RunEventDefinition> eventPool)
    {
        foreach (List<MapNodeData> floor in map.Floors)
        {
            foreach (MapNodeData node in floor)
            {
                switch (node.NodeType)
                {
                    case MapNodeType.EliteBattle:
                        RunEncounterDefinition eliteEncounter = elitePool != null
                            ? elitePool.GetRandomEncounter()
                            : null;
                        node.SetEncounter(eliteEncounter != null ? eliteEncounter : encounterPool?.GetRandomEncounter());
                        break;
                    case MapNodeType.Shop:
                        node.SetShop(defaultShop);
                        break;
                    case MapNodeType.Event:
                        node.SetEvent(GetRandomEventDefinition(eventPool));
                        break;
                    case MapNodeType.Boss:
                        node.SetEncounter(bossEncounter != null ? bossEncounter : encounterPool?.GetRandomEncounter());
                        break;
                    case MapNodeType.Rest:
                        // 休息站不需要額外配置
                        break;
                    default:
                        node.SetEncounter(encounterPool != null ? encounterPool.GetRandomEncounter() : null);
                        break;
                }
            }
        }
    }

    private RunEventDefinition GetRandomEventDefinition(List<RunEventDefinition> eventPool)
    {
        if (eventPool == null || eventPool.Count == 0)
            return null;

        int index = UnityEngine.Random.Range(0, eventPool.Count);
        return eventPool[index];
    }

    private List<int> BuildFloorNodeCounts(int totalFloors, int minNodes, int maxNodes)
    {
        var counts = new List<int>(totalFloors);
        for (int floor = 0; floor < totalFloors; floor++)
        {
            int nodeCount = floor == totalFloors - 1
                ? 1
                : GetRandomNodeCountForFloor(floor, minNodes, maxNodes, floorVarianceChance, totalFloors);
            counts.Add(nodeCount);
        }

        return counts;
    }

    private int GetRandomNodeCountForFloor(
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
        else if (floor <= 4)
        {
            baseMin = midMin;
            baseMax = Mathf.Max(baseMin, midMax);
        }
        else
        {
            baseMin = lateMin;
            baseMax = Mathf.Max(baseMin, lateMax);
        }

        int min = Mathf.Clamp(baseMin, clampedMin, clampedMax);
        int max = Mathf.Clamp(baseMax, min, clampedMax);

        if (UnityEngine.Random.value < floorVarianceChance)
        {
            int variance = UnityEngine.Random.Range(-1, 2);
            min = Mathf.Clamp(min + variance, clampedMin, clampedMax);
            max = Mathf.Clamp(max + variance, min, clampedMax);
        }

        return UnityEngine.Random.Range(min, max + 1);
    }

    [Serializable]
    public readonly struct SlotAllocationSettings
    {
        public int ShopMin { get; }
        public int ShopMax { get; }
        public int EliteMin { get; }
        public int EliteMax { get; }
        public int RestMin { get; }
        public int RestMax { get; }
        public float EventRatioMin { get; }
        public float EventRatioMax { get; }
        public NodeTypeConstraints TypeConstraints { get; }
        public IReadOnlyList<FixedFloorNodeRule> FixedFloorRules { get; }

        public static SlotAllocationSettings Default => new SlotAllocationSettings(2, 3, 2, 4, 4, 6, 0.2f, 0.25f, NodeTypeConstraints.Default, Array.Empty<FixedFloorNodeRule>());

        public SlotAllocationSettings(
            int shopMin,
            int shopMax,
            int eliteMin,
            int eliteMax,
            int restMin,
            int restMax,
            float eventRatioMin,
            float eventRatioMax,
            NodeTypeConstraints? typeConstraints = null,
            IReadOnlyList<FixedFloorNodeRule> fixedFloorRules = null)
        {
            ShopMin = shopMin;
            ShopMax = shopMax;
            EliteMin = eliteMin;
            EliteMax = eliteMax;
            RestMin = restMin;
            RestMax = restMax;
            EventRatioMin = eventRatioMin;
            EventRatioMax = eventRatioMax;
            TypeConstraints = typeConstraints ?? NodeTypeConstraints.Default;
            FixedFloorRules = fixedFloorRules ?? Array.Empty<FixedFloorNodeRule>();
        }

        public SlotAllocationSettings GetClamped()
        {
            int shopMin = Mathf.Max(0, ShopMin);
            int shopMax = Mathf.Max(shopMin, ShopMax);
            int eliteMin = Mathf.Max(0, EliteMin);
            int eliteMax = Mathf.Max(eliteMin, EliteMax);
            int restMin = Mathf.Max(0, RestMin);
            int restMax = Mathf.Max(restMin, RestMax);

            float eventMin = Mathf.Clamp01(EventRatioMin);
            float eventMax = Mathf.Clamp(EventRatioMax, eventMin, 1f);

            NodeTypeConstraints clampedConstraints = TypeConstraints.GetClamped();
            List<FixedFloorNodeRule> clampedRules = FixedFloorRules == null
                ? new List<FixedFloorNodeRule>()
                : FixedFloorRules
                    .Where(r => r.FloorIndex >= 0)
                    .ToList();

            return new SlotAllocationSettings(shopMin, shopMax, eliteMin, eliteMax, restMin, restMax, eventMin, eventMax, clampedConstraints, clampedRules);
        }
    }
    private void ValidateFixedFloorRules(RunMap map, IReadOnlyList<FixedFloorNodeRule> rules)
    {
        if (rules == null || rules.Count == 0 || map?.Floors == null)
            return;

        foreach (FixedFloorNodeRule rule in rules)
        {
            if (rule.FloorIndex < 0 || rule.FloorIndex >= map.Floors.Count)
                continue;

            List<MapNodeData> floorNodes = map.Floors[rule.FloorIndex];
            if (floorNodes == null || floorNodes.Count == 0)
                continue;

            bool allMatch = floorNodes.All(n => n != null && n.NodeType == rule.NodeType);
            if (!allMatch)
            {
                throw new InvalidOperationException(
                    $"FixedFloorRule validation failed at floor {rule.FloorIndex}: expected {rule.NodeType} for all nodes.");
            }
        }
    }
}
