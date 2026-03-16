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
    private readonly int minBranchingNodesPerFloor;
    private readonly int maxBranchingNodesPerFloor;
    internal int MaxOutgoingPerNode => maxOutgoingPerNode;
    internal int MinConnectedSourcesPerRow => minConnectedSourcesPerRow;
    internal float ConnectionDensity => connectionDensity;
    internal int MinIncomingPerTarget => minIncomingPerTarget;
    internal int MinDistinctSourcesToBoss => minDistinctSourcesToBoss;
    internal float LongLinkChance => longLinkChance;
    internal int MinDistinctTargetsPerFloor => minDistinctTargetsPerFloor;
    internal RunMapGenerationFeatureFlags FeatureFlags => featureFlags;
    internal int MinBranchingNodesPerFloor => minBranchingNodesPerFloor;
    internal int MaxBranchingNodesPerFloor => maxBranchingNodesPerFloor;
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
        int minBranchingNodesPerFloor = 1,
        int maxBranchingNodesPerFloor = 3,
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
        this.minBranchingNodesPerFloor = Mathf.Max(0, minBranchingNodesPerFloor);
        this.maxBranchingNodesPerFloor = Mathf.Max(this.minBranchingNodesPerFloor, maxBranchingNodesPerFloor);
        this.featureFlags = featureFlags ?? RunMapGenerationFeatureFlags.Default;
    }

    public void BuildConnections(RunMap map)
    {
        if (map == null || map.Floors == null || map.Floors.Count == 0)
            return;

        ResetExistingConnections(map);

        HashSet<int> reachableSources = new HashSet<int>(Enumerable.Range(0, map.Floors[0].Count));
        for (int floor = 0; floor < map.Floors.Count - 1; floor++)
        {
            List<MapNodeData> currentFloor = map.Floors[floor];
            List<MapNodeData> nextFloor = map.Floors[floor + 1];
            if (currentFloor.Count == 0 || nextFloor.Count == 0)
            {
                reachableSources.Clear();
                continue;
            }

            List<int> activeSources = reachableSources
                .Where(index => index >= 0 && index < currentFloor.Count)
                .OrderBy(index => index)
                .ToList();

            if (activeSources.Count == 0)
            {
                activeSources.Add(Mathf.Clamp(currentFloor.Count / 2, 0, currentFloor.Count - 1));
            }

            bool isLinkToBoss = floor + 1 == map.Floors.Count - 1 && nextFloor.Count == 1;
            Dictionary<int, HashSet<int>> floorConnections = BuildPathConnectionsForFloor(
                activeSources,
                currentFloor.Count,
                nextFloor.Count,
                isLinkToBoss);

            CommitFloorConnections(currentFloor, nextFloor, floorConnections);
            reachableSources = new HashSet<int>(floorConnections.Values.SelectMany(targets => targets));
        }

        PruneUnreachableNodes(map);
    }

    private void ResetExistingConnections(RunMap map)
    {
        foreach (List<MapNodeData> floor in map.Floors)
        {
            foreach (MapNodeData node in floor)
            {
                node?.ClearNextNodes();
            }
        }
    }

    private Dictionary<int, HashSet<int>> BuildPathConnectionsForFloor(
        List<int> activeSources,
        int currentCount,
        int nextCount,
        bool isLinkToBoss)
    {
        var floorConnections = new Dictionary<int, HashSet<int>>();
        if (activeSources == null || activeSources.Count == 0 || currentCount == 0 || nextCount == 0)
            return floorConnections;

        if (isLinkToBoss)
        {
            foreach (int sourceIndex in activeSources)
            {
                AddTarget(floorConnections, sourceIndex, 0);
            }

            return floorConnections;
        }

        var targetUsage = new Dictionary<int, int>();
        int desiredDistinctTargets = GetDesiredDistinctTargetCount(activeSources.Count, nextCount);
        int desiredPrimaryDistinctTargets = Mathf.Clamp(
            desiredDistinctTargets,
            1,
            Mathf.Min(activeSources.Count, nextCount));
        List<int> primaryLaneTargets = BuildPrimaryLaneTargets(desiredPrimaryDistinctTargets, nextCount);
        var primaryTargets = new Dictionary<int, int>();

        for (int sourceOrder = 0; sourceOrder < activeSources.Count; sourceOrder++)
        {
            int sourceIndex = activeSources[sourceOrder];
            int idealTarget = GetPrimaryIdealTarget(sourceOrder, activeSources.Count, primaryLaneTargets);
            int minAllowedTarget = sourceOrder > 0
                ? GetMaxTarget(floorConnections, activeSources[sourceOrder - 1])
                : 0;

            int primaryTarget = PickTargetForSource(
                sourceIndex,
                currentCount,
                nextCount,
                existingTargets: null,
                targetUsage,
                preferUnused: targetUsage.Count < desiredPrimaryDistinctTargets,
                requireUnused: false,
                extraReach: 0,
                minAllowedTarget,
                nextCount - 1,
                idealTarget);

            if (primaryTarget < 0)
            {
                primaryTarget = Mathf.Clamp(Mathf.Max(minAllowedTarget, idealTarget), minAllowedTarget, nextCount - 1);
            }

            if (AddTarget(floorConnections, sourceIndex, primaryTarget))
            {
                primaryTargets[sourceIndex] = primaryTarget;
                IncrementTargetUsage(targetUsage, primaryTarget);
            }
        }

        int desiredBranchingSources = GetDesiredBranchingSourceCount(activeSources.Count, nextCount, desiredDistinctTargets);
        var blockedBranchSources = new HashSet<int>();
        while (CountBranchingSources(floorConnections) < desiredBranchingSources)
        {
            int sourceIndex = PickBranchingSource(
                activeSources.Where(index => !blockedBranchSources.Contains(index)).ToList(),
                floorConnections,
                currentCount);
            if (sourceIndex < 0)
                break;

            int sourceOrder = activeSources.IndexOf(sourceIndex);
            IReadOnlyCollection<int> existingTargets = floorConnections[sourceIndex];
            bool requireUnused = targetUsage.Count < desiredDistinctTargets;
            GetTargetCorridor(activeSources, floorConnections, sourceOrder, nextCount, out int minAllowedTarget, out int maxAllowedTarget);
            int branchTarget = PickTargetForSource(
                sourceIndex,
                currentCount,
                nextCount,
                existingTargets,
                targetUsage,
                preferUnused: true,
                requireUnused,
                extraReach: 1,
                minAllowedTarget,
                maxAllowedTarget,
                primaryTargets.TryGetValue(sourceIndex, out int primaryTarget) ? primaryTarget : -1);

            if (branchTarget < 0 && requireUnused)
            {
                branchTarget = PickTargetForSource(
                    sourceIndex,
                    currentCount,
                    nextCount,
                    existingTargets,
                    targetUsage,
                    preferUnused: false,
                    requireUnused: false,
                    extraReach: 1,
                    minAllowedTarget,
                    maxAllowedTarget,
                    primaryTargets.TryGetValue(sourceIndex, out primaryTarget) ? primaryTarget : -1);
            }

            if (branchTarget < 0 || !AddTarget(floorConnections, sourceIndex, branchTarget))
            {
                blockedBranchSources.Add(sourceIndex);
                continue;
            }

            IncrementTargetUsage(targetUsage, branchTarget);
            blockedBranchSources.Clear();
        }

        var blockedDistinctSources = new HashSet<int>();
        while (targetUsage.Count < desiredDistinctTargets)
        {
            int sourceIndex = PickBranchingSource(
                activeSources.Where(index => !blockedDistinctSources.Contains(index)).ToList(),
                floorConnections,
                currentCount);
            if (sourceIndex < 0)
                break;

            int sourceOrder = activeSources.IndexOf(sourceIndex);
            GetTargetCorridor(activeSources, floorConnections, sourceOrder, nextCount, out int minAllowedTarget, out int maxAllowedTarget);
            int branchTarget = PickTargetForSource(
                sourceIndex,
                currentCount,
                nextCount,
                floorConnections[sourceIndex],
                targetUsage,
                preferUnused: true,
                requireUnused: true,
                extraReach: 2,
                minAllowedTarget,
                maxAllowedTarget,
                primaryTargets.TryGetValue(sourceIndex, out int primaryTarget) ? primaryTarget : -1);

            if (branchTarget < 0 || !AddTarget(floorConnections, sourceIndex, branchTarget))
            {
                blockedDistinctSources.Add(sourceIndex);
                continue;
            }

            IncrementTargetUsage(targetUsage, branchTarget);
            blockedDistinctSources.Clear();
        }

        if (floorConnections.Count == 0)
        {
            int sourceIndex = activeSources[0];
            int targetIndex = Mathf.Clamp(sourceIndex, 0, nextCount - 1);
            AddTarget(floorConnections, sourceIndex, targetIndex);
        }

        NormalizeTargetSetOrder(activeSources, floorConnections);
        return floorConnections;
    }

    private void CommitFloorConnections(
        List<MapNodeData> currentFloor,
        List<MapNodeData> nextFloor,
        Dictionary<int, HashSet<int>> floorConnections)
    {
        if (currentFloor == null || nextFloor == null || floorConnections == null)
            return;

        foreach (KeyValuePair<int, HashSet<int>> pair in floorConnections)
        {
            if (pair.Key < 0 || pair.Key >= currentFloor.Count)
                continue;

            foreach (int targetIndex in pair.Value.OrderBy(index => index))
            {
                if (targetIndex < 0 || targetIndex >= nextFloor.Count)
                    continue;

                currentFloor[pair.Key].AddNextNode(nextFloor[targetIndex]);
            }
        }
    }

    private int PickTargetForSource(
        int sourceIndex,
        int currentCount,
        int nextCount,
        IReadOnlyCollection<int> existingTargets,
        Dictionary<int, int> targetUsage,
        bool preferUnused,
        bool requireUnused,
        int extraReach,
        int minAllowedTarget,
        int maxAllowedTarget,
        int idealTarget)
    {
        List<int> candidates = GetCandidateTargets(
            sourceIndex,
            currentCount,
            nextCount,
            existingTargets,
            targetUsage,
            requireUnused,
            extraReach,
            minAllowedTarget,
            maxAllowedTarget);
        if (candidates.Count == 0)
            return -1;

        GetAnchorRange(sourceIndex, currentCount, nextCount, out int anchor, out _, out _);

        var weights = new List<float>(candidates.Count);
        foreach (int candidate in candidates)
        {
            int usage = targetUsage.TryGetValue(candidate, out int count) ? count : 0;
            float weight = 1f / (1f + Mathf.Abs(candidate - anchor));

            if (idealTarget >= 0)
            {
                weight *= 1.5f / (1f + Mathf.Abs(candidate - idealTarget));
            }

            if (usage == 0)
            {
                weight *= preferUnused ? 3.25f : 1.35f;
            }
            else
            {
                weight *= preferUnused ? 0.3f / usage : 1f / usage;
            }

            if (targetUsage.Count > 0)
            {
                int nearestUsed = targetUsage.Keys.Min(existing => Mathf.Abs(existing - candidate));
                weight *= 1f + Mathf.Clamp(nearestUsed * 0.25f, 0f, preferUnused ? 1.5f : 0.45f);
            }

            if (existingTargets != null && existingTargets.Count > 0)
            {
                int nearestExisting = existingTargets.Min(existing => Mathf.Abs(existing - candidate));
                weight *= 1f + Mathf.Clamp(nearestExisting * 0.4f, 0f, 1.6f);
            }

            float normalized = currentCount <= 1 ? 0.5f : (float)sourceIndex / (currentCount - 1);
            if (normalized <= 0.2f || normalized >= 0.8f)
            {
                bool isEdgeTarget = candidate == 0 || candidate == nextCount - 1;
                if (isEdgeTarget)
                    weight *= 1.15f;
            }

            weights.Add(weight);
        }

        return RunMapConnectionSampling.SampleByWeight(candidates, weights);
    }

    private List<int> GetCandidateTargets(
        int sourceIndex,
        int currentCount,
        int nextCount,
        IReadOnlyCollection<int> existingTargets,
        Dictionary<int, int> targetUsage,
        bool requireUnused,
        int extraReach,
        int minAllowedTarget,
        int maxAllowedTarget)
    {
        if (maxAllowedTarget < minAllowedTarget)
            return new List<int>();

        GetAnchorRange(sourceIndex, currentCount, nextCount, out _, out int lowerBound, out int upperBound);

        int reach = extraReach;
        if (UnityEngine.Random.value < longLinkChance)
        {
            reach += UnityEngine.Random.Range(1, 3);
        }

        lowerBound = Mathf.Max(minAllowedTarget, lowerBound - reach);
        upperBound = Mathf.Min(maxAllowedTarget, upperBound + reach);

        var candidates = new List<int>();
        for (int targetIndex = lowerBound; targetIndex <= upperBound; targetIndex++)
        {
            if (existingTargets != null && existingTargets.Contains(targetIndex))
                continue;

            if (requireUnused && targetUsage.TryGetValue(targetIndex, out int count) && count > 0)
                continue;

            candidates.Add(targetIndex);
        }

        if (candidates.Count > 0)
            return candidates;

        if (!requireUnused)
            return candidates;

        for (int targetIndex = lowerBound; targetIndex <= upperBound; targetIndex++)
        {
            if (existingTargets != null && existingTargets.Contains(targetIndex))
                continue;

            candidates.Add(targetIndex);
        }

        return candidates;
    }

    private List<int> BuildPrimaryLaneTargets(int desiredPrimaryDistinctTargets, int nextCount)
    {
        var laneTargets = new List<int>(desiredPrimaryDistinctTargets);
        if (desiredPrimaryDistinctTargets <= 0 || nextCount <= 0)
            return laneTargets;

        if (desiredPrimaryDistinctTargets == 1)
        {
            laneTargets.Add((nextCount - 1) / 2);
            return laneTargets;
        }

        for (int lane = 0; lane < desiredPrimaryDistinctTargets; lane++)
        {
            float t = (float)lane / (desiredPrimaryDistinctTargets - 1);
            laneTargets.Add(Mathf.RoundToInt(t * (nextCount - 1)));
        }

        return laneTargets;
    }

    private int GetPrimaryIdealTarget(int sourceOrder, int sourceCount, List<int> primaryLaneTargets)
    {
        if (primaryLaneTargets == null || primaryLaneTargets.Count == 0)
            return 0;

        if (sourceCount <= 1 || primaryLaneTargets.Count == 1)
            return primaryLaneTargets[0];

        int laneIndex = Mathf.RoundToInt(((float)sourceOrder / (sourceCount - 1)) * (primaryLaneTargets.Count - 1));
        laneIndex = Mathf.Clamp(laneIndex, 0, primaryLaneTargets.Count - 1);
        return primaryLaneTargets[laneIndex];
    }

    private void GetTargetCorridor(
        List<int> activeSources,
        Dictionary<int, HashSet<int>> floorConnections,
        int sourceOrder,
        int nextCount,
        out int minAllowedTarget,
        out int maxAllowedTarget)
    {
        minAllowedTarget = sourceOrder > 0
            ? GetMaxTarget(floorConnections, activeSources[sourceOrder - 1])
            : 0;
        maxAllowedTarget = sourceOrder < activeSources.Count - 1
            ? GetMinTarget(floorConnections, activeSources[sourceOrder + 1])
            : nextCount - 1;
    }

    private int GetMinTarget(Dictionary<int, HashSet<int>> floorConnections, int sourceIndex)
    {
        return floorConnections.TryGetValue(sourceIndex, out HashSet<int> targets) && targets.Count > 0
            ? targets.Min()
            : 0;
    }

    private int GetMaxTarget(Dictionary<int, HashSet<int>> floorConnections, int sourceIndex)
    {
        return floorConnections.TryGetValue(sourceIndex, out HashSet<int> targets) && targets.Count > 0
            ? targets.Max()
            : 0;
    }

    private void NormalizeTargetSetOrder(
        List<int> activeSources,
        Dictionary<int, HashSet<int>> floorConnections)
    {
        for (int i = 1; i < activeSources.Count; i++)
        {
            int leftSource = activeSources[i - 1];
            int rightSource = activeSources[i];

            while (floorConnections.TryGetValue(leftSource, out HashSet<int> leftTargets)
                && floorConnections.TryGetValue(rightSource, out HashSet<int> rightTargets)
                && leftTargets.Count > 0
                && rightTargets.Count > 0
                && leftTargets.Max() > rightTargets.Min())
            {
                bool trimmed = TrimExtremeBranch(leftTargets, removeHighest: true);
                if (!trimmed)
                {
                    trimmed = TrimExtremeBranch(rightTargets, removeHighest: false);
                }

                if (!trimmed)
                    break;
            }
        }
    }

    private bool TrimExtremeBranch(HashSet<int> targets, bool removeHighest)
    {
        if (targets == null || targets.Count <= 1)
            return false;

        int targetToRemove = removeHighest ? targets.Max() : targets.Min();
        return targets.Remove(targetToRemove);
    }

    private bool AddTarget(Dictionary<int, HashSet<int>> floorConnections, int sourceIndex, int targetIndex)
    {
        if (!floorConnections.TryGetValue(sourceIndex, out HashSet<int> targets))
        {
            targets = new HashSet<int>();
            floorConnections[sourceIndex] = targets;
        }

        if (targets.Count >= maxOutgoingPerNode)
            return false;

        return targets.Add(targetIndex);
    }

    private void IncrementTargetUsage(Dictionary<int, int> targetUsage, int targetIndex)
    {
        targetUsage[targetIndex] = targetUsage.TryGetValue(targetIndex, out int count)
            ? count + 1
            : 1;
    }

    private int CountBranchingSources(Dictionary<int, HashSet<int>> floorConnections)
    {
        return floorConnections.Values.Count(targets => targets.Count > 1);
    }

    private int PickBranchingSource(
        List<int> activeSources,
        Dictionary<int, HashSet<int>> floorConnections,
        int currentCount)
    {
        var candidates = new List<int>();
        var weights = new List<float>();

        foreach (int sourceIndex in activeSources)
        {
            if (!floorConnections.TryGetValue(sourceIndex, out HashSet<int> targets) || targets.Count == 0 || targets.Count >= maxOutgoingPerNode)
                continue;

            candidates.Add(sourceIndex);

            float weight = targets.Count == 1 ? 2.5f : 0.6f;
            float normalized = currentCount <= 1 ? 0.5f : (float)sourceIndex / (currentCount - 1);
            float centrality = 1f - Mathf.Abs(normalized - 0.5f);
            weight *= 1f + centrality * 0.5f;
            weights.Add(weight);
        }

        return RunMapConnectionSampling.SampleByWeight(candidates, weights);
    }

    private int GetDesiredDistinctTargetCount(int activeSourceCount, int nextCount)
    {
        if (nextCount <= 1)
            return nextCount;

        int minTargets = activeSourceCount <= 1
            ? Mathf.Min(nextCount, 2)
            : Mathf.Min(nextCount, Mathf.Max(1, minDistinctTargetsPerFloor));

        int lowerBound = Mathf.Min(nextCount, Mathf.Max(minTargets, activeSourceCount - 1));
        int upperBound = Mathf.Min(nextCount, Mathf.Max(lowerBound, activeSourceCount + Mathf.Max(1, Mathf.CeilToInt(connectionDensity - 1f))));

        float densityFactor = Mathf.Clamp01((connectionDensity - 1f) / Mathf.Max(1f, maxOutgoingPerNode - 1f));
        int baseline = Mathf.RoundToInt(Mathf.Lerp(lowerBound, upperBound, 0.45f + densityFactor * 0.35f));
        int wobble = UnityEngine.Random.Range(-1, 2);

        return Mathf.Clamp(baseline + wobble, lowerBound, upperBound);
    }

    private int GetDesiredBranchingSourceCount(int activeSourceCount, int nextCount, int desiredDistinctTargets)
    {
        if (activeSourceCount <= 0 || nextCount <= 1)
            return 0;

        int minSources = Mathf.Min(activeSourceCount, minBranchingNodesPerFloor);
        int maxSources = Mathf.Min(activeSourceCount, maxBranchingNodesPerFloor);
        if (maxSources <= 0)
            return 0;

        int pressure = Mathf.Max(0, desiredDistinctTargets - activeSourceCount);
        int densityBonus = Mathf.Max(0, Mathf.FloorToInt((connectionDensity - 1.15f) * 3f));
        int baseline = Mathf.Clamp(minSources + pressure + densityBonus, minSources, maxSources);

        if (baseline < maxSources)
        {
            float extraBranchChance = Mathf.Clamp01((connectionDensity - 1f) * 0.2f);
            if (UnityEngine.Random.value < extraBranchChance)
            {
                baseline++;
            }
        }

        return Mathf.Clamp(baseline, minSources, maxSources);
    }

    private void PruneUnreachableNodes(RunMap map)
    {
        if (map == null || map.Floors == null || map.Floors.Count == 0)
            return;

        HashSet<MapNodeData> reachable = new HashSet<MapNodeData>(map.Floors[0]);
        for (int floor = 0; floor < map.Floors.Count - 1; floor++)
        {
            var nextReachable = new HashSet<MapNodeData>();
            foreach (MapNodeData node in map.Floors[floor])
            {
                if (node == null || !reachable.Contains(node))
                    continue;

                foreach (MapNodeData next in node.NextNodes)
                {
                    if (next != null)
                        nextReachable.Add(next);
                }
            }

            map.Floors[floor + 1] = map.Floors[floor + 1]
                .Where(nextReachable.Contains)
                .ToList();

            reachable = nextReachable;
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

    internal void GetAnchorRange(int sourceIndex, int currentCount, int nextCount, out int anchor, out int minTarget, out int maxTarget)
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

    internal int SelectSourceForUnconnectedTarget(
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

    internal bool ConnectAndTrack(FloorConnectionContext context, int sourceIndex, int targetIndex)
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

    internal bool ConnectNodes(FloorConnectionContext context, int sourceIndex, int targetIndex)
    {
        return TryConnectNodes(context.CurrentFloor[sourceIndex], context.NextFloor[targetIndex], context.OutgoingConnections, context.IncomingConnections);
    }

    internal bool TryConnectNodes(
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

    private void EnforceColumnBranchingCoverage(RunMap map)
    {
        if (map.Floors.Count < 2)
            return;

        int maxColumns = map.Floors.Max(floor => floor.Count);
        bool[] columnHasBranch = new bool[maxColumns];
        int[] branchingPerFloor = new int[map.Floors.Count];

        for (int floor = 0; floor < map.Floors.Count - 1; floor++)
        {
            List<MapNodeData> currentFloor = map.Floors[floor];
            for (int column = 0; column < currentFloor.Count; column++)
            {
                int outgoing = currentFloor[column].NextNodes.Count;
                if (outgoing > 1)
                {
                    columnHasBranch[column] = true;
                    branchingPerFloor[floor]++;
                }
            }
        }

        for (int column = 0; column < maxColumns; column++)
        {
            if (columnHasBranch[column])
                continue;

            TryCreateBranchOnColumn(map, column, columnHasBranch, branchingPerFloor);
        }
    }

    private void TryCreateBranchOnColumn(RunMap map, int column, bool[] columnHasBranch, int[] branchingPerFloor)
    {
        for (int floor = 0; floor < map.Floors.Count - 1; floor++)
        {
            List<MapNodeData> currentFloor = map.Floors[floor];
            List<MapNodeData> nextFloor = map.Floors[floor + 1];
            if (column >= currentFloor.Count || nextFloor.Count == 0)
                continue;

            MapNodeData source = currentFloor[column];
            int outgoing = source.NextNodes.Count;
            if (outgoing == 0 || outgoing >= maxOutgoingPerNode)
                continue;

            if (branchingPerFloor[floor] >= maxBranchingNodesPerFloor)
                continue;

            GetAnchorRange(column, currentFloor.Count, nextFloor.Count, out int anchor, out int anchorMin, out int anchorMax);

            // Constrain the target range to avoid crossing existing connections from neighboring columns.
            Dictionary<MapNodeData, int> targetIndexLookup = new Dictionary<MapNodeData, int>(nextFloor.Count);
            for (int i = 0; i < nextFloor.Count; i++)
            {
                targetIndexLookup[nextFloor[i]] = i;
            }

            int leftMax = -1;
            for (int i = 0; i < column; i++)
            {
                foreach (MapNodeData target in currentFloor[i].NextNodes)
                {
                    if (targetIndexLookup.TryGetValue(target, out int index))
                    {
                        leftMax = Mathf.Max(leftMax, index);
                    }
                }
            }

            int rightMin = nextFloor.Count;
            for (int i = column + 1; i < currentFloor.Count; i++)
            {
                foreach (MapNodeData target in currentFloor[i].NextNodes)
                {
                    if (targetIndexLookup.TryGetValue(target, out int index))
                    {
                        rightMin = Mathf.Min(rightMin, index);
                    }
                }
            }

            int lowerBound = Mathf.Max(0, anchorMin, leftMax >= 0 ? leftMax : 0);
            int upperBound = Mathf.Min(nextFloor.Count - 1, anchorMax, rightMin < nextFloor.Count ? rightMin : nextFloor.Count - 1);
            List<int> candidates = new List<int>();
            for (int target = lowerBound; target <= upperBound; target++)
            {
                if (!source.NextNodes.Contains(nextFloor[target]))
                {
                    candidates.Add(target);
                }
            }

            if (candidates.Count == 0)
            {
                int safeLower = Mathf.Max(0, leftMax);
                int safeUpper = rightMin < nextFloor.Count ? rightMin : nextFloor.Count - 1;
                if (safeLower > safeUpper)
                    continue;
                for (int target = safeLower; target <= safeUpper; target++)
                {
                    if (!source.NextNodes.Contains(nextFloor[target]))
                    {
                        candidates.Add(target);
                    }
                }
            }

            if (candidates.Count == 0)
                continue;

            int bestTarget = candidates
                .OrderBy(target => Mathf.Abs(target - anchor))
                .First();

            var outgoingConnections = new Dictionary<MapNodeData, int>();
            var incomingConnections = new Dictionary<MapNodeData, int>();
            foreach (MapNodeData node in currentFloor)
            {
                outgoingConnections[node] = node.NextNodes.Count;
            }

            if (TryConnectNodes(source, nextFloor[bestTarget], outgoingConnections, incomingConnections))
            {
                branchingPerFloor[floor]++;
                columnHasBranch[column] = true;
                return;
            }
        }
    }
}
