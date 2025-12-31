using System.Collections.Generic;            // 使用泛型集合，方便用 List、HashSet 這類容器
using UnityEngine;                           // 使用 Unity 引擎相關的類別（MonoBehaviour、Vector2Int、SerializeField 等）

// 鷹鴿 Boss，繼承你專案裡的 Enemy 基底類別
public class YingGe : Enemy
{
    // 覆寫基底的屬性，讓這個 Boss 不會在每回合自動把護甲歸零
    public override bool ShouldResetBlockEachTurn => false;

    [Header("Ying Ge Base Stats")]            // 在 Inspector 分類顯示：鷹鴿的基本數值
    [SerializeField] private int startingMaxHP = 160;     // 初始最大血量
    [SerializeField] private int startingBaseAttack = 18; // 初始基礎攻擊力

    [Header("Ying Ge Abilities")]             // 在 Inspector 分類顯示：鷹鴿的技能數值
    [SerializeField] private int armorPerTurn = 4;        // 每回合結束自動獲得的護甲量
    [SerializeField] private int miasmaDamage = 5;        // 瘴氣格子給玩家的傷害
    [SerializeField] private int miasmaCenters = 2;       // 開場要放幾個「瘴氣中心」
    [SerializeField] private int stoneFeatherDamage = 15; // 石羽雨命中玩家的傷害
    [SerializeField] private int stoneFeatherCooldown = 4;// 石羽雨冷卻幾回合才能再用

    [Header("Resurrection Stone Settings")]   // 在 Inspector 分類顯示：復活石相關設定
    [SerializeField] private YingGeStone stonePrefab;     // 待會要生出來的復活石預置物
    [SerializeField] private int stoneHealth = 100;       // 復活石的血量
    [SerializeField] private int stoneRespawnWaitTurns = 2; // 復活石要撐幾回合才能讓鷹鴿復活

    // 一個「丟到場外」用的座標，避免真的佔到棋盤
    private static readonly Vector2Int OffBoardSentinel = new Vector2Int(int.MinValue / 2, int.MinValue / 2);

    // 記錄所有被塗成瘴氣的格子座標
    private readonly HashSet<Vector2Int> miasmaTiles = new HashSet<Vector2Int>();
    // 記錄石羽雨這次要打到的所有格子
    private readonly HashSet<Vector2Int> stoneFeatherTargets = new HashSet<Vector2Int>();
    // 記錄這次被高亮顯示的格子（之後要還原）
    private readonly List<BoardTile> stoneFeatherHighlightedTiles = new List<BoardTile>();

    private YingGeStone activeStone;          // 當前正在場上的復活石
    private BattleManager battleManager;      // 戰鬥管理器的參考
    private SpriteRenderer[] cachedRenderers; // 快取這個 Boss 底下所有 SpriteRenderer，方便一起隱藏/顯示
    private EnemyElementStatusDisplay elementStatusDisplay;   //新增：元素圖示控制元件的參考

    // ====== 復活相關旗標 ======
    private bool resurrectionTriggered = false;   // 是否已經進入過復活流程
    private bool awaitingRespawn = false;         // 是否正在等待復活（本體暫時隱藏）
    private bool hasRespawned = false;            // 是否已經復活過一次
    private bool finalDeathHandled = false;       // 是否已經做過最終死亡處理，避免重複呼叫
    private bool stoneRespawnCompleted = false;   // 復活石是否已經撐完時間通知可以復活

    // ====== 石羽雨相關 ======
    private int stoneFeatherCooldownTimer = 0;    // 石羽雨已累積的回合數
    private bool stoneFeatherPending = false;     // 是否目前處於「已經預告，等下一拍真正打下去」的狀態

    private Vector2Int storedGridBeforeHide;      // Boss 在隱藏前原本站的格子位置（之後要回來用）

    public bool IsAwaitingRespawn => awaitingRespawn; // 外部可讀：目前是否在等待復活

    // Unity 生命週期：Awake，這裡做基本初始化
    protected override void Awake()
    {
        maxHP = Mathf.Max(1, startingMaxHP);           // 把最大血量設定成 Inspector 的數值，至少為 1
        BaseAttackDamage = Mathf.Max(0, startingBaseAttack); // 設定基礎攻擊力
        isBoss = true;                                 // 標記這個敵人是 Boss
        battleManager = FindObjectOfType<BattleManager>(); // 找場上的 BattleManager
        base.Awake();                                  // 呼叫基底 Enemy 的 Awake 做原本的初始化
    }

