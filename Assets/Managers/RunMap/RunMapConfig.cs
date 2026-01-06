using System; // 使用 System（Serializable 等）
using System.Collections.Generic; // 使用泛型集合（List、IReadOnlyList 等）
using UnityEngine; // Unity 核心 API

[CreateAssetMenu(menuName = "Cards/Run/Run Map Config", fileName = "RunMapConfig")] // 允許在 Create 選單建立此 ScriptableObject
public class RunMapConfig : ScriptableObject // Run 地圖生成設定資產：集中管理地圖布局、節點配置、連線與功能開關
{
    [Header("Layout")] // Inspector 分組：布局
    [SerializeField] private RunMapLayoutSettings layout = RunMapLayoutSettings.Default; // 地圖樓層與每層節點數等布局設定

    [Header("Slot Allocation")] // Inspector 分組：節點類型分配
    [SerializeField] private RunMapSlotAllocationSettings slotAllocation = RunMapSlotAllocationSettings.Default; // 商店/菁英/休息/事件比例與規則

    [Header("Connections")] // Inspector 分組：連線設定
    [SerializeField] private RunMapConnectionSettings connectionSettings = RunMapConnectionSettings.Default; // 節點之間連線密度、分支等設定

    [Header("Feature Flags")] // Inspector 分組：功能旗標（用於開關某些修正/特性）
    [SerializeField] private bool enableEliteStartFloorFix; // 是否啟用「菁英起始樓層」修正
    [SerializeField] private bool enableMinSideMergesPerFloorFix; // 是否啟用「每層最小側向合流」修正

    public RunMapLayoutSettings Layout => layout; // 對外讀取布局設定
    public RunMapSlotAllocationSettings SlotAllocation => slotAllocation; // 對外讀取節點類型分配設定
    public RunMapConnectionSettings ConnectionSettings => connectionSettings; // 對外讀取連線設定
    public IReadOnlyList<FixedFloorNodeRule> FixedFloorRules => slotAllocation.FixedFloorRules; // 對外讀取固定樓層規則（由 slotAllocation 提供）

    public RunMapGenerationFeatureFlags GetFeatureFlags() // 取得地圖生成用的功能旗標封裝
    {
        return new RunMapGenerationFeatureFlags( // 建立並回傳 FeatureFlags 物件
            enableEliteStartFloorFix, // 傳入菁英起始樓層修正開關
            enableMinSideMergesPerFloorFix); // 傳入每層最小側向合流修正開關
    }

    public RunMapGenerator.SlotAllocationSettings GetSlotAllocationSettings() // 取得給 RunMapGenerator 使用的 SlotAllocationSettings
    {
        return slotAllocation.ToSettings(); // 將可序列化設定轉換成生成器需要的設定物件
    }
}

[Serializable] // 允許此 struct 被序列化（Inspector 顯示/存檔）
public struct RunMapLayoutSettings // 地圖布局設定：樓層數、每層節點數上下限、變異機率
{
    [SerializeField] private int floorCount; // 總樓層數
    [SerializeField] private int minNodesPerFloor; // 每層最少節點數
    [SerializeField] private int maxNodesPerFloor; // 每層最多節點數
    [SerializeField, Range(0f, 1f)] private float floorVarianceChance; // 每層節點數量波動的機率（0~1）

    public RunMapLayoutSettings(int floorCount, int minNodesPerFloor, int maxNodesPerFloor, float floorVarianceChance) // 建構子：設定各欄位
    {
        this.floorCount = floorCount; // 設定樓層數
        this.minNodesPerFloor = minNodesPerFloor; // 設定每層最少節點數
        this.maxNodesPerFloor = maxNodesPerFloor; // 設定每層最多節點數
        this.floorVarianceChance = floorVarianceChance; // 設定樓層變異機率
    }

