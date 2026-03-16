using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Cards/Run/Run Map Config", fileName = "RunMapConfig")]
public class RunMapConfig : ScriptableObject
{
    [Header("Layout")]
    [SerializeField] private RunMapLayoutSettings layout = RunMapLayoutSettings.Default;

    [Header("Slot Allocation")]
    [SerializeField] private RunMapSlotAllocationSettings slotAllocation = RunMapSlotAllocationSettings.Default;

    [Header("Connections")]
    [SerializeField] private RunMapConnectionSettings connectionSettings = RunMapConnectionSettings.Default;

    [Header("Feature Flags")]
    [SerializeField] private bool enableEliteStartFloorFix;
    [SerializeField] private bool enableMinSideMergesPerFloorFix;

    public RunMapLayoutSettings Layout => layout;
    public RunMapSlotAllocationSettings SlotAllocation => slotAllocation;
    public RunMapConnectionSettings ConnectionSettings => connectionSettings;
    public IReadOnlyList<FixedFloorNodeRule> FixedFloorRules => slotAllocation.FixedFloorRules;

    public RunMapGenerationFeatureFlags GetFeatureFlags()
    {
        return new RunMapGenerationFeatureFlags(
            enableEliteStartFloorFix,
            enableMinSideMergesPerFloorFix);
    }

    public RunMapGenerator.SlotAllocationSettings GetSlotAllocationSettings()
    {
        return slotAllocation.ToSettings();
    }
}

[Serializable]
public struct RunMapLayoutSettings
{
    [SerializeField] private int floorCount;
    [SerializeField] private int minNodesPerFloor;
    [SerializeField] private int maxNodesPerFloor;
    [SerializeField, Range(0f, 1f)] private float floorVarianceChance;

    public RunMapLayoutSettings(int floorCount, int minNodesPerFloor, int maxNodesPerFloor, float floorVarianceChance)
    {
        this.floorCount = floorCount;
        this.minNodesPerFloor = minNodesPerFloor;
        this.maxNodesPerFloor = maxNodesPerFloor;
        this.floorVarianceChance = floorVarianceChance;
    }

    public int FloorCount => Mathf.Max(1, floorCount);
    public int MinNodesPerFloor => Mathf.Max(1, Mathf.Min(minNodesPerFloor, maxNodesPerFloor));
    public int MaxNodesPerFloor => Mathf.Max(MinNodesPerFloor, maxNodesPerFloor);
    public float FloorVarianceChance => Mathf.Clamp01(floorVarianceChance);

    public static RunMapLayoutSettings Default => new RunMapLayoutSettings(4, 2, 4, 0.2f);
}

[Serializable]
public struct RunMapSlotAllocationSettings
{
    [SerializeField] private int shopMin;
    [SerializeField] private int shopMax;
    [SerializeField] private int eliteMin;
    [SerializeField] private int eliteMax;
    [SerializeField] private int restMin;
    [SerializeField] private int restMax;
    [SerializeField, Range(0f, 1f)] private float eventRatioMin;
    [SerializeField, Range(0f, 1f)] private float eventRatioMax;
    [SerializeField] private RunMapNodeTypeConstraintSettings typeConstraints;
    [SerializeField] private List<FixedFloorNodeRule> fixedFloorRules;

    public RunMapSlotAllocationSettings(
        int shopMin,
        int shopMax,
        int eliteMin,
        int eliteMax,
        int restMin,
        int restMax,
        float eventRatioMin,
        float eventRatioMax,
        RunMapNodeTypeConstraintSettings typeConstraints,
        List<FixedFloorNodeRule> fixedFloorRules)
    {
        this.shopMin = shopMin;
        this.shopMax = shopMax;
        this.eliteMin = eliteMin;
        this.eliteMax = eliteMax;
        this.restMin = restMin;
        this.restMax = restMax;
        this.eventRatioMin = eventRatioMin;
        this.eventRatioMax = eventRatioMax;
        this.typeConstraints = typeConstraints;
        this.fixedFloorRules = fixedFloorRules ?? new List<FixedFloorNodeRule>();
    }

    public IReadOnlyList<FixedFloorNodeRule> FixedFloorRules => fixedFloorRules ?? new List<FixedFloorNodeRule>();

