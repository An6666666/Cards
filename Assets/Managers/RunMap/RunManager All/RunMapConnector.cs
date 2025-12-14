using System.Collections.Generic;
using System.Linq; // for Contains on IReadOnlyList
using UnityEngine;

public class RunMapConnector
{
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

            var sourceTargets = new List<int>[currentCount];
            var sourceMinTargets = new int[currentCount];
            var sourceMaxTargets = new int[currentCount];
            var prefixMaxTargets = new int[currentCount];
            var suffixMinTargets = new int[currentCount];

            var outgoingCounts = new int[currentCount];
            var incomingCounts = new int[nextCount];

            for (int i = 0; i < currentCount; i++)
            {
                outgoingConnections[currentFloor[i]] = 0;
                sourceTargets[i] = new List<int>();
                sourceMinTargets[i] = int.MaxValue;
                sourceMaxTargets[i] = -1;
            }

            int lastAssignedTarget = 0;

            for (int sourceIndex = 0; sourceIndex < currentCount; sourceIndex++)
            {
                int minimumTarget = Mathf.Clamp(lastAssignedTarget, 0, nextCount - 1);
                int targetIndex = SelectInitialTargetIndex(sourceIndex, currentCount, nextCount, minimumTarget);

                if (!ConnectNodes(
                        currentFloor,
                        nextFloor,
                        sourceIndex,
                        targetIndex,
                        outgoingConnections,
                        incomingConnections,
                        outgoingCounts,
                        incomingCounts,
                        sourceTargets,
                        sourceMinTargets,
                        sourceMaxTargets))
                {
                    continue;
                }

                lastAssignedTarget = Mathf.Max(lastAssignedTarget, targetIndex);
                RecomputeConnectionBounds(currentCount, nextCount, sourceMinTargets, sourceMaxTargets, prefixMaxTargets, suffixMinTargets);
            }

            for (int targetIndex = 0; targetIndex < nextCount; targetIndex++)
            {
                if (incomingCounts[targetIndex] > 0)
                    continue;

                int sourceIndex = SelectSourceForUnconnectedTarget(
                    currentCount,
                    nextCount,
                    targetIndex,
                    sourceTargets,
                    sourceMinTargets,
                    sourceMaxTargets,
                    prefixMaxTargets,
                    suffixMinTargets,
                    outgoingCounts,
                    incomingCounts);

                if (sourceIndex >= 0)
                {
                    ConnectNodes(
                        currentFloor,
                        nextFloor,
                        sourceIndex,
                        targetIndex,
                        outgoingConnections,
                        incomingConnections,
                        outgoingCounts,
                        incomingCounts,
                        sourceTargets,
                        sourceMinTargets,
                        sourceMaxTargets);
                }
            }