    public int FloorCount => Mathf.Max(1, floorCount); // 樓層數至少為 1
    public int MinNodesPerFloor => Mathf.Max(1, Mathf.Min(minNodesPerFloor, maxNodesPerFloor)); // 最少節點數至少為 1 且不得大於 max
    public int MaxNodesPerFloor => Mathf.Max(MinNodesPerFloor, maxNodesPerFloor); // 最大節點數至少不小於最少節點數
    public float FloorVarianceChance => Mathf.Clamp01(floorVarianceChance); // 機率限制在 0~1

    public static RunMapLayoutSettings Default => new RunMapLayoutSettings(4, 2, 4, 0.2f); // 預設值：4 層、每層 2~4 個節點、20% 變異
}

[Serializable] // 允許此 struct 被序列化
public struct RunMapSlotAllocationSettings // 節點類型分配設定：各類節點數量與事件比例範圍、型別限制、固定樓層規則
{
    [SerializeField] private int shopMin; // 商店節點最少數量
    [SerializeField] private int shopMax; // 商店節點最多數量
    [SerializeField] private int eliteMin; // 菁英節點最少數量
    [SerializeField] private int eliteMax; // 菁英節點最多數量
    [SerializeField] private int restMin; // 休息節點最少數量
    [SerializeField] private int restMax; // 休息節點最多數量
    [SerializeField, Range(0f, 1f)] private float eventRatioMin; // 事件節點比例下限（0~1）
    [SerializeField, Range(0f, 1f)] private float eventRatioMax; // 事件節點比例上限（0~1）
    [SerializeField] private RunMapNodeTypeConstraintSettings typeConstraints; // 節點型別限制設定（例如幾樓後才可出現商店/菁英等）
    [SerializeField] private List<FixedFloorNodeRule> fixedFloorRules; // 固定某樓層一定要是某種節點的規則清單

    public RunMapSlotAllocationSettings( // 建構子：設定各欄位
        int shopMin, // 商店最少數
        int shopMax, // 商店最多數
        int eliteMin, // 菁英最少數
        int eliteMax, // 菁英最多數
        int restMin, // 休息最少數
        int restMax, // 休息最多數
        float eventRatioMin, // 事件比例下限
        float eventRatioMax, // 事件比例上限
        RunMapNodeTypeConstraintSettings typeConstraints, // 型別限制
        List<FixedFloorNodeRule> fixedFloorRules) // 固定樓層規則列表
    {
        this.shopMin = shopMin; // 設定商店最少數
        this.shopMax = shopMax; // 設定商店最多數
        this.eliteMin = eliteMin; // 設定菁英最少數
        this.eliteMax = eliteMax; // 設定菁英最多數
        this.restMin = restMin; // 設定休息最少數
        this.restMax = restMax; // 設定休息最多數
        this.eventRatioMin = eventRatioMin; // 設定事件比例下限
        this.eventRatioMax = eventRatioMax; // 設定事件比例上限
        this.typeConstraints = typeConstraints; // 設定型別限制
        this.fixedFloorRules = fixedFloorRules ?? new List<FixedFloorNodeRule>(); // 若傳入為 null 則建立空列表避免 null
    }

    public IReadOnlyList<FixedFloorNodeRule> FixedFloorRules => fixedFloorRules ?? new List<FixedFloorNodeRule>(); // 對外提供固定樓層規則（確保不為 null）

    public RunMapGenerator.SlotAllocationSettings ToSettings() // 將可序列化設定轉成地圖生成器需要的設定型別
    {
        return new RunMapGenerator.SlotAllocationSettings( // 建立並回傳生成器設定
            shopMin, // 商店最少數
            shopMax, // 商店最多數
            eliteMin, // 菁英最少數
            eliteMax, // 菁英最多數
            restMin, // 休息最少數
            restMax, // 休息最多數
            eventRatioMin, // 事件比例下限
            eventRatioMax, // 事件比例上限
            typeConstraints.ToConstraints(), // 將型別限制設定轉成 Constraints
            FixedFloorRules); // 固定樓層規則（IReadOnlyList）
    }

