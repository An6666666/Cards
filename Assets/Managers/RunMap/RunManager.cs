using System;                                      // 為了用 Guid、Serializable
using System.Collections.Generic;                  // 為了用 List<>
using System.Linq;                                 // 為了用 Linq（雖然這裡目前用得不多）
using UnityEngine;                                 // Unity 基本命名空間
using UnityEngine.SceneManagement;                 // 要切換 Scene 所以要引用這個

// 地圖節點的種類：一般戰鬥、菁英戰鬥、商店、事件、Boss
public enum MapNodeType
{
    Battle,
    EliteBattle,
    Shop,
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
    [SerializeField] private List<MapNodeData> nextNodes = new List<MapNodeData>(); // 這個節點連到下一層的哪些節點

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
    // 單例，讓別的場景也能直接 RunManager.Instance 拿到
    public static RunManager Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string runSceneName = "RunScene";       // 地圖場景名稱
    [SerializeField] private string battleSceneName = "BattleScene"; // 戰鬥場景名稱
    [SerializeField] private string shopSceneName = "ShopScene";     // 商店場景名稱
    [SerializeField] private string eventSceneName = "EventScene";   // 事件場景名稱

    [Header("Map Generation")]
    [SerializeField] private int floorCount = 4;                     // 一張圖有幾層
    [SerializeField] private int minNodesPerFloor = 2;               // 每層節點數下限
    [SerializeField] private int maxNodesPerFloor = 4;               // 每層節點數上限
    [SerializeField, Range(0f, 1f)] private float eventRate = 0.2f;  // 生成事件節點的機率
    [SerializeField, Range(0f, 1f)] private float shopRate = 0.15f;  // 生成商店節點的機率
    [SerializeField, Range(0f, 1f)] private float eliteBattleRate = 0.1f; // 生成菁英戰鬥節點的機率
    [SerializeField] private EncounterPool encounterPool;            // 戰鬥池，從這裡抽戰鬥 :contentReference[oaicite:4]{index=4}
    [SerializeField] private EncounterPool eliteEncounterPool;       // 菁英戰鬥池，專門放菁英戰鬥
    [SerializeField] private RunEncounterDefinition bossEncounter;   // Boss 專用戰鬥
    [SerializeField] private ShopInventoryDefinition defaultShopInventory; // 預設商店清單
    [SerializeField] private List<RunEventDefinition> eventPool = new List<RunEventDefinition>(); // 事件池
    [SerializeField] private bool autoGenerateOnStart = true;        // 是否一開場就自動做一張圖

    private readonly List<List<MapNodeData>> mapFloors = new List<List<MapNodeData>>(); // 存每一層的節點
    private MapNodeData currentNode;                                  // 玩家目前所在的節點
    private MapNodeData activeNode;                                   // 正在進行中的節點（正在戰鬥/商店/事件）
    private bool runCompleted;                                        // 這次 run 是否已通關

    private Player player;                                            // 目前這次 run 的玩家物件
    private PlayerRunSnapshot initialPlayerSnapshot;                  // 起始時候的玩家快照（方便死亡重開）
    private PlayerRunSnapshot currentRunSnapshot;                     // 當前 run 的玩家快照（每次戰鬥回來都會更新）

    public IReadOnlyList<IReadOnlyList<MapNodeData>> MapFloors => mapFloors; // 對外讀取整張圖
    public MapNodeData CurrentNode => currentNode;                    // 對外讀目前節點
    public MapNodeData ActiveNode => activeNode;                      // 對外讀正在處理的節點
    public bool RunCompleted => runCompleted;                         // 對外讀這次 run 是否完成
    public ShopInventoryDefinition DefaultShopInventory => defaultShopInventory; // 對外讀預設商店清單
    
    public event Action<IReadOnlyList<IReadOnlyList<MapNodeData>>> MapGenerated; // 生成新地圖時通知 UI
    public event Action MapStateChanged;                              // 地圖狀態（完成/可選節點）變動時通知

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

