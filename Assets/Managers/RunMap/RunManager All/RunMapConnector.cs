using System.Collections.Generic;
using System.Linq; // for Contains on IReadOnlyList
using UnityEngine;

public class RunMapConnector
{
    private readonly RunMapGenerationFeatureFlags featureFlags;

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
        int minConnectedSourcesPerRow = 3,
        int backtrackAllowance = 1,
        float connectionDensity = 1.5f,
        int minIncomingPerTarget = 1,
        int minDistinctSourcesToBoss = 3,
        float longLinkChance = 0.2f,
        int minDistinctTargetsPerFloor = 2,
        RunMapGenerationFeatureFlags featureFlags = null)
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
        this.featureFlags = featureFlags ?? RunMapGenerationFeatureFlags.Default;
    }

    private sealed class FloorConnectionContext
    {
        public FloorConnectionContext(List<MapNodeData> currentFloor, List<MapNodeData> nextFloor)
        {
            CurrentFloor = currentFloor;
            NextFloor = nextFloor;

            OutgoingConnections = new Dictionary<MapNodeData, int>();
            IncomingConnections = new Dictionary<MapNodeData, int>();

            PrimaryTargets = new int[currentFloor.Count];
            IncomingCounts = new int[nextFloor.Count];
            MinTargets = new int[currentFloor.Count];
            MaxTargets = new int[currentFloor.Count];
            PrefixMaxTargets = new int[currentFloor.Count];
            SuffixMinTargets = new int[currentFloor.Count];

            for (int i = 0; i < CurrentCount; i++)
            {
                OutgoingConnections[currentFloor[i]] = 0;
                PrimaryTargets[i] = -1;
                MinTargets[i] = int.MaxValue;
                MaxTargets[i] = -1;
            }
        }

        public List<MapNodeData> CurrentFloor { get; }
        public List<MapNodeData> NextFloor { get; }

        public Dictionary<MapNodeData, int> OutgoingConnections { get; }
        public Dictionary<MapNodeData, int> IncomingConnections { get; }

        public int[] PrimaryTargets { get; }
        public int[] IncomingCounts { get; }
        public int[] MinTargets { get; }
        public int[] MaxTargets { get; }
        public int[] PrefixMaxTargets { get; }
        public int[] SuffixMinTargets { get; }

        public int CurrentCount => CurrentFloor.Count;
        public int NextCount => NextFloor.Count;
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

            var context = new FloorConnectionContext(currentFloor, nextFloor);
            bool isLinkToBoss = (floor + 1 == map.Floors.Count - 1) && context.NextCount == 1;

            InitializePrimaryConnections(context);
            RecomputeBounds(context);

            EnsureIncomingCoverage(context, floor);
            EnsureMinimumConnectedSources(context);
            if (isLinkToBoss)
            {
                EnsureBossConnections(context, floor);
            }

            AddBranchingConnections(context);
            EnsureMinimumDistinctTargets(context);
            EnsureMinimumOutgoing(context);
        }
    }

    private void InitializePrimaryConnections(FloorConnectionContext context)
    {
        int previousTarget = -1;
        for (int sourceIndex = 0; sourceIndex < context.CurrentCount; sourceIndex++)
        {
            GetAnchorRange(sourceIndex, context.CurrentCount, context.NextCount, out int anchor, out int anchorMin, out int anchorMax);
            int lowerBound = Mathf.Max(previousTarget - backtrackAllowance, anchorMin);
            int upperBound = anchorMax;
            if (lowerBound > upperBound)
            {
                lowerBound = Mathf.Min(lowerBound, context.NextCount - 1);
                upperBound = Mathf.Max(lowerBound, upperBound);
            }

            int targetIndex = Mathf.Clamp(anchor, lowerBound, upperBound);
            if (ConnectAndTrack(context, sourceIndex, targetIndex))
            {
                previousTarget = Mathf.Max(previousTarget, targetIndex);
            }
        }
    }

    private void EnsureIncomingCoverage(FloorConnectionContext context, int floor)
    {
        for (int targetIndex = 0; targetIndex < context.NextCount; targetIndex++)
        {
            if (context.IncomingCounts[targetIndex] >= minIncomingPerTarget)
                continue;

            int sourceIndex = SelectSourceForUnconnectedTarget(floor, targetIndex, context);
            if (sourceIndex >= 0 && ConnectAndTrack(context, sourceIndex, targetIndex))
            {
                RecomputeBounds(context);
            }
        }
    }

    private void EnsureMinimumConnectedSources(FloorConnectionContext context)
    {
        int connectedSources = context.OutgoingConnections.Count(pair => pair.Value > 0);
        int requiredSources = context.CurrentCount >= 3
            ? Mathf.Min(minConnectedSourcesPerRow, context.CurrentCount)
            : Mathf.Min(context.CurrentCount, minConnectedSourcesPerRow);
        if (connectedSources >= requiredSources)
            return;

        for (int sourceIndex = 0; sourceIndex < context.CurrentCount && connectedSources < requiredSources; sourceIndex++)
        {
            if (context.OutgoingConnections[context.CurrentFloor[sourceIndex]] > 0)
                continue;

            GetAnchorRange(sourceIndex, context.CurrentCount, context.NextCount, out int anchorTarget, out int anchorLower, out int anchorUpper);

            int lowerBound = sourceIndex > 0 ? context.PrefixMaxTargets[sourceIndex - 1] + 1 : 0;
            int upperBound = sourceIndex < context.CurrentCount - 1
                ? Mathf.Min(context.NextCount - 1, context.SuffixMinTargets[sourceIndex + 1] - 1)
                : context.NextCount - 1;

            lowerBound = Mathf.Max(lowerBound, anchorLower);
            upperBound = Mathf.Min(upperBound, anchorUpper);

            if (lowerBound > upperBound)
                continue;

            int candidate = Mathf.Clamp(anchorTarget, lowerBound, upperBound);
            if (ConnectAndTrack(context, sourceIndex, candidate))
            {
                connectedSources++;
                RecomputeBounds(context);
            }
        }
    }

    private void AddBranchingConnections(FloorConnectionContext context)
    {
        for (int sourceIndex = 0; sourceIndex < context.CurrentCount; sourceIndex++)
        {
            int desiredConnections = Mathf.CeilToInt(((float)context.NextCount / context.CurrentCount) * connectionDensity) + 1;
            desiredConnections = Mathf.Clamp(desiredConnections, 2, maxOutgoingPerNode);
            if (context.OutgoingConnections[context.CurrentFloor[sourceIndex]] >= desiredConnections)
                continue;

            GetAnchorRange(sourceIndex, context.CurrentCount, context.NextCount, out int anchorTarget, out int anchorLower, out int anchorUpper);
            if (context.PrimaryTargets[sourceIndex] >= 0)
            {
                anchorTarget = Mathf.Clamp(context.PrimaryTargets[sourceIndex], anchorLower, anchorUpper);
            }

            int maxAttempts = 6;
            for (int attempt = 0; attempt < maxAttempts && context.OutgoingConnections[context.CurrentFloor[sourceIndex]] < desiredConnections; attempt++)
            {
                int lowerBound = sourceIndex > 0 ? context.PrefixMaxTargets[sourceIndex - 1] + 1 : 0;
                int upperBound = sourceIndex < context.CurrentCount - 1 ? context.SuffixMinTargets[sourceIndex + 1] - 1 : context.NextCount - 1;
                int adjustedLower = anchorLower;
                int adjustedUpper = anchorUpper;

                if (UnityEngine.Random.value < longLinkChance)
                {
                    int expand = UnityEngine.Random.Range(1, 3);
                    adjustedLower = Mathf.Max(0, anchorLower - expand);
                    adjustedUpper = Mathf.Min(context.NextCount - 1, anchorUpper + expand);
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
                    float weight = 1f / (1 + context.IncomingCounts[t]);
                    weights.Add(weight);
                }

                if (candidates.Count == 0)
                    break;

                for (int pickAttempt = 0; pickAttempt < 6 && context.OutgoingConnections[context.CurrentFloor[sourceIndex]] < desiredConnections; pickAttempt++)
                {
                    int candidate = SampleByWeight(candidates, weights);
                    if (ConnectAndTrack(context, sourceIndex, candidate))
                    {
                        RecomputeBounds(context);
                        break;
                    }
                }
            }
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
        FloorConnectionContext context)
    {
        int bestSource = -1;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < context.PrimaryTargets.Length; i++)
        {
            GetAnchorRange(i, context.CurrentCount, context.NextCount, out int anchor, out int anchorMin, out int anchorMax);

            int lowerBound = i > 0 ? context.PrefixMaxTargets[i - 1] + 1 : 0;
            int upperBound = i < context.CurrentCount - 1
                ? Mathf.Min(context.NextCount - 1, context.SuffixMinTargets[i + 1] - 1)
                : context.NextCount - 1;

            lowerBound = Mathf.Max(lowerBound, anchorMin);
            upperBound = Mathf.Min(upperBound, anchorMax);

            if (lowerBound > upperBound || targetIndex < lowerBound || targetIndex > upperBound)
                continue;

            int outgoing = context.OutgoingConnections.TryGetValue(context.CurrentFloor[i], out int value) ? value : 0;
            if (outgoing >= maxOutgoingPerNode)
                continue;

            anchor = context.PrimaryTargets[i] >= 0
                ? Mathf.Clamp(context.PrimaryTargets[i], anchorMin, anchorMax)
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

        int relaxedBestSource = -1;
        bestDistance = int.MaxValue;

        for (int i = 0; i < context.PrimaryTargets.Length; i++)
        {
            GetAnchorRange(i, context.CurrentCount, context.NextCount, out int anchor, out _, out _);

            int lowerBound = i > 0 ? context.PrefixMaxTargets[i - 1] + 1 : 0;
            int upperBound = i < context.CurrentCount - 1
                ? Mathf.Min(context.NextCount - 1, context.SuffixMinTargets[i + 1] - 1)
                : context.NextCount - 1;

            if (lowerBound > upperBound || targetIndex < lowerBound || targetIndex > upperBound)
                continue;

            int outgoing = context.OutgoingConnections.TryGetValue(context.CurrentFloor[i], out int value) ? value : 0;
            if (outgoing >= maxOutgoingPerNode)
                continue;

            int clampedAnchor = context.PrimaryTargets[i] >= 0
                ? Mathf.Clamp(context.PrimaryTargets[i], 0, context.NextCount - 1)
                : Mathf.Clamp(anchor, 0, context.NextCount - 1);

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

    private void EnsureMinimumDistinctTargets(FloorConnectionContext context)
    {
        int nextCount = context.NextFloor.Count;
        int currentCount = context.CurrentFloor.Count;
        int distinctTargets = context.IncomingCounts.Count(count => count >= minIncomingPerTarget);

        if (distinctTargets >= minDistinctTargetsPerFloor)
            return;

        var orderedSources = context.OutgoingConnections
            .OrderBy(pair => pair.Value)
            .ThenBy(pair => context.CurrentFloor.IndexOf(pair.Key))
            .Select(pair => context.CurrentFloor.IndexOf(pair.Key))
            .ToList();

        foreach (int sourceIndex in orderedSources)
        {
            if (distinctTargets >= minDistinctTargetsPerFloor)
                break;

            if (context.OutgoingConnections[context.CurrentFloor[sourceIndex]] >= maxOutgoingPerNode)
                continue;

            GetAnchorRange(sourceIndex, currentCount, nextCount, out int anchor, out int anchorMin, out int anchorMax);

            int lowerBound = sourceIndex > 0 ? context.PrefixMaxTargets[sourceIndex - 1] + 1 : 0;
            int upperBound = sourceIndex < currentCount - 1 ? context.SuffixMinTargets[sourceIndex + 1] - 1 : nextCount - 1;
            lowerBound = Mathf.Max(lowerBound, anchorMin);
            upperBound = Mathf.Min(upperBound, anchorMax);

            if (lowerBound > upperBound)
                continue;

            int minIncoming = context.IncomingCounts.Skip(lowerBound).Take(upperBound - lowerBound + 1).Min();
            var candidates = new List<int>();
            for (int t = lowerBound; t <= upperBound; t++)
            {
                if (context.IncomingCounts[t] == minIncoming)
                {
                    candidates.Add(t);
                }
            }

            if (candidates.Count == 0)
                continue;

            int bestTarget = candidates.OrderBy(t => Mathf.Abs(t - anchor)).First();
            if (ConnectAndTrack(context, sourceIndex, bestTarget))
            {
                distinctTargets = context.IncomingCounts.Count(c => c >= minIncomingPerTarget);
                RecomputeBounds(context);
            }
        }
    }

    private bool ConnectNodes(FloorConnectionContext context, int sourceIndex, int targetIndex)
    {
        return TryConnectNodes(context.CurrentFloor[sourceIndex], context.NextFloor[targetIndex], context.OutgoingConnections, context.IncomingConnections);
    }

    private bool ConnectAndTrack(FloorConnectionContext context, int sourceIndex, int targetIndex)
    {
        if (ConnectNodes(context, sourceIndex, targetIndex))
        {
            context.IncomingCounts[targetIndex]++;
            context.MinTargets[sourceIndex] = Mathf.Min(context.MinTargets[sourceIndex], targetIndex);
            context.MaxTargets[sourceIndex] = Mathf.Max(context.MaxTargets[sourceIndex], targetIndex);
            if (context.PrimaryTargets[sourceIndex] == -1)
            {
                context.PrimaryTargets[sourceIndex] = targetIndex;
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

     private void RecomputeBounds(FloorConnectionContext context)
    {
        int currentCount = context.CurrentCount;
        int nextCount = context.NextCount;

        int runningMax = -1;
        for (int i = 0; i < currentCount; i++)
        {
            if (context.MaxTargets[i] >= 0)
            {
                runningMax = Mathf.Max(runningMax, context.MaxTargets[i]);
            }
            context.PrefixMaxTargets[i] = runningMax;
        }

        int runningMin = nextCount;
        for (int i = currentCount - 1; i >= 0; i--)
        {
            if (context.MinTargets[i] != int.MaxValue)
            {
                runningMin = Mathf.Min(runningMin, context.MinTargets[i]);
            }
            context.SuffixMinTargets[i] = runningMin;
        }
    }

    private void EnsureMinimumOutgoing(FloorConnectionContext context)
    {
        int currentCount = context.CurrentFloor.Count;
        int nextCount = context.NextFloor.Count;

        for (int sourceIndex = 0; sourceIndex < currentCount; sourceIndex++)
        {
            if (context.OutgoingConnections[context.CurrentFloor[sourceIndex]] > 0)
                continue;

            GetAnchorRange(sourceIndex, currentCount, nextCount, out int anchor, out int anchorMin, out int anchorMax);

            int lowerBound = sourceIndex > 0 ? context.PrefixMaxTargets[sourceIndex - 1] + 1 : 0;
            int upperBound = sourceIndex < currentCount - 1
                ? Mathf.Min(nextCount - 1, context.SuffixMinTargets[sourceIndex + 1] - 1)
                : nextCount - 1;

            lowerBound = Mathf.Max(lowerBound, anchorMin);
            upperBound = Mathf.Min(upperBound, anchorMax);

            if (lowerBound > upperBound)
                continue;

            int targetIndex = Mathf.Clamp(anchor, lowerBound, upperBound);
            ConnectAndTrack(context, sourceIndex, targetIndex);
        }
    }

    private void EnsureBossConnections(FloorConnectionContext context, int floorIndex)
    {
        if (context.NextFloor.Count == 0)
            return;

        int currentCount = context.CurrentFloor.Count;
        int nextCount = context.NextFloor.Count;
        int bossTarget = 0;
        int required = Mathf.Max(0, Mathf.Min(minDistinctSourcesToBoss, currentCount) - context.IncomingCounts[bossTarget]);
        if (required <= 0)
            return;

        for (int pass = 0; pass < required; pass++)
        {
            int bestSource = -1;
            int bestScore = int.MaxValue;

            for (int sourceIndex = 0; sourceIndex < currentCount; sourceIndex++)
            {
                int outgoing = context.OutgoingConnections.TryGetValue(context.CurrentFloor[sourceIndex], out int value) ? value : 0;
                if (outgoing >= maxOutgoingPerNode)
                    continue;

                GetAnchorRange(sourceIndex, currentCount, nextCount, out int anchor, out int anchorMin, out int anchorMax);

                int lowerBound = sourceIndex > 0 ? context.PrefixMaxTargets[sourceIndex - 1] + 1 : 0;
                int upperBound = sourceIndex < currentCount - 1 ? context.SuffixMinTargets[sourceIndex + 1] - 1 : nextCount - 1;
                lowerBound = Mathf.Max(lowerBound, anchorMin);
                upperBound = Mathf.Min(upperBound, anchorMax);

                if (bossTarget < lowerBound || bossTarget > upperBound)
                    continue;

                int anchorTarget = context.PrimaryTargets[sourceIndex] >= 0
                    ? Mathf.Clamp(context.PrimaryTargets[sourceIndex], anchorMin, anchorMax)
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

            if (ConnectAndTrack(context, bestSource, bossTarget))
            {
                RecomputeBounds(context);
            }
            else
            {
                Debug.LogWarning($"Failed to enforce boss connection on floor {floorIndex} from source {bestSource}");
            }
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
}