    public RunMapGenerator.SlotAllocationSettings ToSettings()
    {
        return new RunMapGenerator.SlotAllocationSettings(
            shopMin,
            shopMax,
            eliteMin,
            eliteMax,
            restMin,
            restMax,
            eventRatioMin,
            eventRatioMax,
            typeConstraints.ToConstraints(),
            FixedFloorRules);
    }

    public static RunMapSlotAllocationSettings Default => new RunMapSlotAllocationSettings(
        2,
        3,
        2,
        4,
        4,
        6,
        0.2f,
        0.25f,
        RunMapNodeTypeConstraintSettings.Default,
        new List<FixedFloorNodeRule>());
}

[Serializable]
public struct RunMapNodeTypeConstraintSettings
{
    [SerializeField] private int eliteMinFloorOffset;
    [SerializeField] private int eliteMaxFloorOffsetFromBoss;
    [SerializeField] private int shopMinFloorOffset;
    [SerializeField] private int shopMaxFloorOffsetFromBoss;
    [SerializeField] private int eventMinFloorOffset;
    [SerializeField] private int eventMaxFloorOffsetFromBoss;
    [SerializeField] private int restMinFloorOffset;
    [SerializeField] private int restMaxFloorOffsetFromBoss;
    [SerializeField] private int eliteMinGap;
    [SerializeField] private int shopMinGap;
    [SerializeField] private int restMinGap;
    [SerializeField] private bool forbidConsecutiveElite;
    [SerializeField] private bool forbidConsecutiveShop;
    [SerializeField] private bool forbidConsecutiveRest;
    [SerializeField] private int maxConsecutiveEvents;

    public RunMapNodeTypeConstraintSettings(
        int eliteMinFloorOffset,
        int eliteMaxFloorOffsetFromBoss,
        int shopMinFloorOffset,
        int shopMaxFloorOffsetFromBoss,
        int eventMinFloorOffset,
        int eventMaxFloorOffsetFromBoss,
        int restMinFloorOffset,
        int restMaxFloorOffsetFromBoss,
        int eliteMinGap,
        int shopMinGap,
        int restMinGap,
        bool forbidConsecutiveElite,
        bool forbidConsecutiveShop,
        bool forbidConsecutiveRest,
        int maxConsecutiveEvents)
    {
        this.eliteMinFloorOffset = eliteMinFloorOffset;
        this.eliteMaxFloorOffsetFromBoss = eliteMaxFloorOffsetFromBoss;
        this.shopMinFloorOffset = shopMinFloorOffset;
        this.shopMaxFloorOffsetFromBoss = shopMaxFloorOffsetFromBoss;
        this.eventMinFloorOffset = eventMinFloorOffset;
        this.eventMaxFloorOffsetFromBoss = eventMaxFloorOffsetFromBoss;
        this.restMinFloorOffset = restMinFloorOffset;
        this.restMaxFloorOffsetFromBoss = restMaxFloorOffsetFromBoss;
        this.eliteMinGap = eliteMinGap;
        this.shopMinGap = shopMinGap;
        this.restMinGap = restMinGap;
        this.forbidConsecutiveElite = forbidConsecutiveElite;
        this.forbidConsecutiveShop = forbidConsecutiveShop;
        this.forbidConsecutiveRest = forbidConsecutiveRest;
        this.maxConsecutiveEvents = maxConsecutiveEvents;
    }

    public NodeTypeConstraints ToConstraints()
    {
        return new NodeTypeConstraints(
            eliteMinFloorOffset,
            eliteMaxFloorOffsetFromBoss,
            shopMinFloorOffset,
            shopMaxFloorOffsetFromBoss,
            eventMinFloorOffset,
            eventMaxFloorOffsetFromBoss,
            restMinFloorOffset,
            restMaxFloorOffsetFromBoss,
            eliteMinGap,
            shopMinGap,
            restMinGap,
            forbidConsecutiveElite,
            forbidConsecutiveShop,
            forbidConsecutiveRest,
            maxConsecutiveEvents);
    }

