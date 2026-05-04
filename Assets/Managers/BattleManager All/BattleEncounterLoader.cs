using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 負責載入本場戰鬥的遭遇資料、生成妖怪，
/// 並安排開場的「妖怪進場 -> 玩家選擇落點 -> 正式開始戰鬥」流程。
/// </summary>
public class BattleEncounterLoader
{
    // 開場時等待妖怪進場動畫的最長秒數，避免動畫狀態異常時卡住流程。
    private const float InitialEnemyEntranceWaitCapSeconds = 1.0f;
    // 顯示「選擇落點回合」提示時的停留秒數。
    private const float SelectStartTileHintDurationSeconds = 1.0f;

    // 戰鬥主控制器。
    private readonly BattleManager battleManager;
    // 棋盤資料，用來查格子與可選位置。
    private readonly Board board;
    // 玩家物件。
    private readonly Player player;
    // 當前場上的妖怪清單，由 BattleManager 持有並共用。
    private readonly List<Enemy> enemies;
    // 本場戰鬥要生成的妖怪設定。
    private readonly List<EnemySpawnConfig> enemySpawnConfigs;
    // 戰鬥狀態機，用來切換到玩家回合。
    private readonly BattleStateMachine stateMachine;
    // 教學戰鬥控制器；沒有教學時可為 null。
    private readonly TutorialBattleController tutorialController;

    // 目前是否正處於玩家選擇起始落點的階段。
    private bool isSelectingStartTile = false;

    /// <summary>
    /// 建立戰鬥遭遇載入器，接收本場戰鬥會用到的核心參考。
    /// </summary>
    public BattleEncounterLoader(
        BattleManager battleManager,
        Board board,
        Player player,
        List<Enemy> enemies,
        List<EnemySpawnConfig> enemySpawnConfigs,
        BattleStateMachine stateMachine,
        TutorialBattleController tutorialController)
    {
        this.battleManager = battleManager;
        this.board = board;
        this.player = player;
        this.enemies = enemies;
        this.enemySpawnConfigs = enemySpawnConfigs;
        this.stateMachine = stateMachine;
        this.tutorialController = tutorialController;
    }

    /// <summary>
    /// 從 RunManager 讀取目前節點的遭遇內容，整理成可生成的妖怪設定清單。
    /// </summary>
    public void LoadEncounterFromRunManager()
    {
        if (enemySpawnConfigs == null)
        {
            return;
        }

        var runManager = RunManager.Instance;
        var activeNode = runManager?.ActiveNode;
        if (activeNode == null)
        {
            return;
        }

        enemySpawnConfigs.Clear();

        var encounter = activeNode.Encounter;

        if (encounter == null)
        {
            Debug.LogWarning($"BattleEncounterLoader: active run node '{activeNode.NodeId}' has no encounter. Enemy spawn configs were cleared.", runManager);
            return;
        }

        var enemyGroups = encounter.EnemyGroups;
        if (enemyGroups == null || enemyGroups.Count == 0)
        {
            Debug.LogWarning($"BattleEncounterLoader: encounter '{encounter.EncounterId}' has no enemy groups.", encounter);
            return;
        }

        foreach (var group in enemyGroups)
        {
            if (group == null)
            {
                continue;
            }

            var configCopy = new EnemySpawnConfig
            {
                enemyPrefab = group.enemyPrefab,
                count = group.count
            };

            enemySpawnConfigs.Add(configCopy);
        }
    }

    /// <summary>
    /// 戰鬥開場主流程。
    /// 先生成妖怪並等待進場，再讓玩家選擇落點，最後切入正式戰鬥。
    /// </summary>
    public IEnumerator GameStartRoutine()
    {
        Vector2Int tutorialStartPos = default;
        bool hasTutorialFixedStart = false;

        if (board != null)
        {
            if (tutorialController != null)
            {
                hasTutorialFixedStart = tutorialController.TryGetPlayerStartPosition(out tutorialStartPos);
            }

            if (hasTutorialFixedStart)
            {
                battleManager.playerStartPos = tutorialStartPos;
            }
        }

        SpawnInitialEnemies(hasTutorialFixedStart);
        battleManager.RefreshEnemiesFromScene();
        yield return WaitForInitialEnemyEntrance();

        if (board != null)
        {
            if (hasTutorialFixedStart)
            {
                SetupPlayer();
            }
            else
            {
                yield return battleManager.StartCoroutine(SelectPlayerStartTile());
            }
        }

        player?.NotifyBattleStarted();

        battleManager.SetBattleStarted(true);
        battleManager.SetEndTurnButtonInteractable(false);
        tutorialController?.HandleBattleStarted();

        while (tutorialController != null && tutorialController.IsWaitingForBattleStartSequence)
        {
            yield return null;
        }

        stateMachine.ChangeState(new PlayerTurnState(battleManager));
    }

