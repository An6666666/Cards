using System;
using System.Collections;                                 // 引用非泛型集合
using System.Collections.Generic;                        // 引用泛型集合
using Unity.VisualScripting;                             // 引用視覺化腳本功能（若未使用可移除）
using UnityEngine;                                       // 引用 Unity 核心功能
using UnityEngine.UI;                                    // 引用 UI 元件功能

[Serializable]
public class EnemySpawnConfig
{
    public Enemy enemyPrefab;                              // 該組要生成的敵人 Prefab
    public int count = 1;                                  // 生成的數量
}

public class BattleManager : MonoBehaviour               // 戰鬥流程管理器，掛在場景中的空物件上
{
    public Player player;                                 // 場景中玩家角色的引用
    [NonSerialized]
    public List<Enemy> enemies = new List<Enemy>();       // 場景中敵人角色列表
    public GameObject cardPrefab;                         // 卡牌的 Prefab，用於生成卡牌 UI

    [Header("Initial Setup")]
    public List<EnemySpawnConfig> enemySpawnConfigs = new List<EnemySpawnConfig>();
    public Vector2Int playerStartPos = Vector2Int.zero;    // 玩家起始格子
    // 定義回合狀態枚舉
    private BattleStateMachine stateMachine = new BattleStateMachine();

    public Transform handPanel;                           // Inspector 中指定的手牌區域
    public Transform deckPile;                            // Inspector 中指定的牌庫區域
    public Transform discardPile;                         // Inspector 中指定的棄牌堆區域

    public Text energyText;                               // 顯示能量用的文字

    [Header("UI References")]
    [SerializeField] private Button endTurnButton;        // 玩家結束回合按鈕

    public Board board;                                   // Inspector 中指定的棋盤管理器

    [Header("Guaranteed Cards")]
    public Move_YiDong guaranteedMovementCard;            // 必定發給玩家的移動卡模板
    private Move_YiDong guaranteedMovementCardInstance;   // 實際放在手牌中的移動卡實例

    [Header("Rewards")]
    public List<CardBase> allCardPool = new List<CardBase>();
    private int defeatedEnemyCount = 0;
    public RewardUI rewardUIPrefab;
    private RewardUI rewardUIInstance;
    private bool battleStarted = false;                     // 是否已開始戰鬥，避免開場即觸發勝利

    // 是否正在選擇移動目標的旗標
    private bool isSelectingMovementTile = false;
    // 儲存當前正在使用的移動卡
    private CardBase currentMovementCard = null;

    // 是否正在選擇攻擊目標的旗標
    private bool isSelectingAttackTarget = false;

    // 是否正在選擇起始位置的旗標
    private bool isSelectingStartTile = false;

    // 儲存當前正在使用的攻擊卡
    private CardBase currentAttackCard = null;

    // 被高亮的敵人列表，用於攻擊選擇階段
    private List<Enemy> highlightedEnemies = new List<Enemy>();

    // 被高亮的格子列表，用於移動選擇階段
    private List<BoardTile> highlightedTiles = new List<BoardTile>();

    public float cardUseDelay = 0f;               // 玩家回合開始後，延遲幾秒才能操作卡牌
    private bool _cardInteractionLocked = false;  // 全域鎖定旗標
    public bool IsCardInteractionLocked => _cardInteractionLocked;

    private bool processingPlayerTurnStart = false;
    private bool processingEnemyTurnStart = false;

    public bool IsProcessingPlayerTurnStart => processingPlayerTurnStart;
    public bool IsProcessingEnemyTurnStart => processingEnemyTurnStart;

    void Awake()
    {
        SetEndTurnButtonInteractable(false);
        LoadEncounterFromRunManager();
    }

    void Start()
    {
        StartCoroutine(GameStartRoutine());
    }