    // Unity 生命週期：Start，通常這裡可以做需要場景都載入後的動作
    private void Start()
    {
        ApplyInitialMiasma();                          // 一開場就把瘴氣灑到棋盤上
    }

    // 每回合開始時會被呼叫
    public override void ProcessTurnStart()
    {
        base.ProcessTurnStart();                       // 先讓基底做它原本的回合開始處理
        if (battleManager == null)
        {
            battleManager = FindObjectOfType<BattleManager>();
        }

        if (battleManager == null || battleManager.IsProcessingEnemyTurnStart)
        {
            AdvanceStoneFeatherCooldown();
        }
    }

    // 敵人這回合實際要做的行動
    public override void EnemyAction(Player player)
    {
        // 如果正在等待復活，就什麼都不做
        if (awaitingRespawn)
        {
            return;
        }

        // 先看有沒有被冰凍、暈眩這類控場，如果有，這回合就等於沒行動，但還是會加護甲
        if (HandleCrowdControl())
        {
            GainEndOfTurnArmor(); // 控場狀態下也可以疊護甲
            return;
        }

        // 如果石羽雨已經預告過了，這回合要正式落下
        if (stoneFeatherPending)
        {
            ResolveStoneFeather(player); // 判定玩家位置是否在被標記的格子上，若是就扣血
            GainEndOfTurnArmor();        // 行動完加護甲
            return;
        }

        // 如果冷卻到了、玩家存在，而且這回合成功啟動石羽雨，就結束行動
        if (player != null && stoneFeatherCooldownTimer >= stoneFeatherCooldown && TryActivateStoneFeather(player))
        {
            GainEndOfTurnArmor();        // 啟動完也加護甲
            return;
        }

        // 上面都沒進去，就走基底 Enemy 的一般行動（可能是普攻或其他 AI）
        base.EnemyAction(player);
        GainEndOfTurnArmor();            // 行動完一樣加護甲
    }

    // 覆寫死亡流程，因為這個 Boss 有一次特殊復活
    protected internal override void Die()
    {
        // 如果還沒觸發復活、而且也還沒復活過
        if (!resurrectionTriggered && !hasRespawned)
        {
            // 嘗試進入第一次死亡 → 生成復活石 → 隱藏本體
            if (TryHandleFirstDeath())
            {
                // 如果處理成功，就先不真的死
                return;
            }
        }

        // 否則就是該真的死了
        FinalizeDeath();
    }

    // 處理冰凍、暈眩這類控場回合消耗，回傳 true 表示這回合不能動
    private bool HandleCrowdControl()
    {
        if (frozenTurns > 0)             // 還有冰凍回合
        {
            frozenTurns--;               // 減少一回合
            return true;                 // 這回合結束
        }

        if (buffs.stun > 0)              // 還有暈眩回合
        {
            buffs.stun--;                // 減少一回合
            return true;                 // 這回合結束
        }

        return false;                    // 沒有控場，這回合可以正常行動
    }

    // 行動結束時自動加護甲
    private void GainEndOfTurnArmor()
    {
        if (armorPerTurn <= 0)           // 沒設定就不加
        {
            return;
        }

        // block = 現在護甲 + 每回合護甲，確保不會是負的
        block = Mathf.Max(0, block + armorPerTurn);
    }

    // 石羽雨的冷卻回合往前推進
    private void AdvanceStoneFeatherCooldown()
    {
        if (stoneFeatherPending)         // 如果已經在「等待落下」的狀態，就先不要累積
        {
            return;
        }

        if (stoneFeatherCooldownTimer < stoneFeatherCooldown)   // 還在累積回合
        {
            stoneFeatherCooldownTimer++;     // 增加一回合累積
        }
    }

