using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class RunMapUI : MonoBehaviour
{
    private void BuildMap(IReadOnlyList<IReadOnlyList<MapNodeData>> floors)
    {
        ClearMap();

        if (mapContainer == null || nodeButtonPrefab == null || floors == null || floors.Count == 0)
            return;

        for (int floorIndex = 0; floorIndex < floors.Count; floorIndex++)
        {
            IReadOnlyList<MapNodeData> floorNodes = floors[floorIndex];
            if (floorNodes == null || floorNodes.Count == 0)
                continue;

            List<float> floorYPositions = BuildFloorYPositions(floorIndex, floorNodes.Count);

            for (int nodeIndex = 0; nodeIndex < floorNodes.Count; nodeIndex++)
            {
                MapNodeData node = floorNodes[nodeIndex];
                Transform nodeParent = nodesRoot != null ? nodesRoot : mapContainer;
                Button buttonInstance = Instantiate(nodeButtonPrefab, nodeParent);
                buttonInstance.name = $"Node_{node.NodeId}";

                RectTransform buttonRect = buttonInstance.GetComponent<RectTransform>();
                buttonRect.anchorMin = new Vector2(0.5f, 1f);
                buttonRect.anchorMax = new Vector2(0.5f, 1f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);

                float x = GetBaseNodeX(nodeIndex, floorNodes.Count);
                float jitterX = horizontalPositionJitter > 0f
                    ? Random.Range(-horizontalPositionJitter, horizontalPositionJitter)
                    : 0f;

                buttonRect.anchoredPosition = new Vector2(x + jitterX, floorYPositions[nodeIndex]);

                ConfigureButtonVisuals(buttonInstance, node);
                buttonInstance.onClick.AddListener(() => OnNodeClicked(node));
                RegisterNodeHoverEvents(buttonInstance, node);

                nodeButtons[node] = buttonInstance;
                nodeRects[node] = buttonRect;
                nodeBaseScales[node] = buttonRect.localScale;
            }
        }

        EnforceSameFloorMinimumDistance(floors);
        EnforceDifferentFloorMinimumDistance(floors);

        for (int pass = 0; pass < 6; pass++)
        {
            ConstrainConnectedOffsets(floors);
            EnforceFloorNodeOrder(floors);
            ResolveNodeOverlaps(floors);
            EnforceSameFloorMinimumDistance(floors);
            EnforceDifferentFloorMinimumDistance(floors);
            EnforceFloorNodeOrder(floors);

            if (CountConnectionCrossings(floors) == 0)
                break;
        }

        EnforceSameFloorMinimumDistance(floors);
        EnforceDifferentFloorMinimumDistance(floors);
        EnforceFloorNodeOrder(floors);
        CalculateNodeBounds(out float minY, out float maxY);

        ClearLines();
        CreateConnections();
        ApplyPaddingAndResize(minY, maxY);
        hasBuilt = true;
    }

    private List<float> BuildFloorYPositions(int floorIndex, int nodeCount)
    {
        var positions = new List<float>(nodeCount);
        float centerY = -(floorIndex * floorSpacing);

        if (nodeCount <= 1)
        {
            positions.Add(centerY);
            return positions;
        }

        Vector2 verticalBounds = GetFloorVerticalBounds(floorIndex);
        float targetGap = GetSameFloorInitialGap(verticalBounds, nodeCount);

        if (targetGap > 0f)
        {
            float availableSlack = Mathf.Max(0f, (verticalBounds.y - verticalBounds.x) - targetGap * (nodeCount - 1));
            float[] slackBuckets = new float[nodeCount + 1];
            float slackWeightSum = 0f;

            for (int i = 0; i < slackBuckets.Length; i++)
            {
                slackBuckets[i] = Random.Range(0.15f, 1f);
                slackWeightSum += slackBuckets[i];
            }

            float slackScale = slackWeightSum > 0f ? availableSlack / slackWeightSum : 0f;
            float cursorY = verticalBounds.x + slackBuckets[0] * slackScale;

            for (int i = 0; i < nodeCount; i++)
            {
                positions.Add(Mathf.Clamp(cursorY, verticalBounds.x, verticalBounds.y));

                if (i < nodeCount - 1)
                    cursorY += targetGap + slackBuckets[i + 1] * slackScale;
            }

            ApplyVerticalJitterToOrderedPositions(positions, verticalBounds, targetGap);
            ShuffleList(positions);
            return positions;
        }

        float slotHeight = (verticalBounds.y - verticalBounds.x) / Mathf.Max(1, nodeCount);
        float slotJitter = Mathf.Min(verticalPositionJitter, slotHeight * 0.35f);

        for (int i = 0; i < nodeCount; i++)
        {
            float localY = verticalBounds.x + slotHeight * (i + 0.5f);
            if (slotJitter > 0f)
                localY += Random.Range(-slotJitter, slotJitter);

            positions.Add(Mathf.Clamp(localY, verticalBounds.x, verticalBounds.y));
        }

        ApplyVerticalJitterToOrderedPositions(positions, verticalBounds, targetGap);
        ShuffleList(positions);
        return positions;
    }

    private void ApplyVerticalJitterToOrderedPositions(List<float> positions, Vector2 verticalBounds, float minGap)
    {
        if (positions == null || positions.Count <= 1)
            return;

        positions.Sort();
        float clampedMinGap = Mathf.Max(0f, Mathf.Min(minGap, (verticalBounds.y - verticalBounds.x) / (positions.Count - 1)));
        var desiredPositions = new float[positions.Count];

        for (int i = 0; i < positions.Count; i++)
        {
            float desiredY = positions[i];
            if (verticalPositionJitter > 0f)
                desiredY += Random.Range(-verticalPositionJitter, verticalPositionJitter);

            desiredPositions[i] = desiredY;
        }

        float prefixGap = 0f;
        for (int i = 0; i < positions.Count; i++)
        {
            float remainingGap = clampedMinGap * (positions.Count - 1 - i);
            float minY = verticalBounds.x + prefixGap;
            if (i > 0)
                minY = Mathf.Max(minY, positions[i - 1] + clampedMinGap);

            float maxY = verticalBounds.y - remainingGap;
            positions[i] = Mathf.Clamp(desiredPositions[i], minY, maxY);
            prefixGap += clampedMinGap;
        }
    }

    private void EnforceSameFloorMinimumDistance(IReadOnlyList<IReadOnlyList<MapNodeData>> floors)
    {
        if (floors == null || floors.Count == 0 || sameFloorMinDistance <= 0f)
            return;

        for (int floorIndex = 0; floorIndex < floors.Count; floorIndex++)
        {
            IReadOnlyList<MapNodeData> floorNodes = floors[floorIndex];
            if (floorNodes == null || floorNodes.Count <= 1)
                continue;

            var floorRects = new List<RectTransform>(floorNodes.Count);
            foreach (MapNodeData node in floorNodes)
            {
                if (node != null && nodeRects.TryGetValue(node, out RectTransform rect) && rect != null)
                    floorRects.Add(rect);
            }

            if (floorRects.Count <= 1)
                continue;

            floorRects.Sort((left, right) => left.anchoredPosition.y.CompareTo(right.anchoredPosition.y));
            Vector2 verticalBounds = GetFloorVerticalBounds(floorIndex);

            for (int iteration = 0; iteration < 6; iteration++)
            {
                bool movedAny = false;

                for (int i = 0; i < floorRects.Count; i++)
                    ClampRectYToBounds(floorRects[i], verticalBounds);

                for (int i = 1; i < floorRects.Count; i++)
                {
                    RectTransform previousRect = floorRects[i - 1];
                    RectTransform currentRect = floorRects[i];
                    float requiredGap = GetSameFloorRequiredDistance(previousRect, currentRect);
                    float minCurrentY = previousRect.anchoredPosition.y + requiredGap;

                    if (currentRect.anchoredPosition.y + 0.01f >= minCurrentY)
                        continue;

                    SetRectYWithinBounds(currentRect, minCurrentY, verticalBounds);
                    movedAny = true;
                }

                for (int i = floorRects.Count - 2; i >= 0; i--)
                {
                    RectTransform currentRect = floorRects[i];
                    RectTransform nextRect = floorRects[i + 1];
                    float requiredGap = GetSameFloorRequiredDistance(currentRect, nextRect);
                    float maxCurrentY = nextRect.anchoredPosition.y - requiredGap;

                    if (currentRect.anchoredPosition.y - 0.01f <= maxCurrentY)
                        continue;

                    SetRectYWithinBounds(currentRect, maxCurrentY, verticalBounds);
                    movedAny = true;
                }

                if (!movedAny)
                    break;
            }
        }
    }

    private void EnforceDifferentFloorMinimumDistance(IReadOnlyList<IReadOnlyList<MapNodeData>> floors)
    {
        if (floors == null || floors.Count <= 1 || differentFloorMinDistance <= 0f)
            return;

        for (int iteration = 0; iteration < 6; iteration++)
        {
            bool movedAny = false;

            for (int floorIndex = 0; floorIndex < floors.Count - 1; floorIndex++)
            {
                IReadOnlyList<MapNodeData> upperFloorNodes = floors[floorIndex];
                IReadOnlyList<MapNodeData> lowerFloorNodes = floors[floorIndex + 1];
                if (upperFloorNodes == null || lowerFloorNodes == null)
                    continue;

                Vector2 upperBounds = GetFloorVerticalBounds(floorIndex);
                Vector2 lowerBounds = GetFloorVerticalBounds(floorIndex + 1);

                foreach (MapNodeData upperNode in upperFloorNodes)
                {
                    if (upperNode == null || !nodeRects.TryGetValue(upperNode, out RectTransform upperRect) || upperRect == null)
                        continue;

                    foreach (MapNodeData lowerNode in lowerFloorNodes)
                    {
                        if (lowerNode == null || !nodeRects.TryGetValue(lowerNode, out RectTransform lowerRect) || lowerRect == null)
                            continue;

                        float requiredGap = GetDifferentFloorRequiredDistance(upperRect, lowerRect);
                        float currentGap = upperRect.anchoredPosition.y - lowerRect.anchoredPosition.y;
                        if (currentGap + 0.01f >= requiredGap)
                            continue;

                        float deficit = requiredGap - currentGap;
                        float upperShift = deficit * 0.5f;
                        float lowerShift = deficit - upperShift;

                        SetRectYWithinBounds(upperRect, upperRect.anchoredPosition.y + upperShift, upperBounds);
                        SetRectYWithinBounds(lowerRect, lowerRect.anchoredPosition.y - lowerShift, lowerBounds);
                        movedAny = true;
                    }
                }
            }

            if (!movedAny)
                break;
        }
    }

    private void ResolveNodeOverlaps(IReadOnlyList<IReadOnlyList<MapNodeData>> floors)
    {
        if (floors == null || nodeRects.Count < 2)
            return;

        var floorLookup = new Dictionary<MapNodeData, int>(nodeRects.Count);
        var floorHorizontalBounds = new Dictionary<int, Vector2>(floors.Count);
        var floorVerticalBounds = new Dictionary<int, Vector2>(floors.Count);
        var orderedNodes = new List<MapNodeData>(nodeRects.Count);

        for (int floorIndex = 0; floorIndex < floors.Count; floorIndex++)
        {
            IReadOnlyList<MapNodeData> floorNodes = floors[floorIndex];
            if (floorNodes == null || floorNodes.Count == 0)
                continue;

            floorHorizontalBounds[floorIndex] = GetFloorHorizontalBounds(floorNodes.Count);
            floorVerticalBounds[floorIndex] = GetFloorVerticalBounds(floorIndex);

            foreach (MapNodeData node in floorNodes)
            {
                if (!nodeRects.ContainsKey(node))
                    continue;

                floorLookup[node] = floorIndex;
                orderedNodes.Add(node);
            }
        }

        for (int iteration = 0; iteration < 16; iteration++)
        {
            bool movedAny = false;

            for (int i = 0; i < orderedNodes.Count; i++)
            {
                MapNodeData nodeA = orderedNodes[i];
                RectTransform rectA = nodeRects[nodeA];

                for (int j = i + 1; j < orderedNodes.Count; j++)
                {
                    MapNodeData nodeB = orderedNodes[j];
                    RectTransform rectB = nodeRects[nodeB];

                    Vector2 posA = rectA.anchoredPosition;
                    Vector2 posB = rectB.anchoredPosition;
                    float deltaX = posB.x - posA.x;
                    float deltaY = posB.y - posA.y;

                    float requiredX = (rectA.rect.width + rectB.rect.width) * 0.5f + nodeOverlapPadding;
                    float requiredY = (rectA.rect.height + rectB.rect.height) * 0.5f + nodeOverlapPadding;
                    float overlapX = requiredX - Mathf.Abs(deltaX);
                    float overlapY = requiredY - Mathf.Abs(deltaY);

                    if (overlapX <= 0f || overlapY <= 0f)
                        continue;

                    movedAny = true;

                    int floorA = floorLookup[nodeA];
                    int floorB = floorLookup[nodeB];

                    if (floorA == floorB)
                    {
                        ResolveSameFloorOverlap(
                            rectA,
                            rectB,
                            overlapX,
                            deltaX,
                            floorHorizontalBounds[floorA],
                            floorVerticalBounds[floorA]);
                    }
                    else
                    {
                        ResolveCrossFloorOverlap(
                            rectA,
                            rectB,
                            floorA,
                            floorB,
                            overlapX,
                            overlapY,
                            deltaX,
                            floorHorizontalBounds[floorA],
                            floorHorizontalBounds[floorB],
                            floorVerticalBounds[floorA],
                            floorVerticalBounds[floorB]);
                    }
                }
            }

            if (!movedAny)
                break;
        }
    }

    private void ConstrainConnectedOffsets(IReadOnlyList<IReadOnlyList<MapNodeData>> floors)
    {
        if (floors == null || floors.Count <= 1 || maxConnectionHorizontalOffset <= 0f)
            return;

        Dictionary<MapNodeData, List<MapNodeData>> predecessors = BuildPredecessorMap(floors);

        for (int pass = 0; pass < 3; pass++)
        {
            for (int floorIndex = 1; floorIndex < floors.Count; floorIndex++)
            {
                IReadOnlyList<MapNodeData> floorNodes = floors[floorIndex];
                if (floorNodes == null || floorNodes.Count == 0)
                    continue;

                Vector2 horizontalBounds = GetFloorHorizontalBounds(floorNodes.Count);
                foreach (MapNodeData node in floorNodes)
                {
                    if (!predecessors.TryGetValue(node, out List<MapNodeData> parents) || parents.Count == 0)
                        continue;

                    float anchorX = AverageNodeX(parents);
                    ClampNodeXToAnchor(node, anchorX, horizontalBounds);
                }
            }

            for (int floorIndex = floors.Count - 2; floorIndex >= 0; floorIndex--)
            {
                IReadOnlyList<MapNodeData> floorNodes = floors[floorIndex];
                if (floorNodes == null || floorNodes.Count == 0)
                    continue;

                Vector2 horizontalBounds = GetFloorHorizontalBounds(floorNodes.Count);
                float bossApproachRelaxation = GetBossApproachVisualRelaxation(floorIndex, floors.Count);
                foreach (MapNodeData node in floorNodes)
                {
                    if (node.NextNodes == null || node.NextNodes.Count == 0)
                        continue;

                    float anchorX = AverageNodeX(node.NextNodes);
                    float offsetMultiplier = Mathf.Lerp(1f, 1f + bossApproachSpreadBoost, bossApproachRelaxation);
                    ClampNodeXToAnchor(node, anchorX, horizontalBounds, offsetMultiplier);
                }
            }
        }
    }

    private void EnforceFloorNodeOrder(IReadOnlyList<IReadOnlyList<MapNodeData>> floors)
    {
        if (floors == null || floors.Count == 0)
            return;

        Dictionary<MapNodeData, List<MapNodeData>> predecessors = BuildPredecessorMap(floors);

        for (int floorIndex = 0; floorIndex < floors.Count; floorIndex++)
        {
            IReadOnlyList<MapNodeData> floorNodes = floors[floorIndex];
            if (floorNodes == null || floorNodes.Count <= 1)
                continue;

            var rects = new List<RectTransform>(floorNodes.Count);
            var desiredPositions = new List<float>(floorNodes.Count);
            Vector2 horizontalBounds = GetFloorHorizontalBounds(floorNodes.Count);

            for (int nodeIndex = 0; nodeIndex < floorNodes.Count; nodeIndex++)
            {
                MapNodeData node = floorNodes[nodeIndex];
                if (node == null || !nodeRects.TryGetValue(node, out RectTransform rect) || rect == null)
                    continue;

                rects.Add(rect);
                desiredPositions.Add(GetDesiredOrderedNodeX(
                    node,
                    rect,
                    predecessors,
                    horizontalBounds,
                    floorIndex,
                    floors.Count,
                    nodeIndex,
                    floorNodes.Count));
            }

            if (rects.Count <= 1)
                continue;

            float[] gaps = BuildHorizontalOrderGaps(rects, horizontalBounds);
            float[] solvedPositions = SolveOrderedFloorXPositions(desiredPositions, gaps, horizontalBounds);

            for (int i = 0; i < rects.Count; i++)
            {
                Vector2 position = rects[i].anchoredPosition;
                position.x = solvedPositions[i];
                rects[i].anchoredPosition = position;
            }
        }
    }

    private Dictionary<MapNodeData, List<MapNodeData>> BuildPredecessorMap(IReadOnlyList<IReadOnlyList<MapNodeData>> floors)
    {
        var predecessors = new Dictionary<MapNodeData, List<MapNodeData>>();

        for (int floorIndex = 0; floorIndex < floors.Count - 1; floorIndex++)
        {
            IReadOnlyList<MapNodeData> floorNodes = floors[floorIndex];
            if (floorNodes == null)
                continue;

            foreach (MapNodeData node in floorNodes)
            {
                if (node?.NextNodes == null)
                    continue;

                foreach (MapNodeData nextNode in node.NextNodes)
                {
                    if (nextNode == null)
                        continue;

                    if (!predecessors.TryGetValue(nextNode, out List<MapNodeData> parents))
                    {
                        parents = new List<MapNodeData>();
                        predecessors[nextNode] = parents;
                    }

                    parents.Add(node);
                }
            }
        }

        return predecessors;
    }

    private float AverageNodeX(IReadOnlyList<MapNodeData> nodes)
    {
        float total = 0f;
        int count = 0;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] == null || !nodeRects.TryGetValue(nodes[i], out RectTransform rect))
                continue;

            total += rect.anchoredPosition.x;
            count++;
        }

        return count > 0 ? total / count : 0f;
    }

    private void ClampNodeXToAnchor(MapNodeData node, float anchorX, Vector2 horizontalBounds, float offsetMultiplier = 1f)
    {
        if (node == null || !nodeRects.TryGetValue(node, out RectTransform rect))
            return;

        Vector2 position = rect.anchoredPosition;
        float effectiveOffset = maxConnectionHorizontalOffset * Mathf.Max(1f, offsetMultiplier);
        float minX = anchorX - effectiveOffset;
        float maxX = anchorX + effectiveOffset;
        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.x = Mathf.Clamp(position.x, horizontalBounds.x, horizontalBounds.y);
        rect.anchoredPosition = position;
    }

    private float GetDesiredOrderedNodeX(
        MapNodeData node,
        RectTransform rect,
        Dictionary<MapNodeData, List<MapNodeData>> predecessors,
        Vector2 horizontalBounds,
        int floorIndex,
        int totalFloorCount,
        int nodeIndex,
        int nodeCount)
    {
        float bossApproachRelaxation = GetBossApproachVisualRelaxation(floorIndex, totalFloorCount);
        float currentWeight = Mathf.Lerp(1f, 1f + bossApproachSpreadBoost, bossApproachRelaxation);
        float parentWeight = Mathf.Lerp(1.35f, 1.15f, bossApproachRelaxation);
        float childWeight = Mathf.Lerp(1.35f, 1.35f * bossApproachChildPullMultiplier, bossApproachRelaxation);
        float baseSlotWeight = bossApproachRelaxation * (0.85f + bossApproachSpreadBoost * 0.35f);
        float baseSlotX = GetBaseNodeX(nodeIndex, nodeCount);

        float weightedTotal = rect.anchoredPosition.x * currentWeight + baseSlotX * baseSlotWeight;
        float totalWeight = currentWeight + baseSlotWeight;

        if (node != null && predecessors != null && predecessors.TryGetValue(node, out List<MapNodeData> parents) && parents.Count > 0)
        {
            weightedTotal += AverageNodeX(parents) * parentWeight;
            totalWeight += parentWeight;
        }

        if (node?.NextNodes != null && node.NextNodes.Count > 0)
        {
            weightedTotal += AverageNodeX(node.NextNodes) * childWeight;
            totalWeight += childWeight;
        }

        float desiredX = weightedTotal / totalWeight;
        if (node != null && horizontalPositionJitter > 0f)
        {
            float stableBias = GetStableNodeJitter($"{node.NodeId}_x");
            desiredX += stableBias * horizontalPositionJitter;
        }

        return Mathf.Clamp(desiredX, horizontalBounds.x, horizontalBounds.y);
    }

    private float[] BuildHorizontalOrderGaps(IReadOnlyList<RectTransform> rects, Vector2 horizontalBounds)
    {
        int gapCount = Mathf.Max(0, rects.Count - 1);
        var gaps = new float[gapCount];
        if (gapCount == 0)
            return gaps;

        float totalGap = 0f;
        for (int i = 0; i < gapCount; i++)
        {
            gaps[i] = GetRequiredHorizontalGap(rects[i], rects[i + 1]);
            totalGap += gaps[i];
        }

        float availableWidth = Mathf.Max(0f, horizontalBounds.y - horizontalBounds.x);
        if (totalGap > availableWidth && totalGap > 0.001f)
        {
            float scale = availableWidth / totalGap;
            for (int i = 0; i < gapCount; i++)
                gaps[i] *= scale;
        }

        return gaps;
    }

    private float[] SolveOrderedFloorXPositions(IReadOnlyList<float> desiredPositions, IReadOnlyList<float> gaps, Vector2 horizontalBounds)
    {
        int count = desiredPositions?.Count ?? 0;
        var solved = new float[count];
        if (count == 0)
            return solved;

        float prefixGap = 0f;
        for (int i = 0; i < count; i++)
        {
            float remainingGap = 0f;
            for (int gapIndex = i; gapIndex < gaps.Count; gapIndex++)
                remainingGap += gaps[gapIndex];

            float minX = horizontalBounds.x + prefixGap;
            if (i > 0)
                minX = Mathf.Max(minX, solved[i - 1] + gaps[i - 1]);

            float maxX = horizontalBounds.y - remainingGap;
            solved[i] = Mathf.Clamp(desiredPositions[i], minX, maxX);
            if (i < gaps.Count)
                prefixGap += gaps[i];
        }

        return solved;
    }

    private float GetRequiredHorizontalGap(RectTransform leftRect, RectTransform rightRect)
    {
        return (leftRect.rect.width + rightRect.rect.width) * 0.5f + nodeOverlapPadding;
    }

    private float GetBaseNodeX(int nodeIndex, int nodeCount)
    {
        if (nodeCount <= 1)
            return 0f;

        int effectiveSlotCount = GetEffectiveHorizontalSlotCount(nodeCount);
        float width = (effectiveSlotCount - 1) * nodeSpacing;
        float t = nodeCount <= 1 ? 0.5f : nodeIndex / (float)(nodeCount - 1);
        return Mathf.Lerp(-width * 0.5f, width * 0.5f, t);
    }

    private int GetEffectiveHorizontalSlotCount(int nodeCount)
    {
        return Mathf.Max(1, Mathf.Max(nodeCount, minHorizontalSlotsPerFloor));
    }

    private float GetBossApproachVisualRelaxation(int floorIndex, int totalFloorCount)
    {
        if (relaxedBossApproachFloorCount <= 0 || totalFloorCount <= 1)
            return 0f;

        int bossFloorIndex = totalFloorCount - 1;
        int startRelaxFloorIndex = Mathf.Max(0, bossFloorIndex - relaxedBossApproachFloorCount);
        if (floorIndex < startRelaxFloorIndex || floorIndex >= bossFloorIndex)
            return 0f;

        int relaxedFloorOrder = floorIndex - startRelaxFloorIndex + 1;
        return Mathf.Clamp01(relaxedFloorOrder / (float)relaxedBossApproachFloorCount);
    }

    private int CountConnectionCrossings(IReadOnlyList<IReadOnlyList<MapNodeData>> floors)
    {
        if (floors == null || floors.Count <= 1)
            return 0;

        int crossingCount = 0;
        for (int floorIndex = 0; floorIndex < floors.Count - 1; floorIndex++)
        {
            IReadOnlyList<MapNodeData> floorNodes = floors[floorIndex];
            if (floorNodes == null || floorNodes.Count == 0)
                continue;

            var segments = new List<ConnectionSegment>();
            foreach (MapNodeData sourceNode in floorNodes)
            {
                if (sourceNode?.NextNodes == null || !nodeRects.TryGetValue(sourceNode, out RectTransform sourceRect) || sourceRect == null)
                    continue;

                foreach (MapNodeData targetNode in sourceNode.NextNodes)
                {
                    if (targetNode == null || !nodeRects.TryGetValue(targetNode, out RectTransform targetRect) || targetRect == null)
                        continue;

                    segments.Add(new ConnectionSegment(
                        sourceNode,
                        targetNode,
                        GetConnectionAnchorPosition(sourceNode, sourceRect),
                        GetConnectionAnchorPosition(targetNode, targetRect)));
                }
            }

            for (int i = 0; i < segments.Count; i++)
            {
                for (int j = i + 1; j < segments.Count; j++)
                {
                    ConnectionSegment segmentA = segments[i];
                    ConnectionSegment segmentB = segments[j];
                    if (segmentA.Source == segmentB.Source || segmentA.Target == segmentB.Target)
                        continue;

                    if (DoSegmentsCross(segmentA.Start, segmentA.End, segmentB.Start, segmentB.End))
                        crossingCount++;
                }
            }
        }

        return crossingCount;
    }

    private bool DoSegmentsCross(Vector2 aStart, Vector2 aEnd, Vector2 bStart, Vector2 bEnd)
    {
        const float epsilon = 0.01f;

        float orientation1 = Cross(aEnd - aStart, bStart - aStart);
        float orientation2 = Cross(aEnd - aStart, bEnd - aStart);
        float orientation3 = Cross(bEnd - bStart, aStart - bStart);
        float orientation4 = Cross(bEnd - bStart, aEnd - bStart);

        if (Mathf.Abs(orientation1) <= epsilon ||
            Mathf.Abs(orientation2) <= epsilon ||
            Mathf.Abs(orientation3) <= epsilon ||
            Mathf.Abs(orientation4) <= epsilon)
        {
            return false;
        }

        return (orientation1 > 0f) != (orientation2 > 0f) &&
               (orientation3 > 0f) != (orientation4 > 0f);
    }

    private float Cross(Vector2 left, Vector2 right)
    {
        return left.x * right.y - left.y * right.x;
    }

    private void ResolveSameFloorOverlap(
        RectTransform rectA,
        RectTransform rectB,
        float overlapX,
        float deltaX,
        Vector2 horizontalBounds,
        Vector2 verticalBounds)
    {
        Vector2 posA = rectA.anchoredPosition;
        Vector2 posB = rectB.anchoredPosition;

        float directionX = Mathf.Abs(deltaX) > 0.001f ? Mathf.Sign(deltaX) : (Random.value < 0.5f ? -1f : 1f);
        float pushX = overlapX * 0.5f + 0.5f;

        posA.x = Mathf.Clamp(posA.x - directionX * pushX, horizontalBounds.x, horizontalBounds.y);
        posB.x = Mathf.Clamp(posB.x + directionX * pushX, horizontalBounds.x, horizontalBounds.y);

        float requiredX = (rectA.rect.width + rectB.rect.width) * 0.5f + nodeOverlapPadding;
        if (Mathf.Abs(posB.x - posA.x) < requiredX)
        {
            float directionY = posA.y >= posB.y ? 1f : -1f;
            float pushY = Mathf.Max(2f, Mathf.Min(verticalPositionJitter + 2f, floorSpacing * 0.08f));

            posA.y = Mathf.Clamp(posA.y + directionY * pushY, verticalBounds.x, verticalBounds.y);
            posB.y = Mathf.Clamp(posB.y - directionY * pushY, verticalBounds.x, verticalBounds.y);
        }

        rectA.anchoredPosition = posA;
        rectB.anchoredPosition = posB;
    }

    private void ResolveCrossFloorOverlap(
        RectTransform rectA,
        RectTransform rectB,
        int floorA,
        int floorB,
        float overlapX,
        float overlapY,
        float deltaX,
        Vector2 horizontalBoundsA,
        Vector2 horizontalBoundsB,
        Vector2 verticalBoundsA,
        Vector2 verticalBoundsB)
    {
        Vector2 posA = rectA.anchoredPosition;
        Vector2 posB = rectB.anchoredPosition;

        bool aIsUpperFloor = floorA < floorB;
        float pushY = overlapY * 0.5f + 0.5f;

        posA.y = Mathf.Clamp(posA.y + (aIsUpperFloor ? pushY : -pushY), verticalBoundsA.x, verticalBoundsA.y);
        posB.y = Mathf.Clamp(posB.y + (aIsUpperFloor ? -pushY : pushY), verticalBoundsB.x, verticalBoundsB.y);

        float requiredY = (rectA.rect.height + rectB.rect.height) * 0.5f + nodeOverlapPadding;
        if (Mathf.Abs(posB.y - posA.y) < requiredY)
        {
            float directionX = Mathf.Abs(deltaX) > 0.001f ? Mathf.Sign(deltaX) : (Random.value < 0.5f ? -1f : 1f);
            float pushX = overlapX * 0.5f + 0.5f;

            posA.x = Mathf.Clamp(posA.x - directionX * pushX, horizontalBoundsA.x, horizontalBoundsA.y);
            posB.x = Mathf.Clamp(posB.x + directionX * pushX, horizontalBoundsB.x, horizontalBoundsB.y);
        }

        rectA.anchoredPosition = posA;
        rectB.anchoredPosition = posB;
    }

    private float GetSameFloorInitialGap(Vector2 verticalBounds, int nodeCount)
    {
        if (sameFloorMinDistance <= 0f || nodeCount <= 1)
            return 0f;

        float maxGap = (verticalBounds.y - verticalBounds.x) / (nodeCount - 1);
        return Mathf.Max(0f, Mathf.Min(sameFloorMinDistance, maxGap));
    }

    private float GetSameFloorRequiredDistance(RectTransform rectA, RectTransform rectB)
    {
        float overlapGap = (rectA.rect.height + rectB.rect.height) * 0.5f + nodeOverlapPadding;
        return Mathf.Max(sameFloorMinDistance, overlapGap);
    }

    private float GetDifferentFloorRequiredDistance(RectTransform upperRect, RectTransform lowerRect)
    {
        return (upperRect.rect.height + lowerRect.rect.height) * 0.5f + differentFloorMinDistance;
    }

    private void ClampRectYToBounds(RectTransform rect, Vector2 verticalBounds)
    {
        SetRectYWithinBounds(rect, rect.anchoredPosition.y, verticalBounds);
    }

    private void SetRectYWithinBounds(RectTransform rect, float targetY, Vector2 verticalBounds)
    {
        if (rect == null)
            return;

        Vector2 position = rect.anchoredPosition;
        float halfHeight = rect.rect.height * 0.5f;
        float minY = verticalBounds.x + halfHeight;
        float maxY = verticalBounds.y - halfHeight;
        position.y = Mathf.Clamp(targetY, minY, maxY);
        rect.anchoredPosition = position;
    }

    private Vector2 GetFloorVerticalBounds(int floorIndex)
    {
        float centerY = -(floorIndex * floorSpacing);
        float requestedBandHeight = floorSpacing * Mathf.Clamp(floorBandFill, 0.2f, 0.95f);
        float maxBandHeight = Mathf.Max(0f, floorSpacing - GetNodePrefabHeight() - differentFloorMinDistance);
        float bandHeight = Mathf.Min(requestedBandHeight, maxBandHeight);
        float bandHalf = bandHeight * 0.5f;
        return new Vector2(centerY - bandHalf, centerY + bandHalf);
    }

    private float GetNodePrefabHeight()
    {
        if (nodeButtonPrefab == null)
            return 0f;

        RectTransform prefabRect = nodeButtonPrefab.GetComponent<RectTransform>();
        return prefabRect != null ? prefabRect.rect.height : 0f;
    }

    private Vector2 GetFloorHorizontalBounds(int nodeCount)
    {
        float width = (GetEffectiveHorizontalSlotCount(nodeCount) - 1) * nodeSpacing;
        float halfWidth = width * 0.5f + Mathf.Max(horizontalPositionJitter, nodeOverlapPadding + 8f);
        return new Vector2(-halfWidth, halfWidth);
    }

    private void CalculateNodeBounds(out float minY, out float maxY)
    {
        minY = float.PositiveInfinity;
        maxY = float.NegativeInfinity;

        foreach (RectTransform rect in nodeRects.Values)
        {
            if (rect == null)
                continue;

            Vector2 pos = rect.anchoredPosition;
            float halfHeight = rect.rect.height * 0.5f;
            minY = Mathf.Min(minY, pos.y - halfHeight);
            maxY = Mathf.Max(maxY, pos.y + halfHeight);
        }

        if (float.IsInfinity(minY) || float.IsInfinity(maxY))
        {
            minY = 0f;
            maxY = 0f;
        }
    }

    private void ShuffleList<T>(List<T> items)
    {
        if (items == null)
            return;

        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }

    private void RegisterNodeHoverEvents(Button button, MapNodeData node)
    {
        if (button == null || node == null)
            return;

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();
        AddNodeHoverCallback(trigger, EventTriggerType.PointerEnter, _ => OnNodePointerEnter(node));
        AddNodeHoverCallback(trigger, EventTriggerType.PointerExit, _ => OnNodePointerExit(node));
    }

    private void AddNodeHoverCallback(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }
}
