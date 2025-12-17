using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class RunMapConnectionRules
{
    private readonly RunMapConnector connector;

    public RunMapConnectionRules(RunMapConnector connector)
    {
        this.connector = connector;
    }

    public void EnsureIncomingCoverage(FloorConnectionContext context, int floor)
    {
        for (int targetIndex = 0; targetIndex < context.NextCount; targetIndex++)
        {
            if (context.IncomingCounts[targetIndex] >= connector.MinIncomingPerTarget)
                continue;

            int sourceIndex = connector.SelectSourceForUnconnectedTarget(floor, targetIndex, context);
            if (sourceIndex >= 0 && connector.ConnectAndTrack(context, sourceIndex, targetIndex))
            {
                context.RecomputeBounds();
            }
        }
    }

    public void EnsureMinimumConnectedSources(FloorConnectionContext context)
    {
        int connectedSources = context.OutgoingConnections.Count(pair => pair.Value > 0);
        int requiredSources = context.CurrentCount >= 3
            ? Mathf.Min(connector.MinConnectedSourcesPerRow, context.CurrentCount)
            : Mathf.Min(context.CurrentCount, connector.MinConnectedSourcesPerRow);
        if (connectedSources >= requiredSources)
            return;

        for (int sourceIndex = 0; sourceIndex < context.CurrentCount && connectedSources < requiredSources; sourceIndex++)
        {
            if (context.OutgoingConnections[context.CurrentFloor[sourceIndex]] > 0)
                continue;

            connector.GetAnchorRange(sourceIndex, context.CurrentCount, context.NextCount, out int anchorTarget, out int anchorLower, out int anchorUpper);

            int lowerBound = sourceIndex > 0 ? context.PrefixMaxTargets[sourceIndex - 1] + 1 : 0;
            int upperBound = sourceIndex < context.CurrentCount - 1
                ? Mathf.Min(context.NextCount - 1, context.SuffixMinTargets[sourceIndex + 1] - 1)
                : context.NextCount - 1;

            lowerBound = Mathf.Max(lowerBound, anchorLower);
            upperBound = Mathf.Min(upperBound, anchorUpper);

            if (lowerBound > upperBound)
                continue;

            int candidate = Mathf.Clamp(anchorTarget, lowerBound, upperBound);
            if (connector.ConnectAndTrack(context, sourceIndex, candidate))
            {
                connectedSources++;
                context.RecomputeBounds();
            }
        }
    }

    public void AddBranchingConnections(FloorConnectionContext context)
    {
        for (int sourceIndex = 0; sourceIndex < context.CurrentCount; sourceIndex++)
        {
            int desiredConnections = Mathf.CeilToInt(((float)context.NextCount / context.CurrentCount) * connector.ConnectionDensity) + 1;
            desiredConnections = Mathf.Clamp(desiredConnections, 2, connector.MaxOutgoingPerNode);
            if (context.OutgoingConnections[context.CurrentFloor[sourceIndex]] >= desiredConnections)
                continue;

            connector.GetAnchorRange(sourceIndex, context.CurrentCount, context.NextCount, out int anchorTarget, out int anchorLower, out int anchorUpper);
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

                if (UnityEngine.Random.value < connector.LongLinkChance)
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
                    int candidate = RunMapConnectionSampling.SampleByWeight(candidates, weights);
                    if (connector.ConnectAndTrack(context, sourceIndex, candidate))
                    {
                        context.RecomputeBounds();
                        break;
                    }
                }
            }
        }
    }

    public void EnsureMinimumDistinctTargets(FloorConnectionContext context)
    {
        int nextCount = context.NextFloor.Count;
        int currentCount = context.CurrentFloor.Count;
        int distinctTargets = context.IncomingCounts.Count(count => count >= connector.MinIncomingPerTarget);

        if (distinctTargets >= connector.MinDistinctTargetsPerFloor)
            return;

        var orderedSources = context.OutgoingConnections
            .OrderBy(pair => pair.Value)
            .ThenBy(pair => context.CurrentFloor.IndexOf(pair.Key))
            .Select(pair => context.CurrentFloor.IndexOf(pair.Key))
            .ToList();

        foreach (int sourceIndex in orderedSources)
        {
            if (distinctTargets >= connector.MinDistinctTargetsPerFloor)
                break;

            if (context.OutgoingConnections[context.CurrentFloor[sourceIndex]] >= connector.MaxOutgoingPerNode)
                continue;

            connector.GetAnchorRange(sourceIndex, currentCount, nextCount, out int anchor, out int anchorMin, out int anchorMax);

            int lowerBound = sourceIndex > 0 ? context.PrefixMaxTargets[sourceIndex - 1] + 1 : 0;
            int upperBound = sourceIndex < currentCount - 1 ? context.SuffixMinTargets[sourceIndex + 1] - 1 : nextCount - 1;
            lowerBound = Mathf.Max(lowerBound, anchorMin);
            upperBound = Mathf.Min(upperBound, anchorMax);

            if (lowerBound > upperBound)
                continue;

            var candidates = new List<int>();
            for (int t = lowerBound; t <= upperBound; t++)
            {
                if (context.IncomingCounts[t] >= connector.MinIncomingPerTarget)
                    continue;

                candidates.Add(t);
            }

            if (candidates.Count == 0)
                continue;

            int bestTarget = candidates.OrderBy(t => Mathf.Abs(t - anchor)).First();
            if (connector.ConnectAndTrack(context, sourceIndex, bestTarget))
            {
                distinctTargets = context.IncomingCounts.Count(c => c >= connector.MinIncomingPerTarget);
                context.RecomputeBounds();
            }
        }
    }

    public void EnsureMinimumOutgoing(FloorConnectionContext context)
    {
        int currentCount = context.CurrentFloor.Count;
        int nextCount = context.NextFloor.Count;

        for (int sourceIndex = 0; sourceIndex < currentCount; sourceIndex++)
        {
            if (context.OutgoingConnections[context.CurrentFloor[sourceIndex]] > 0)
                continue;

            connector.GetAnchorRange(sourceIndex, currentCount, nextCount, out int anchor, out int anchorMin, out int anchorMax);

            int lowerBound = sourceIndex > 0 ? context.PrefixMaxTargets[sourceIndex - 1] + 1 : 0;
            int upperBound = sourceIndex < currentCount - 1
                ? Mathf.Min(nextCount - 1, context.SuffixMinTargets[sourceIndex + 1] - 1)
                : nextCount - 1;

            lowerBound = Mathf.Max(lowerBound, anchorMin);
            upperBound = Mathf.Min(upperBound, anchorMax);

            if (lowerBound > upperBound)
                continue;

            int targetIndex = Mathf.Clamp(anchor, lowerBound, upperBound);
            connector.ConnectAndTrack(context, sourceIndex, targetIndex);
        }
    }

    public void EnsureBossConnections(FloorConnectionContext context, int floorIndex)
    {
        if (context.NextFloor.Count == 0)
            return;

        int currentCount = context.CurrentFloor.Count;
        int nextCount = context.NextFloor.Count;
        int bossTarget = 0;
        int required = Mathf.Max(0, Mathf.Min(connector.MinDistinctSourcesToBoss, currentCount) - context.IncomingCounts[bossTarget]);
        if (required <= 0)
            return;

        for (int pass = 0; pass < required; pass++)
        {
            int bestSource = -1;
            int bestScore = int.MaxValue;

            for (int sourceIndex = 0; sourceIndex < currentCount; sourceIndex++)
            {
                int outgoing = context.OutgoingConnections.TryGetValue(context.CurrentFloor[sourceIndex], out int value) ? value : 0;
                if (outgoing >= connector.MaxOutgoingPerNode)
                    continue;

                connector.GetAnchorRange(sourceIndex, currentCount, nextCount, out int anchor, out int anchorMin, out int anchorMax);

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

            if (connector.ConnectAndTrack(context, bestSource, bossTarget))
            {
                context.RecomputeBounds();
            }
            else
            {
                Debug.LogWarning($"Failed to enforce boss connection on floor {floorIndex} from source {bestSource}");
            }
        }
    }
}