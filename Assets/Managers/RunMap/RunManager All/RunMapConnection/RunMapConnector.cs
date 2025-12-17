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
        this.minBranchingNodesPerFloor = Mathf.Max(1, minBranchingNodesPerFloor);
        this.maxBranchingNodesPerFloor = Mathf.Max(this.minBranchingNodesPerFloor, maxBranchingNodesPerFloor);
        this.featureFlags = featureFlags ?? RunMapGenerationFeatureFlags.Default;
    }

    public void BuildConnections(RunMap map)
    {
        if (map == null || map.Floors == null)
            return;
        
        var rules = new RunMapConnectionRules(this);
        for (int floor = 0; floor < map.Floors.Count - 1; floor++)
        {
            List<MapNodeData> currentFloor = map.Floors[floor];
            List<MapNodeData> nextFloor = map.Floors[floor + 1];

            if (currentFloor.Count == 0 || nextFloor.Count == 0)
                continue;

            var context = new FloorConnectionContext(currentFloor, nextFloor);
            bool isLinkToBoss = (floor + 1 == map.Floors.Count - 1) && context.NextCount == 1;

            InitializePrimaryConnections(context);
            context.RecomputeBounds();

            rules.EnsureIncomingCoverage(context, floor);
            rules.EnsureMinimumConnectedSources(context);
            if (isLinkToBoss)
            {
                rules.EnsureBossConnections(context, floor);
            }

            rules.AddBranchingConnections(context);
            rules.EnsureMinimumDistinctTargets(context);
            rules.EnsureMinimumOutgoing(context);
            rules.EnsureMinimumBranchingPerFloor(context);
        }
        EnforceColumnBranchingCoverage(map);
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
