using System;                                      // 為了用 Guid、Serializable
using System.Collections.Generic;                  // 為了用 List<>
using System.Linq;                                 // 為了用 Contains on IReadOnlyList
using UnityEngine;                                 // Unity 基本命名空間

// 地圖節點的種類：一般戰鬥、菁英戰鬥、商店、事件、Boss
public enum MapNodeType
{
    Battle,
    EliteBattle,
    Shop,
    Rest,
    Event,
    Boss
}

[Serializable]                                      // 讓這個資料結構可以在 Inspector 中看見
public class MapNodeData
{
    [SerializeField] private string nodeId;         // 節點的唯一 ID
    [SerializeField] private MapNodeType nodeType;  // 節點類型（一般戰鬥/菁英戰鬥/商店/事件/Boss）
    [SerializeField] private int floorIndex;        // 這個節點位於第幾層（第幾排）
    [SerializeField] private bool isCompleted;      // 是否已經完成過
    [SerializeField] private RunEncounterDefinition encounter;       // 如果是戰鬥節點，這裡放要打哪一場戰
    [SerializeField] private RunEventDefinition eventDefinition;     // 如果是事件節點，這裡放哪一個事件
    [SerializeField] private ShopInventoryDefinition shopInventory;  // 如果是商店節點，這裡放哪個商店清單
    [NonSerialized] private List<MapNodeData> nextNodes = new List<MapNodeData>(); // 這個節點連到下一層的哪些節點

    // 建構子：建立一個節點的時候一定要給 id、類型、所在樓層
    public MapNodeData(string id, MapNodeType type, int floor)
    {
        nodeId = id;
        nodeType = type;
        floorIndex = floor;
    }

    // 一些對外唯讀屬性，方便外面拿資料
    public string NodeId => nodeId;
    public MapNodeType NodeType => nodeType;
    public int FloorIndex => floorIndex;
    public bool IsCompleted => isCompleted;
    public RunEncounterDefinition Encounter => encounter;
    public RunEventDefinition Event => eventDefinition;
    public ShopInventoryDefinition ShopInventory => shopInventory;
    public IReadOnlyList<MapNodeData> NextNodes => nextNodes;
    public bool IsBoss => nodeType == MapNodeType.Boss;  // 快速判斷是不是 Boss 節點

    // 設定這個節點的戰鬥配置
    public void SetEncounter(RunEncounterDefinition definition)
    {
        encounter = definition;
    }

    // 設定這個節點的事件
    public void SetEvent(RunEventDefinition definition)
    {
        eventDefinition = definition;
    }

    // 設定這個節點的商店
    public void SetShop(ShopInventoryDefinition definition)
    {
        shopInventory = definition;
    }

    // 更新節點類型（Slot 分配後用）
    public void SetNodeType(MapNodeType type)
    {
        nodeType = type;
    }

    // 標記這個節點完成
    public void MarkCompleted()
    {
        isCompleted = true;
    }

    // 把完成狀態清回去（重開 run 用）
    public void ResetProgress()
    {
        isCompleted = false;
    }

    // 增加一個連到下一層的節點
    public void AddNextNode(MapNodeData node)
    {
        if (node == null || nextNodes.Contains(node))
            return;
        nextNodes.Add(node);
    }
}

// 這是整個跑團流程的核心控制器
public class RunManager : MonoBehaviour
{
    private const int RestHealAmount = 30;

    // 單例，讓別的場景也能直接 RunManager.Instance 拿到
    public static RunManager Instance { get; private set; }
    [Header("Config Assets")]
    [SerializeField] private RunMapConfig mapConfig;
    [Header("Scene Names")]
    [SerializeField] private string runSceneName = "RunScene";       // 地圖場景名稱
    [SerializeField] private string battleSceneName = "BattleScene"; // 戰鬥場景名稱
    [SerializeField] private string shopSceneName = "ShopScene";     // 商店場景名稱
    [SerializeField] private string deathReturnSceneName = "";       // 玩家死亡後要回到的場景名稱（預設回地圖）
    [SerializeField] private RunEventUIManager eventUIManager;        // 事件彈窗管理器