    public static RunMapSlotAllocationSettings Default => new RunMapSlotAllocationSettings( // 預設值（方便一鍵建立可用配置）
        2, // shopMin
        3, // shopMax
        2, // eliteMin
        4, // eliteMax
        4, // restMin
        6, // restMax
        0.2f, // eventRatioMin
        0.25f, // eventRatioMax
        RunMapNodeTypeConstraintSettings.Default, // 預設型別限制
        new List<FixedFloorNodeRule>()); // 預設固定樓層規則：空
}

[Serializable] // 允許此 struct 被序列化
public struct RunMapNodeTypeConstraintSettings // 節點型別限制設定：控制各類節點在樓層出現的限制與間隔
{
    [SerializeField] private int eliteMinFloorOffset; // 菁英最早可出現的樓層偏移（距起點）
    [SerializeField] private int eliteMaxFloorOffsetFromBoss; // 菁英距 Boss 樓層的最大偏移（避免太接近 Boss）
    [SerializeField] private int shopMinFloorOffset; // 商店最早可出現的樓層偏移
    [SerializeField] private int shopMaxFloorOffsetFromBoss; // 商店距 Boss 的最大偏移
    [SerializeField] private int eventMinFloorOffset; // 事件最早可出現的樓層偏移
    [SerializeField] private int eventMaxFloorOffsetFromBoss; // 事件距 Boss 的最大偏移
    [SerializeField] private int restMinFloorOffset; // 休息最早可出現的樓層偏移
    [SerializeField] private int restMaxFloorOffsetFromBoss; // 休息距 Boss 的最大偏移
    [SerializeField] private int eliteMinGap; // 兩個菁英之間至少相隔多少樓
    [SerializeField] private int shopMinGap; // 兩個商店之間至少相隔多少樓
    [SerializeField] private int restMinGap; // 兩個休息之間至少相隔多少樓
    [SerializeField] private bool forbidConsecutiveElite; // 是否禁止連續菁英
    [SerializeField] private bool forbidConsecutiveShop; // 是否禁止連續商店
    [SerializeField] private bool forbidConsecutiveRest; // 是否禁止連續休息
    [SerializeField] private int maxConsecutiveEvents; // 事件最多允許連續幾個

    public RunMapNodeTypeConstraintSettings( // 建構子：設定所有限制參數
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
        this.eliteMinFloorOffset = eliteMinFloorOffset; // 設定菁英最早出現樓層
        this.eliteMaxFloorOffsetFromBoss = eliteMaxFloorOffsetFromBoss; // 設定菁英距 Boss 最遠偏移
        this.shopMinFloorOffset = shopMinFloorOffset; // 設定商店最早出現樓層
        this.shopMaxFloorOffsetFromBoss = shopMaxFloorOffsetFromBoss; // 設定商店距 Boss 最遠偏移
        this.eventMinFloorOffset = eventMinFloorOffset; // 設定事件最早出現樓層
        this.eventMaxFloorOffsetFromBoss = eventMaxFloorOffsetFromBoss; // 設定事件距 Boss 最遠偏移
        this.restMinFloorOffset = restMinFloorOffset; // 設定休息最早出現樓層
        this.restMaxFloorOffsetFromBoss = restMaxFloorOffsetFromBoss; // 設定休息距 Boss 最遠偏移
        this.eliteMinGap = eliteMinGap; // 設定菁英最小間隔
        this.shopMinGap = shopMinGap; // 設定商店最小間隔
        this.restMinGap = restMinGap; // 設定休息最小間隔
        this.forbidConsecutiveElite = forbidConsecutiveElite; // 是否禁止連續菁英
        this.forbidConsecutiveShop = forbidConsecutiveShop; // 是否禁止連續商店
        this.forbidConsecutiveRest = forbidConsecutiveRest; // 是否禁止連續休息
        this.maxConsecutiveEvents = maxConsecutiveEvents; // 設定事件最大連續數
    }

