using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class RunMapSlotAllocator
{
    private readonly RunMapSlotScoring scoring = new RunMapSlotScoring();
    public Dictionary<MapNodeType, int> ApplyFixedFloorRules(
        SlotAssignmentContext context,
        IReadOnlyList<FixedFloorNodeRule> rules)
    {
        var placedCounts = new Dictionary<MapNodeType, int>();
        if (rules == null || rules.Count == 0 || context?.AvailableSlots == null)
            return placedCounts;

        var groupedRules = rules.GroupBy(r => r.FloorIndex);
        foreach (IGrouping<int, FixedFloorNodeRule> ruleGroup in groupedRules)
        {
            int floorIndex = ruleGroup.Key;
            MapNodeType nodeType = ruleGroup.First().NodeType;
            bool hasConflict = ruleGroup.Any(r => r.NodeType != nodeType);
            if (hasConflict)
                throw new InvalidOperationException($"FixedFloorRules conflict at floor {floorIndex}: multiple node types specified.");

            List<NodeSlot> floorSlots = context.AvailableSlots
                .Where(s => s.FloorIndex == floorIndex)
                .ToList();

            if (floorSlots.Count == 0)
                continue;

            foreach (NodeSlot slot in floorSlots)
            {
                SetSlotType(context, slot, nodeType);
                if (!placedCounts.ContainsKey(nodeType))
                    placedCounts[nodeType] = 0;
                placedCounts[nodeType]++;
            }
        }

        return placedCounts;
    }
    public int GetSlotCount(int min, int max, int remaining)
    {
        if (remaining <= 0)
            return 0;

        int clampedMax = Mathf.Max(min, Mathf.Min(max, remaining));
        int clampedMin = Mathf.Max(0, Mathf.Min(min, clampedMax));
        return UnityEngine.Random.Range(clampedMin, clampedMax + 1);
    }

    public void AllocateSlots(
        SlotAssignmentContext context,
        int shopSlots,
        int eliteSlots,
        int restSlots,
        int eventSlots)
    {
        AssignShops(context, shopSlots);
        AssignElites(context, eliteSlots);
        AssignRests(context, restSlots);
        AssignEvents(context, eventSlots);
    }

    private void AssignShops(
        SlotAssignmentContext context,
        int count)
    {
        var placed = new List<NodeSlot>();
        var targets = new List<float> { 0.25f, 0.65f, 0.85f };

        for (int i = 0; i < count; i++)
        {
            float target = i < targets.Count ? targets[i] : targets.Last();
            NodeSlot? slot = PickBestSlot(
                context.AvailableSlots,
                s => scoring.ScoreShopSlot(s, target, context.TotalFloors, placed),
                s => RunMapSlotConstraints.IsTypeAllowed(s, MapNodeType.Shop, placed, context.Predecessors, context.TotalFloors, context.Constraints));

            if (slot.HasValue)
            {
                SetSlotType(context, slot.Value, MapNodeType.Shop);
                placed.Add(slot.Value);
            }
        }
    }

    private void AssignElites(
        SlotAssignmentContext context,
        int count)
    {
        var placed = new List<NodeSlot>();
        for (int i = 0; i < count; i++)
        {
            NodeSlot? slot = PickBestSlot(
                context.AvailableSlots,
                s => scoring.ScoreEliteSlot(s, context.TotalFloors),
                s => RunMapSlotConstraints.IsTypeAllowed(s, MapNodeType.EliteBattle, placed, context.Predecessors, context.TotalFloors, context.Constraints));

            if (slot.HasValue)
            {
                SetSlotType(context, slot.Value, MapNodeType.EliteBattle);
                placed.Add(slot.Value);
            }
        }
    }

    private void AssignRests(
        SlotAssignmentContext context,
        int count)
    {
        var placed = new List<NodeSlot>();
        for (int i = 0; i < count; i++)
        {
            NodeSlot? slot = PickBestSlot(
                context.AvailableSlots,
                s => scoring.ScoreRestSlot(s, context.TotalFloors, placed),
                s => RunMapSlotConstraints.IsTypeAllowed(s, MapNodeType.Rest, placed, context.Predecessors, context.TotalFloors, context.Constraints));

            if (slot.HasValue)
            {
                SetSlotType(context, slot.Value, MapNodeType.Rest);
                placed.Add(slot.Value);
            }
        }
    }

    private void AssignEvents(
        SlotAssignmentContext context,
        int count)
    {
        var placed = new List<NodeSlot>();
        for (int i = 0; i < count; i++)
        {
            NodeSlot? slot = PickBestSlot(
                context.AvailableSlots,
                s => scoring.ScoreEventSlot(s, context.TotalFloors),
                s => RunMapSlotConstraints.IsTypeAllowed(s, MapNodeType.Event, placed, context.Predecessors, context.TotalFloors, context.Constraints));

            if (slot.HasValue)
            {
                SetSlotType(context, slot.Value, MapNodeType.Event);
                placed.Add(slot.Value);
            }
        }
    }

    private void SetSlotType(SlotAssignmentContext context, NodeSlot slot, MapNodeType type)
    {
        slot.Node.SetNodeType(type);
        context.AvailableSlots.RemoveAll(s => s.Node == slot.Node);
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
}