    // 嘗試啟動石羽雨（決定這次要打哪些格子、顯示高亮、讓 Boss 暫時消失）
    private bool TryActivateStoneFeather(Player player)
    {
        Board board = FindObjectOfType<Board>();   // 拿棋盤
        if (board == null)                         // 沒棋盤就沒辦法發動
        {
            return false;
        }

        stoneFeatherTargets.Clear();               // 先把上一次記錄的攻擊目標清掉
        ClearStoneFeatherIndicators();             // 把上一次的高亮也清掉

        Vector2Int playerPos = player.position;    // 拿玩家目前站的格子
        // 這次要高亮的「橫列」：玩家這一列、上面兩格的那一列、下面兩格的那一列
        int[] rowCandidates = { playerPos.y, playerPos.y + 2, playerPos.y - 2 };

        // 把這三條列上所有格子都標記起來
        foreach (int row in rowCandidates)
        {
            HighlightRow(board, row);
        }

        // 如果標記完結果沒有任何格子要打，就當作這次石羽雨失敗
        if (stoneFeatherTargets.Count == 0)
        {
            return false;
        }

        // 開始進入「石羽雨準備中」狀態，下一回合會真的打下去
        stoneFeatherPending = true;
        // 重設累積時間
        stoneFeatherCooldownTimer = 0;

        // 記錄隱藏前的位置，之後要回來
        storedGridBeforeHide = gridPosition;
        SetHidden(true);                 // 把 Boss 外觀藏起來
        SetHighlight(false);             // 不要讓它像被選取一樣
        SetForceHideIntent(true);        // 石羽雨預告期間 → 不顯示意圖
        gridPosition = OffBoardSentinel; // 把格子位置移到場外，避免跟其他單位衝突

        // 確認手上有 battleManager
        if (battleManager == null)
        {
            battleManager = FindObjectOfType<BattleManager>();
        }

        return true;                     // 成功啟動石羽雨
    }

    // 真正讓石羽雨落下，檢查玩家是否在目標格子上
    private void ResolveStoneFeather(Player player)
    {
        // 如果玩家還在，且玩家目前的位置是我們這次標記的其中一格，就扣血
        if (player != null && stoneFeatherTargets.Contains(player.position))
        {
            player.TakeDamage(stoneFeatherDamage); // 玩家吃到石羽雨傷害
        }

        stoneFeatherPending = false;     // 這次石羽雨處理完了
        ClearStoneFeatherIndicators();   // 把高亮清掉
        ReappearAfterStoneFeather();     // Boss 再次出現在棋盤上
    }

    // 把某一條橫列全部標記成這次石羽雨的攻擊範圍
    private void HighlightRow(Board board, int row)
    {
        List<Vector2Int> allPositions = board.GetAllPositions(); // 拿到棋盤上所有格子的座標
        foreach (Vector2Int pos in allPositions)
        {
            if (pos.y != row)           // 只要 y 不等於我們要的那條列，就跳過
            {
                continue;
            }

            BoardTile tile = board.GetTileAt(pos); // 用座標拿格子
            if (tile == null)                      // 有可能為空，就跳過
            {
                continue;
            }

            if (!stoneFeatherTargets.Add(pos))     // 把這格加入這次要攻擊的格子集合，如果已經有了就不重複
            {
                continue;
            }

            if (!stoneFeatherHighlightedTiles.Contains(tile)) // 沒有高亮過這格才處理
            {
                tile.SetAttackHighlight(true);     // 顯示攻擊預警（一般是紅色框）
                stoneFeatherHighlightedTiles.Add(tile); // 記錄起來，之後要清
            }
        }
    }

    // 把石羽雨的高亮預警全部取消
    private void ClearStoneFeatherIndicators()
    {
        foreach (BoardTile tile in stoneFeatherHighlightedTiles)
        {
            if (tile != null)
            {
                tile.SetAttackHighlight(false);    // 把格子高亮關掉
            }
        }

        stoneFeatherHighlightedTiles.Clear();      // 清除高亮的清單
        stoneFeatherTargets.Clear();               // 清除要攻擊的目標格子
    }

    // 石羽雨打完後，Boss 要再出現，並找一個位置站
    private void ReappearAfterStoneFeather()
    {
        if (battleManager == null)
        {
            battleManager = FindObjectOfType<BattleManager>();
        }

        Board board = FindObjectOfType<Board>();   // 拿棋盤
        // 預設可用位置先抓棋盤所有格子
        List<Vector2Int> availablePositions = board != null ? board.GetAllPositions() : new List<Vector2Int>();
        Player player = FindObjectOfType<Player>(); // 找玩家
        if (player != null)
        {
            availablePositions.Remove(player.position); // 不要跟玩家站同一格
        }

        // 把場上其他敵人佔的格子也拔掉，避免重疊
        Enemy[] allEnemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in allEnemies)
        {
            if (enemy != null && enemy != this)
            {
                availablePositions.Remove(enemy.gridPosition);
            }
        }

        // 預設要回到隱藏前的原位
        Vector2Int targetPos = storedGridBeforeHide;
        // 如果有空格，就隨機挑一個站
        if (availablePositions.Count > 0)
        {
            targetPos = availablePositions[Random.Range(0, availablePositions.Count)];
        }

