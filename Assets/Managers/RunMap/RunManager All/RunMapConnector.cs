using System.Collections.Generic;
using System.Linq; // for Contains on IReadOnlyList
using UnityEngine;

public class RunMapConnector
{
    private readonly int maxOutgoingPerNode;
    private readonly int neighborWindow; // 限制連線僅在左右相鄰一格內
    private readonly int minConnectedSourcesPerRow;

    private readonly int backtrackAllowance;

    private readonly float connectionDensity;
    private readonly int minIncomingPerTarget;
    private readonly int minDistinctSourcesToBoss;
    private readonly float longLinkChance;
    private readonly int minDistinctTargetsPerFloor;


    public RunMapConnector(
        int neighborWindow = 2,
        int maxOutgoingPerNode = 4,
        int minConnectedSourcesPerRow = 2,
        int backtrackAllowance = 1,
        float connectionDensity = 1.5f,
        int minIncomingPerTarget = 1,
        int minDistinctSourcesToBoss = 3,
        float longLinkChance = 0.2f,
        int minDistinctTargetsPerFloor = 2)
    {
        this.neighborWindow = Mathf.Max(0, neighborWindow);
        this.maxOutgoingPerNode = Mathf.Max(1, maxOutgoingPerNode);
        this.minConnectedSourcesPerRow = Mathf.Max(1, minConnectedSourcesPerRow);
        this.backtrackAllowance = Mathf.Max(0, backtrackAllowance);
        this.connectionDensity = Mathf.Max(1f, connectionDensity);
        this.minIncomingPerTarget = Mathf.Max(1, minIncomingPerTarget);
        this.minDistinctSourcesToBoss = Mathf.Max(1, minDistinctSourcesToBoss);
        this.longLinkChance = Mathf.Clamp01(longLinkChance);
        this.minDistinctTargetsPerFloor = Mathf.Max(1, minDistinctTargetsPerFloor);
    }
    public void BuildConnections(RunMap map)
    {
        if (map == null || map.Floors == null)
            return;

        for (int floor = 0; floor < map.Floors.Count - 1; floor++)
        {
            List<MapNodeData> currentFloor = map.Floors[floor];
            List<MapNodeData> nextFloor = map.Floors[floor + 1];

            if (currentFloor.Count == 0 || nextFloor.Count == 0)
                continue;

            int currentCount = currentFloor.Count;
            int nextCount = nextFloor.Count;

            var outgoingConnections = new Dictionary<MapNodeData, int>();
            var incomingConnections = new Dictionary<MapNodeData, int>();

            var primaryTargets = new int[currentCount];
            var incomingCounts = new int[nextCount];
            var minTargets = new int[currentCount];
            var maxTargets = new int[currentCount];
            var prefixMaxTargets = new int[currentCount];
            var suffixMinTargets = new int[currentCount];


            for (int i = 0; i < currentCount; i++)
            {
                outgoingConnections[currentFloor[i]] = 0;
                primaryTargets[i] = -1;
                minTargets[i] = int.MaxValue;
                maxTargets[i] = -1;
            }

            // 第一輪：每個來源至少連到一個目標，保持目標索引非遞減以避免交叉，且僅連到相鄰索引範圍
            int previousTarget = -1;
            for (int sourceIndex = 0; sourceIndex < currentCount; sourceIndex++)
            {
                GetAnchorRange(sourceIndex, currentCount, nextCount, out int anchor, out int anchorMin, out int anchorMax);
                int lowerBound = Mathf.Max(previousTarget - backtrackAllowance, anchorMin);
                int upperBound = anchorMax;
                if (lowerBound > upperBound)
                {
                    lowerBound = Mathf.Min(lowerBound, nextCount - 1);
                    upperBound = Mathf.Max(lowerBound, upperBound);
                }

                int targetIndex = Mathf.Clamp(anchor, lowerBound, upperBound);
                if (ConnectAndTrack(
                        currentFloor,
                        nextFloor,
                        sourceIndex,
                        targetIndex,
                        outgoingConnections,
                        incomingConnections,
                        incomingCounts,
                        minTargets,
                        maxTargets,
                        primaryTargets))
                {
                    previousTarget = Mathf.Max(previousTarget, targetIndex);
                }
            }

            // 建立界線陣列供後續判斷交叉
            RecomputeBounds(currentCount, nextCount, minTargets, maxTargets, prefixMaxTargets, suffixMinTargets);

            // 第二輪：確保每個目標至少有一條進線
            for (int targetIndex = 0; targetIndex < nextCount; targetIndex++)
            {
                if (incomingCounts[targetIndex] >= minIncomingPerTarget)
                    continue;

                int sourceIndex = SelectSourceForUnconnectedTarget(
                    floor,
                    targetIndex,
                    primaryTargets,
                    outgoingConnections,
                    minTargets,
                    maxTargets,
                    prefixMaxTargets,
                    suffixMinTargets,
                    currentFloor,
                    nextCount);

                if (sourceIndex >= 0 && ConnectAndTrack(
                        currentFloor,
                        nextFloor,
                        sourceIndex,
                        targetIndex,
                        outgoingConnections,
                        incomingConnections,
                        incomingCounts,
                        minTargets,
                        maxTargets,
                        primaryTargets))
                {
                    RecomputeBounds(currentCount, nextCount, minTargets, maxTargets, prefixMaxTargets, suffixMinTargets);
                }
            }

            // 補強：至少有 MinConnectedSourcesPerRow 個來源節點與下一列連結
            int connectedSources = outgoingConnections.Count(pair => pair.Value > 0);
            if (connectedSources < minConnectedSourcesPerRow)
            {
                for (int sourceIndex = 0; sourceIndex < currentCount && connectedSources < minConnectedSourcesPerRow; sourceIndex++)
                {
                    if (outgoingConnections[currentFloor[sourceIndex]] > 0)
                        continue;

                    GetAnchorRange(sourceIndex, currentCount, nextCount, out int anchorTarget, out int anchorLower, out int anchorUpper);

                    int lowerBound = sourceIndex > 0 ? prefixMaxTargets[sourceIndex - 1] + 1 : 0;
                    int upperBound = sourceIndex < currentCount - 1
                        ? Mathf.Min(nextCount - 1, suffixMinTargets[sourceIndex + 1] - 1)
                        : nextCount - 1;

                    lowerBound = Mathf.Max(lowerBound, anchorLower);
                    upperBound = Mathf.Min(upperBound, anchorUpper);

                    if (lowerBound > upperBound)
                        continue;

                    int candidate = Mathf.Clamp(anchorTarget, lowerBound, upperBound);
                    if (ConnectAndTrack(
                            currentFloor,
                            nextFloor,
                            sourceIndex,
                            candidate,
                            outgoingConnections,
                            incomingConnections,
                            incomingCounts,
                            minTargets,
                            maxTargets,
                            primaryTargets))
                    {
                        connectedSources++;
                        RecomputeBounds(currentCount, nextCount, minTargets, maxTargets, prefixMaxTargets, suffixMinTargets);
                    }
                }
            }

            // Boss 前一層特別處理：確保有足夠來源連到 Boss
            bool isLinkToBoss = (floor + 1 == map.Floors.Count - 1) && nextCount == 1;
            if (isLinkToBoss)
            {
                EnsureBossConnections(
                    currentFloor,
                    nextFloor,
                    outgoingConnections,
                    incomingConnections,
                    incomingCounts,
                    minTargets,
                    maxTargets,
                    prefixMaxTargets,
                    suffixMinTargets,
                    primaryTargets,
                    floor);
            }

            // 第三輪：在不破壞順序的範圍內增加適量分叉
            for (int sourceIndex = 0; sourceIndex < currentCount; sourceIndex++)
            {
                int desiredConnections = Mathf.CeilToInt(((float)nextCount / currentCount) * connectionDensity) + 1;
                desiredConnections = Mathf.Clamp(desiredConnections, 2, maxOutgoingPerNode);
                if (outgoingConnections[currentFloor[sourceIndex]] >= desiredConnections)
                    continue;

                GetAnchorRange(sourceIndex, currentCount, nextCount, out int anchorTarget, out int anchorLower, out int anchorUpper);
                if (primaryTargets[sourceIndex] >= 0)
                {
                    anchorTarget = Mathf.Clamp(primaryTargets[sourceIndex], anchorLower, anchorUpper);
                }

                int maxAttempts = 6;
                for (int attempt = 0; attempt < maxAttempts && outgoingConnections[currentFloor[sourceIndex]] < desiredConnections; attempt++)
                {
                    int lowerBound = sourceIndex > 0 ? prefixMaxTargets[sourceIndex - 1] + 1 : 0;
                    int upperBound = sourceIndex < currentCount - 1 ? suffixMinTargets[sourceIndex + 1] - 1 : nextCount - 1;
                    int adjustedLower = anchorLower;
                    int adjustedUpper = anchorUpper;

                    if (UnityEngine.Random.value < longLinkChance)
                    {
                        int expand = UnityEngine.Random.Range(1, 3);
                        adjustedLower = Mathf.Max(0, anchorLower - expand);
                        adjustedUpper = Mathf.Min(nextCount - 1, anchorUpper + expand);
                    }

                    lowerBound = Mathf.Max(lowerBound, adjustedLower);
                    upperBound = Mathf.Min(upperBound, adjustedUpper);
                    if (lowerBound > upperBound)
                        break;

                    var candidates = new List<int>();
                    var weights = new List<float>();
                    for (int t = lowerBound; t <= upperBound; t++)
                    {
                        candidates.Add(t);
                        float weight = 1f / (1 + incomingCounts[t]);
                        weights.Add(weight);
                    }

                    if (candidates.Count == 0)
                        break;

                    for (int pickAttempt = 0; pickAttempt < 6 && outgoingConnections[currentFloor[sourceIndex]] < desiredConnections; pickAttempt++)
                    {
                        int candidate = SampleByWeight(candidates, weights);
                        if (ConnectAndTrack(
                                currentFloor,
                                nextFloor,
                                sourceIndex,
                                candidate,
                                outgoingConnections,
                                incomingConnections,
                                incomingCounts,
                                minTargets,
                                maxTargets,
                                primaryTargets))
                        {
                            RecomputeBounds(currentCount, nextCount, minTargets, maxTargets, prefixMaxTargets, suffixMinTargets);
                            break;
                        }
                    }
                }
            }

            // 每層至少 N 個不同 target 被連到，避免路徑過度收斂
            EnsureMinimumDistinctTargets(
                currentFloor,
                nextFloor,
                outgoingConnections,
                incomingConnections,
                incomingCounts,
                minTargets,
                maxTargets,
                prefixMaxTargets,
                suffixMinTargets,
                primaryTargets);

            // 最終保底：如果仍然不足兩個來源節點有連線，
            // 以不交叉且鄰近的方式強制連線，避免出現整行無連線的情況。
            EnsureMinimumOutgoing(
                currentFloor,
                nextFloor,
                outgoingConnections,
                incomingConnections,
                incomingCounts,
                minTargets,
                maxTargets,
                prefixMaxTargets,
                suffixMinTargets,
                primaryTargets);
        }
    }