        // 第一次註冊的時候，抓一份起始快照
        if (initialPlayerSnapshot == null)
        {
            initialPlayerSnapshot = PlayerRunSnapshot.Capture(newPlayer);
            currentRunSnapshot = initialPlayerSnapshot.Clone();
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
        mapFloors.Clear(); // 先清空之前的地圖
        int totalFloors = Mathf.Max(1, floorCount);
        for (int floor = 0; floor < totalFloors; floor++)

        {
            // 最後一層只生成一個節點（通常是 Boss）
            int nodeCount = floor == totalFloors - 1 ? 1 : GetRandomNodeCountForFloor(floor);

            var floorNodes = new List<MapNodeData>(nodeCount);            
            for (int i = 0; i < nodeCount; i++)
            {
                // 決定這一格要生成什麼類型的節點
                MapNodeType type = DetermineNodeTypeForFloor(floor);
                // 幫節點生一個唯一 ID：F樓層_N第幾個_再加一組 Guid 避免重複
                string nodeId = $"F{floor}_N{i}_{Guid.NewGuid():N}";
                var node = new MapNodeData(nodeId, type, floor);

                // 依照節點類型塞對應的資料
                if (type == MapNodeType.Battle)
                {
                    // 戰鬥節點就從戰鬥池抽一場
                    node.SetEncounter(encounterPool != null ? encounterPool.GetRandomEncounter() : null);
                }
                else if (type == MapNodeType.EliteBattle)
                {
                    // 菁英戰鬥節點就從菁英戰鬥池抽一場，沒有就退回一般戰鬥池
                    RunEncounterDefinition eliteEncounter = eliteEncounterPool != null
                        ? eliteEncounterPool.GetRandomEncounter()
                        : null;
                    node.SetEncounter(eliteEncounter != null ? eliteEncounter : encounterPool?.GetRandomEncounter());
                }
                else if (type == MapNodeType.Boss)
                {
                    // Boss 節點如果有指定 bossEncounter 就用沒有就退而求其次
                    node.SetEncounter(bossEncounter != null ? bossEncounter : encounterPool?.GetRandomEncounter());
                }
                else if (type == MapNodeType.Event)
                {
                    // 事件節點就從事件池抽一個
                    node.SetEvent(GetRandomEventDefinition());
                }
                else if (type == MapNodeType.Shop)
                {
                    // 商店節點就塞預設商店
                    node.SetShop(defaultShopInventory);
                }

                floorNodes.Add(node);
            }
            mapFloors.Add(floorNodes);
        }

        // 把每一層之間的節點連起來
        BuildConnections();
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
        LoadSceneForNode(node); // 依節點類型載入場景
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

        MapStateChanged?.Invoke();
    }

    // 商店 / 事件等非戰鬥節點完成時呼叫：標記節點已完成並更新目前位置
    public void CompleteActiveNodeWithoutBattle()
    {
        if (activeNode == null)
            return;

        activeNode.MarkCompleted();
        currentNode = activeNode;
        MapStateChanged?.Invoke();
    }

    // 被戰鬥場景呼叫：打輸了
    public void HandleBattleDefeat()
    {
        ResetRun();     // 把整個 run 重置
        LoadRunScene(); // 回到地圖場景重新開始
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
        LoadRunScene();         // 載入地圖場景
        MapStateChanged?.Invoke();
    }

    // 把玩家目前狀態記起來，之後回到地圖時可以還原
    public void SyncPlayerRunState()
    {
        if (player == null)
            return;

        currentRunSnapshot = PlayerRunSnapshot.Capture(player);
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
            ApplySnapshotToPlayer(player, currentRunSnapshot);
        }

