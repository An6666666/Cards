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
        int remainingSlots = availableSlots.Count;

        int shopSlots = Mathf.Min(GetSlotCount(clamped.ShopMin, clamped.ShopMax, remainingSlots), remainingSlots);
        remainingSlots -= shopSlots;

        int eliteSlots = Mathf.Min(GetSlotCount(clamped.EliteMin, clamped.EliteMax, remainingSlots), remainingSlots);
        remainingSlots -= eliteSlots;

        int restSlots = Mathf.Min(GetSlotCount(clamped.RestMin, clamped.RestMax, remainingSlots), remainingSlots);
        remainingSlots -= restSlots;

        int eventSlots = Mathf.Clamp(
            Mathf.RoundToInt(remainingSlots * UnityEngine.Random.Range(clamped.EventRatioMin, clamped.EventRatioMax)),
            0,
            remainingSlots);
        remainingSlots -= eventSlots;

        AssignShops(map, availableSlots, shopSlots, totalFloors, predecessors, constraints);
        AssignElites(map, availableSlots, eliteSlots, totalFloors, predecessors, constraints);
        AssignRests(map, availableSlots, restSlots, totalFloors, predecessors, constraints);
        AssignEvents(map, availableSlots, eventSlots, totalFloors, predecessors, constraints);

        ApplyNodePayloads(map, encounterPool, elitePool, bossEncounter, defaultShop, eventPool);
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
    private int GetSlotCount(int min, int max, int remaining)
    {
        if (remaining <= 0)
            return 0;

        int clampedMax = Mathf.Max(min, Mathf.Min(max, remaining));
        int clampedMin = Mathf.Max(0, Mathf.Min(min, clampedMax));
        return UnityEngine.Random.Range(clampedMin, clampedMax + 1);
    }

    private void AssignShops(
        RunMap map,
        List<NodeSlot> available,
        int count,
        int totalFloors,
        Dictionary<MapNodeData, List<MapNodeData>> predecessors,
        NodeTypeConstraints constraints)
    {
        var placed = new List<NodeSlot>();
        var targets = new List<float> { 0.25f, 0.65f, 0.85f };

        for (int i = 0; i < count; i++)
        {
            float target = i < targets.Count ? targets[i] : targets.Last();
            NodeSlot? slot = PickBestSlot(
                available,
                s => ScoreShopSlot(s, target, totalFloors, placed),
                s => IsTypeAllowed(s, MapNodeType.Shop, placed, predecessors, totalFloors, constraints));

            if (slot.HasValue)
            {
                SetSlotType(map, available, slot.Value, MapNodeType.Shop);
                placed.Add(slot.Value);
            }
        }
    }

    private void AssignElites(
        RunMap map,
        List<NodeSlot> available,
        int count,
        int totalFloors,
        Dictionary<MapNodeData, List<MapNodeData>> predecessors,
        NodeTypeConstraints constraints)
    {
        var placed = new List<NodeSlot>();
        for (int i = 0; i < count; i++)
        {
            NodeSlot? slot = PickBestSlot(
                available,
                s => ScoreEliteSlot(s, totalFloors),
                s => IsTypeAllowed(s, MapNodeType.EliteBattle, placed, predecessors, totalFloors, constraints));

            if (slot.HasValue)
            {
                SetSlotType(map, available, slot.Value, MapNodeType.EliteBattle);
                placed.Add(slot.Value);
            }
        }
    }

    private void AssignRests(
        RunMap map,
        List<NodeSlot> available,
        int count,
        int totalFloors,
        Dictionary<MapNodeData, List<MapNodeData>> predecessors,
        NodeTypeConstraints constraints)
    {
        var placed = new List<NodeSlot>();
        for (int i = 0; i < count; i++)
        {
            NodeSlot? slot = PickBestSlot(
                available,
                s => ScoreRestSlot(s, totalFloors, placed),
                s => IsTypeAllowed(s, MapNodeType.Rest, placed, predecessors, totalFloors, constraints));

            if (slot.HasValue)
            {
                SetSlotType(map, available, slot.Value, MapNodeType.Rest);
                placed.Add(slot.Value);
            }
        }
    }

    private void AssignEvents(
        RunMap map,
        List<NodeSlot> available,
        int count,
        int totalFloors,
        Dictionary<MapNodeData, List<MapNodeData>> predecessors,
        NodeTypeConstraints constraints)
    {
        var placed = new List<NodeSlot>();
        for (int i = 0; i < count; i++)
        {
            NodeSlot? slot = PickBestSlot(
                available,
                s => ScoreEventSlot(s, totalFloors),
                s => IsTypeAllowed(s, MapNodeType.Event, placed, predecessors, totalFloors, constraints));

            if (slot.HasValue)
            {
                SetSlotType(map, available, slot.Value, MapNodeType.Event);
                placed.Add(slot.Value);
            }
        }
    }

    private void SetSlotType(RunMap map, List<NodeSlot> available, NodeSlot slot, MapNodeType type)
    {
        slot.Node.SetNodeType(type);
        available.RemoveAll(s => s.Node == slot.Node);
    }

    private NodeSlot? PickBestSlot(
        List<NodeSlot> available,
        Func<NodeSlot, float> scoreFunc,
        Func<NodeSlot, bool> isValid)
    {
        var ordered = available
            .Where(isValid)
            .Select(slot => new
            {
                Slot = slot,
                Score = scoreFunc(slot) + UnityEngine.Random.value * 0.1f // 小幅隨機，避免每次都一樣
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        return ordered.Count > 0 ? ordered[0].Slot : (NodeSlot?)null;
    }

    private float ScoreShopSlot(NodeSlot slot, float targetNormalizedFloor, int totalFloors, List<NodeSlot> existingShops)
    {
        float normalizedFloor = slot.GetNormalizedFloor(totalFloors);
        float distance = Mathf.Abs(normalizedFloor - targetNormalizedFloor);
         float traffic = Mathf.Log(1f + slot.Incoming + Mathf.Max(1, slot.Outgoing));
        float spacingBonus = existingShops.Count == 0
            ? 0.25f
            : Mathf.Clamp(existingShops.Min(s => Mathf.Abs(s.FloorIndex - slot.FloorIndex)) * 0.2f, -0.1f, 0.6f);

        return 1.3f - distance * 1.4f + traffic + spacingBonus;
    }

    private float ScoreEliteSlot(NodeSlot slot, int totalFloors)
    {
        float incomingPressure = 1.2f / (1 + slot.Incoming); // incoming 越低越偏側路
        float edgeBonus = slot.IsEdge ? 1.4f : 0.6f * Mathf.Abs(slot.GetCenterOffset());
        float narrowness = slot.NodesOnFloor <= 2 ? 0.9f : slot.NodesOnFloor == 3 ? 0.4f : 0.1f;
        float bottleneck = slot.Incoming <= 1 ? 0.35f : 0f; // 只有單一路線能到
        float depth = Mathf.Lerp(0.2f, 0.85f, slot.GetNormalizedFloor(totalFloors));

        return incomingPressure + edgeBonus + narrowness + bottleneck + depth;
    }

    private bool AreNodesCloseOnPath(MapNodeData a, MapNodeData b, int maxFloorGap, Dictionary<MapNodeData, List<MapNodeData>> predecessors)
    {
        if (a == null || b == null)
            return false;

        MapNodeData start = a.FloorIndex <= b.FloorIndex ? a : b;
        MapNodeData target = a.FloorIndex <= b.FloorIndex ? b : a;

        int floorDelta = target.FloorIndex - start.FloorIndex;
        if (floorDelta > maxFloorGap)
            return false;

        var queue = new Queue<MapNodeData>();
        var visited = new HashSet<MapNodeData>();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            MapNodeData current = queue.Dequeue();
            if (current == target)
                return true;

            foreach (MapNodeData next in current.NextNodes)
            {
                if (next == null || visited.Contains(next))
                    continue;

                int depth = next.FloorIndex - start.FloorIndex;
                if (depth > maxFloorGap)
                    continue;

                visited.Add(next);
                queue.Enqueue(next);
            }
        }

        // 反向檢查：如果圖允許反向可達路徑，避免相鄰跨層菁英（防止孤點誤判）
        if (predecessors != null && predecessors.TryGetValue(target, out var parentList))
        {
            foreach (MapNodeData parent in parentList)
            {
                if (parent == null || visited.Contains(parent))
                    continue;

                if (Mathf.Abs(parent.FloorIndex - target.FloorIndex) <= maxFloorGap && AreNodesCloseOnPath(parent, start, maxFloorGap - 1, predecessors))
                    return true;
            }
        }

        return false;
    }

    private bool IsTypeAllowed(
        NodeSlot slot,
        MapNodeType type,
        List<NodeSlot> placedOfType,
        Dictionary<MapNodeData, List<MapNodeData>> predecessors,
        int totalFloors,
        NodeTypeConstraints constraints)
    {
        (int minOffset, int maxOffsetFromBoss, int minGap, bool forbidConsecutive, int maxConsecutiveEvents) = type switch
        {
            MapNodeType.EliteBattle => (
                constraints.EliteMinFloorOffset,
                constraints.EliteMaxFloorOffsetFromBoss,
                constraints.EliteMinGap,
                constraints.ForbidConsecutiveElite,
                0),
            MapNodeType.Shop => (
                constraints.ShopMinFloorOffset,
                constraints.ShopMaxFloorOffsetFromBoss,
                constraints.ShopMinGap,
                constraints.ForbidConsecutiveShop,
                0),
            MapNodeType.Rest => (
                constraints.RestMinFloorOffset,
                constraints.RestMaxFloorOffsetFromBoss,
                constraints.RestMinGap,
                constraints.ForbidConsecutiveRest,
                0),
            MapNodeType.Event => (
                constraints.EventMinFloorOffset,
                constraints.EventMaxFloorOffsetFromBoss,
                0,
                false,
                constraints.MaxConsecutiveEvents),
            _ => (0, 0, 0, false, 0)
        };

        if (slot.FloorIndex < minOffset)
            return false;

        int distanceToBoss = (totalFloors - 1) - slot.FloorIndex;
        if (distanceToBoss < maxOffsetFromBoss)
            return false;

        foreach (NodeSlot placed in placedOfType)
        {
            int floorGap = Mathf.Abs(placed.FloorIndex - slot.FloorIndex);
            if (minGap > 0 && floorGap < minGap && AreNodesCloseOnPath(placed.Node, slot.Node, minGap, predecessors))
                return false;

            if (forbidConsecutive && floorGap == 1 && AreNodesCloseOnPath(placed.Node, slot.Node, 1, predecessors))
                return false;
        }

        if (type == MapNodeType.Event && maxConsecutiveEvents > 0)
        {
            var memo = new Dictionary<MapNodeData, int>();
            int chain = GetConsecutiveEventDepth(slot.Node, predecessors, memo, true);
            if (chain > maxConsecutiveEvents)
                return false;
        }

        return true;
    }

    private int GetConsecutiveEventDepth(
        MapNodeData node,
        Dictionary<MapNodeData, List<MapNodeData>> predecessors,
        Dictionary<MapNodeData, int> memo,
        bool treatNodeAsEvent = false)
    {
        bool isEvent = treatNodeAsEvent || node.NodeType == MapNodeType.Event;
        if (!isEvent)
            return 0;

        if (!treatNodeAsEvent && memo.TryGetValue(node, out int cached))
            return cached;

        int maxParentChain = 0;
        if (predecessors != null && predecessors.TryGetValue(node, out var parents))
        {
            foreach (MapNodeData parent in parents)
            {
                if (parent == null)
                    continue;

                int parentChain = GetConsecutiveEventDepth(parent, predecessors, memo);
                maxParentChain = Mathf.Max(maxParentChain, parentChain);
            }
        }

        int result = 1 + maxParentChain;
        if (!treatNodeAsEvent)
            memo[node] = result;
        return result;
    }

    private float ScoreRestSlot(NodeSlot slot, int totalFloors, List<NodeSlot> existingRests)
    {
        float normalized = slot.GetNormalizedFloor(totalFloors);
        float midBias = 1.05f - Mathf.Abs(normalized - 0.5f) * 1.1f;

        if (slot.FloorIndex <= 1)
            midBias -= 0.25f;
        if (slot.FloorIndex >= totalFloors - 2)
            midBias -= 0.35f; // Boss 前避免太近

        float spacing = 0.2f;
        foreach (NodeSlot rest in existingRests)
        {
            spacing += Mathf.Clamp01(Mathf.Abs(rest.FloorIndex - slot.FloorIndex) * 0.15f);
        }

        return midBias + spacing;
    }

    private float ScoreEventSlot(NodeSlot slot, int totalFloors)
    {
        float normalized = slot.GetNormalizedFloor(totalFloors);
        float midFocus = 1.2f - Mathf.Abs(normalized - 0.5f) * 1.5f; // 事件集中在中段

        if (slot.FloorIndex <= 1)
            midFocus -= 0.4f; // 前 1-2 層降低事件比例
        if (slot.FloorIndex >= totalFloors - 2)
            midFocus -= 0.4f; // Boss 前降低事件比例

        return midFocus;
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

    private RunEventDefinition GetRandomEventDefinition(List<RunEventDefinition> eventPool)
    {
        if (eventPool == null || eventPool.Count == 0)
            return null;
        int index = UnityEngine.Random.Range(0, eventPool.Count);
        return eventPool[index];
    }

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

        public static SlotAllocationSettings Default => new SlotAllocationSettings(2, 3, 2, 4, 4, 6, 0.2f, 0.25f, NodeTypeConstraints.Default);

        public SlotAllocationSettings(
            int shopMin,
            int shopMax,
            int eliteMin,
            int eliteMax,
            int restMin,
            int restMax,
            float eventRatioMin,
            float eventRatioMax,
            NodeTypeConstraints? typeConstraints = null)
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

            return new SlotAllocationSettings(shopMin, shopMax, eliteMin, eliteMax, restMin, restMax, eventMin, eventMax, clampedConstraints);
        }
    }

    [Serializable]
    public readonly struct NodeTypeConstraints
    {
        public int EliteMinFloorOffset { get; }
        public int EliteMaxFloorOffsetFromBoss { get; }
        public int ShopMinFloorOffset { get; }
        public int ShopMaxFloorOffsetFromBoss { get; }
        public int EventMinFloorOffset { get; }
        public int EventMaxFloorOffsetFromBoss { get; }
        public int RestMinFloorOffset { get; }
        public int RestMaxFloorOffsetFromBoss { get; }
        public int EliteMinGap { get; }
        public int ShopMinGap { get; }
        public int RestMinGap { get; }
        public bool ForbidConsecutiveElite { get; }
        public bool ForbidConsecutiveShop { get; }
        public bool ForbidConsecutiveRest { get; }
        public int MaxConsecutiveEvents { get; }

        public static NodeTypeConstraints Default => new NodeTypeConstraints(
            eliteMinFloorOffset: 5,
            eliteMaxFloorOffsetFromBoss: 2,
            shopMinFloorOffset: 2,
            shopMaxFloorOffsetFromBoss: 3,
            eventMinFloorOffset: 1,
            eventMaxFloorOffsetFromBoss: 2,
            restMinFloorOffset: 2,
            restMaxFloorOffsetFromBoss: 1,
            eliteMinGap: 3,
            shopMinGap: 3,
            restMinGap: 2,
            forbidConsecutiveElite: true,
            forbidConsecutiveShop: true,
            forbidConsecutiveRest: true,
            maxConsecutiveEvents: 2);

        public NodeTypeConstraints(
            int eliteMinFloorOffset,
            int eliteMaxFloorOffsetFromBoss,
            int shopMinFloorOffset,
            int shopMaxFloorOffsetFromBoss,
            int eventMinFloorOffset,
            int eventMaxFloorOffsetFromBoss,
            int restMinFloorOffset,
            int restMaxFloorOffsetFromBoss,
            int eliteMinGap,
            int shopMinGap,
            int restMinGap,
            bool forbidConsecutiveElite,
            bool forbidConsecutiveShop,
            bool forbidConsecutiveRest,
            int maxConsecutiveEvents)
        {
            EliteMinFloorOffset = eliteMinFloorOffset;
            EliteMaxFloorOffsetFromBoss = eliteMaxFloorOffsetFromBoss;
            ShopMinFloorOffset = shopMinFloorOffset;
            ShopMaxFloorOffsetFromBoss = shopMaxFloorOffsetFromBoss;
            EventMinFloorOffset = eventMinFloorOffset;
            EventMaxFloorOffsetFromBoss = eventMaxFloorOffsetFromBoss;
            RestMinFloorOffset = restMinFloorOffset;
            RestMaxFloorOffsetFromBoss = restMaxFloorOffsetFromBoss;
            EliteMinGap = eliteMinGap;
            ShopMinGap = shopMinGap;
            RestMinGap = restMinGap;
            ForbidConsecutiveElite = forbidConsecutiveElite;
            ForbidConsecutiveShop = forbidConsecutiveShop;
            ForbidConsecutiveRest = forbidConsecutiveRest;
            MaxConsecutiveEvents = maxConsecutiveEvents;
        }

        public NodeTypeConstraints GetClamped()
        {
            return new NodeTypeConstraints(
                Mathf.Max(0, EliteMinFloorOffset),
                Mathf.Max(0, EliteMaxFloorOffsetFromBoss),
                Mathf.Max(0, ShopMinFloorOffset),
                Mathf.Max(0, ShopMaxFloorOffsetFromBoss),
                Mathf.Max(0, EventMinFloorOffset),
                Mathf.Max(0, EventMaxFloorOffsetFromBoss),
                Mathf.Max(0, RestMinFloorOffset),
                Mathf.Max(0, RestMaxFloorOffsetFromBoss),
                Mathf.Max(0, EliteMinGap),
                Mathf.Max(0, ShopMinGap),
                Mathf.Max(0, RestMinGap),
                ForbidConsecutiveElite,
                ForbidConsecutiveShop,
                ForbidConsecutiveRest,
                Mathf.Max(0, MaxConsecutiveEvents));
        }
    }

    private readonly struct NodeSlot
    {
        public MapNodeData Node { get; }
        public int FloorIndex { get; }
        public int ColumnIndex { get; }
        public int NodesOnFloor { get; }
        public int Incoming { get; }
        public int Outgoing { get; }
        public int TotalFloors { get; }

        public NodeSlot(MapNodeData node, int floorIndex, int columnIndex, int nodesOnFloor, int incoming, int totalFloors)
        {
            Node = node;
            FloorIndex = floorIndex;
            ColumnIndex = columnIndex;
            NodesOnFloor = nodesOnFloor;
            Incoming = incoming;
            Outgoing = node.NextNodes?.Count ?? 0;
            TotalFloors = totalFloors;
        }

        public bool IsEdge => ColumnIndex == 0 || ColumnIndex == NodesOnFloor - 1;

        public float GetNormalizedFloor(int totalFloors)
        {
            return totalFloors <= 1 ? 0f : (float)FloorIndex / (totalFloors - 1);
        }

        public float GetCenterOffset()
        {
            float center = (NodesOnFloor - 1) / 2f;
            return (ColumnIndex - center) / Mathf.Max(1f, center);
        }
    }
}
