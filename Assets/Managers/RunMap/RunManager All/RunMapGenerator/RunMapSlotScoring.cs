using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class RunMapSlotScoring
{
    public float ScoreShopSlot(NodeSlot slot, float targetNormalizedFloor, int totalFloors, List<NodeSlot> existingShops)
    {
        float normalizedFloor = slot.GetNormalizedFloor(totalFloors);
        float distance = Mathf.Abs(normalizedFloor - targetNormalizedFloor);
        float traffic = Mathf.Log(1f + slot.Incoming + Mathf.Max(1, slot.Outgoing));
        float spacingBonus = existingShops.Count == 0
            ? 0.25f
            : Mathf.Clamp(existingShops.Min(s => Mathf.Abs(s.FloorIndex - slot.FloorIndex)) * 0.2f, -0.1f, 0.6f);

        return 1.3f - distance * 1.4f + traffic + spacingBonus;
    }

    public float ScoreEliteSlot(NodeSlot slot, int totalFloors)
    {
        float incomingPressure = 1.2f / (1 + slot.Incoming);
        float edgeBonus = slot.IsEdge ? 1.4f : 0.6f * Mathf.Abs(slot.GetCenterOffset());
        float narrowness = slot.NodesOnFloor <= 2 ? 0.9f : slot.NodesOnFloor == 3 ? 0.4f : 0.1f;
        float bottleneck = slot.Incoming <= 1 ? 0.35f : 0f;
        float depth = Mathf.Lerp(0.2f, 0.85f, slot.GetNormalizedFloor(totalFloors));

        return incomingPressure + edgeBonus + narrowness + bottleneck + depth;
    }

    public float ScoreRestSlot(NodeSlot slot, int totalFloors, List<NodeSlot> existingRests)
    {
        float normalized = slot.GetNormalizedFloor(totalFloors);
        float midBias = 1.05f - Mathf.Abs(normalized - 0.5f) * 1.1f;

        if (slot.FloorIndex <= 1)
            midBias -= 0.25f;
        if (slot.FloorIndex >= totalFloors - 2)
            midBias -= 0.35f;

        float spacing = 0.2f;
        foreach (NodeSlot rest in existingRests)
        {
            spacing += Mathf.Clamp01(Mathf.Abs(rest.FloorIndex - slot.FloorIndex) * 0.15f);
        }

        return midBias + spacing;
    }

    public float ScoreEventSlot(NodeSlot slot, int totalFloors, float targetNormalizedFloor, List<NodeSlot> existingEvents)
    {
        float normalized = slot.GetNormalizedFloor(totalFloors);
        float targetDistance = Mathf.Abs(normalized - targetNormalizedFloor);
        float softMidBias = 0.5f - Mathf.Abs(normalized - 0.5f) * 0.35f;
        float routeTraffic = Mathf.Clamp((slot.Incoming + slot.Outgoing - 2) * 0.1f, 0f, 0.35f);

        float spacingBonus = 0.2f;
        if (existingEvents != null && existingEvents.Count > 0)
        {
            int nearestFloorDistance = existingEvents.Min(existing => Mathf.Abs(existing.FloorIndex - slot.FloorIndex));
            spacingBonus = Mathf.Clamp((nearestFloorDistance - 1f) * 0.22f, -0.35f, 0.85f);
        }

        return 1.2f - targetDistance * 1.85f + softMidBias + spacingBonus + routeTraffic;
    }
}