        MoveToPosition(targetPos);       // 把 Boss 的邏輯位置移回棋盤
        SetHidden(false);                // 顯示出來
        SetForceHideIntent(false);      //回到場上 → 意圖恢復顯示

        // 確保 Boss 有被加回 battleManager 的敵人列表裡
        if (battleManager != null && !battleManager.enemies.Contains(this))
        {
            battleManager.enemies.Add(this);
        }
    }

    // 處理第一次死亡（假死）：生成復活石，隱藏 Boss，等復活石撐完時間再回來
    private bool TryHandleFirstDeath()
    {
        // 如果已經在等復活，或已經觸發過復活，就不用再進來
        if (awaitingRespawn || resurrectionTriggered)
        {
            return awaitingRespawn;      // 回傳目前是不是在等復活
        }

        resurrectionTriggered = true;    // 標記我們已經進入復活流程了
        storedGridBeforeHide = gridPosition; // 記錄現在的位置，之後可能要用

        Vector2Int stoneGrid = gridPosition;   // 復活石要放的格子（就放在原地）
        Vector3 stoneWorld = transform.position; // 復活石要放的世界座標

        gridPosition = OffBoardSentinel; // 先把 Boss 移出棋盤
        SetHidden(true);                 // 把 Boss 藏起來
        SetHighlight(false);             // 也不要有高亮
        SetForceHideIntent(true);       //進入假死階段 → 不顯示意圖

        // 實際生成復活石
        YingGeStone stone = CreateStoneInstance(stoneGrid, stoneWorld);
        if (stone == null)               // 如果生成失敗，就只好放棄復活，回原位
        {
            gridPosition = storedGridBeforeHide;
            SetHidden(false);
            return false;
        }

        activeStone = stone;             // 記住這顆是我們的復活石
        stoneRespawnCompleted = false;   // 還沒撐完
        awaitingRespawn = true;          // 目前是在等復活的狀態

        if (battleManager == null)
        {
            battleManager = FindObjectOfType<BattleManager>();
        }

        if (battleManager != null)
        {
            battleManager.enemies.Remove(this); // 暫時把 Boss 從敵人列表移除
            if (!battleManager.enemies.Contains(stone))
            {
                battleManager.enemies.Add(stone); // 把復活石加進敵人列表，讓它也能吃回合
            }
        }

        return true;                     // 成功進入假死流程
    }

    // 實際生成一顆復活石，並初始化它的資料
    private YingGeStone CreateStoneInstance(Vector2Int gridPos, Vector3 worldPos)
    {
        YingGeStone instance = null;
        if (stonePrefab != null)         // 有指定預置物就用預置物
        {
            instance = Instantiate(stonePrefab, worldPos, Quaternion.identity);
        }
        else                             // 沒有的話就動態建一個空物件再加腳本
        {
            GameObject go = new GameObject("YingGeStone");
            go.transform.position = worldPos;
            instance = go.AddComponent<YingGeStone>();
        }

        if (instance == null)            // 還是沒成功就回傳 null
        {
            return null;
        }

        // 把這顆石頭的等待回合、血量、位置通通設定好，並且告訴它誰是老闆（這個 Boss）
        instance.ConfigureFromOwner(this, gridPos, worldPos, stoneRespawnWaitTurns, stoneHealth);
        return instance;
    }

    // 復活石撐完回合後會呼叫這個，通知 Boss 可以復活了
    public void HandleStoneReady(YingGeStone stone)
    {
        // 如果不是我們記錄的那顆石頭，就忽略
        if (stone == null || stone != activeStone)
        {
            return;
        }

        stoneRespawnCompleted = true;    // 石頭任務完成
        awaitingRespawn = false;         // 不用再等復活了
        activeStone = null;              // 清掉目前石頭的參考

        Vector2Int respawnGrid = stone.gridPosition;         // 用石頭所在的格子當復活位置
        Vector3 respawnWorld = stone.transform.position;     // 用石頭的世界座標當復活位置

        stone.DetachOwner();             // 石頭跟 Boss 脫鉤（避免再通知）
        // 把石頭從戰鬥列表移除
        if (battleManager != null)
        {
            battleManager.enemies.Remove(stone);
        }

        // 讓 Boss 復活回來
        RespawnFromStone(respawnGrid, respawnWorld);
        Destroy(stone.gameObject);       // 石頭完成任務後就銷毀
    }

    // 如果復活石在時間內被打爆，會呼叫這個，告訴 Boss 復活失敗
    public void OnStoneDestroyed(YingGeStone stone)
    {
        // 不是我們的那顆石頭就不管
        if (stone == null || stone != activeStone)
        {
            return;
        }

        activeStone = null;              // 清掉石頭
        awaitingRespawn = false;         // 不用等了，因為石頭死了

        if (stoneRespawnCompleted)       // 如果石頭其實已經通知過可以復活，就不要再死一次
        {
            return;
        }

        FinalizeDeath();                 // 石頭被破壞而且還沒復活 → Boss 真的死
    }

    // 從復活石的位置復活 Boss
    private void RespawnFromStone(Vector2Int gridPos, Vector3 worldPos)
    {
        hasRespawned = true;             // 標記已經復活過了，之後再死就不會再進復活流程
        currentHP = maxHP;               // 回滿血
        block = 0;                       // 护甲歸零
        transform.position = worldPos;   // 把物件位置移到石頭原本的位置
        gridPosition = gridPos;          // 棋盤座標也設成一樣
        SetHidden(false);                // 顯示出來
        SetForceHideIntent(false);       // 復活回來 → 意圖恢復顯示

        if (battleManager == null)
        {
            battleManager = FindObjectOfType<BattleManager>();
        }

        if (battleManager != null && !battleManager.enemies.Contains(this))
        {
            battleManager.enemies.Add(this);  // 把 Boss 再加回敵人列表
        }
    }

    // 最終真正的死亡流程，只做一次
    private void FinalizeDeath()
    {
        if (finalDeathHandled)           // 已經處理過就不要再做
        {
            return;
        }

        finalDeathHandled = true;        // 標記已處理
        SetHidden(false);                // 保證死亡時會顯示（避免看起來憑空消失）
        base.Die();                      // 呼叫基底 Enemy 的真正死亡
    }

    // 把這個 Boss 下面所有 SpriteRenderer 一次打開或關閉
        private void SetHidden(bool hidden)
    {
        EnsureRendererCache();
        foreach (var renderer in cachedRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = !hidden;   // 本體 sprite 開關
            }
        }

    // 🔹 頭上的攻擊意圖一起關掉 / 打開
        SetForceHideIntent(hidden);

    // 🔹 元素狀態圖示一起關掉 / 打開
        if (elementStatusDisplay == null)
        {
            elementStatusDisplay = GetComponentInChildren<EnemyElementStatusDisplay>(true);
        }
        if (elementStatusDisplay != null)
        {
            elementStatusDisplay.gameObject.SetActive(!hidden);
        }
    }

    // 如果還沒快取 Renderer，就抓一次
    private void EnsureRendererCache()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    // 一開場時灑瘴氣
    private void ApplyInitialMiasma()
    {
        Board board = FindObjectOfType<Board>();   // 拿棋盤
        if (board == null)
        {
            return;
        }

        List<Vector2Int> positions = board.GetAllPositions(); // 拿到所有格子位置
        if (positions.Count == 0)
        {
            return;
        }

        // 實際要放幾個中心點，不能比格子數還多
        int count = Mathf.Clamp(miasmaCenters, 0, positions.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, positions.Count); // 隨機挑一個格子當中心
            Vector2Int center = positions[idx];
            positions.RemoveAt(idx);                    // 拿掉，避免重複選到
            SpreadMiasma(board, center);                // 以這格當中心向周圍擴散
        }
    }

    // 以一個中心格，把它跟周圍相鄰的格子都變成瘴氣
    private void SpreadMiasma(Board board, Vector2Int center)
    {
        BoardTile centerTile = board.GetTileAt(center);
        if (centerTile != null)
        {
            ApplyMiasmaToTile(centerTile);             // 中心格設為瘴氣
            miasmaTiles.Add(centerTile.gridPosition);  // 記錄起來
        }

        // 把跟中心相鄰的格子也變成瘴氣
        foreach (BoardTile tile in board.GetAdjacentTiles(center))
        {
            if (tile == null)
            {
                continue;
            }

            ApplyMiasmaToTile(tile);
            miasmaTiles.Add(tile.gridPosition);
        }
    }

    // 實際把一個格子設成瘴氣狀態，並帶上傷害數值
    private void ApplyMiasmaToTile(BoardTile tile)
    {
        tile.SetMiasma(true, miasmaDamage); // 告訴格子：你現在是瘴氣格，玩家踩到要扣 miasmaDamage
    }
}