    private void GetAnchorRange(int sourceIndex, int currentCount, int nextCount, out int anchor, out int minTarget, out int maxTarget)
    {
        if (currentCount <= 1)
        {
            anchor = (nextCount - 1) / 2;
        }
        else
        {
            float t = (float)sourceIndex / (currentCount - 1);
            anchor = Mathf.RoundToInt(t * (nextCount - 1));
        }

        minTarget = Mathf.Clamp(anchor - neighborWindow, 0, nextCount - 1);
        maxTarget = Mathf.Clamp(anchor + neighborWindow, 0, nextCount - 1);
        if (minTarget > maxTarget)
        {
            minTarget = maxTarget;
        }
    }

    private int SelectSourceForUnconnectedTarget(
        int floor,
        int targetIndex,
        int[] primaryTargets,
        Dictionary<MapNodeData, int> outgoingConnections,
        int[] minTargets,
        int[] maxTargets,
        int[] prefixMaxTargets,
        int[] suffixMinTargets,
        List<MapNodeData> currentFloor,
        int nextCount)
    {
        int bestSource = -1;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < primaryTargets.Length; i++)
        {
            GetAnchorRange(i, currentFloor.Count, nextCount, out int anchor, out int anchorMin, out int anchorMax);

            int lowerBound = i > 0 ? prefixMaxTargets[i - 1] + 1 : 0;
            int upperBound = i < currentFloor.Count - 1
                ? Mathf.Min(nextCount - 1, suffixMinTargets[i + 1] - 1)
                : nextCount - 1;

            lowerBound = Mathf.Max(lowerBound, anchorMin);
            upperBound = Mathf.Min(upperBound, anchorMax);

            if (lowerBound > upperBound || targetIndex < lowerBound || targetIndex > upperBound)
                continue;

            int outgoing = outgoingConnections.TryGetValue(currentFloor[i], out int value) ? value : 0;
            if (outgoing >= maxOutgoingPerNode)
                continue;

            anchor = primaryTargets[i] >= 0
                ? Mathf.Clamp(primaryTargets[i], anchorMin, anchorMax)
                : Mathf.Clamp(targetIndex, lowerBound, upperBound);
            int distance = Mathf.Abs(targetIndex - anchor);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestSource = i;
            }
        }

        if (bestSource >= 0)
            return bestSource;

        // Relaxed pass: 放寬 anchor 範圍但仍維持不交叉與輸出上限
        int relaxedBestSource = -1;
        bestDistance = int.MaxValue;

        for (int i = 0; i < primaryTargets.Length; i++)
        {
            GetAnchorRange(i, currentFloor.Count, nextCount, out int anchor, out _, out _);

            int lowerBound = i > 0 ? prefixMaxTargets[i - 1] + 1 : 0;
            int upperBound = i < currentFloor.Count - 1
                ? Mathf.Min(nextCount - 1, suffixMinTargets[i + 1] - 1)
                : nextCount - 1;

            if (lowerBound > upperBound || targetIndex < lowerBound || targetIndex > upperBound)
                continue;

            int outgoing = outgoingConnections.TryGetValue(currentFloor[i], out int value) ? value : 0;
            if (outgoing >= maxOutgoingPerNode)
                continue;

            int clampedAnchor = primaryTargets[i] >= 0
                ? Mathf.Clamp(primaryTargets[i], 0, nextCount - 1)
                : Mathf.Clamp(anchor, 0, nextCount - 1);

            int distance = Mathf.Abs(targetIndex - clampedAnchor);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                relaxedBestSource = i;
            }
        }

        if (relaxedBestSource == -1)
        {
            Debug.LogWarning($"Unconnected target {targetIndex} on floor {floor}");
        }

        return relaxedBestSource;
    }

    private void EnsureMinimumDistinctTargets(
        List<MapNodeData> currentFloor,
        List<MapNodeData> nextFloor,
        Dictionary<MapNodeData, int> outgoingConnections,
        Dictionary<MapNodeData, int> incomingConnections,
        int[] incomingCounts,
        int[] minTargets,
        int[] maxTargets,
        int[] prefixMaxTargets,
        int[] suffixMinTargets,
        int[] primaryTargets)
    {
        int nextCount = nextFloor.Count;
        int currentCount = currentFloor.Count;
        int distinctTargets = incomingCounts.Count(count => count > 0);

        if (distinctTargets >= minDistinctTargetsPerFloor)
            return;

        var orderedSources = outgoingConnections
            .OrderBy(pair => pair.Value)
            .ThenBy(pair => currentFloor.IndexOf(pair.Key))
            .Select(pair => currentFloor.IndexOf(pair.Key))
            .ToList();

        foreach (int sourceIndex in orderedSources)
        {
            if (distinctTargets >= minDistinctTargetsPerFloor)
                break;

            if (outgoingConnections[currentFloor[sourceIndex]] >= maxOutgoingPerNode)
                continue;

            GetAnchorRange(sourceIndex, currentCount, nextCount, out int anchor, out int anchorMin, out int anchorMax);

            int lowerBound = sourceIndex > 0 ? prefixMaxTargets[sourceIndex - 1] + 1 : 0;
            int upperBound = sourceIndex < currentCount - 1 ? suffixMinTargets[sourceIndex + 1] - 1 : nextCount - 1;
            lowerBound = Mathf.Max(lowerBound, anchorMin);
            upperBound = Mathf.Min(upperBound, anchorMax);

            if (lowerBound > upperBound)
                continue;

            int minIncoming = incomingCounts.Skip(lowerBound).Take(upperBound - lowerBound + 1).Min();
            var candidates = new List<int>();
            for (int t = lowerBound; t <= upperBound; t++)
            {
                if (incomingCounts[t] == minIncoming)
                {
                    candidates.Add(t);
                }
            }

            if (candidates.Count == 0)
                continue;

            int bestTarget = candidates.OrderBy(t => Mathf.Abs(t - anchor)).First();
            if (ConnectAndTrack(
                    currentFloor,
                    nextFloor,
                    sourceIndex,
                    bestTarget,
                    outgoingConnections,
                    incomingConnections,
                    incomingCounts,
                    minTargets,
                    maxTargets,
                    primaryTargets))
            {
                distinctTargets = incomingCounts.Count(c => c >= minIncomingPerTarget);
                RecomputeBounds(currentCount, nextCount, minTargets, maxTargets, prefixMaxTargets, suffixMinTargets);
            }
        }
    }

    private bool ConnectNodes(
        List<MapNodeData> currentFloor,
        List<MapNodeData> nextFloor,
        int sourceIndex,
        int targetIndex,
        Dictionary<MapNodeData, int> outgoingConnections,
        Dictionary<MapNodeData, int> incomingConnections)
    {
        return TryConnectNodes(currentFloor[sourceIndex], nextFloor[targetIndex], outgoingConnections, incomingConnections);
    }

    private bool ConnectAndTrack(
        List<MapNodeData> currentFloor,
        List<MapNodeData> nextFloor,
        int sourceIndex,
        int targetIndex,
        Dictionary<MapNodeData, int> outgoingConnections,
        Dictionary<MapNodeData, int> incomingConnections,
        int[] incomingCounts,
        int[] minTargets,
        int[] maxTargets,
        int[] primaryTargets)
    {
        if (ConnectNodes(
                currentFloor,
                nextFloor,
                sourceIndex,
                targetIndex,
                outgoingConnections,
                incomingConnections))
        {
            incomingCounts[targetIndex]++;
            minTargets[sourceIndex] = Mathf.Min(minTargets[sourceIndex], targetIndex);
            maxTargets[sourceIndex] = Mathf.Max(maxTargets[sourceIndex], targetIndex);
            if (primaryTargets[sourceIndex] == -1)
            {
                primaryTargets[sourceIndex] = targetIndex;
            }
            return true;
        }

        return false;
    }

    private bool TryConnectNodes(
        MapNodeData source,
        MapNodeData target,
        Dictionary<MapNodeData, int> outgoingConnections,
        Dictionary<MapNodeData, int> incomingConnections)
    {
        if (source == null || target == null)
            return false;

        int currentCount = outgoingConnections.TryGetValue(source, out int count) ? count : source.NextNodes.Count;
        if (currentCount >= maxOutgoingPerNode)
            return false;

        if (source.NextNodes.Contains(target))
            return false;

        source.AddNextNode(target);
        outgoingConnections[source] = source.NextNodes.Count;

        if (incomingConnections != null)
        {
            incomingConnections[target] = incomingConnections.TryGetValue(target, out int incoming)
                ? incoming + 1
                : 1;
        }

        return true;
    }

     private void EnsureMinimumOutgoing(
        List<MapNodeData> currentFloor,
        List<MapNodeData> nextFloor,
        Dictionary<MapNodeData, int> outgoingConnections,
        Dictionary<MapNodeData, int> incomingConnections,
        int[] incomingCounts,
        int[] minTargets,
        int[] maxTargets,
        int[] prefixMaxTargets,
        int[] suffixMinTargets,
        int[] primaryTargets)
    {
        int currentCount = currentFloor.Count;
        int nextCount = nextFloor.Count;

        int connectedSources = outgoingConnections.Count(pair => pair.Value > 0);
        int required = Mathf.Min(minConnectedSourcesPerRow, currentCount) - connectedSources;
        if (required <= 0)
            return;

        int previousTarget = -1;
        for (int sourceIndex = 0; sourceIndex < currentCount && required > 0; sourceIndex++)
        {
            if (outgoingConnections[currentFloor[sourceIndex]] > 0)
                continue;

            GetAnchorRange(sourceIndex, currentCount, nextCount, out int anchor, out int anchorMin, out int anchorMax);

            int lowerBound = sourceIndex > 0 ? prefixMaxTargets[sourceIndex - 1] + 1 : 0;
            int upperBound = sourceIndex < currentCount - 1 ? suffixMinTargets[sourceIndex + 1] - 1 : nextCount - 1;
            lowerBound = Mathf.Max(lowerBound, anchorMin);
            upperBound = Mathf.Min(upperBound, anchorMax);

            // 確保非遞減的目標索引，避免交叉
            lowerBound = Mathf.Max(lowerBound, previousTarget);

            // 如果界線太緊，放寬到鄰近窗口內的最接近值
            if (lowerBound > upperBound)
            {
                lowerBound = Mathf.Clamp(anchorMin, 0, nextCount - 1);
                upperBound = Mathf.Clamp(anchorMax, 0, nextCount - 1);
                lowerBound = Mathf.Min(lowerBound, upperBound);
            }

            int targetIndex = Mathf.Clamp(anchor, lowerBound, upperBound);
            if (ConnectAndTrack(
                    currentFloor,
                    nextFloor,
                    sourceIndex,
                    targetIndex,
                    outgoingConnections,
                    incomingConnections,
                    incomingCounts,
                    minTargets,
                    maxTargets,
                    primaryTargets))
            {
                required--;
                previousTarget = Mathf.Max(previousTarget, targetIndex);
                RecomputeBounds(currentCount, nextCount, minTargets, maxTargets, prefixMaxTargets, suffixMinTargets);
            }
        }

        // 若仍不足，允許在既有連線的來源上補一條鄰近連線作為最後手段
        for (int sourceIndex = 0; sourceIndex < currentCount && required > 0; sourceIndex++)
        {
            if (outgoingConnections[currentFloor[sourceIndex]] >= maxOutgoingPerNode)
                continue;

            GetAnchorRange(sourceIndex, currentCount, nextCount, out int anchor, out int anchorMin, out int anchorMax);

            int lowerBound = sourceIndex > 0 ? prefixMaxTargets[sourceIndex - 1] + 1 : 0;

            int upperBound = sourceIndex < currentCount - 1 ? suffixMinTargets[sourceIndex + 1] - 1 : nextCount - 1;
            lowerBound = Mathf.Max(lowerBound, anchorMin);
            upperBound = Mathf.Min(upperBound, anchorMax);
            if (lowerBound > upperBound)
                continue;

            int targetIndex = Mathf.Clamp(anchor, lowerBound, upperBound);
            if (ConnectAndTrack(
                    currentFloor,
                    nextFloor,
                    sourceIndex,
                    targetIndex,
                    outgoingConnections,
                    incomingConnections,
                    incomingCounts,
                    minTargets,
                    maxTargets,
                    primaryTargets))
            {
                required--;
                RecomputeBounds(currentCount, nextCount, minTargets, maxTargets, prefixMaxTargets, suffixMinTargets);
            }
        }
    }

    private void RecomputeBounds(
        int currentCount,
        int nextCount,
        int[] minTargets,
        int[] maxTargets,
        int[] prefixMaxTargets,
        int[] suffixMinTargets)
    {
        int runningMax = -1;
        for (int i = 0; i < currentCount; i++)
        {
            if (maxTargets[i] != -1)
            {
                runningMax = Mathf.Max(runningMax, maxTargets[i]);
            }
            prefixMaxTargets[i] = runningMax;
        }

        int runningMin = nextCount;
        for (int i = currentCount - 1; i >= 0; i--)
        {
            if (minTargets[i] != int.MaxValue)
            {
                runningMin = Mathf.Min(runningMin, minTargets[i]);
            }
            suffixMinTargets[i] = runningMin;
        }
    }

    private int SampleByWeight(List<int> candidates, List<float> weights)
    {
        if (candidates == null || weights == null || candidates.Count == 0 || weights.Count != candidates.Count)
            return -1;

        float total = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            total += Mathf.Max(0f, weights[i]);
        }

        if (total <= 0f)
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];

        float roll = UnityEngine.Random.value * total;
        float cumulative = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += Mathf.Max(0f, weights[i]);
            if (roll <= cumulative)
            {
                return candidates[i];
            }
        }

        return candidates[candidates.Count - 1];
    }

    private void EnsureBossConnections(
        List<MapNodeData> currentFloor,
        List<MapNodeData> nextFloor,
        Dictionary<MapNodeData, int> outgoingConnections,
        Dictionary<MapNodeData, int> incomingConnections,
        int[] incomingCounts,
        int[] minTargets,
        int[] maxTargets,
        int[] prefixMaxTargets,
        int[] suffixMinTargets,
        int[] primaryTargets,
        int floorIndex)
    {
        if (nextFloor.Count == 0)
            return;

        int currentCount = currentFloor.Count;
        int nextCount = nextFloor.Count;
        int bossTarget = 0;
        int required = Mathf.Max(0, Mathf.Min(minDistinctSourcesToBoss, currentCount) - incomingCounts[bossTarget]);
        if (required <= 0)
            return;

        for (int pass = 0; pass < required; pass++)
        {
            int bestSource = -1;
            int bestScore = int.MaxValue;

            for (int sourceIndex = 0; sourceIndex < currentCount; sourceIndex++)
            {
                int outgoing = outgoingConnections.TryGetValue(currentFloor[sourceIndex], out int value) ? value : 0;
                if (outgoing >= maxOutgoingPerNode)
                    continue;

                GetAnchorRange(sourceIndex, currentCount, nextCount, out int anchor, out int anchorMin, out int anchorMax);

                int lowerBound = sourceIndex > 0 ? prefixMaxTargets[sourceIndex - 1] + 1 : 0;
                int upperBound = sourceIndex < currentCount - 1 ? suffixMinTargets[sourceIndex + 1] - 1 : nextCount - 1;
                lowerBound = Mathf.Max(lowerBound, anchorMin);
                upperBound = Mathf.Min(upperBound, anchorMax);

                if (bossTarget < lowerBound || bossTarget > upperBound)
                    continue;

                int anchorTarget = primaryTargets[sourceIndex] >= 0
                    ? Mathf.Clamp(primaryTargets[sourceIndex], anchorMin, anchorMax)
                    : anchor;
                int distance = Mathf.Abs(bossTarget - anchorTarget);
                int score = outgoing * 10 + distance;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestSource = sourceIndex;
                }
            }

            if (bestSource < 0)
                break;

            if (ConnectAndTrack(
                    currentFloor,
                    nextFloor,
                    bestSource,
                    bossTarget,
                    outgoingConnections,
                    incomingConnections,
                    incomingCounts,
                    minTargets,
                    maxTargets,
                    primaryTargets))
            {
                RecomputeBounds(currentCount, nextCount, minTargets, maxTargets, prefixMaxTargets, suffixMinTargets);
            }
            else
            {
                Debug.LogWarning($"Failed to enforce boss connection on floor {floorIndex} from source {bestSource}");
            }
        }
    }
}
