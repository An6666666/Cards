using System.Collections.Generic;         // 使用泛型集合，例如 List<T>
using UnityEngine;                        // 使用 Unity 引擎的核心功能

public class GouShe : Enemy               // 鉤蛇怪物類別，繼承自 Enemy 基底類
{
    private static readonly Vector2Int OffBoardSentinel = new Vector2Int(int.MinValue / 2, int.MinValue / 2);
    // 一個特殊座標，用來代表「暫時離開棋盤」（不在任何有效格子上）

    [Header("Gou She Settings")]
    [SerializeField] private int waterArmor = 2;                 // 站在水格上時獲得的額外護甲值
    [SerializeField] private int columnStrikeDamage = 10;        // 直線打擊技能的傷害
    [SerializeField] private int columnStrikeWeakDuration = 2;   // 直線打擊附加虛弱狀態的回合數
    [SerializeField] private int columnStrikeCooldownTurns = 2;  // 直線打擊技能冷卻回合數

    [Header("Passive Settings")]
    [SerializeField, Range(0f, 1f)] private float extraStrikeChance = 0.5f;
    // 普通攻擊時額外多打一段傷害的機率（0~1 之間）

    [SerializeField, Range(0f, 1f)] private float extraStrikeDamageRatio = 0.3f;
    // 額外一段傷害的比例（相對於本次攻擊傷害）

    private int columnStrikeCooldownRemaining = 0;               // 目前距離直線打擊可用還剩幾回合冷卻
    private bool columnStrikePending = false;                    // 是否已經進入「直線打擊準備完成，等待發動」狀態
    private int columnStrikeTargetColumn = 0;                    // 要攻擊的目標欄位（x 座標）
    private readonly List<BoardTile> columnStrikeHighlightedTiles = new List<BoardTile>();
    // 被標記為即將被直線打擊的格子清單，用來之後清除高亮

    private Vector2Int storedGridBeforeHide;                     // 在消失前記錄的原來棋盤座標
    private SpriteRenderer[] cachedRenderers;                    // 快取身上所有 SpriteRenderer，方便一鍵隱藏/顯示
    private bool initialWaterPrepared = false;                   // 是否已經建立過初始水域區域

    protected override void Awake()
    {
        enemyName = "鉤蛇";          // 設定敵人名稱
        maxHP = 60;                 // 最大生命值
        BaseAttackDamage = 10;      // 基礎攻擊傷害
        base.Awake();               // 呼叫基底 Enemy.Awake() 做通用初始化
    }

    private void Start()
    {
        PrepareInitialWaterZones(); // 開場時建立初始的水元素區域
    }

    public override void ProcessTurnStart()
    {
        base.ProcessTurnStart();    // 先執行基底的回合開始流程（處理 buff 等）
        TickColumnStrikeCooldown(); // 處理直線打擊技能的冷卻回合遞減
    }

    public override void EnemyAction(Player player)
    {
        if (HandleFrozenOrStunned())   // 若有凍結或暈眩狀態，處理回合消耗後直接結束行動
        {
            return;
        }

        ApplyWaterArmorIfOnTile();     // 若站在水格上，獲得水護甲加成

        if (columnStrikePending)       // 若已進入直線打擊準備完成狀態
        {
            ResolveColumnStrike(player); // 執行直線打擊（結算傷害、回到場上）
            return;
        }

        if (columnStrikeCooldownRemaining <= 0 && IsOnWaterTile() && TryPrepareColumnStrike(player))
        {
            // 冷卻結束 + 站在水格上 + 成功準備直線打擊 → 本回合只做準備就 return
            return;
        }

        if (IsPlayerInRange(player))   // 若玩家在普通攻擊範圍內
        {
            PerformAttackWithBonus(player); // 進行帶有被動額外傷害機率的普通攻擊
        }
        else
        {
            MoveOneStepTowards(player); // 否則朝玩家移動一格
        }
    }

