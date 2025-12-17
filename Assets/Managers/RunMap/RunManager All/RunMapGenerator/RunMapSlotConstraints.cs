using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

internal sealed class SlotAssignmentContext
{
    public SlotAssignmentContext(
        RunMap map,
        NodeTypeConstraints constraints,
        List<NodeSlot> availableSlots,
        Dictionary<MapNodeData, List<MapNodeData>> predecessors)
    {
        Map = map;
        Constraints = constraints;
        AvailableSlots = availableSlots;
        Predecessors = predecessors;
        TotalFloors = map?.Floors?.Count ?? 0;
    }

    public RunMap Map { get; }
    public NodeTypeConstraints Constraints { get; }
    public List<NodeSlot> AvailableSlots { get; }
    public Dictionary<MapNodeData, List<MapNodeData>> Predecessors { get; }
    public int TotalFloors { get; }
}

public readonly struct NodeSlot
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

internal static class RunMapSlotConstraints
{
    public static bool IsTypeAllowed(
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

    public static bool AreNodesCloseOnPath(MapNodeData a, MapNodeData b, int maxFloorGap, Dictionary<MapNodeData, List<MapNodeData>> predecessors)
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

    private static int GetConsecutiveEventDepth(
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
}