    public NodeTypeConstraints ToConstraints() // 轉成 NodeTypeConstraints（生成器運算用）
    {
        return new NodeTypeConstraints( // 建立並回傳 constraints
            eliteMinFloorOffset, // 菁英最早出現樓層
            eliteMaxFloorOffsetFromBoss, // 菁英距 Boss 最大偏移
            shopMinFloorOffset, // 商店最早出現樓層
            shopMaxFloorOffsetFromBoss, // 商店距 Boss 最大偏移
            eventMinFloorOffset, // 事件最早出現樓層
            eventMaxFloorOffsetFromBoss, // 事件距 Boss 最大偏移
            restMinFloorOffset, // 休息最早出現樓層
            restMaxFloorOffsetFromBoss, // 休息距 Boss 最大偏移
            eliteMinGap, // 菁英最小間隔
            shopMinGap, // 商店最小間隔
            restMinGap, // 休息最小間隔
            forbidConsecutiveElite, // 禁止連續菁英
            forbidConsecutiveShop, // 禁止連續商店
            forbidConsecutiveRest, // 禁止連續休息
            maxConsecutiveEvents); // 事件最大連續數
    }

    public static RunMapNodeTypeConstraintSettings Default => new RunMapNodeTypeConstraintSettings( // 預設限制（具名參數方便閱讀）
        eliteMinFloorOffset: 5, // 菁英最早第 5 層後
        eliteMaxFloorOffsetFromBoss: 2, // 菁英距 Boss 至少保留 2 層
        shopMinFloorOffset: 2, // 商店最早第 2 層後
        shopMaxFloorOffsetFromBoss: 3, // 商店距 Boss 至少保留 3 層內不生成
        eventMinFloorOffset: 1, // 事件最早第 1 層後
        eventMaxFloorOffsetFromBoss: 2, // 事件距 Boss 至少保留 2 層
        restMinFloorOffset: 2, // 休息最早第 2 層後
        restMaxFloorOffsetFromBoss: 1, // 休息距 Boss 至少保留 1 層
        eliteMinGap: 3, // 菁英間隔至少 3 層
        shopMinGap: 3, // 商店間隔至少 3 層
        restMinGap: 2, // 休息間隔至少 2 層
        forbidConsecutiveElite: true, // 禁止連續菁英
        forbidConsecutiveShop: true, // 禁止連續商店
        forbidConsecutiveRest: true, // 禁止連續休息
        maxConsecutiveEvents: 2); // 事件最多連續 2 個
}

[Serializable] // 允許此 struct 被序列化
public struct FixedFloorNodeRule // 固定樓層節點規則：指定某樓層必定是某種類型
{
    [SerializeField] private int floorIndex; // 指定樓層索引（通常從 0 開始）
    [SerializeField] private MapNodeType nodeType; // 指定該樓層應生成的節點類型

    public int FloorIndex => floorIndex; // 對外讀取樓層索引
    public MapNodeType NodeType => nodeType; // 對外讀取節點類型

    public FixedFloorNodeRule(int floorIndex, MapNodeType nodeType) // 建構子：設定樓層與類型
    {
        this.floorIndex = floorIndex; // 設定樓層索引
        this.nodeType = nodeType; // 設定節點類型
    }
}