    public static RunMapNodeTypeConstraintSettings Default => new RunMapNodeTypeConstraintSettings(
        eliteMinFloorOffset: 5,
        eliteMaxFloorOffsetFromBoss: 2,
        shopMinFloorOffset: 2,
        shopMaxFloorOffsetFromBoss: 3,
        eventMinFloorOffset: 1,
        eventMaxFloorOffsetFromBoss: 2,
        restMinFloorOffset: 2,
        restMaxFloorOffsetFromBoss: 1,
        eliteMinGap: 3,
        shopMinGap: 3,
        restMinGap: 2,
        forbidConsecutiveElite: true,
        forbidConsecutiveShop: true,
        forbidConsecutiveRest: true,
        maxConsecutiveEvents: 2);
}

[Serializable]
public struct FixedFloorNodeRule
{
    [SerializeField] private int floorIndex;
    [SerializeField] private MapNodeType nodeType;

    public int FloorIndex => floorIndex;
    public MapNodeType NodeType => nodeType;

    public FixedFloorNodeRule(int floorIndex, MapNodeType nodeType)
    {
        this.floorIndex = floorIndex;
        this.nodeType = nodeType;
    }
}

[Serializable]
public struct RunMapConnectionSettings
{
    [SerializeField] private int connectionNeighborWindow;
    [SerializeField] private int maxOutgoingPerNode;
    [SerializeField] private int minConnectedSourcesPerRow;
    [SerializeField] private int backtrackAllowance;
    [SerializeField] private float connectionDensity;
    [SerializeField] private int minIncomingPerTarget;
    [SerializeField] private int minDistinctSourcesToBoss;
    [SerializeField, Range(0f, 1f)] private float longLinkChance;
    [SerializeField] private int minDistinctTargetsPerFloor;
    [SerializeField] private int minBranchingNodesPerFloor;
    [SerializeField] private int maxBranchingNodesPerFloor;

    public RunMapConnectionSettings(
        int connectionNeighborWindow,
        int maxOutgoingPerNode,
        int minConnectedSourcesPerRow,
        int backtrackAllowance,
        float connectionDensity,
        int minIncomingPerTarget,
        int minDistinctSourcesToBoss,
        float longLinkChance,
        int minDistinctTargetsPerFloor,
        int minBranchingNodesPerFloor,
        int maxBranchingNodesPerFloor)
    {
        this.connectionNeighborWindow = connectionNeighborWindow;
        this.maxOutgoingPerNode = maxOutgoingPerNode;
        this.minConnectedSourcesPerRow = minConnectedSourcesPerRow;
        this.backtrackAllowance = backtrackAllowance;
        this.connectionDensity = connectionDensity;
        this.minIncomingPerTarget = minIncomingPerTarget;
        this.minDistinctSourcesToBoss = minDistinctSourcesToBoss;
        this.longLinkChance = longLinkChance;
        this.minDistinctTargetsPerFloor = minDistinctTargetsPerFloor;
        this.minBranchingNodesPerFloor = minBranchingNodesPerFloor;
        this.maxBranchingNodesPerFloor = maxBranchingNodesPerFloor;
    }

    public int ConnectionNeighborWindow => Mathf.Max(0, connectionNeighborWindow);
    public int MaxOutgoingPerNode => Mathf.Max(1, maxOutgoingPerNode);
    public int MinConnectedSourcesPerRow => Mathf.Max(1, minConnectedSourcesPerRow);
    public int BacktrackAllowance => Mathf.Max(0, backtrackAllowance);
    public float ConnectionDensity => Mathf.Max(1f, connectionDensity);
    public int MinIncomingPerTarget => Mathf.Max(1, minIncomingPerTarget);
    public int MinDistinctSourcesToBoss => Mathf.Max(1, minDistinctSourcesToBoss);
    public float LongLinkChance => Mathf.Clamp01(longLinkChance);
    public int MinDistinctTargetsPerFloor => Mathf.Max(1, minDistinctTargetsPerFloor);
    public int MinBranchingNodesPerFloor => Mathf.Max(0, minBranchingNodesPerFloor);
    public int MaxBranchingNodesPerFloor => Mathf.Max(MinBranchingNodesPerFloor, maxBranchingNodesPerFloor);

    public static RunMapConnectionSettings Default => new RunMapConnectionSettings(
        2,
        2,
        3,
        1,
        1.2f,
        1,
        3,
        0.05f,
        2,
        0,
        2);
}