    private void LoadEncounterFromRunManager()
    {
        var runManager = RunManager.Instance;
        var encounter = runManager?.ActiveNode?.Encounter;

        if (encounter == null)
            return;

        if (enemySpawnConfigs == null)
            enemySpawnConfigs = new List<EnemySpawnConfig>();
        else
            enemySpawnConfigs.Clear();

        var enemyGroups = encounter.EnemyGroups;
        if (enemyGroups == null)
            return;

        foreach (var group in enemyGroups)
        {
            if (group == null)
                continue;

            var configCopy = new EnemySpawnConfig
            {
                enemyPrefab = group.enemyPrefab,
                count = group.count
            };

            enemySpawnConfigs.Add(configCopy);
        }
    }

    private IEnumerator GameStartRoutine()
    {
        if (board != null)
            yield return StartCoroutine(SelectPlayerStartTile());

        SpawnInitialEnemies();
        enemies = new List<Enemy>(FindObjectsOfType<Enemy>());  // 收集場上的敵人
        stateMachine.ChangeState(new PlayerTurnState(this));
        battleStarted = true;                                  // 完成初始設定後才開始判定勝利
        SetEndTurnButtonInteractable(true);
    }

    private IEnumerator SelectPlayerStartTile()
    {
        isSelectingStartTile = true;

        List<Vector2Int> positions = board.GetAllPositions();
        foreach (var pos in positions)
        {
            BoardTile t = board.GetTileAt(pos);
            if (t.GetComponent<BoardTileSelectable>() == null)
                t.gameObject.AddComponent<BoardTileSelectable>();
            if (t.GetComponent<BoardTileHoverHighlight>() == null)
                t.gameObject.AddComponent<BoardTileHoverHighlight>();
        }

        while (isSelectingStartTile)
            yield return null;

        foreach (var pos in positions)
        {
            BoardTile t = board.GetTileAt(pos);
            BoardTileHoverHighlight hover = t.GetComponent<BoardTileHoverHighlight>();
            if (hover) Destroy(hover);
            t.SetHighlight(false);
        }

        SetupPlayer();
        board.ResetAllTilesSelectable();
    }

    // 移動玩家到指定起始格子
    private void SetupPlayer()
    {
        if (player == null || board == null) return;
        BoardTile tile = board.GetTileAt(playerStartPos);
        if (tile != null)
        {
            player.MoveToPosition(playerStartPos);
        }
    }

    // 依設定隨機生成敵人
    private void SpawnInitialEnemies()
    {
        if (board == null || enemySpawnConfigs == null) return;

        List<Vector2Int> positions = board.GetAllPositions();
        positions.Remove(playerStartPos); // 避免與玩家重疊

        foreach (var config in enemySpawnConfigs)
        {
            if (config == null || config.enemyPrefab == null) continue;

            int spawnCount = Mathf.Max(0, config.count);
            for (int i = 0; i < spawnCount && positions.Count > 0; i++)
            {
                int idx = UnityEngine.Random.Range(0, positions.Count);
                Vector2Int pos = positions[idx];
                positions.RemoveAt(idx);

                BoardTile tile = board.GetTileAt(pos);
                if (tile == null) continue;

                Enemy e = Instantiate(config.enemyPrefab, tile.transform.position, Quaternion.identity);
                e.gridPosition = pos;
            }

            if (positions.Count == 0)
                break;
        }
    }

    void Update()
    {
        stateMachine.Update();

        if (!battleStarted) return;                          // 尚未開始戰鬥時不檢查勝負

        // 移除已被摧毀的敵人
        enemies.RemoveAll(e => e == null);

        // 全部敵人死亡則進入勝利狀態
        bool allDead = enemies.Count == 0 || enemies.TrueForAll(e => e.currentHP <= 0);
        if (allDead && !(stateMachine.Current is VictoryState))
        {
            stateMachine.ChangeState(new VictoryState(this));
        }

        if (player.currentHP <= 0 && !(stateMachine.Current is DefeatState))
        {
            stateMachine.ChangeState(new DefeatState(this));
        }
    }

    /// <summary>
    /// 玩家結束回合：棄手牌、結算玩家回合、啟動敵人回合
    /// </summary>
    public void EndPlayerTurn()
    {
        if (!battleStarted || !(stateMachine.Current is PlayerTurnState))
            return;

        SetEndTurnButtonInteractable(false);
        DiscardAllHand();                                 // 棄掉所有手牌

        player.EndTurn();
        GameEvents.RaiseTurnEnded();
        stateMachine.ChangeState(new EnemyTurnState(this));
    }