[Serializable] // 允許此 struct 被序列化
public struct RunMapConnectionSettings // 連線設定：控制節點之間連線數量、密度、分支等
{
    [SerializeField] private int connectionNeighborWindow; // 連線鄰近範圍（候選目標的水平窗格大小）
    [SerializeField] private int maxOutgoingPerNode; // 每個節點最多向外連出幾條
    [SerializeField] private int minConnectedSourcesPerRow; // 每一列至少要有幾個來源被連上（避免孤立）
    [SerializeField] private int backtrackAllowance; // 允許回連/回溯的程度（若生成器支援）
    [SerializeField] private float connectionDensity; // 連線密度係數（越大越多連線）
    [SerializeField] private int minIncomingPerTarget; // 每個目標至少要有幾條進入連線
    [SerializeField] private int minDistinctSourcesToBoss; // 到 Boss 的路徑至少要有多少不同來源（確保多樣性）
    [SerializeField, Range(0f, 1f)] private float longLinkChance; // 長連線出現機率（跨更遠距離）
    [SerializeField] private int minDistinctTargetsPerFloor; // 每層至少要有多少不同目標節點
    [SerializeField] private int minBranchingNodesPerFloor; // 每層至少有多少節點會分岔
    [SerializeField] private int maxBranchingNodesPerFloor; // 每層最多有多少節點會分岔

    public RunMapConnectionSettings( // 建構子：設定所有連線參數
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
        this.connectionNeighborWindow = connectionNeighborWindow; // 設定鄰近範圍
        this.maxOutgoingPerNode = maxOutgoingPerNode; // 設定最大外連數
        this.minConnectedSourcesPerRow = minConnectedSourcesPerRow; // 設定每列最小被連來源數
        this.backtrackAllowance = backtrackAllowance; // 設定回溯允許程度
        this.connectionDensity = connectionDensity; // 設定連線密度
        this.minIncomingPerTarget = minIncomingPerTarget; // 設定每個目標最小進入連線數
        this.minDistinctSourcesToBoss = minDistinctSourcesToBoss; // 設定到 Boss 的最小不同來源數
        this.longLinkChance = longLinkChance; // 設定長連線機率
        this.minDistinctTargetsPerFloor = minDistinctTargetsPerFloor; // 設定每層最小不同目標數
        this.minBranchingNodesPerFloor = minBranchingNodesPerFloor; // 設定每層最小分岔節點數
        this.maxBranchingNodesPerFloor = maxBranchingNodesPerFloor; // 設定每層最大分岔節點數
    }

    public int ConnectionNeighborWindow => Mathf.Max(0, connectionNeighborWindow); // 鄰近範圍至少 0
    public int MaxOutgoingPerNode => Mathf.Max(1, maxOutgoingPerNode); // 最大外連至少 1
    public int MinConnectedSourcesPerRow => Mathf.Max(1, minConnectedSourcesPerRow); // 每列最小連通來源至少 1
    public int BacktrackAllowance => Mathf.Max(0, backtrackAllowance); // 回溯允許至少 0
    public float ConnectionDensity => Mathf.Max(1f, connectionDensity); // 連線密度至少 1（避免過低導致過少連線）
    public int MinIncomingPerTarget => Mathf.Max(1, minIncomingPerTarget); // 每個目標最小進入連線至少 1
    public int MinDistinctSourcesToBoss => Mathf.Max(1, minDistinctSourcesToBoss); // 到 Boss 的不同來源至少 1
    public float LongLinkChance => Mathf.Clamp01(longLinkChance); // 長連線機率限制在 0~1
    public int MinDistinctTargetsPerFloor => Mathf.Max(1, minDistinctTargetsPerFloor); // 每層不同目標至少 1
    public int MinBranchingNodesPerFloor => Mathf.Max(1, minBranchingNodesPerFloor); // 每層分岔節點至少 1
    public int MaxBranchingNodesPerFloor => Mathf.Max(MinBranchingNodesPerFloor, maxBranchingNodesPerFloor); // 最大分岔至少不小於最小分岔

    public static RunMapConnectionSettings Default => new RunMapConnectionSettings( // 預設連線設定
        2, // connectionNeighborWindow
        4, // maxOutgoingPerNode
        3, // minConnectedSourcesPerRow
        1, // backtrackAllowance
        1.5f, // connectionDensity
        1, // minIncomingPerTarget
        3, // minDistinctSourcesToBoss
        0.2f, // longLinkChance
        2, // minDistinctTargetsPerFloor
        1, // minBranchingNodesPerFloor
        3); // maxBranchingNodesPerFloor
}