        GenerateNewRun(); // 重做一張圖
    }

    // 載入地圖場景
    private void LoadRunScene()
    {
        if (!string.IsNullOrEmpty(runSceneName))
        {
            SceneManager.LoadScene(runSceneName);
        }
    }

    // 依節點類型載入對應場景
    private void LoadSceneForNode(MapNodeData node)
    {
        string sceneName = null;
        switch (node.NodeType)
        {
            case MapNodeType.Battle:
            case MapNodeType.EliteBattle:
            case MapNodeType.Boss:
                sceneName = battleSceneName;
                break;
            case MapNodeType.Shop:
                sceneName = shopSceneName;
                break;
            case MapNodeType.Event:
                sceneName = eventSceneName;
                break;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning($"RunManager: Scene name for {node.NodeType} is not configured.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    // 把每一層的節點彼此連起來，形成可以走的路
    private void BuildConnections()
    {
        for (int floor = 0; floor < mapFloors.Count - 1; floor++)
        {
            List<MapNodeData> currentFloor = mapFloors[floor];
            List<MapNodeData> nextFloor = mapFloors[floor + 1];

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

            // 第一步：依照左右順序為每個節點建立主要連線，確保走線單調遞增
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

            // 第二步：確保下一層的每個節點至少有一個來源
            for (int targetIndex = 0; targetIndex < nextCount; targetIndex++)
            {
                if (incomingCounts[targetIndex] > 0)
                    continue;

                int sourceIndex = SelectSourceForUnconnectedTarget(
                    targetIndex,
                    currentCount,
                    nextCount,
                    outgoingCounts,
                    prefixMaxTargets,
                    suffixMinTargets);

                if (sourceIndex < 0)
                    continue;

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

                RecomputeConnectionBounds(currentCount, nextCount, sourceMinTargets, sourceMaxTargets, prefixMaxTargets, suffixMinTargets);
            }

            // 第三步：視情況增加第二條支線，仍舊維持單調以避免交叉
            for (int sourceIndex = 0; sourceIndex < currentCount; sourceIndex++)
            {
                if (outgoingCounts[sourceIndex] >= 2)
                    continue;

                if (UnityEngine.Random.value > 0.5f)
                    continue;

                int minAllowed = sourceIndex > 0 ? Mathf.Max(0, prefixMaxTargets[sourceIndex - 1]) : 0;
                int maxAllowed = sourceIndex < currentCount - 1
                    ? Mathf.Min(nextCount - 1, suffixMinTargets[sourceIndex + 1])
                    : nextCount - 1;

                if (minAllowed > maxAllowed)
                    continue;

                foreach (int candidate in EnumeratePreferredTargetIndices(sourceIndex, currentCount, nextCount))
                {
                    if (candidate < minAllowed || candidate > maxAllowed)
                        continue;

                    if (sourceTargets[sourceIndex].BinarySearch(candidate) >= 0)
                        continue;

                    if (!ConnectNodes(
                            currentFloor,
                            nextFloor,
                            sourceIndex,
                            candidate,
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

                    RecomputeConnectionBounds(currentCount, nextCount, sourceMinTargets, sourceMaxTargets, prefixMaxTargets, suffixMinTargets);
                    break;
                }
            }
        }
    }

    private int SelectInitialTargetIndex(int sourceIndex, int currentCount, int nextCount, int minimumTarget)
    {
        if (nextCount <= 1)
            return 0;

        foreach (int candidate in EnumeratePreferredTargetIndices(sourceIndex, currentCount, nextCount))
        {
            if (candidate < minimumTarget)
                continue;

            return candidate;
        }

        return Mathf.Clamp(minimumTarget, 0, nextCount - 1);
    }

    private int SelectSourceForUnconnectedTarget(
        int targetIndex,
        int currentCount,
        int nextCount,
        int[] outgoingCounts,
        int[] prefixMaxTargets,
        int[] suffixMinTargets)
    {
        int bestSource = -1;
        int bestScore = int.MaxValue;

        for (int sourceIndex = 0; sourceIndex < currentCount; sourceIndex++)
        {
            if (outgoingCounts[sourceIndex] >= 2)
                continue;

            int minAllowed = sourceIndex > 0 ? Mathf.Max(0, prefixMaxTargets[sourceIndex - 1]) : 0;
            int maxAllowed = sourceIndex < currentCount - 1
                ? Mathf.Min(nextCount - 1, suffixMinTargets[sourceIndex + 1])
                : nextCount - 1;

            if (targetIndex < minAllowed || targetIndex > maxAllowed)
                continue;

            int preferred = EstimatePreferredTargetIndex(sourceIndex, currentCount, nextCount);
            int score = Mathf.Abs(targetIndex - preferred);

            if (score < bestScore)
            {
                bestScore = score;
                bestSource = sourceIndex;
            }
        }

        if (bestSource < 0)
        {
            for (int sourceIndex = 0; sourceIndex < currentCount; sourceIndex++)
            {
                if (outgoingCounts[sourceIndex] >= 2)
                    continue;

                bestSource = sourceIndex;
                break;
            }
        }

        return bestSource;
    }

    private int EstimatePreferredTargetIndex(int sourceIndex, int currentCount, int nextCount)
    {
        if (nextCount <= 1)
            return 0;

        if (currentCount <= 1)
            return nextCount / 2;

        float t = (float)sourceIndex / (currentCount - 1);
        return Mathf.RoundToInt(t * (nextCount - 1));
    }

    private IEnumerable<int> EnumeratePreferredTargetIndices(int sourceIndex, int currentCount, int nextCount)
    {
        if (nextCount <= 0)
            yield break;

        if (nextCount == 1)
        {
            yield return 0;
            yield break;
        }

        int baseIndex = EstimatePreferredTargetIndex(sourceIndex, currentCount, nextCount);
        int[] offsets = { 0, -1, 1, -2, 2, -3, 3 };
        var seen = new HashSet<int>();

        foreach (int offset in offsets)
        {
            int candidate = Mathf.Clamp(baseIndex + offset, 0, nextCount - 1);
            if (seen.Add(candidate))
                yield return candidate;
        }
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
        if (sourceIndex < 0 || sourceIndex >= currentFloor.Count)
            return false;

        if (targetIndex < 0 || targetIndex >= nextFloor.Count)
            return false;

        MapNodeData source = currentFloor[sourceIndex];
        MapNodeData target = nextFloor[targetIndex];

        if (!TryConnectNodes(source, target, outgoingConnections, incomingConnections))
            return false;

        outgoingCounts[sourceIndex] = outgoingConnections[source];
        incomingCounts[targetIndex] = incomingConnections.TryGetValue(target, out int count) ? count : 0;

        List<int> targets = sourceTargets[sourceIndex];
        int insertIndex = targets.BinarySearch(targetIndex);
        if (insertIndex < 0)
            targets.Insert(~insertIndex, targetIndex);

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
            runningMax = Mathf.Max(runningMax, sourceMaxTargets[i]);
            prefixMaxTargets[i] = runningMax;
        }

        int runningMin = nextCount - 1;
        for (int i = currentCount - 1; i >= 0; i--)
        {
            int value = sourceMinTargets[i] == int.MaxValue ? nextCount - 1 : sourceMinTargets[i];
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

    // 決定這一層要生成幾個節點，維持在設定的範圍內並盡量讓樓層之間平滑過渡
    private int GetRandomNodeCountForFloor(int floor)
    {
        int clampedMin = Mathf.Max(1, Mathf.Min(minNodesPerFloor, maxNodesPerFloor));
        int clampedMax = Mathf.Max(clampedMin, maxNodesPerFloor);

        if (floor <= 0 || mapFloors.Count == 0)
        {
            return UnityEngine.Random.Range(clampedMin, clampedMax + 1);
        }

        int previousCount = mapFloors[mapFloors.Count - 1].Count;
        int smoothMin = Mathf.Max(clampedMin, previousCount - 1);
        int smoothMax = Mathf.Min(clampedMax, previousCount + 1);

        if (smoothMin > smoothMax)
        {
            smoothMin = clampedMin;
            smoothMax = clampedMax;
        }

        return UnityEngine.Random.Range(smoothMin, smoothMax + 1);
    }
    
    // 決定這一層要生成什麼類型的節點
    private MapNodeType DetermineNodeTypeForFloor(int floor)
    {
        // 第一層只放一般戰鬥，避免一開始就遇到事件或商店
        if (floor == 0)
            return MapNodeType.Battle;

        // 最後一層固定是 Boss
        if (floor == Mathf.Max(1, floorCount) - 1)
            return MapNodeType.Boss;

        float roll = UnityEngine.Random.value; // 0~1 隨機數
        float clampedShopRate = Mathf.Clamp01(shopRate);
        float clampedEventRate = Mathf.Clamp01(eventRate);
        float clampedEliteRate = Mathf.Clamp01(eliteBattleRate);

        if (roll < clampedShopRate)
            return MapNodeType.Shop;
        if (roll < clampedShopRate + clampedEventRate)
            return MapNodeType.Event;
        if (roll < clampedShopRate + clampedEventRate + clampedEliteRate)
            return MapNodeType.EliteBattle;
        return MapNodeType.Battle;
    }

    // 從事件池抽一個事件
    private RunEventDefinition GetRandomEventDefinition()
    {
        if (eventPool == null || eventPool.Count == 0)
            return null;
        int index = UnityEngine.Random.Range(0, eventPool.Count);
        return eventPool[index];
    }

    // 把快照裡的資料套回玩家身上
    private void ApplySnapshotToPlayer(Player target, PlayerRunSnapshot snapshot)
    {
        if (target == null || snapshot == null)
            return;

        target.maxHP = snapshot.maxHP;
        target.currentHP = Mathf.Clamp(snapshot.currentHP, 0, snapshot.maxHP);
        target.gold = snapshot.gold;

        // 套牌組
        target.deck = snapshot.deck != null ? new List<CardBase>(snapshot.deck) : new List<CardBase>();
        // 套遺物
        target.relics = snapshot.relics != null ? new List<CardBase>(snapshot.relics) : new List<CardBase>();

        // 清戰鬥中才會有的暫時狀態
        target.discardPile.Clear();
        target.Hand.Clear();
        target.block = 0;
        target.energy = target.maxEnergy;
        target.exhaustCountThisTurn = 0;
        target.buffs.ResetAll();
        target.ShuffleDeck(); // 重洗牌
    }

    // 這是內部用的「玩家快照」資料結構，用來記錄玩家 run 中的狀態
    [Serializable]
    private class PlayerRunSnapshot
    {
        public int maxHP;
        public int currentHP;
        public int gold;
        public List<CardBase> deck;
        public List<CardBase> relics;
        public List<CardBase> exhaustPile;

        // 從一個 Player 抓下來一份快照
        public static PlayerRunSnapshot Capture(Player source)
        {
            if (source == null)
                return new PlayerRunSnapshot
                {
                    deck = new List<CardBase>(),
                    relics = new List<CardBase>()
                };

            var combinedDeck = new List<CardBase>();

            if (source.deck != null && source.deck.Count > 0)
            {
                combinedDeck.AddRange(source.deck.Where(card => card != null));
            }

            var hand = source.Hand;
            if (hand != null && hand.Count > 0)
            {
                combinedDeck.AddRange(hand.Where(card => card != null));
            }

            if (source.discardPile != null && source.discardPile.Count > 0)
            {
                combinedDeck.AddRange(source.discardPile.Where(card => card != null));
            }

            if (source.exhaustPile != null && source.exhaustPile.Count > 0)
            {
                combinedDeck.AddRange(source.exhaustPile.Where(card => card != null));
            }

            return new PlayerRunSnapshot
            {
                maxHP = source.maxHP,
                currentHP = source.currentHP,
                gold = source.gold,
                deck = combinedDeck,
                relics = source.relics != null ? new List<CardBase>(source.relics.Where(card => card != null)) : new List<CardBase>(),
                exhaustPile = source.exhaustPile != null ? new List<CardBase>(source.exhaustPile.Where(card => card != null)) : new List<CardBase>()
            };
        }

        // 做一份一樣的副本（避免共用同一個 List）
        public PlayerRunSnapshot Clone()
        {
            return new PlayerRunSnapshot
            {
                maxHP = this.maxHP,
                currentHP = this.currentHP,
                gold = this.gold,
                deck = this.deck != null ? new List<CardBase>(this.deck) : new List<CardBase>(),
                relics = this.relics != null ? new List<CardBase>(this.relics) : new List<CardBase>(),
                exhaustPile = this.exhaustPile != null ? new List<CardBase>(this.exhaustPile) : new List<CardBase>()
            };
        }
    }
}