            for (int sourceIndex = 0; sourceIndex < currentCount; sourceIndex++)
            {
                int connectionsNeeded = Mathf.Max(1, Mathf.CeilToInt((float)nextCount / currentCount));
                while (outgoingCounts[sourceIndex] < connectionsNeeded)
                {
                    int targetIndex = UnityEngine.Random.Range(0, nextCount);
                    if (TryConnectNodes(
                            currentFloor[sourceIndex],
                            nextFloor[targetIndex],
                            outgoingConnections,
                            incomingConnections))
                    {
                        outgoingCounts[sourceIndex]++;
                        incomingCounts[targetIndex]++;
                        sourceTargets[sourceIndex].Add(targetIndex);
                        sourceMinTargets[sourceIndex] = Mathf.Min(sourceMinTargets[sourceIndex], targetIndex);
                        sourceMaxTargets[sourceIndex] = Mathf.Max(sourceMaxTargets[sourceIndex], targetIndex);
                        RecomputeConnectionBounds(currentCount, nextCount, sourceMinTargets, sourceMaxTargets, prefixMaxTargets, suffixMinTargets);
                    }
                }
            }
        }
    }

    private int SelectInitialTargetIndex(int sourceIndex, int currentCount, int nextCount, int minimumTarget)
    {
        float t = currentCount <= 1 ? 0.5f : (float)sourceIndex / (currentCount - 1);
        int estimatedTarget = Mathf.RoundToInt(t * (nextCount - 1));
        return Mathf.Clamp(estimatedTarget, minimumTarget, nextCount - 1);
    }

    private int SelectSourceForUnconnectedTarget(
        int currentCount,
        int nextCount,
        int targetIndex,
        List<int>[] sourceTargets,
        int[] sourceMinTargets,
        int[] sourceMaxTargets,
        int[] prefixMaxTargets,
        int[] suffixMinTargets,
        int[] outgoingCounts,
        int[] incomingCounts)
    {
        int bestSource = -1;
        int bestScore = int.MaxValue;

        for (int sourceIndex = 0; sourceIndex < currentCount; sourceIndex++)
        {
            if (outgoingCounts[sourceIndex] >= 2)
                continue;

            int currentIncoming = incomingCounts[targetIndex];
            int currentOutgoing = outgoingCounts[sourceIndex];
            int score = currentIncoming * 10 + currentOutgoing * 5;

            int minTarget = sourceMinTargets[sourceIndex];
            int maxTarget = sourceMaxTargets[sourceIndex];
            bool hasLeft = minTarget != int.MaxValue && targetIndex < minTarget;
            bool hasRight = maxTarget != -1 && targetIndex > maxTarget;

            if (hasLeft && hasRight)
                score += 20;
            else if (hasLeft || hasRight)
                score += 10;

            int lowerBound = sourceIndex > 0 ? prefixMaxTargets[sourceIndex - 1] : -1;
            int upperBound = sourceIndex < currentCount - 1 ? suffixMinTargets[sourceIndex + 1] : nextCount;

            if (targetIndex < lowerBound || targetIndex > upperBound)
                score += 30;

            if (score < bestScore)
            {
                bestScore = score;
                bestSource = sourceIndex;
            }
        }

        return bestSource;
    }

    private bool ConnectNodes(
        List<MapNodeData> currentFloor,
        List<MapNodeData> nextFloor,
        int sourceIndex,
        int targetIndex,
        Dictionary<MapNodeData, int> outgoingConnections,
        Dictionary<MapNodeData, int> incomingConnections,
        int[] outgoingCounts,
        int[] incomingCounts,
        List<int>[] sourceTargets,
        int[] sourceMinTargets,
        int[] sourceMaxTargets)
    {
        if (!TryConnectNodes(currentFloor[sourceIndex], nextFloor[targetIndex], outgoingConnections, incomingConnections))
            return false;

        outgoingCounts[sourceIndex]++;
        incomingCounts[targetIndex]++;
        sourceTargets[sourceIndex].Add(targetIndex);

        sourceMinTargets[sourceIndex] = Mathf.Min(sourceMinTargets[sourceIndex], targetIndex);
        sourceMaxTargets[sourceIndex] = Mathf.Max(sourceMaxTargets[sourceIndex], targetIndex);
        return true;
    }

    private void RecomputeConnectionBounds(
        int currentCount,
        int nextCount,
        int[] sourceMinTargets,
        int[] sourceMaxTargets,
        int[] prefixMaxTargets,
        int[] suffixMinTargets)
    {
        int runningMax = -1;
        for (int i = 0; i < currentCount; i++)
        {
            int value = sourceMaxTargets[i] == -1 ? -1 : Mathf.Max(runningMax, sourceMaxTargets[i]);
            runningMax = Mathf.Max(runningMax, sourceMaxTargets[i]);
            prefixMaxTargets[i] = value;
        }

        int runningMin = nextCount;
        for (int i = currentCount - 1; i >= 0; i--)
        {
            int value = sourceMinTargets[i] == int.MaxValue ? nextCount : Mathf.Min(runningMin, sourceMinTargets[i]);
            runningMin = Mathf.Min(runningMin, value);
            suffixMinTargets[i] = runningMin;
        }
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
        if (currentCount >= 2)
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
}
