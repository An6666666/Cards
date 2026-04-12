using System;                                      // ?箔???Guid?erializable
using System.Collections.Generic;                  // ?箔???List<>
using System.Linq;                                 // ?箔???Contains on IReadOnlyList
using UnityEngine;                                 // Unity ?箸?賢?蝛粹?

// ?啣?蝭暺?蝔桅?嚗??祆擛乓??望擛乓?摨?隞嗚oss
public enum MapNodeType
{
    Battle,
    EliteBattle,
    Shop,
    Rest,
    Event,
    Boss
}

[Serializable]                                      // 霈???瑽隞亙 Inspector 銝剔?閬?
public class MapNodeData
{
    [SerializeField] private string nodeId;         // 蝭暺??臭? ID
    [SerializeField] private MapNodeType nodeType;  // 蝭暺???銝?祆擛???圈洛/??/鈭辣/Boss嚗?
    [SerializeField] private int floorIndex;        // ??暺??潛洵撟曉惜嚗洵撟暹?嚗?
    [SerializeField] private bool isCompleted;      // ?臬撌脩?摰???
    [SerializeField] private RunEncounterDefinition encounter;       // 憒??舀擛亦?暺??ㄐ?曇??銝?湔
    [SerializeField] private RunEventDefinition eventDefinition;     // 憒??臭?隞嗥?暺??ㄐ?曉銝??隞?
    [SerializeField] private ShopInventoryDefinition shopInventory;  // 憒??臬?摨?暺??ㄐ?曉??摨???
    [NonSerialized] private List<MapNodeData> nextNodes = new List<MapNodeData>(); // ??暺?銝?撅斤??芯?蝭暺?

    // 撱箸?摮?撱箇?銝??暺???摰?蝯?id?????冽?撅?
    public MapNodeData(string id, MapNodeType type, int floor)
    {
        nodeId = id;
        nodeType = type;
        floorIndex = floor;
    }

    // 銝鈭?憭霈撅祆改??嫣噶憭?輯???
    public string NodeId => nodeId;
    public MapNodeType NodeType => nodeType;
    public int FloorIndex => floorIndex;
    public bool IsCompleted => isCompleted;
    public RunEncounterDefinition Encounter => encounter;
    public RunEventDefinition Event => eventDefinition;
    public ShopInventoryDefinition ShopInventory => shopInventory;
    public IReadOnlyList<MapNodeData> NextNodes => nextNodes;
    public bool IsBoss => nodeType == MapNodeType.Boss;  // 敹恍?瑟銝 Boss 蝭暺?

    // 閮剖???暺??圈洛?蔭
    public void SetEncounter(RunEncounterDefinition definition)
    {
        encounter = definition;
    }

    // 閮剖???暺?鈭辣
    public void SetEvent(RunEventDefinition definition)
    {
        eventDefinition = definition;
    }

    // 閮剖???暺???
    public void SetShop(ShopInventoryDefinition definition)
    {
        shopInventory = definition;
    }

    // ?湔蝭暺???Slot ??敺嚗?
    public void SetNodeType(MapNodeType type)
    {
        nodeType = type;
    }

    // 璅???暺???
    public void MarkCompleted()
    {
        isCompleted = true;
    }

    // ???????嚗???run ?剁?
    public void ResetProgress()
    {
        isCompleted = false;
    }

    // 憓?銝??銝?撅斤?蝭暺?
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

// ??游???蝔??詨??批??
public partial class RunManager : MonoBehaviour
{
    private const int RestHealAmount = 30;

    // ?桐?嚗??亦??湔銋?湔 RunManager.Instance ?踹
    public static RunManager Instance { get; private set; }
    [Header("Config Assets")]
    [SerializeField] private RunMapConfig mapConfig;
    [Header("Scene Names")]
    [SerializeField] private string runSceneName = "RunScene";       // ?啣??湔?迂
    [SerializeField] private string battleSceneName = "BattleScene"; // ?圈洛?湔?迂
    [SerializeField] private string shopSceneName = "ShopScene";     // ???湔?迂
    [SerializeField] private string deathReturnSceneName = "";       // ?拙振甇颱滿敺????臬?蝔梧??身???
    [SerializeField, Min(0f)] private float nodeEnterDelaySeconds = 0f; // 暺?蝭暺?嚗脣蝭暺?蝔???敺嗾蝘?
    [SerializeField] private RunEventUIManager eventUIManager;        // 鈭辣敶?蝞∠???

    [Header("Tutorial")]
    [SerializeField] private bool tutorialRun;

