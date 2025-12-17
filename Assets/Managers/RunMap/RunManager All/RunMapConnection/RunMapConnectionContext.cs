using System.Collections.Generic;
using UnityEngine;

internal sealed class FloorConnectionContext
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

    public void RecomputeBounds()
    {
        int currentCount = CurrentCount;
        int nextCount = NextCount;

        int runningMax = -1;
        for (int i = 0; i < currentCount; i++)
        {
            if (MaxTargets[i] >= 0)
            {
                runningMax = Mathf.Max(runningMax, MaxTargets[i]);
            }
            PrefixMaxTargets[i] = runningMax;
        }

        int runningMin = nextCount;
        for (int i = currentCount - 1; i >= 0; i--)
        {
            if (MinTargets[i] != int.MaxValue)
            {
                runningMin = Mathf.Min(runningMin, MinTargets[i]);
            }
            SuffixMinTargets[i] = runningMin;
        }
    }
}