    [Header("Map Generation")]
    [SerializeField] private int floorCount = 4;                     // 一張圖有幾層
    [SerializeField] private int minNodesPerFloor = 2;               // 每層節點數下限
    [SerializeField] private int maxNodesPerFloor = 4;               // 每層節點數上限
    [Obsolete("Slot-based generation no longer uses single-node rates")] [SerializeField, Range(0f, 1f)] private float eventRate = 0.2f;  // 已棄用
    [Obsolete("Slot-based generation no longer uses single-node rates")] [SerializeField, Range(0f, 1f)] private float shopRate = 0.15f;  // 已棄用
    [Obsolete("Slot-based generation no longer uses single-node rates")] [SerializeField, Range(0f, 1f)] private float eliteBattleRate = 0.1f; // 已棄用
    [SerializeField] private int shopMin = 2;
    [SerializeField] private int shopMax = 3;
    [SerializeField] private int eliteMin = 2;
    [SerializeField] private int eliteMax = 4;
    [SerializeField] private int restMin = 4;
    [SerializeField] private int restMax = 6;
    [SerializeField, Range(0f, 1f)] private float eventRatioMin = 0.2f;
    [SerializeField, Range(0f, 1f)] private float eventRatioMax = 0.25f;
    [SerializeField] private EncounterPool encounterPool;            // 戰鬥池，從這裡抽戰鬥 :contentReference[oaicite:4]{index=
    [SerializeField] private EncounterPool eliteEncounterPool;       // 菁英戰鬥池，專門放菁英戰鬥
    [SerializeField] private RunEncounterDefinition bossEncounter;   // Boss 專用戰鬥
    [SerializeField] private ShopInventoryDefinition defaultShopInventory; // 預設商店清單
    [SerializeField] private List<RunEventDefinition> eventPool = new List<RunEventDefinition>(); // 事件池
    [SerializeField] private bool autoGenerateOnStart = true;        // 是否一開場就自動做一張圖

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
    private readonly List<List<MapNodeData>> mapFloors = new List<List<MapNodeData>>(); // 存每一層的節點
    private MapNodeData currentNode;                                  // 玩家目前所在的節點
    private MapNodeData activeNode;                                   // 正在進行中的節點（正在戰鬥/商店/事件）
    private bool runCompleted;                                        // 這次 run 是否已通關

    private Player player;                                            // 目前這次 run 的玩家物件
    private PlayerRunSnapshot initialPlayerSnapshot;                  // 起始時候的玩家快照（方便死亡重開）
    private PlayerRunSnapshot currentRunSnapshot;                     // 當前 run 的玩家快照（每次戰鬥回來都會更新）
    public PlayerRunSnapshot CurrentRunSnapshot => currentRunSnapshot;


    private RunMapGenerator mapGenerator;
    private RunMapConnector mapConnector;
    private RunSceneRouter sceneRouter;
    private RunEventResolver eventResolver;

    public IReadOnlyList<IReadOnlyList<MapNodeData>> MapFloors => mapFloors; // 對外讀取整張圖
    public MapNodeData CurrentNode => currentNode;                    // 對外讀目前節點
    public MapNodeData ActiveNode => activeNode;                      // 對外讀正在處理的節點
    public bool RunCompleted => runCompleted;                         // 對外讀這次 run 是否完成
    public ShopInventoryDefinition DefaultShopInventory => defaultShopInventory; // 對外讀預設商店清單

    public event Action<IReadOnlyList<IReadOnlyList<MapNodeData>>> MapGenerated; // 生成新地圖時通知 UI
    public event Action MapStateChanged;                              // 地圖狀態（完成/可選節點）變動時通知
    public event Action<MapNodeData> NodeEntered;                     // 進入某個節點時通知（商店/事件/戰鬥等）
    public event Action<MapNodeData> NodeCompleted;                   // 完成某個節點時通知
    private void Awake()
    {
        // 確保只有一個 RunManager，重複的就刪掉
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // 場景切換時不要把我刪掉

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
        // 如果有勾自動生成，就建一張新圖
        if (autoGenerateOnStart)
        {
            GenerateNewRun();
        }
    }

    // 登記玩家物件，讓 RunManager 可以存/還原他的資料
    public void RegisterPlayer(Player newPlayer)
    {
        if (newPlayer == null)
            return;

        player = newPlayer;
        eventResolver.Player = newPlayer;

        // 第一次註冊的時候，抓一份起始快照
        if (initialPlayerSnapshot == null)
        {
            initialPlayerSnapshot = PlayerRunSnapshot.Capture(newPlayer);
            currentRunSnapshot = initialPlayerSnapshot.Clone();
            eventResolver.InitialPlayerSnapshot = initialPlayerSnapshot;
        }

        // 如果有目前快照，就套回去（例如從戰鬥場景回到地圖場景時）
        if (currentRunSnapshot != null)
        {
            ApplySnapshotToPlayer(newPlayer, currentRunSnapshot);
        }
    }

    // 產生一張新的 run 地圖
    public void GenerateNewRun()
    {
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

        mapFloors.Clear();
        mapFloors.AddRange(map.Floors);
        currentNode = null;     // 還沒選起始節點
        activeNode = null;      // 還沒進入任何節點
        runCompleted = false;   // 新的 run 當然還沒通關

        MapGenerated?.Invoke(mapFloors);
        MapStateChanged?.Invoke();
    }

    // 給 UI 用：現在有哪些節點可以選
    public IReadOnlyList<MapNodeData> GetAvailableNodes()
    {
        // 如果現在有一個節點正在進行（還沒回來），那就不能再選別的
        if (activeNode != null)
            return Array.Empty<MapNodeData>();

        // 如果根本還沒有地圖，就回空陣列
        if (mapFloors.Count == 0)
            return Array.Empty<MapNodeData>();

        // 如果還沒選過節點，就把第一層全部回去當可選
        if (currentNode == null)
            return mapFloors[0];

        // 如果目前的節點沒有往下的連線，就沒有東西可以選
        if (currentNode.NextNodes.Count == 0)
            return Array.Empty<MapNodeData>();

        // 否則就回傳下一層連線的節點
        return currentNode.NextNodes;
    }