    /// <summary>
    /// 啟動玩家回合：處理敵人狀態、抽牌與 UI 更新
    /// </summary>
    public void StartPlayerTurn()
    {
        // ★ 打開鎖定旗標：任何新舊卡都應該被鎖
        _cardInteractionLocked = true;

        if (battleStarted)
            SetEndTurnButtonInteractable(true);

        // 玩家回合開始：補滿能量（保留）
        player.energy = player.maxEnergy;
        UpdateEnergyUI();

        // 敵人回合開始效果（保留）
        processingPlayerTurnStart = true;

        var enemiesAtTurnStart = new List<Enemy>(enemies);
        foreach (var e in enemiesAtTurnStart)
        {
            if (e != null)
                e.ProcessTurnStart();
        }

        processingPlayerTurnStart = false;

        // 計算抽牌數（保留）
        int drawCount = player.baseHandCardCount + player.buffs.nextTurnDrawChange;
        drawCount = Mathf.Max(0, drawCount);
        player.buffs.nextTurnDrawChange = 0;

        // 重新抽牌 / 保證移動卡 / 刷新 UI（保留）
        player.DrawNewHand(drawCount);
        EnsureMovementCardInHand();
        RefreshHandUI(true); // ★ 這裡會生成新的 CardUI

        // ★ 立刻把「鎖定狀態」套到目前場上所有卡（包含剛生成的）
        ApplyInteractableToAllCards(false);

        // ★ 延遲幾秒後再解鎖
        StartCoroutine(EnableCardsAfterDelay(cardUseDelay));

        // ★ 新增：更新敵人攻擊意圖（世界空間小圖示）
        foreach (var e in enemies)
        {
            if (e != null)
            {
                e.DecideNextIntent(player);
            }
        }
    }

    private void ApplyInteractableToAllCards(bool value)
    {
        var cards = FindObjectsOfType<CardUI>();
        for (int i = 0; i < cards.Length; i++)
            cards[i].SetInteractable(value);
    }

