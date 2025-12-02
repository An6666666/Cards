using System.Collections;                     // 使用協程相關類別（IEnumerator 等）
using System.Collections.Generic;             // 使用泛型集合（List<T> 等）
using UnityEngine;                            // 使用 Unity 引擎核心 API

public class BattleEncounterLoader            // 負責「載入戰鬥遭遇、選起始位置、生成敵人」的控制類
{
    private readonly BattleManager battleManager;          // 戰鬥管理器（核心總管，包含玩家起始位置等設定）
    private readonly Board board;                          // 棋盤（用來取得所有格子、座標等）
    private readonly Player player;                        // 玩家物件
    private readonly List<Enemy> enemies;                  // 場上敵人清單（由 BattleManager 持有的列表）
    private readonly List<EnemySpawnConfig> enemySpawnConfigs; // 敵人生成設定清單（Prefab + 數量）
    private readonly BattleStateMachine stateMachine;      // 戰鬥狀態機（切換 PlayerTurn / EnemyTurn 等）

    private bool isSelectingStartTile = false;             // 是否正在選擇玩家起始格的旗標

    public BattleEncounterLoader(                         // 建構子，用來注入所有需要的相依物件
        BattleManager battleManager,
        Board board,
        Player player,
        List<Enemy> enemies,
        List<EnemySpawnConfig> enemySpawnConfigs,
        BattleStateMachine stateMachine)
    {
        this.battleManager = battleManager;                // 指定戰鬥管理器
        this.board = board;                                // 指定棋盤
        this.player = player;                              // 指定玩家
        this.enemies = enemies;                            // 指定敵人列表（會被填滿）
        this.enemySpawnConfigs = enemySpawnConfigs;        // 指定敵人生成設定列表
        this.stateMachine = stateMachine;                  // 指定戰鬥狀態機
    }

    public void LoadEncounterFromRunManager()
    {
        var runManager = RunManager.Instance;              // 從單例取得 RunManager
        var encounter = runManager?.ActiveNode?.Encounter; // 從目前地圖節點取得 Encounter 設定（可能為 null）

        if (encounter == null)                             // 若沒有設定 Encounter
            return;                                        // 直接結束（不載入敵人配置）

        if (enemySpawnConfigs == null)                     // 若外部沒有給生成設定列表
            return;                                        // 直接結束

        enemySpawnConfigs.Clear();                         // 先清空原本的生成設定

        var enemyGroups = encounter.EnemyGroups;           // 從 Encounter 取得敵人群組資料（每組 enemyPrefab + count）
        if (enemyGroups == null)                           // 沒資料就不處理
            return;

        foreach (var group in enemyGroups)                 // 對每一組敵人群組進行處理
        {
            if (group == null)                             // 防呆：群組可能為 null
                continue;

            var configCopy = new EnemySpawnConfig          // 建立一個新的 EnemySpawnConfig 複本
            {
                enemyPrefab = group.enemyPrefab,           // 指定敵人預製物
                count = group.count                        // 指定要生成的數量
            };

            enemySpawnConfigs.Add(configCopy);             // 加入到本地的生成設定清單
        }
    }

    public IEnumerator GameStartRoutine()
    {
        if (board != null)                                 // 若有棋盤存在
            yield return battleManager.StartCoroutine(SelectPlayerStartTile());
            // 交給 BattleManager 啟動協程：讓玩家選擇起始站立格子（等待玩家點選）

        SpawnInitialEnemies();                             // 在棋盤上生成初始敵人
        enemies.Clear();                                   // 清空原本敵人列表
        enemies.AddRange(Object.FindObjectsOfType<Enemy>()); // 從場景中抓出所有 Enemy，重新填入列表

        stateMachine.ChangeState(new PlayerTurnState(battleManager));
        // 將戰鬥狀態切換為「玩家回合」狀態（PlayerTurnState）

        battleManager.SetBattleStarted(true);              // 告訴 BattleManager：戰鬥正式開始
        battleManager.SetEndTurnButtonInteractable(true);  // 允許玩家按「結束回合」按鈕
    }