    // 嘗試進入某個節點，成功就會切到對應的場景
    public bool TryEnterNode(MapNodeData node)
    {
        if (node == null)
            return false;
        if (activeNode != null)
            return false;
        if (!IsNodeSelectable(node))
            return false;

        activeNode = node;     // 標記現在正在這個節點
        NodeEntered?.Invoke(node);
        if (node.NodeType == MapNodeType.Event)
        {
            eventResolver.CurrentRunSnapshot = currentRunSnapshot;
            eventResolver.InitialPlayerSnapshot = initialPlayerSnapshot;
            eventResolver.EventUIManager = eventUIManager;
            eventResolver.HandleEventNode(node, () =>
            {
                CompleteActiveNodeWithoutBattle();
                currentRunSnapshot = eventResolver.CurrentRunSnapshot;
            });
        }
        else if (node.NodeType == MapNodeType.Rest)
        {
            ApplyRestEffect();
            CompleteActiveNodeWithoutBattle();
        }
        else
        {
            sceneRouter.LoadSceneForNode(node); // 依節點類型載入場景
        }
        MapStateChanged?.Invoke();
        return true;
    }

    // 檢查這個節點能不能被選
    public bool IsNodeSelectable(MapNodeData node)
    {
        if (node == null || node.IsCompleted)
            return false;

        // 還沒走過任何節點時，只能選第 0 層的
        if (currentNode == null)
            return node.FloorIndex == 0;

        // 有走過的話，就只能選目前節點連出去的那些
        return currentNode.NextNodes.Contains(node);
    }

    // 被戰鬥場景呼叫：打贏了
    public void HandleBattleVictory()
    {
        if (activeNode == null)
            return;

        activeNode.MarkCompleted(); // 標記這個節點完成了
        currentNode = activeNode;   // 玩家現在就站在這個節點上
        if (activeNode.IsBoss)
        {
            runCompleted = true;    // 如果這個是 Boss，那 run 結束
        }
        NodeCompleted?.Invoke(activeNode);
        MapStateChanged?.Invoke();
    }

    // 商店 / 事件等非戰鬥節點完成時呼叫：標記節點已完成並更新目前位置
    public void CompleteActiveNodeWithoutBattle()
    {
        if (activeNode == null)
            return;

        activeNode.MarkCompleted();
        currentNode = activeNode;
        activeNode = null;
        NodeCompleted?.Invoke(currentNode);
        MapStateChanged?.Invoke();
    }

    // 被戰鬥場景呼叫：打輸了
    public void HandleBattleDefeat()
    {
        ResetRun();     // 把整個 run 重置
        sceneRouter.LoadDeathReturnScene(); // 回到設定的場景（預設地圖）重新開始
    }

    // 戰鬥 / 商店 / 事件做完，要回到地圖時呼叫這個
    public void ReturnToRunSceneFromBattle()
    {
        SyncPlayerRunState();   // 先把玩家目前狀態存起來

        if (runCompleted)
        {
            ResetRun();         // 如果 run 已經完成了，就直接重開一張新的
        }

        activeNode = null;      // 不再有正在進行的節點
        sceneRouter.LoadRunScene();         // 載入地圖場景
        MapStateChanged?.Invoke();
    }

    // 把玩家目前狀態記起來，之後回到地圖時可以還原
    public void SyncPlayerRunState()
    {
        if (player == null)
            return;

        currentRunSnapshot = PlayerRunSnapshot.Capture(player);
        eventResolver.CurrentRunSnapshot = currentRunSnapshot;
    }

    // 重置整個 run：玩家回初始、地圖重做
    public void ResetRun()
    {
        runCompleted = false;
        activeNode = null;
        currentNode = null;

        // 如果有存起始快照，就套回去
        if (initialPlayerSnapshot != null)
        {
            currentRunSnapshot = initialPlayerSnapshot.Clone();
            eventResolver.CurrentRunSnapshot = currentRunSnapshot;
            ApplySnapshotToPlayer(player, currentRunSnapshot);
        }

        GenerateNewRun(); // 重做一張圖
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
            return;
        }

        EnsureCurrentRunSnapshot();
        if (currentRunSnapshot == null)
            return;

        int clampedHp = Mathf.Clamp(currentRunSnapshot.currentHP + RestHealAmount, 0, currentRunSnapshot.maxHP);
        currentRunSnapshot.currentHP = clampedHp;
        eventResolver.CurrentRunSnapshot = currentRunSnapshot;
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
            relics = new List<CardBase>(),
            exhaustPile = new List<CardBase>()
        };
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