    /// <summary>
    /// 進入玩家選擇起始落點的階段，將所有可選格子標示為可點擊。
    /// </summary>
    private IEnumerator SelectPlayerStartTile()
    {
        yield return battleManager.ShowBattlePhaseHintAndWait("選擇落點回合", SelectStartTileHintDurationSeconds);
        isSelectingStartTile = true;

        List<Vector2Int> positions = board.GetAllPositions();
        foreach (var pos in positions)
        {
            BoardTile tile = board.GetTileAt(pos);
            if (tile == null)
            {
                continue;
            }

            // 已被妖怪佔據的格子不能選。
            if (board.IsTileOccupied(pos))
            {
                tile.SetSelectable(false);
                tile.SetHighlight(false);
                continue;
            }

            if (tile.GetComponent<BoardTileSelectable>() == null)
            {
                tile.gameObject.AddComponent<BoardTileSelectable>();
            }

            if (tile.GetComponent<BoardTileHoverHighlight>() == null)
            {
                tile.gameObject.AddComponent<BoardTileHoverHighlight>();
            }
        }

        while (isSelectingStartTile)
        {
            yield return null;
        }

        // 結束選位後，清掉這一階段臨時加上的高亮與互動效果。
        foreach (var pos in positions)
        {
            BoardTile tile = board.GetTileAt(pos);
            if (tile == null)
            {
                continue;
            }

            BoardTileHoverHighlight hover = tile.GetComponent<BoardTileHoverHighlight>();
            if (hover)
            {
                Object.Destroy(hover);
            }

            tile.SetHighlight(false);
        }

        SetupPlayer();
        board.ResetAllTilesSelectable();
    }

    /// <summary>
    /// 將玩家放到目前記錄的起始格。
    /// </summary>
    private void SetupPlayer()
    {
        if (player == null || board == null)
        {
            return;
        }

        BoardTile tile = board.GetTileAt(battleManager.playerStartPos);
        if (tile != null)
        {
            player.MoveToPosition(battleManager.playerStartPos);
        }
    }

    /// <summary>
    /// 等待開場的妖怪進場動畫播完，但最多只等固定上限秒數。
    /// </summary>
    private IEnumerator WaitForInitialEnemyEntrance()
    {
        if (enemies == null || enemies.Count == 0)
        {
            yield break;
        }

        // 先等一幀，讓新生成的妖怪有機會完成 Awake/Start 並觸發 Appear。
        yield return null;

        float elapsed = 0f;
        while (elapsed < InitialEnemyEntranceWaitCapSeconds && AreAnyEnemiesPlayingAppearAnimation())
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 檢查目前場上是否仍有妖怪正在播放進場動畫。
    /// </summary>
    private bool AreAnyEnemiesPlayingAppearAnimation()
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy != null && enemy.Visual != null && enemy.Visual.IsAppearAnimationPlaying())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 根據遭遇設定隨機把妖怪生成到棋盤上。
    /// 若需要保留玩家起始格，會先把那格從可生成清單中排除。
    /// </summary>
    private void SpawnInitialEnemies(bool reservePlayerStartTile)
    {
        if (board == null || enemySpawnConfigs == null)
        {
            return;
        }

        // 教學戰鬥的生成交給教學控制器負責。
        if (tutorialController != null && tutorialController.IsActive)
        {
            tutorialController.SpawnEnemiesForCurrentStep();
            return;
        }

        List<Vector2Int> positions = board.GetAllPositions();
        if (reservePlayerStartTile)
        {
            positions.Remove(battleManager.playerStartPos);
        }

        foreach (var config in enemySpawnConfigs)
        {
            if (config == null || config.enemyPrefab == null)
            {
                continue;
            }

            int spawnCount = Mathf.Max(0, config.count);
            for (int i = 0; i < spawnCount && positions.Count > 0; i++)
            {
                int idx = UnityEngine.Random.Range(0, positions.Count);
                Vector2Int pos = positions[idx];
                positions.RemoveAt(idx);

                BoardTile tile = board.GetTileAt(pos);
                if (tile == null)
                {
                    continue;
                }

                Enemy enemy = Object.Instantiate(
                    config.enemyPrefab,
                    tile.transform.position,
                    Quaternion.identity);

                enemy.gridPosition = pos;
            }

            if (positions.Count == 0)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 處理玩家在選位階段點到格子的事件。
    /// </summary>
    public bool HandleTileSelection(BoardTile tile)
    {
        if (!isSelectingStartTile || tile == null)
        {
            return false;
        }

        if (board != null && board.IsTileOccupied(tile.gridPosition))
        {
            return false;
        }

        battleManager.playerStartPos = tile.gridPosition;
        isSelectingStartTile = false;
        return true;
    }
}