    public override void DecideNextIntent(Player player)
    {
        if (player == null)                       // 沒有玩家目標時
        {
            nextIntent.type = EnemyIntentType.Idle;   // 顯示為待機
            nextIntent.value = 0;
            UpdateIntentIcon();                      // 更新頭上意圖圖示
            return;
        }

        if (frozenTurns > 0 || buffs.stun > 0)   // 若下回合會被凍結或暈眩
        {
            nextIntent.type = EnemyIntentType.Idle;   // 意圖顯示為無行動
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        if (columnStrikePending)                // 若已經準備好直線打擊，下一步就是發動技能
        {
            nextIntent.type = EnemyIntentType.Skill;  // 顯示技能意圖
            nextIntent.value = columnStrikeDamage;    // 顯示預計傷害
            UpdateIntentIcon();
            return;
        }

        bool specialReady = columnStrikeCooldownRemaining <= 0 && IsOnWaterTile();
        // 判斷直線打擊是否可準備（冷卻歸零且在水格）

        if (specialReady)
        {
            nextIntent.type = EnemyIntentType.Skill;  // 下一步打算施放技能
            nextIntent.value = columnStrikeDamage;
            UpdateIntentIcon();
            return;
        }

        if (IsPlayerInRange(player))           // 否則看玩家是否在普通攻擊範圍內
        {
            nextIntent.type = EnemyIntentType.Attack;     // 顯示普通攻擊意圖
            nextIntent.value = CalculateAttackDamage();   // 顯示普通攻擊傷害
        }
        else if (canMove)                      // 不在攻擊範圍，但可以移動
        {
            nextIntent.type = EnemyIntentType.Move;       // 顯示移動意圖
            nextIntent.value = 0;
        }
        else                                   // 無法移動也無法攻擊
        {
            nextIntent.type = EnemyIntentType.Idle;       // 顯示待機
            nextIntent.value = 0;
        }

        UpdateIntentIcon();                    // 最後更新意圖圖示
    }

    private bool HandleFrozenOrStunned()
    {
        if (frozenTurns > 0)         // 若目前有凍結回合
        {
            frozenTurns--;          // 減少一回合
            return true;            // 回合直接結束（這回合不能動）
        }

        if (buffs.stun > 0)         // 若目前有暈眩回合
        {
            buffs.stun--;           // 減少一回合
            return true;            // 回合直接結束
        }

        return false;               // 沒有凍結或暈眩，可以正常行動
    }

    private void ApplyWaterArmorIfOnTile()
    {
        if (waterArmor <= 0)        // 若設定為 0 或以下，就不處理
        {
            return;
        }

        if (!IsOnWaterTile())       // 若沒站在水元素格
        {
            return;
        }

        block += waterArmor;        // 增加護甲（block）
    }

    private bool IsOnWaterTile()
    {
        Board board = FindObjectOfType<Board>(); // 尋找棋盤物件
        if (board == null)
        {
            return false;                        // 沒有棋盤就無法判斷
        }

        BoardTile tile = board.GetTileAt(gridPosition); // 取得當前所在格子
        return tile != null && tile.HasElement(ElementType.Water);
        // 若格子存在且具有水元素，則回傳 true
    }

    private void PerformAttackWithBonus(Player player)
    {
        if (player == null)             // 若沒有玩家目標
        {
            return;
        }

        int damage = CalculateAttackDamage(); // 由 Enemy 基底計算實際攻擊傷害（含 buff 等）
        if (damage <= 0)              // 若傷害不大於 0，就不攻擊
        {
            return;
        }

        player.TakeDamage(damage);    // 對玩家造成一次基本攻擊傷害

        if (Random.value <= extraStrikeChance)   // 依照機率額外再打一段傷害
        {
            int extraDamage = Mathf.CeilToInt(damage * extraStrikeDamageRatio);
            // 額外傷害 = 本次傷害 * 比例，向上取整

            if (extraDamage > 0)
            {
                player.TakeDamage(extraDamage);  // 再次對玩家造成額外傷害
            }
        }
    }

    private bool TryPrepareColumnStrike(Player player)
    {
        if (player == null)           // 無玩家目標就無法準備直線打擊
        {
            return false;
        }

        Board board = FindObjectOfType<Board>(); // 取得棋盤
        if (board == null)
        {
            return false;
        }

        List<Vector2Int> columnPositions = new List<Vector2Int>(); // 用來記錄與玩家同一欄位的所有格子座標
        foreach (Vector2Int pos in board.GetAllPositions())        // 走訪棋盤上所有位置
        {
            if (pos.x == player.position.x)                        // 若該位置的 x 與玩家位置的 x 相同
            {
                columnPositions.Add(pos);                          // 加入同一欄位清單
            }
        }

        if (columnPositions.Count == 0)                            // 若沒有任何同欄位格子（理論上不會發生）
        {
            return false;
        }

        ClearColumnHighlights();                                   // 清除舊的高亮格子

        foreach (Vector2Int pos in columnPositions)                // 將同欄位的每一個格子標記為攻擊範圍
        {
            BoardTile tile = board.GetTileAt(pos);
            if (tile != null)
            {
                tile.SetAttackHighlight(true);                     // 顯示攻擊高亮
                columnStrikeHighlightedTiles.Add(tile);            // 加入到目前高亮清單中
            }
        }

        storedGridBeforeHide = gridPosition;                       // 記錄消失前的原本座標
        columnStrikeTargetColumn = player.position.x;              // 設定要打擊的目標欄位
        columnStrikePending = true;                                // 標記為「已準備好，下回合發動」
        SetHidden(true);                                           // 把自己隱藏（SpriteRenderer.enabled = false）
        SetHighlight(false);                                       // 關閉自身的選取高亮
        gridPosition = OffBoardSentinel;                           // 把棋盤座標設為「離開棋盤」的特殊值
        return true;                                               // 準備成功
    }

    private void ResolveColumnStrike(Player player)
    {
        bool playerHit = player != null && player.position.x == columnStrikeTargetColumn;
        // 判斷玩家目前是否仍然站在被鎖定的欄位上

        if (playerHit)
        {
            player.TakeDamage(columnStrikeDamage);                    // 對玩家造成直線打擊傷害
            player.buffs.ApplyWeakFromEnemy(columnStrikeWeakDuration);// 對玩家施加虛弱 debuff
        }

        ClearColumnHighlights();                                      // 清除欄位上的攻擊預告高亮

        Board board = FindObjectOfType<Board>();                      // 再次抓棋盤
        Vector2Int targetPos = ChooseReappearPosition(board, player); // 選一個重新現身的位置
        MoveToPosition(targetPos);                                    // 將敵人移動到該位置（更新座標與位置）
        SetHidden(false);                                             // 顯示自己（恢復 SpriteRenderer）

        columnStrikePending = false;                                  // 不再處於待發動狀態
        columnStrikeTargetColumn = 0;                                 // 清空目標欄位
        columnStrikeCooldownRemaining = columnStrikeCooldownTurns;    // 重置技能冷卻
    }

    private Vector2Int ChooseReappearPosition(Board board, Player player)
    {
        Vector2Int bestPos = storedGridBeforeHide;                    // 預設回到消失前的位置
        float bestDistance = float.MaxValue;                          // 用於找距離玩家最近的目標

        if (board != null)
        {
            foreach (Vector2Int pos in board.GetAllPositions())       // 走訪棋盤上所有格
            {
                BoardTile tile = board.GetTileAt(pos);
                if (tile == null || !tile.HasElement(ElementType.Water))
                {
                    continue;                                         // 必須是存在、而且有水元素的格子
                }

                if (IsPositionBlocked(board, pos, player))
                {
                    continue;                                         // 若該位置被佔用就略過
                }

                float dist = player != null ? Vector2Int.Distance(pos, player.position) : 0f;
                // 若有玩家，就計算與玩家的距離；否則距離設為 0

                if (dist < bestDistance)                              // 找距離玩家最近的位置
                {
                    bestDistance = dist;
                    bestPos = pos;
                }
            }
        }

        if (board != null && (board.GetTileAt(bestPos) == null || IsPositionBlocked(board, bestPos, player)))
        {
            // 若剛剛選出來的位置已經不可用（或沒有格子），就退而求其次找任一沒被阻擋的格子
            foreach (Vector2Int pos in board.GetAllPositions())
            {
                if (!IsPositionBlocked(board, pos, player))
                {
                    bestPos = pos;                                    // 找到第一個可站的格子就用它
                    break;
                }
            }
        }

        return bestPos;                                               // 回傳最後決定的現身位置
    }

    private bool IsPositionBlocked(Board board, Vector2Int pos, Player player)
    {
        if (board == null)
        {
            return true;                                              // 沒有棋盤就視為不可站
        }

        if (player != null && player.position == pos)
        {
            return true;                                              // 若該格是玩家目前位置，也視為被占用
        }

        return board.IsTileOccupied(pos);                             // 若棋盤判定該格有其他單位，也視為被占用
    }

    private void ClearColumnHighlights()
    {
        foreach (BoardTile tile in columnStrikeHighlightedTiles)      // 將之前紀錄的高亮格子逐一清除
        {
            if (tile != null)
            {
                tile.SetAttackHighlight(false);                       // 關閉攻擊預告高亮
            }
        }

        columnStrikeHighlightedTiles.Clear();                         // 清空清單
    }

    private void PrepareInitialWaterZones()
    {
        if (initialWaterPrepared)              // 若已經準備過初始水域，就不重複進行
        {
            return;
        }

        Board board = FindObjectOfType<Board>(); // 抓取棋盤
        if (board == null)
        {
            return;
        }

        List<Vector2Int> positions = board.GetAllPositions(); // 取得棋盤上所有位置
        if (positions.Count == 0)
        {
            return;                                           // 若沒有格子就沒事可做
        }

        initialWaterPrepared = true;                          // 標記已經完成一次初始化

        int clusterCount = Mathf.Min(3, positions.Count);     // 最多建立 3 個水域群組（若格子較少就取較小值）
        for (int i = 0; i < clusterCount; i++)
        {
            int index = Random.Range(0, positions.Count);     // 隨機從剩餘格子中選一個中心點
            Vector2Int center = positions[index];             // 把該位置當成水域中心
            positions.RemoveAt(index);                        // 從列表中移除，避免重複選中
            ApplyWaterAround(center, board);                  // 在該中心周圍布置水元素格
        }
    }

    private void ApplyWaterAround(Vector2Int center, Board board)
    {
        BoardTile centerTile = board.GetTileAt(center);       // 取得中心格子
        if (centerTile != null)
        {
            centerTile.AddElement(ElementType.Water);          // 在中心格加上水元素
        }

        foreach (BoardTile tile in board.GetAdjacentTiles(center)) // 取得中心格的相鄰格子
        {
            tile.AddElement(ElementType.Water);                // 對每個鄰近格也加上水元素
        }
    }

    private void TickColumnStrikeCooldown()
    {
        if (columnStrikePending)                               // 若直線打擊正在等待發動（pending），就不扣冷卻
        {
            return;
        }

        if (columnStrikeCooldownRemaining > 0)                 // 冷卻回合大於 0 才需要遞減
        {
            columnStrikeCooldownRemaining--;                   // 每回合開始遞減 1
        }
    }

    private void SetHidden(bool hidden)
    {
        EnsureRendererCache();                                 // 確保已經把所有子孫 SpriteRenderer 抓起來
        foreach (var renderer in cachedRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = !hidden;                    // hidden = true → 關閉渲染；false → 顯示
            }
        }
    }

    private void EnsureRendererCache()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            // 從自己與子物件中撈出所有 SpriteRenderer（含隱藏物件）
        }
    }
}