    private IEnumerator SelectPlayerStartTile()
    {
        isSelectingStartTile = true;                       // 設成「正在選擇起始格」

        List<Vector2Int> positions = board.GetAllPositions(); // 取得棋盤上所有格子的座標
        foreach (var pos in positions)                        // 對每一個格子進行處理
        {
            BoardTile t = board.GetTileAt(pos);               // 取得該位置對應的 BoardTile
            if (t.GetComponent<BoardTileSelectable>() == null)
                t.gameObject.AddComponent<BoardTileSelectable>();
            // 若該格沒有 BoardTileSelectable 元件，就加上，讓它可以被點擊選擇

            if (t.GetComponent<BoardTileHoverHighlight>() == null)
                t.gameObject.AddComponent<BoardTileHoverHighlight>();
            // 若該格沒有滑鼠懸停高亮元件，就加上，讓滑鼠移過可顯示高亮
        }

        while (isSelectingStartTile)                        // 當 isSelectingStartTile 為 true（尚未選完）
            yield return null;                              // 每幀等待（直到某處把 isSelectingStartTile 設為 false）

        foreach (var pos in positions)                      // 玩家已經選完起始格，清理臨時效果
        {
            BoardTile t = board.GetTileAt(pos);             // 再次取得每一個格子
            BoardTileHoverHighlight hover = t.GetComponent<BoardTileHoverHighlight>();
            if (hover) Object.Destroy(hover);               // 若有 HoverHighlight，就刪掉此元件
            t.SetHighlight(false);                          // 關閉格子高亮
        }

        SetupPlayer();                                      // 將玩家放到選好的起始格位置上
        board.ResetAllTilesSelectable();                    // 重置所有格子的可選取狀態（移除 BoardTileSelectable 上的選取狀態）
    }

    private void SetupPlayer()
    {
        if (player == null || board == null) return;        // 若玩家或棋盤不存在就直接跳出

        BoardTile tile = board.GetTileAt(battleManager.playerStartPos);
        // 從 BattleManager 上的 playerStartPos 取得對應的格子

        if (tile != null)                                   // 若該格子存在
        {
            player.MoveToPosition(battleManager.playerStartPos);
            // 把玩家的邏輯座標（gridPosition）移動到該位置，並同步實際 transform.position
        }
    }

    private void SpawnInitialEnemies()
    {
        if (board == null || enemySpawnConfigs == null) return;
        // 若棋盤或敵人生成設定列表不存在，就不生成敵人

        List<Vector2Int> positions = board.GetAllPositions();   // 拿到所有棋盤位置
        positions.Remove(battleManager.playerStartPos);         // 把玩家起始位置從可用位置中移除（避免敵人站到玩家頭上）

        foreach (var config in enemySpawnConfigs)               // 對每一種敵人配置進行迴圈
        {
            if (config == null || config.enemyPrefab == null) continue;
            // 若這個配置不存在或沒指定 Prefab，就跳過

            int spawnCount = Mathf.Max(0, config.count);        // 防呆：生成數量最少為 0
            for (int i = 0; i < spawnCount && positions.Count > 0; i++)
            {
                int idx = UnityEngine.Random.Range(0, positions.Count);
                // 在目前剩餘的可用位置中隨機抽一個 index

                Vector2Int pos = positions[idx];                // 取得選到的座標
                positions.RemoveAt(idx);                        // 從列表中移除這個位置（避免重複生成）

                BoardTile tile = board.GetTileAt(pos);          // 取得該座標上的格子
                if (tile == null) continue;                     // 若格子不存在就跳過

                Enemy e = Object.Instantiate(
                    config.enemyPrefab,                         // 要生成的敵人 Prefab
                    tile.transform.position,                    // 放到該格子的世界座標位置
                    Quaternion.identity);                       // 不旋轉生成（預設方向）

                e.gridPosition = pos;                           // 設定敵人的邏輯格子座標
            }

            if (positions.Count == 0)                           // 如果已經沒有可用位置可以放敵人
                break;                                          // 結束外層迴圈，不再生成更多敵人
        }
    }

    public bool HandleTileSelection(BoardTile tile)
    {
        if (!isSelectingStartTile) return false;                // 若目前不是在選起始格階段，直接忽略
        battleManager.playerStartPos = tile.gridPosition;       // 將玩家起始位置設為被點擊的這格
        isSelectingStartTile = false;                           // 結束選取流程（SelectPlayerStartTile 的 while 會跳出）
        return true;                                            // 回傳 true 表示這次點擊有被處理（成功選點）
    }
}