    [Header("Map Generation")]
    [SerializeField] private int floorCount = 4;                     // 銝撘萄??嗾撅?
    [SerializeField] private int minNodesPerFloor = 2;               // 瘥惜蝭暺銝?
    [SerializeField] private int maxNodesPerFloor = 4;               // 瘥惜蝭暺銝?
    [Obsolete("Slot-based generation no longer uses single-node rates")] [SerializeField, Range(0f, 1f)] private float eventRate = 0.2f;  // 撌脫???
    [Obsolete("Slot-based generation no longer uses single-node rates")] [SerializeField, Range(0f, 1f)] private float shopRate = 0.15f;  // 撌脫???
    [Obsolete("Slot-based generation no longer uses single-node rates")] [SerializeField, Range(0f, 1f)] private float eliteBattleRate = 0.1f; // 撌脫???
    [SerializeField] private int shopMin = 2;
    [SerializeField] private int shopMax = 3;
    [SerializeField] private int eliteMin = 2;
    [SerializeField] private int eliteMax = 4;
    [SerializeField] private int restMin = 4;
    [SerializeField] private int restMax = 6;
    [SerializeField, Range(0f, 1f)] private float eventRatioMin = 0.2f;
    [SerializeField, Range(0f, 1f)] private float eventRatioMax = 0.25f;
    [SerializeField] private EncounterPool encounterPool;            // ?圈洛瘙?敺ㄐ?賣擛?:contentReference[oaicite:4]{index=
    [SerializeField] private EncounterPool eliteEncounterPool;       // ??圈洛瘙?撠??曇??望擛?
    [SerializeField] private RunEncounterDefinition bossEncounter;   // Boss 撠?圈洛
    [SerializeField] private ShopInventoryDefinition defaultShopInventory; // ?身??皜
    [SerializeField] private List<RunEventDefinition> eventPool = new List<RunEventDefinition>(); // 鈭辣瘙?
    [SerializeField] private bool autoGenerateOnStart = true;        // ?臬銝?撠梯??銝撘萄?

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
    private readonly List<List<MapNodeData>> mapFloors = new List<List<MapNodeData>>(); // 摮?銝撅斤?蝭暺?
    private MapNodeData currentNode;                                  // ?拙振?桀???函?蝭暺?
    private MapNodeData activeNode;                                   // 甇??脰?銝剔?蝭暺?甇??圈洛/??/鈭辣嚗?
    private bool runCompleted;                                        // ?活 run ?臬撌脤?

    private Player player;                                            // ?桀??活 run ?摰嗥隞?
    private PlayerRunSnapshot initialPlayerSnapshot;                  // 韏瑕????拙振敹怎嚗靘踵香鈭⊿???
    private PlayerRunSnapshot currentRunSnapshot;                     // ?嗅? run ?摰嗅翰?改?瘥活?圈洛???賣??湔嚗?
    private int runSequenceId;                                        // 每次產生新冒險地圖就 +1，用於區分不同冒險
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
    public IReadOnlyList<IReadOnlyList<MapNodeData>> MapFloors => mapFloors; // 撠?霈?撘萄?
    public MapNodeData CurrentNode => currentNode;                    // 撠?霈?桀?蝭暺?
    public MapNodeData ActiveNode => activeNode;                      // 撠?霈甇?????暺?
    public bool RunCompleted => runCompleted;                         // 撠?霈?活 run ?臬摰?
    public ShopInventoryDefinition DefaultShopInventory => defaultShopInventory; // 撠?霈?身??皜
    public Player RegisteredPlayer => player;
    public bool IsTutorialRun => tutorialRun;

    public event Action<IReadOnlyList<IReadOnlyList<MapNodeData>>> MapGenerated; // ???啣??? UI
    public event Action MapStateChanged;                              // ?啣????摰?/?舫蝭暺?霈??
    public event Action<MapNodeData> NodeEntered;                     // ?脣??暺??嚗?摨?鈭辣/?圈洛蝑?
    public event Action<MapNodeData> NodeCompleted;                   // 摰???暺??
    public event Action<PlayerRunSnapshot> RunSnapshotChanged;
    private void Awake()
    {
        // 蝣箔??芣?銝??RunManager嚗?銴?撠勗??
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // ?湔????閬????

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

        // 憒???芸???嚗停撱箔?撘菜??
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

    // ?餉??拙振?拐辣嚗? RunManager ?臭誑摮???隞?鞈?
    public void RegisterPlayer(Player newPlayer)
    {
        if (newPlayer == null)
            return;

        player = newPlayer;
        eventResolver.Player = newPlayer;

        // 蝚砌?甈∟酉??????隞質絲憪翰??
        if (initialPlayerSnapshot == null)
        {
            initialPlayerSnapshot = PlayerRunSnapshot.Capture(newPlayer);
            if (currentRunSnapshot == null)
            {
                currentRunSnapshot = initialPlayerSnapshot.Clone();
            }
        }

        eventResolver.InitialPlayerSnapshot = initialPlayerSnapshot;
        eventResolver.CurrentRunSnapshot = currentRunSnapshot;

        // 憒???翰?改?撠勗??嚗?憒??圈洛?湔??啣??湔??
        if (currentRunSnapshot != null)
        {
            ApplySnapshotToPlayer(newPlayer, currentRunSnapshot);
            RaiseRunSnapshotChanged();
        }

        SaveCurrentProgress();
    }

    // ?Ｙ?銝撘菜??run ?啣?
    public void GenerateNewRun()
    {
        runSequenceId++; // 這次是全新一輪冒險，遞增序號
        ResetGuideState();

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
        currentNode = null;     // ???貉絲憪?暺?
        activeNode = null;      // ???脣隞颱?蝭暺?
        runCompleted = false;   // ?啁? run ?嗥????

        MapGenerated?.Invoke(mapFloors);
        MapStateChanged?.Invoke();
        SaveCurrentProgress();
    }

    // 蝯?UI ?剁??曉?鈭?暺隞仿
    public IReadOnlyList<MapNodeData> GetAvailableNodes()
    {
        // 憒??曉????暺迤?券脰?嚗?瘝?靘?嚗撠曹??賢??詨??
        if (activeNode != null)
            return Array.Empty<MapNodeData>();

        // 憒??寞?????撠勗?蝛粹??
        if (mapFloors.Count == 0)
            return Array.Empty<MapNodeData>();

        // 憒????賊?蝭暺?撠望?蝚砌?撅文?典??餌?舫
        if (currentNode == null)
            return mapFloors[0];

        // 憒??桀???暺???銝????嚗停瘝??梯正?臭誑??
        if (currentNode.NextNodes.Count == 0)
            return Array.Empty<MapNodeData>();

        // ?血?撠勗??喃?銝撅日????暺?
        return currentNode.NextNodes;
    }

    // ?岫?脣??暺???撠望??撠????
    public bool TryEnterNode(MapNodeData node)
    {
        if (node == null)
            return false;
        if (activeNode != null)
            return false;
        if (!IsNodeSelectable(node))
            return false;

        activeNode = node;     // 璅??曉甇???暺?
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
            eventResolver.EventUIManager = eventUIManager;
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
            sceneRouter.LoadSceneForNode(node); // 靘?暺????亙??
        }
        pendingNodeTransitionCoroutine = null;
    }

