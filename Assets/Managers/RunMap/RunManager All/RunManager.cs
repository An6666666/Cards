using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum MapNodeType
{
    Battle,
    EliteBattle,
    Shop,
    Rest,
    Event,
    Boss
}

[Serializable]
public class MapNodeData
{
    [SerializeField] private string nodeId;
    [SerializeField] private MapNodeType nodeType;
    [SerializeField] private int floorIndex;
    [SerializeField] private bool isCompleted;
    [SerializeField] private RunEncounterDefinition encounter;
    [SerializeField] private RunEventDefinition eventDefinition;
    [SerializeField] private ShopInventoryDefinition shopInventory;
    [SerializeField] private Sprite iconOverride;
    [NonSerialized] private List<MapNodeData> nextNodes = new List<MapNodeData>();
    [NonSerialized] private readonly List<CardBase> shopCardOffers = new List<CardBase>();
    [NonSerialized] private readonly List<RelicBase> shopRelicOffers = new List<RelicBase>();
    [NonSerialized] private bool shopOffersGenerated;
    public MapNodeData(string id, MapNodeType type, int floor)
    {
        nodeId = id;
        nodeType = type;
        floorIndex = floor;
    }

    public string NodeId => nodeId;
    public MapNodeType NodeType => nodeType;
    public int FloorIndex => floorIndex;
    public bool IsCompleted => isCompleted;
    public RunEncounterDefinition Encounter => encounter;
    public RunEventDefinition Event => eventDefinition;
    public ShopInventoryDefinition ShopInventory => shopInventory;
    public Sprite IconOverride => iconOverride;
    public IReadOnlyList<MapNodeData> NextNodes => nextNodes;
    public IReadOnlyList<CardBase> ShopCardOffers => shopCardOffers;
    public IReadOnlyList<RelicBase> ShopRelicOffers => shopRelicOffers;
    public bool ShopOffersGenerated => shopOffersGenerated;
    public bool IsBoss => nodeType == MapNodeType.Boss;

    public void SetEncounter(RunEncounterDefinition definition)
    {
        encounter = definition;
    }

    public void SetEvent(RunEventDefinition definition)
    {
        eventDefinition = definition;
    }

    public void SetShop(ShopInventoryDefinition definition)
    {
        shopInventory = definition;
    }

    public void SetIconOverride(Sprite sprite)
    {
        iconOverride = sprite;
    }

    public void SetShopOfferState(bool generated, IEnumerable<CardBase> cards, IEnumerable<RelicBase> relics)
    {
        shopOffersGenerated = generated;

        shopCardOffers.Clear();
        if (cards != null)
        {
            foreach (CardBase card in cards)
            {
                if (card != null)
                {
                    shopCardOffers.Add(card);
                }
            }
        }

        shopRelicOffers.Clear();
        if (relics != null)
        {
            foreach (RelicBase relic in relics)
            {
                if (relic != null)
                {
                    shopRelicOffers.Add(relic);
                }
            }
        }
    }

    public void SetNodeType(MapNodeType type)
    {
        nodeType = type;
    }

    public void MarkCompleted()
    {
        isCompleted = true;
    }

    public void ResetProgress()
    {
        isCompleted = false;
    }

    public void AddNextNode(MapNodeData node)
    {
        if (node == null || nextNodes.Contains(node))
            return;
        nextNodes.Add(node);
    }

    public void ClearNextNodes()
    {
        nextNodes.Clear();
    }
}

public partial class RunManager : MonoBehaviour
{
    private const int RestHealAmount = 30;

    public static RunManager Instance { get; private set; }

    [Header("Config Assets")]
    [SerializeField] private RunMapConfig mapConfig;

    [Header("Scene Names")]
    [SerializeField] private string runSceneName = "RunScene";
    [SerializeField] private string battleSceneName = "BattleScene";
    [SerializeField] private string shopSceneName = "ShopScene";
    [SerializeField] private string deathReturnSceneName = "";
    [SerializeField, Min(0f)] private float nodeEnterDelaySeconds = 0f;
    [SerializeField] private bool allowAnyNodeEntry = false;
    [SerializeField] private RunEventUIManager eventUIManager;