    private IEnumerator EnableCardsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        _cardInteractionLocked = false;
        ApplyInteractableToAllCards(true);
    }

    private void SetEndTurnButtonInteractable(bool value)
    {
        if (endTurnButton != null)
            endTurnButton.interactable = value;
    }

    private Move_YiDong GetGuaranteedMovementCardInstance()
    {
        if (guaranteedMovementCardInstance == null)
        {
            if (guaranteedMovementCard == null)
            {
                Debug.LogWarning("Guaranteed movement card template is not assigned.");
                return null;
            }

            guaranteedMovementCardInstance = Instantiate(guaranteedMovementCard);
        }

        return guaranteedMovementCardInstance;
    }

    public bool IsGuaranteedMovementCard(CardBase card)
    {
        if (card == null)
            return false;

        Move_YiDong instance = GetGuaranteedMovementCardInstance();
        if (instance == null)
            return false;

        if (ReferenceEquals(card, instance))
            return true;

        if (guaranteedMovementCard != null && ReferenceEquals(card, guaranteedMovementCard))
            return true;

        return false;
    }

    private void RemoveGuaranteedMovementCardFromPiles()
    {
        if (player == null) return;

        // 移除所有移動卡模板與實例，避免被計入牌庫與棄牌堆
        player.deck.RemoveAll(card => card is Move_YiDong);
        player.discardPile.RemoveAll(card => card is Move_YiDong);
    }

    private void EnsureMovementCardInHand()
    {
        if (player == null) return;

        Move_YiDong movementCard = GetGuaranteedMovementCardInstance();
        if (movementCard == null) return;

        RemoveGuaranteedMovementCardFromPiles();

        // 移除其他移動卡版本，確保只保留這張保證卡
        int removedDuplicateCount = 0;
        for (int i = player.Hand.Count - 1; i >= 0; i--)
        {
            CardBase card = player.Hand[i];
            if (card is Move_YiDong && !ReferenceEquals(card, movementCard))
            {
                player.Hand.RemoveAt(i);
                removedDuplicateCount++;
            }
        }

        if (!player.Hand.Contains(movementCard))
        {
            player.Hand.Add(movementCard);
        }

        if (removedDuplicateCount > 0)
        {
            player.DrawCards(removedDuplicateCount);
        }
    }

    /// <summary>
    /// 棄掉所有手牌並更新 UI
    /// </summary>
    private void DiscardAllHand()
    {
        Move_YiDong movementCard = guaranteedMovementCardInstance;
        bool hadGuaranteedCard = false;

        if (movementCard != null)
        {
            hadGuaranteedCard = player.Hand.Remove(movementCard);
            // 確保保證卡不會意外留在棄牌堆
            player.discardPile.Remove(movementCard);
        }
        player.discardPile.AddRange(player.Hand);          // 全部移入棄牌堆
        player.Hand.Clear();                               // 清空手牌
        if (hadGuaranteedCard)
        {
            player.Hand.Add(movementCard);                 // 保證卡重新回到手牌
        }

        RemoveGuaranteedMovementCardFromPiles();
        RefreshHandUI();                                   // 更新 UI 顯示
    }

    /// <summary>
    /// 敵人回合流程：開始效果 → 行動 → 結束後回到玩家回合
    /// </summary>
    public IEnumerator EnemyTurnCoroutine()
    {

        processingEnemyTurnStart = true;

        var enemiesAtEnemyTurnStart = new List<Enemy>(enemies);
        foreach (var e in enemiesAtEnemyTurnStart)
        {
            if (e != null)
                e.ProcessTurnStart();                     // 敵人回合開始效果
        }

        processingEnemyTurnStart = false;
        yield return new WaitForSeconds(1f);               // 等待 1 秒

        var enemiesTakingActions = new List<Enemy>(enemies);
        foreach (var e in enemiesTakingActions)
        {
            if (e != null)
                e.EnemyAction(player);                    // 敵人執行攻擊或行動
        }

        yield return new WaitForSeconds(1f);               // 等待 1 秒

        // 清除本回合所有格擋 (Slay the Spire 流程)
        player.block = 0;
        var enemiesAtTurnEnd = new List<Enemy>(enemies);
        foreach (var e in enemiesAtTurnEnd)
        {
            if (e != null && e.ShouldResetBlockEachTurn) e.block = 0;
        }

        // 回到玩家回合
        stateMachine.ChangeState(new PlayerTurnState(this));
        Debug.Log("Player Turn");
    }

    /// <summary>
    /// 玩卡：處理費用、執行效果、棄牌、更新 UI
    /// </summary>
    public bool PlayCard(CardBase cardData)
    {
        if (!(stateMachine.Current is PlayerTurnState)) return false;
        if (cardData == null) return false;
        if (player == null || player.Hand == null) return false;
        if (!player.Hand.Contains(cardData)) return false;

        // 計算最終費用 (包含 Buff 修改)
        int finalCost = cardData.cost + player.GetCardCostModifier(cardData);
        if (cardData.cardType == CardType.Attack && player.buffs.nextAttackCostModify != 0)
        {
            finalCost += player.buffs.nextAttackCostModify;
        }
        if (cardData.cardType == CardType.Movement && player.buffs.movementCostModify != 0)
        {
            finalCost += player.buffs.movementCostModify;
        }
        finalCost = Mathf.Max(0, finalCost);

        if (player.energy < finalCost)                     // 能量不足時拒絕
        {
            Debug.Log("Not enough energy");
            return false;
        }

        Enemy target = enemies.Find(e => e != null && e.currentHP > 0);
        cardData.ExecuteEffect(player, target);            // 執行卡牌效果
        // 使用攻擊卡時，更新統計和清除下一次加傷
        if (cardData.cardType == CardType.Attack)
        {
            player.attackUsedThisTurn++;
            if (player.buffs.nextAttackPlus > 0)
                player.buffs.nextAttackPlus = 0;
        }

        // 若手牌中仍含此卡，則移至棄牌堆
        bool isGuaranteedMovement = IsGuaranteedMovementCard(cardData);
        bool removedFromHand = player.Hand.Remove(cardData);

        if (removedFromHand)
        {
            player.ClearCardCostModifier(cardData);
            if (isGuaranteedMovement)
            {
                RemoveGuaranteedMovementCardFromPiles();
            }
            else if (cardData.exhaustOnUse)
            {
                player.ExhaustCard(cardData);
            }
            else
            {
                player.discardPile.Add(cardData);
            }
        }
        else if (isGuaranteedMovement)
        {
            RemoveGuaranteedMovementCardFromPiles();
        }

        player.UseEnergy(finalCost);
        GameEvents.RaiseCardPlayed(cardData);
        RefreshHandUI();
        return true;
    }

    /// <summary>
    /// 使用移動牌：檢查能量 → 進入選格子模式 → 高亮格子
    /// </summary>
    public void UseMovementCard(CardBase movementCard)
    {
        if (movementCard == null)
        {
            return;
        }
        if (!(stateMachine.Current is PlayerTurnState))
        {
            return;
        }
        if (player == null)
        {
            Debug.LogWarning("Player reference not assigned.");
            return;
        }
        if (!player.buffs.CanMove())
        {
            Debug.Log("Cannot use movement: movement is currently restricted.");
            return;
        }
        int previewCost = movementCard.cost + player.GetCardCostModifier(movementCard);
        previewCost += player.buffs.movementCostModify;
        previewCost = Mathf.Max(0, previewCost);

        if (player.energy < previewCost)             // 能量檢查
        {
            Debug.Log("Not enough energy for movement");
            return;
        }
        if (isSelectingMovementTile)                      // 已在選擇中則拒絕
        {
            Debug.Log("Already selecting movement tile");
            return;
        }

        isSelectingMovementTile = true;                   // 標記選擇中
        currentMovementCard = movementCard;               // 記錄使用的移動卡

        // 取得此卡的移動偏移範圍
        MovementCardBase mCard = movementCard as MovementCardBase;
        List<Vector2Int> offs = (mCard != null && mCard.rangeOffsets?.Count > 0)
            ? mCard.rangeOffsets
            : new List<Vector2Int>                     // 預設上下左右 1 格
            {
                new Vector2Int(0,1), new Vector2Int(0,-1),
                new Vector2Int(-1,0), new Vector2Int(1,0)
            };

        highlightedTiles.Clear();
        HighlightTilesWithOffsets(player.position, offs); // 高亮目標格子
    }

    /// <summary>
    /// 高亮給定中心與偏移的所有格子
    /// </summary>
    private void HighlightTilesWithOffsets(Vector2Int centerPos, List<Vector2Int> offsets)
    {
        foreach (var off in offsets)
        {
            Vector2Int tilePos = centerPos + off;
            BoardTile tile = board.GetTileAt(tilePos);
            if (tile != null && !board.IsTileOccupied(tilePos))
            {
                tile.SetSelectable(true);                  // 標記該格可選
                highlightedTiles.Add(tile);
            }
        }
    }

    /// <summary>
    /// 重置所有格子為不可選
    /// </summary>
    public void ResetAllTilesSelectable()
    {
        board.ResetAllTilesSelectable();
    }

    /// <summary>
    /// 取消移動選擇，清理狀態與高亮
    /// </summary>
    public void CancelMovementSelection()
    {
        isSelectingMovementTile = false;
        currentMovementCard = null;
        foreach (var t in highlightedTiles)
            t.SetSelectable(false);
        highlightedTiles.Clear();
        board.ResetAllTilesSelectable();
    }

    /// <summary>
    /// 玩家點擊格子時觸發：執行移動、扣能量、棄牌、重置
    /// </summary>
    public bool OnTileClicked(BoardTile tile)
    {
        if (isSelectingStartTile)
        {
            playerStartPos = tile.gridPosition;
            isSelectingStartTile = false;
            return true;
        }
        if (!isSelectingMovementTile) return false;
        if (!highlightedTiles.Contains(tile))
        {
            CancelMovementSelection();
            return false;
        }
        if (player == null || !player.buffs.CanMove())
        {
            Debug.Log("Cannot move: movement is currently restricted.");
            CancelMovementSelection();
            return false;
        }
        if (board.IsTileOccupied(tile.gridPosition))
        {
            Debug.Log("Cannot move: tile occupied by enemy.");
            CancelMovementSelection();
            return false;
        }

        currentMovementCard.ExecuteOnPosition(player, tile.gridPosition);  // 執行移動卡效果

        int finalCost = currentMovementCard.cost + player.GetCardCostModifier(currentMovementCard) + player.buffs.movementCostModify;
        finalCost = Mathf.Max(0, finalCost);
        player.UseEnergy(finalCost);                      // 扣除能量

        // 棄掉已使用的移動卡
        if (player.Hand.Contains(currentMovementCard))
        {
            player.Hand.Remove(currentMovementCard);

            if (!IsGuaranteedMovementCard(currentMovementCard))
            {
                player.discardPile.Add(currentMovementCard);
            }
            else
            {
                RemoveGuaranteedMovementCardFromPiles();
            }
        }

        isSelectingMovementTile = false;                  // 重置狀態
        currentMovementCard = null;
        foreach (var t in highlightedTiles)
            t.SetSelectable(false);
        highlightedTiles.Clear();
        board.ResetAllTilesSelectable();                  // 清除所有高亮
        RefreshHandUI();
        return true;                                   // 更新 UI
    }

    /// <summary>
    /// 更新手牌、牌庫、棄牌堆的 UI 顯示
    /// </summary>
    public void RefreshHandUI(bool playDrawAnimation = false)
    {
        UpdateEnergyUI();
        // 牌庫 / 棄牌數量（有 UI 就保留，沒有就可刪）
        if (deckPile)
        {
            var t = deckPile.GetComponentInChildren<UnityEngine.UI.Text>();
            if (t) t.text = $"{player.deck.Count}";
        }
        if (discardPile)
        {
            var t2 = discardPile.GetComponentInChildren<UnityEngine.UI.Text>();
            if (t2) t2.text = $"{player.discardPile.Count}";
        }

        // 清空原本的手牌 UI（用反向 for 比 foreach 更安全）
        for (int i = handPanel.childCount - 1; i >= 0; i--)
        {
            Destroy(handPanel.GetChild(i).gameObject);
        }

        List<CardUI> createdCards = new List<CardUI>();

        // 依手牌資料重新生成卡牌 UI
        foreach (var cardData in player.Hand)
        {
            GameObject cardObj = Instantiate(cardPrefab, handPanel);
            var cardUI = cardObj.GetComponent<CardUI>();
            if (cardUI == null) continue;

            cardUI.SetupCard(cardData);

            // ★ 關鍵：新卡一生成就依旗標套用互動狀態（延遲期間要鎖住）
            cardUI.SetInteractable(!_cardInteractionLocked);

            // （選配但推薦）把位置/狀態歸零交給 Layout，避免殘留
            cardUI.ForceResetToHand(handPanel);
            createdCards.Add(cardUI);
        }

        if (handPanel is RectTransform handRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(handRect);

        if (playDrawAnimation)
        {
            RectTransform deckRect = deckPile as RectTransform;
            for (int i = 0; i < createdCards.Count; i++)
                createdCards[i].PlayDrawAnimation(deckRect);
        }
    }

    private void UpdateEnergyUI()
    {
        if (energyText != null && player != null)
        {
            energyText.text = $"{player.energy}/{player.maxEnergy}";
        }
    }

    /// <summary>
    /// 開始選擇攻擊目標：檢查能量 → 高亮範圍內的敵人
    /// </summary>
    public void StartAttackSelect(CardBase attackCard)
    {
        int finalCost = attackCard.cost + player.GetCardCostModifier(attackCard) + player.buffs.nextAttackCostModify;
        finalCost = Mathf.Max(0, finalCost);
        if (player.energy < finalCost)
        {
            Debug.Log("Not enough energy");
            return;
        }

        isSelectingAttackTarget = true;                  // 標記攻擊選擇中
        currentAttackCard = attackCard;                  // 記錄使用的攻擊卡

        // 取得攻擊範圍偏移
        AttackCardBase aCard = attackCard as AttackCardBase;
        List<Vector2Int> offs = (aCard != null && aCard.rangeOffsets?.Count > 0)
            ? aCard.rangeOffsets
            : new List<Vector2Int>                     // 預設 8 方位
            {
                new Vector2Int(1,0), new Vector2Int(-1,0),
                new Vector2Int(0,1), new Vector2Int(0,-1),
                new Vector2Int(1,1), new Vector2Int(1,-1),
                new Vector2Int(-1,1), new Vector2Int(-1,-1)
            };

        HighlightEnemiesWithOffsets(player.position, offs);  // 高亮範圍內所有敵人
    }

    /// <summary>
    /// 當場上有敵人被點擊時執行攻擊
    /// </summary>
    public bool OnEnemyClicked(Enemy e)
    {
        if (!isSelectingAttackTarget) return false;            // 非攻擊階段忽略
        if (!highlightedEnemies.Contains(e)) return false;     // 範圍外的敵人忽略

        currentAttackCard.ExecuteEffect(player, e);       // 執行攻擊卡效果

        int finalCost = currentAttackCard.cost + player.GetCardCostModifier(currentAttackCard) + player.buffs.nextAttackCostModify;
        finalCost = Mathf.Max(0, finalCost);

        // 棄掉已使用的攻擊卡
        if (player.Hand.Remove(currentAttackCard))
        {
            player.ClearCardCostModifier(currentAttackCard);
        }

        if (currentAttackCard.exhaustOnUse)
        {
            player.ExhaustCard(currentAttackCard);
        }
        else
        {
            player.discardPile.Add(currentAttackCard);
        }
        player.UseEnergy(finalCost);                      // 扣除能量

        EndAttackSelect();                                // 結束攻擊選擇
        RefreshHandUI();
        return true;                              // 更新 UI
    }

    /// <summary>
    /// 結束攻擊選擇：重置狀態並清除高亮
    /// </summary>
    public void EndAttackSelect()
    {
        isSelectingAttackTarget = false;
        currentAttackCard = null;
        foreach (var en in highlightedEnemies)
            en.SetHighlight(false);
        highlightedEnemies.Clear();
    }

    /// <summary>
    /// 高亮指定偏移範圍內的敵人 (配合 StartAttackSelect 使用)
    /// </summary>
    private void HighlightEnemiesWithOffsets(Vector2Int center, List<Vector2Int> offsets)
    {
        highlightedEnemies.Clear();                       // 清空上次結果
        Enemy[] all = FindObjectsOfType<Enemy>();         // 找到場上所有敵人

        foreach (var off in offsets)
        {
            Vector2Int targetPos = center + off;          // 計算目標格子座標
            foreach (var e in all)
            {
                if (e.gridPosition == targetPos && !highlightedEnemies.Contains(e))
                {
                    e.SetHighlight(true);               // 高亮敵人
                    highlightedEnemies.Add(e);          // 加入可選列表
                }
            }
        }
    }

    public void OnEnemyDefeated(Enemy e)
    {
        defeatedEnemyCount++;
    }

    public void ShowVictoryRewards()
    {
        int goldReward = defeatedEnemyCount * 3;
        player.AddGold(goldReward);
        var cardChoices = GetRandomCards(allCardPool, 3);
        Canvas canvas = handPanel != null ? handPanel.GetComponentInParent<Canvas>() : FindObjectOfType<Canvas>();
        if (rewardUIInstance == null)
            rewardUIInstance = Instantiate(rewardUIPrefab, canvas.transform);
        rewardUIInstance.Show(this, goldReward, cardChoices);
    }

    public List<CardBase> GetRandomCards(List<CardBase> pool, int count)
    {
        List<CardBase> result = new List<CardBase>();
        if (pool == null) return result;
        List<CardBase> temp = new List<CardBase>(pool);
        for (int i = 0; i < count && temp.Count > 0; i++)
        {
            int idx = UnityEngine.Random.Range(0, temp.Count);
            result.Add(temp[idx]);
            temp.RemoveAt(idx);
        }
        return result;
    }
}