    // 瑼Ｘ??暺銝鋡恍
    public bool IsNodeSelectable(MapNodeData node)
    {
        if (node == null || node.IsCompleted)
            return false;

        // ??韏圈?隞颱?蝭暺?嚗?賡蝚?0 撅斤?
        if (currentNode == null)
            return node.FloorIndex == 0;

        // ?粥??閰梧?撠勗?賡?桀?蝭暺??餌????
        return currentNode.NextNodes.Contains(node);
    }

    // 鋡急擛亙?臬?恬???鈭?
    public void HandleBattleVictory()
    {
        if (activeNode == null)
            return;

        activeNode.MarkCompleted(); // 璅???暺???
        currentNode = activeNode;   // ?拙振?曉撠梁??券?暺?
        if (activeNode.IsBoss)
        {
            runCompleted = true;    // 憒?? Boss嚗 run 蝯?
        }
        NodeCompleted?.Invoke(activeNode);
        MapStateChanged?.Invoke();
        SaveCurrentProgress();
    }

    // ?? / 鈭辣蝑??圈洛蝭暺????澆嚗?閮?暺歇摰?銝行?啁??蝵?
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

    // 鋡急擛亙?臬?恬??撓鈭?
    public void HandleBattleDefeat()
    {
        suppressAutosave = true;
        RunProgressPersistence.ClearSavedProgress();
        ResetRun();     // ???run ?蔭
        sceneRouter.LoadDeathReturnScene(); // ?閮剖???荔??身?啣?嚗??圈?憪?
    }

    // ?圈洛 / ?? / 鈭辣??嚗???啣???恍?
    public void ReturnToRunSceneFromBattle()
    {
        SyncPlayerRunState();   // ???拙振?桀????韏瑚?

        if (runCompleted)
        {
            suppressAutosave = true;
            RunProgressPersistence.ClearSavedProgress();
            ResetRun();         // 憒? run 撌脩?摰?鈭?撠梁?仿???撘菜??
            activeNode = null;
            sceneRouter.LoadDeathReturnScene();
            MapStateChanged?.Invoke();
            return;
        }

        activeNode = null;      // 銝??迤?券脰???暺?
        SaveCurrentProgress();
        sceneRouter.LoadRunScene();         // 頛?啣??湔
        MapStateChanged?.Invoke();
    }

    // ?摰嗥????韏瑚?嚗?敺??啣???臭誑??
    public void SyncPlayerRunState()
    {
        if (player == null)
            return;

        currentRunSnapshot = PlayerRunSnapshot.Capture(player);
        eventResolver.CurrentRunSnapshot = currentRunSnapshot;
        RaiseRunSnapshotChanged();
        SaveCurrentProgress();
    }

    // ?蔭?游?run嚗摰嗅????????
    public void ResetRun()
    {
        runCompleted = false;
        activeNode = null;
        currentNode = null;

        // 憒???韏瑕?敹怎嚗停憟???
        if (initialPlayerSnapshot != null)
        {
            currentRunSnapshot = initialPlayerSnapshot.Clone();
            eventResolver.CurrentRunSnapshot = currentRunSnapshot;
            ApplySnapshotToPlayer(player, currentRunSnapshot);
            RaiseRunSnapshotChanged();
        }

        GenerateNewRun(); // ??銝撘萄?
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