    [Header("Tutorial")]
    [SerializeField] private bool tutorialRun;
    [SerializeField, Tooltip("Deck asset used only when this run is marked as a tutorial run.")]
    private TutorialStartingDeckDefinition tutorialStartingDeckDefinition;
    [SerializeField, Tooltip("Fallback cards used only when this run is marked as a tutorial run and no tutorial deck asset is assigned.")]
    private List<CardBase> tutorialStartingDeck = new List<CardBase>();

    [Header("Starting Player Snapshot")]
    [SerializeField, Min(1)] private int defaultPlayerMaxHP = 80;
    [SerializeField] private int defaultPlayerCurrentHP = 80;
    [SerializeField, Min(0)] private int defaultPlayerGold = 40;
    [SerializeField] private StartingDeckDefinition defaultStartingDeckDefinition;

    [Header("Map Generation")]
    [SerializeField] private int floorCount = 4;
    [SerializeField] private int minNodesPerFloor = 2;
    [SerializeField] private int maxNodesPerFloor = 4;
    [Obsolete("Slot-based generation no longer uses single-node rates")]
    [SerializeField, Range(0f, 1f)] private float eventRate = 0.2f;
    [Obsolete("Slot-based generation no longer uses single-node rates")]
    [SerializeField, Range(0f, 1f)] private float shopRate = 0.15f;
    [Obsolete("Slot-based generation no longer uses single-node rates")]
    [SerializeField, Range(0f, 1f)] private float eliteBattleRate = 0.1f;
    [SerializeField] private int shopMin = 2;
    [SerializeField] private int shopMax = 3;
    [SerializeField] private int eliteMin = 2;
    [SerializeField] private int eliteMax = 4;
    [SerializeField] private int restMin = 4;
    [SerializeField] private int restMax = 6;
    [SerializeField, Range(0f, 1f)] private float eventRatioMin = 0.2f;
    [SerializeField, Range(0f, 1f)] private float eventRatioMax = 0.25f;
    [SerializeField] private EncounterPool encounterPool;
    [SerializeField] private EncounterPool eliteEncounterPool;
    [SerializeField] private RunEncounterDefinition bossEncounter;
    [SerializeField] private ShopInventoryDefinition defaultShopInventory;
    [SerializeField] private List<RunEventDefinition> eventPool = new List<RunEventDefinition>();
    [SerializeField] private bool autoGenerateOnStart = true;
    [SerializeField] private bool forceRestFloorBeforeBoss = true;

    [Header("Map Connection Tuning")]
    [SerializeField] private int connectionNeighborWindow = 2;
    [SerializeField] private int maxOutgoingPerNode = 4;
    [SerializeField, Range(1f, 2.5f)] private float connectionDensity = 1.5f;
    [SerializeField, Range(1, 3)] private int minIncomingPerTarget = 1;
    [SerializeField, Range(2, 4)] private int minDistinctSourcesToBoss = 3;
    [SerializeField, Range(0f, 0.4f)] private float longLinkChance = 0.2f;
    [SerializeField, Range(0f, 1f)] private float floorVarianceChance = 0.2f;
    [SerializeField, Range(1, 4)] private int minConnectedSourcesPerRow = 3;
    [SerializeField, Range(1, 3)] private int minBranchingNodesPerFloor = 1;
    [SerializeField, Range(1, 3)] private int maxBranchingNodesPerFloor = 3;
    [SerializeField] private int backtrackAllowance = 1;
    [SerializeField] private int minDistinctTargetsPerFloor = 2;

    private readonly List<List<MapNodeData>> mapFloors = new List<List<MapNodeData>>();
    private MapNodeData currentNode;
    private MapNodeData activeNode;
    private bool runCompleted;
    private Player player;
    private PlayerRunSnapshot initialPlayerSnapshot;
    private PlayerRunSnapshot currentRunSnapshot;
    private int runSequenceId;
    private readonly HashSet<string> guideFlags = new HashSet<string>(StringComparer.Ordinal);
    private bool suppressDefaultShopEntryDialogueOnce;
    private bool suppressAutosave;
    public PlayerRunSnapshot CurrentRunSnapshot => currentRunSnapshot;
    public int RunSequenceId => runSequenceId;


    private RunMapGenerator mapGenerator;
    private RunMapConnector mapConnector;
    private RunSceneRouter sceneRouter;
    private RunEventResolver eventResolver;
    private Coroutine pendingNodeTransitionCoroutine;
    public IReadOnlyList<IReadOnlyList<MapNodeData>> MapFloors => mapFloors;
    public MapNodeData CurrentNode => currentNode;
    public MapNodeData ActiveNode => activeNode;
    public bool RunCompleted => runCompleted;
    public ShopInventoryDefinition DefaultShopInventory => defaultShopInventory;
    public Player RegisteredPlayer => player;
    public bool IsTutorialRun => tutorialRun;
    public bool AllowAnyNodeEntry => allowAnyNodeEntry;

    public event Action<IReadOnlyList<IReadOnlyList<MapNodeData>>> MapGenerated;
    public event Action MapStateChanged;
    public event Action<MapNodeData> NodeEntered;
    public event Action<MapNodeData> NodeCompleted;
    public event Action<PlayerRunSnapshot> RunSnapshotChanged;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildMapSystemsFromConfig(mapConfig);

        sceneRouter = new RunSceneRouter(runSceneName, battleSceneName, shopSceneName, deathReturnSceneName);
        eventResolver = new RunEventResolver(eventUIManager);
    }
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void DestroyInstance()
    {
        if (Instance == null)
            return;

        Destroy(Instance.gameObject);
    }
    private void Start()
    {
        if (TryResumeSavedRunIfRequested())
        {
            return;
        }

        if (autoGenerateOnStart)
        {
            GenerateNewRun();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveCurrentProgress();
        }
    }

    private void OnApplicationQuit()
    {
        SaveCurrentProgress();
    }

    public void RegisterPlayer(Player newPlayer)
    {
        if (newPlayer == null)
            return;

        player = newPlayer;
        eventResolver.Player = newPlayer;

        if (initialPlayerSnapshot == null)
        {
            initialPlayerSnapshot = HasTutorialStartingDeck()
                ? BuildDefaultStartingSnapshot()
                : PlayerRunSnapshot.Capture(newPlayer);
            if (currentRunSnapshot == null)
            {
                currentRunSnapshot = initialPlayerSnapshot.Clone();
            }
        }

        eventResolver.InitialPlayerSnapshot = initialPlayerSnapshot;
        eventResolver.CurrentRunSnapshot = currentRunSnapshot;

        if (currentRunSnapshot != null)
        {
            if (IsSnapshotDeckEmpty(currentRunSnapshot))
            {
                PlayerRunSnapshot playerSnapshot = PlayerRunSnapshot.Capture(newPlayer);
                currentRunSnapshot.deck = playerSnapshot.deck;
                currentRunSnapshot.exhaustPile = playerSnapshot.exhaustPile;
            }

            ApplySnapshotToPlayer(newPlayer, currentRunSnapshot);
            RaiseRunSnapshotChanged();
        }

        SaveCurrentProgress();
    }

    private static bool IsSnapshotDeckEmpty(PlayerRunSnapshot snapshot)
    {
        return snapshot == null || snapshot.deck == null || snapshot.deck.Count == 0;
    }

    public void GenerateNewRun()
    {
        runSequenceId++;
        BattleEndSummaryStore.ResetRunTotals();
        ResetGuideState();
        EnsureStartingRunSnapshot();

        RunMapGenerator.SlotAllocationSettings slotSettings = GetActiveSlotSettings();
        RunMapLayoutSettings layoutSettings = GetActiveLayoutSettings();

        RunMap map = mapGenerator.Generate(
            layoutSettings.FloorCount,
            layoutSettings.MinNodesPerFloor,
            layoutSettings.MaxNodesPerFloor,
            encounterPool,
            eliteEncounterPool,
            bossEncounter,
            defaultShopInventory,
            eventPool,
            slotSettings);

        mapConnector.BuildConnections(map);
        Debug.Log($"[RunMap] FixedFloorRules = {slotSettings.FixedFloorRules?.Count ?? 0}");
        mapGenerator.AllocateSlotsAcrossMapAfterConnections(
            map,
            encounterPool,
            eliteEncounterPool,
            bossEncounter,
            defaultShopInventory,
            eventPool,
            slotSettings);
        ApplyRestFloorBeforeBossRule(map);

        mapFloors.Clear();
        mapFloors.AddRange(map.Floors);
        currentNode = null;
        activeNode = null;
        runCompleted = false;

        MapGenerated?.Invoke(mapFloors);
        RaiseRunSnapshotChanged();
        MapStateChanged?.Invoke();
        SaveCurrentProgress();
    }

    private void ApplyRestFloorBeforeBossRule(RunMap map)
    {
        if (!forceRestFloorBeforeBoss || tutorialRun || map?.Floors == null || map.Floors.Count < 2)
        {
            return;
        }

        int bossFloorIndex = map.Floors.Count - 1;
        int restFloorIndex = bossFloorIndex - 1;
        List<MapNodeData> restFloor = map.Floors[restFloorIndex];
        if (restFloor == null)
        {
            return;
        }

        for (int i = 0; i < restFloor.Count; i++)
        {
            MapNodeData node = restFloor[i];
            if (node == null)
            {
                continue;
            }

            node.SetNodeType(MapNodeType.Rest);
            node.SetEncounter(null);
            node.SetEvent(null);
            node.SetShop(null);
            node.SetShopOfferState(false, null, null);
            node.SetIconOverride(null);
        }
    }

    private void EnsureStartingRunSnapshot()
    {
        if (player != null)
        {
            initialPlayerSnapshot = HasTutorialStartingDeck()
                ? BuildDefaultStartingSnapshot()
                : PlayerRunSnapshot.Capture(player);
            currentRunSnapshot = initialPlayerSnapshot.Clone();
            eventResolver.InitialPlayerSnapshot = initialPlayerSnapshot;
            eventResolver.CurrentRunSnapshot = currentRunSnapshot;
            ApplySnapshotToPlayer(player, currentRunSnapshot);
            return;
        }

        PlayerRunSnapshot snapshot = BuildDefaultStartingSnapshot();
        initialPlayerSnapshot = snapshot;
        currentRunSnapshot = snapshot.Clone();
        eventResolver.InitialPlayerSnapshot = initialPlayerSnapshot;
        eventResolver.CurrentRunSnapshot = currentRunSnapshot;
    }

    private PlayerRunSnapshot BuildDefaultStartingSnapshot()
    {
        int maxHP = Mathf.Max(1, defaultPlayerMaxHP);
        int currentHP = defaultPlayerCurrentHP > 0
            ? Mathf.Clamp(defaultPlayerCurrentHP, 0, maxHP)
            : maxHP;

        return new PlayerRunSnapshot
        {
            maxHP = maxHP,
            currentHP = currentHP,
            gold = Mathf.Max(0, defaultPlayerGold),
            deck = BuildDefaultStartingDeck(),
            relics = new List<RelicBase>(),
            exhaustPile = new List<CardBase>()
        };
    }

    private List<CardBase> BuildDefaultStartingDeck()
    {
        if (HasTutorialStartingDeck())
        {
            return BuildTutorialStartingDeck();
        }

        StartingDeckDefinition definition = defaultStartingDeckDefinition;
        if (definition == null)
        {
            definition = Resources.Load<StartingDeckDefinition>("StartingDeckDefinition");
        }

        if (definition != null &&
            StartingDeckSelection.TryGetRunSelectedElements(out IReadOnlyList<ElementType> selectedElements))
        {
            List<CardBase> builtDeck = definition.BuildDeck(selectedElements);
            if (builtDeck != null && builtDeck.Count > 0)
            {
                return new List<CardBase>(builtDeck.Where(PlayerRunSnapshot.ShouldPersistCard));
            }
        }

        return new List<CardBase>();
    }

    private bool HasTutorialStartingDeck()
    {
        if (!tutorialRun)
        {
            return false;
        }

        if (tutorialStartingDeckDefinition != null && tutorialStartingDeckDefinition.HasCards)
        {
            return true;
        }

        return tutorialStartingDeck != null && tutorialStartingDeck.Any(PlayerRunSnapshot.ShouldPersistCard);
    }

    private List<CardBase> BuildTutorialStartingDeck()
    {
        if (tutorialStartingDeckDefinition != null)
        {
            List<CardBase> assetDeck = tutorialStartingDeckDefinition.BuildDeck();
            if (assetDeck.Count > 0)
            {
                return assetDeck;
            }
        }

        return tutorialStartingDeck != null
            ? new List<CardBase>(tutorialStartingDeck.Where(PlayerRunSnapshot.ShouldPersistCard))
            : new List<CardBase>();
    }

    private void ApplyConfigNodeOverrides()
    {
        IReadOnlyList<FixedFloorNodeRule> rules = mapConfig != null ? mapConfig.FixedFloorRules : null;
        if (rules == null || rules.Count == 0 || mapFloors.Count == 0)
            return;

        for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            FixedFloorNodeRule rule = rules[ruleIndex];
            if (rule.FloorIndex < 0 || rule.FloorIndex >= mapFloors.Count)
                continue;

            List<MapNodeData> floor = mapFloors[rule.FloorIndex];
            if (floor == null)
                continue;

            for (int nodeIndex = 0; nodeIndex < floor.Count; nodeIndex++)
            {
                MapNodeData node = floor[nodeIndex];
                if (node != null && node.NodeType == rule.NodeType)
                {
                    node.SetIconOverride(rule.GetIconOverride(nodeIndex));

                    RunEncounterDefinition encounterOverride = rule.GetEncounterOverride(nodeIndex);
                    if (encounterOverride != null)
                    {
                        node.SetEncounter(encounterOverride);
                    }
                }
            }
        }
    }

    public IReadOnlyList<MapNodeData> GetAvailableNodes()
    {
        if (activeNode != null)
            return Array.Empty<MapNodeData>();

        if (mapFloors.Count == 0)
            return Array.Empty<MapNodeData>();

        if (allowAnyNodeEntry)
            return mapFloors
                .Where(floor => floor != null)
                .SelectMany(floor => floor)
                .Where(node => node != null)
                .ToList();

        if (currentNode == null)
            return mapFloors[0];

        if (currentNode.NextNodes.Count == 0)
            return Array.Empty<MapNodeData>();

        return currentNode.NextNodes;
    }

    public bool TryEnterNode(MapNodeData node)
    {
        if (node == null)
            return false;
        if (activeNode != null)
            return false;
        if (!IsNodeSelectable(node))
            return false;

        activeNode = node;
        NodeEntered?.Invoke(node);
        if (pendingNodeTransitionCoroutine != null)
        {
            StopCoroutine(pendingNodeTransitionCoroutine);
        }

        pendingNodeTransitionCoroutine = StartCoroutine(HandleNodeTransition(node));
        MapStateChanged?.Invoke();
        SaveCurrentProgress();
        return true;
    }

    private System.Collections.IEnumerator HandleNodeTransition(MapNodeData node)
    {
        float delaySeconds = Mathf.Max(0f, nodeEnterDelaySeconds);
        if (delaySeconds > 0f)
        {
            yield return new WaitForSeconds(delaySeconds);
        }

        if (activeNode != node)
        {
            pendingNodeTransitionCoroutine = null;
            yield break;
        }
        if (node.NodeType == MapNodeType.Event)
        {
            eventResolver.CurrentRunSnapshot = currentRunSnapshot;
            eventResolver.InitialPlayerSnapshot = initialPlayerSnapshot;
            eventResolver.EventUIManager = ResolveEventUIManager();
            eventResolver.HandleEventNode(node, () =>
            {
                CompleteActiveNodeWithoutBattle();
                currentRunSnapshot = eventResolver.CurrentRunSnapshot;
                RaiseRunSnapshotChanged();
                SaveCurrentProgress();
            });
        }
        else if (node.NodeType == MapNodeType.Rest)
        {
            ApplyRestEffect();
            CompleteActiveNodeWithoutBattle();
        }
        else
        {
            sceneRouter.LoadSceneForNode(node);
        }
        pendingNodeTransitionCoroutine = null;
    }

    private RunEventUIManager ResolveEventUIManager()
    {
        if (eventUIManager != null)
            return eventUIManager;

        eventUIManager = FindObjectOfType<RunEventUIManager>(includeInactive: true);
        return eventUIManager;
    }

    public bool IsNodeSelectable(MapNodeData node)
    {
        if (node == null)
            return false;

        if (allowAnyNodeEntry)
            return mapFloors.Any(floor => floor != null && floor.Contains(node));

        if (node.IsCompleted)
            return false;

        if (currentNode == null)
            return node.FloorIndex == 0;

        return currentNode.NextNodes.Contains(node);
    }

    public void HandleBattleVictory()
    {
        if (activeNode == null)
            return;

        if (activeNode.IsCompleted && !allowAnyNodeEntry)
            return;

        activeNode.MarkCompleted();
        currentNode = activeNode;
        if (activeNode.IsBoss)
        {
            runCompleted = true;
        }
        NodeCompleted?.Invoke(activeNode);
        MapStateChanged?.Invoke();
        SaveCurrentProgress();
    }

    public void CompleteActiveNodeWithoutBattle()
    {
        if (activeNode == null)
            return;

        activeNode.MarkCompleted();
        currentNode = activeNode;
        activeNode = null;
        NodeCompleted?.Invoke(currentNode);
        MapStateChanged?.Invoke();
        SaveCurrentProgress();
    }

    public void HandleBattleDefeat()
    {
        suppressAutosave = true;
        RunProgressPersistence.ClearSavedProgress();
        ResetRun();
        sceneRouter.LoadDeathReturnScene();
    }

    public void ReturnToRunSceneFromBattle(bool suppressTitleSummary = false)
    {
        SyncPlayerRunState();

        if (runCompleted)
        {
            suppressAutosave = true;
            RunProgressPersistence.ClearSavedProgress();
            ResetRun();
            activeNode = null;
            if (suppressTitleSummary)
            {
                BattleEndSummaryStore.ClearLastSummary();
            }
            sceneRouter.LoadDeathReturnScene();
            MapStateChanged?.Invoke();
            return;
        }

        activeNode = null;
        SaveCurrentProgress();
        sceneRouter.LoadRunScene();
        MapStateChanged?.Invoke();
    }

    public void SyncPlayerRunState()
    {
        if (player == null)
            return;

        currentRunSnapshot = PlayerRunSnapshot.Capture(player);
        eventResolver.CurrentRunSnapshot = currentRunSnapshot;
        RaiseRunSnapshotChanged();
        SaveCurrentProgress();
    }

    public void ResetRun()
    {
        runCompleted = false;
        activeNode = null;
        currentNode = null;

        if (initialPlayerSnapshot != null)
        {
            currentRunSnapshot = initialPlayerSnapshot.Clone();
            eventResolver.CurrentRunSnapshot = currentRunSnapshot;
            ApplySnapshotToPlayer(player, currentRunSnapshot);
            RaiseRunSnapshotChanged();
        }

        GenerateNewRun();
    }

    public bool HasGuideFlag(string flag)
    {
        if (string.IsNullOrWhiteSpace(flag))
            return false;

        return guideFlags.Contains(flag.Trim());
    }

    public void MarkGuideFlag(string flag)
    {
        if (string.IsNullOrWhiteSpace(flag))
            return;

        guideFlags.Add(flag.Trim());
        SaveCurrentProgress();
    }

    public void RequestDefaultShopEntryDialogueSuppression()
    {
        suppressDefaultShopEntryDialogueOnce = true;
        SaveCurrentProgress();
    }

    public bool ConsumeDefaultShopEntryDialogueSuppression()
    {
        bool shouldSuppress = suppressDefaultShopEntryDialogueOnce;
        suppressDefaultShopEntryDialogueOnce = false;
        return shouldSuppress;
    }

    public void SaveProgressForTitleReturn()
    {
        SaveCurrentProgress();
    }

    private void ApplySnapshotToPlayer(Player target, PlayerRunSnapshot snapshot)
    {
        if (target == null || snapshot == null)
            return;

        snapshot.ApplyTo(target);
    }
    private void ApplyRestEffect()
    {
        if (player != null)
        {
            int healedHP = Mathf.Clamp(player.currentHP + RestHealAmount, 0, player.maxHP);
            player.currentHP = healedHP;
            currentRunSnapshot = PlayerRunSnapshot.Capture(player);
            eventResolver.CurrentRunSnapshot = currentRunSnapshot;
            RaiseRunSnapshotChanged();
            SaveCurrentProgress();
            return;
        }

        EnsureCurrentRunSnapshot();
        if (currentRunSnapshot == null)
            return;

        int clampedHp = Mathf.Clamp(currentRunSnapshot.currentHP + RestHealAmount, 0, currentRunSnapshot.maxHP);
        currentRunSnapshot.currentHP = clampedHp;
        eventResolver.CurrentRunSnapshot = currentRunSnapshot;
        RaiseRunSnapshotChanged();
        SaveCurrentProgress();
    }

    private void EnsureCurrentRunSnapshot()
    {
        if (currentRunSnapshot != null)
            return;

        if (player != null)
        {
            currentRunSnapshot = PlayerRunSnapshot.Capture(player);
            return;
        }

        if (initialPlayerSnapshot != null)
        {
            currentRunSnapshot = initialPlayerSnapshot.Clone();
            return;
        }

        currentRunSnapshot = new PlayerRunSnapshot
        {
            deck = new List<CardBase>(),
            relics = new List<RelicBase>(),
            exhaustPile = new List<CardBase>()
        };
    }

    private void ResetGuideState()
    {
        guideFlags.Clear();
        suppressDefaultShopEntryDialogueOnce = false;
    }

    private void RaiseRunSnapshotChanged()
    {
        RunSnapshotChanged?.Invoke(currentRunSnapshot);
    }
    public void ApplyMapConfig(RunMapConfig config, bool regenerate = true)
    {
        mapConfig = config;
        BuildMapSystemsFromConfig(mapConfig);
        if (regenerate)
        {
            GenerateNewRun();
        }
    }

    private RunMapLayoutSettings GetActiveLayoutSettings()
    {
        if (mapConfig != null)
            return mapConfig.Layout;

        return new RunMapLayoutSettings(
            floorCount,
            minNodesPerFloor,
            maxNodesPerFloor,
            floorVarianceChance);
    }

    private RunMapConnectionSettings GetActiveConnectionSettings()
    {
        if (mapConfig != null)
            return mapConfig.ConnectionSettings;

        return new RunMapConnectionSettings(
            connectionNeighborWindow,
            maxOutgoingPerNode,
            minConnectedSourcesPerRow,
            backtrackAllowance,
            connectionDensity,
            minIncomingPerTarget,
            minDistinctSourcesToBoss,
            longLinkChance,
            minDistinctTargetsPerFloor,
            minBranchingNodesPerFloor,
            maxBranchingNodesPerFloor);
    }

    private RunMapGenerator.SlotAllocationSettings GetActiveSlotSettings()
    {
        if (mapConfig != null)
            return mapConfig.GetSlotAllocationSettings();

        return new RunMapGenerator.SlotAllocationSettings(
            shopMin,
            shopMax,
            eliteMin,
            eliteMax,
            restMin,
            restMax,
            eventRatioMin,
            eventRatioMax,
            NodeTypeConstraints.Default);
    }

    private void BuildMapSystemsFromConfig(RunMapConfig config)
    {
        RunMapGenerationFeatureFlags generationFeatureFlags = config != null
            ? config.GetFeatureFlags()
            : RunMapGenerationFeatureFlags.Default;

        RunMapLayoutSettings layoutSettings = config != null
            ? config.Layout
            : new RunMapLayoutSettings(
                floorCount,
                minNodesPerFloor,
                maxNodesPerFloor,
                floorVarianceChance);

        RunMapConnectionSettings connectionSettings = GetActiveConnectionSettings();

        mapGenerator = new RunMapGenerator(layoutSettings.FloorVarianceChance, featureFlags: generationFeatureFlags);
        mapConnector = new RunMapConnector(
            connectionSettings.ConnectionNeighborWindow,
            connectionSettings.MaxOutgoingPerNode,
            minConnectedSourcesPerRow: connectionSettings.MinConnectedSourcesPerRow,
            backtrackAllowance: connectionSettings.BacktrackAllowance,
            connectionDensity: connectionSettings.ConnectionDensity,
            minIncomingPerTarget: connectionSettings.MinIncomingPerTarget,
            minDistinctSourcesToBoss: connectionSettings.MinDistinctSourcesToBoss,
            longLinkChance: connectionSettings.LongLinkChance,
            minDistinctTargetsPerFloor: connectionSettings.MinDistinctTargetsPerFloor,
            minBranchingNodesPerFloor: connectionSettings.MinBranchingNodesPerFloor,
            maxBranchingNodesPerFloor: connectionSettings.MaxBranchingNodesPerFloor,
            featureFlags: generationFeatureFlags);
    }
}
