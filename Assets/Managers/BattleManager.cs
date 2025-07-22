using System.Collections;                                 // 引用非泛型集合
using System.Collections.Generic;                        // 引用泛型集合
using Unity.VisualScripting;                             // 引用視覺化腳本功能（若未使用可移除）
using UnityEngine;                                       // 引用 Unity 核心功能
using UnityEngine.UI;                                    // 引用 UI 元件功能

public class BattleManager : MonoBehaviour               // 戰鬥流程管理器，掛在場景中的空物件上
{
    public Player player;                                 // 場景中玩家角色的引用
    public Enemy enemy;                                   // 場景中敵人角色的引用
    public GameObject cardPrefab;                         // 卡牌的 Prefab，用於生成卡牌 UI

    // 定義回合狀態枚舉
    private BattleStateMachine stateMachine = new BattleStateMachine();

    public Transform handPanel;                           // Inspector 中指定的手牌區域
    public Transform deckPile;                            // Inspector 中指定的牌庫區域
    public Transform discardPile;                         // Inspector 中指定的棄牌堆區域
    public Board board;                                   // Inspector 中指定的棋盤管理器

    // 是否正在選擇移動目標的旗標
    private bool isSelectingMovementTile = false;
    // 儲存當前正在使用的移動卡
    private CardBase currentMovementCard = null;

    // 是否正在選擇攻擊目標的旗標
    private bool isSelectingAttackTarget = false;
    // 儲存當前正在使用的攻擊卡
    private CardBase currentAttackCard = null;

    // 被高亮的敵人列表，用於攻擊選擇階段
    private List<Enemy> highlightedEnemies = new List<Enemy>();

    void Start()
    {
       stateMachine.ChangeState(new PlayerTurnState(this));
    }

    void Update()
    {
       stateMachine.Update();

        if (enemy != null && enemy.currentHP <= 0 && !(stateMachine.Current is VictoryState))
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
        if (enemy != null)
            enemy.ProcessTurnStart();                     // 敵人回合開始效果

        int drawCount = 5 + player.buffs.nextTurnDrawChange;  // 計算抽牌數量
        drawCount = Mathf.Max(0, drawCount);               // 確保不為負
        player.buffs.nextTurnDrawChange = 0;               // 重置下回合抽牌變更

        player.DrawNewHand(drawCount);                     // 重新抽牌
        RefreshHandUI();                                   // 同步手牌 UI
    }

    /// <summary>
    /// 棄掉所有手牌並更新 UI
    /// </summary>
    private void DiscardAllHand()
    {
        player.discardPile.AddRange(player.hand);          // 全部移入棄牌堆
        player.hand.Clear();                               // 清空手牌
        RefreshHandUI();                                   // 更新 UI 顯示
    }

    /// <summary>
    /// 敵人回合流程：開始效果 → 行動 → 結束後回到玩家回合
    /// </summary>
     public IEnumerator EnemyTurnCoroutine()
    {
        
        if (enemy != null)
            enemy.ProcessTurnStart();                     // 敵人回合開始效果

        yield return new WaitForSeconds(1f);               // 等待 1 秒

        if (enemy != null)
            enemy.EnemyAction(player);                    // 敵人執行攻擊或行動

        yield return new WaitForSeconds(1f);               // 等待 1 秒

        // 清除本回合所有格擋 (Slay the Spire 流程)
        player.block = 0;
        if (enemy != null) enemy.block = 0;

        // 回到玩家回合
        stateMachine.ChangeState(new PlayerTurnState(this));
        Debug.Log("Player Turn");
    }

    /// <summary>
    /// 玩卡：處理費用、執行效果、棄牌、更新 UI
    /// </summary>
    public void PlayCard(CardBase cardData)
    {
         if (!(stateMachine.Current is PlayerTurnState)) return;

        // 計算最終費用 (包含 Buff 修改)
        int finalCost = cardData.cost;
        if (cardData.cardType == CardType.Attack && player.buffs.nextAttackCostModify != 0)
        {
            finalCost += player.buffs.nextAttackCostModify;
            finalCost = Mathf.Max(0, finalCost);
        }
        if (cardData.cardType == CardType.Movement && player.buffs.movementCostModify != 0)
        {
            finalCost += player.buffs.movementCostModify;
            finalCost = Mathf.Max(0, finalCost);
        }

        if (player.energy < finalCost)                     // 能量不足時拒絕
        {
            Debug.Log("Not enough energy");
            return;
        }

        cardData.ExecuteEffect(player, enemy);            // 執行卡牌效果

        // 使用攻擊卡時，更新統計和清除下一次加傷
        if (cardData.cardType == CardType.Attack)
        {
            player.attackUsedThisTurn++;
            if (player.buffs.nextAttackPlus > 0)
                player.buffs.nextAttackPlus = 0;
        }

        // 若手牌中仍含此卡，則移至棄牌堆
        if (player.hand.Contains(cardData))
        {
            player.hand.Remove(cardData);
            player.discardPile.Add(cardData);
        }

        player.UseEnergy(finalCost);
        GameEvents.RaiseCardPlayed(cardData);
        RefreshHandUI();
    }

    /// <summary>
    /// 使用移動牌：檢查能量 → 進入選格子模式 → 高亮格子
    /// </summary>
    public void UseMovementCard(CardBase movementCard)
    {
         if (!(stateMachine.Current is PlayerTurnState))
        {
            return;
        }
        if (player.energy < movementCard.cost)             // 能量檢查
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
            if (tile != null&& !board.IsTileOccupied(tilePos))
                tile.SetSelectable(true);                  // 標記該格可選
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
        board.ResetAllTilesSelectable();
    }

    /// <summary>
    /// 玩家點擊格子時觸發：執行移動、扣能量、棄牌、重置
    /// </summary>
    public bool OnTileClicked(BoardTile tile)
    {
        if (!isSelectingMovementTile) return false;
        if (board.IsTileOccupied(tile.gridPosition))
        {
            Debug.Log("Cannot move: tile occupied by enemy.");
            CancelMovementSelection();
            return false;
        }

        currentMovementCard.ExecuteOnPosition(player, tile.gridPosition);  // 執行移動卡效果

        int finalCost = currentMovementCard.cost + player.buffs.movementCostModify;
        finalCost = Mathf.Max(0, finalCost);
        player.UseEnergy(finalCost);                      // 扣除能量

        // 棄掉已使用的移動卡
        if (player.hand.Contains(currentMovementCard))
        {
            player.hand.Remove(currentMovementCard);
            player.discardPile.Add(currentMovementCard);
        }

        isSelectingMovementTile = false;                  // 重置狀態
        currentMovementCard = null;
        board.ResetAllTilesSelectable();                  // 清除所有高亮
        RefreshHandUI();
        return true;                                   // 更新 UI
    }

    /// <summary>
    /// 更新手牌、牌庫、棄牌堆的 UI 顯示
    /// </summary>
    public void RefreshHandUI()
    {
        // 更新牌庫區文字
        if (deckPile)
        {
            Text t = deckPile.GetComponentInChildren<Text>();
            if (t) t.text = "牌庫區: " + player.deck.Count;
        }
        // 更新棄牌區文字
        if (discardPile)
        {
            Text t2 = discardPile.GetComponentInChildren<Text>();
            if (t2) t2.text = "棄牌區: " + player.discardPile.Count;
        }
        // 清空原本的手牌 UI
        foreach (Transform child in handPanel)
            Destroy(child.gameObject);

        // 依手牌資料重新生成卡牌 UI
        foreach (var cardData in player.hand)
        {
            GameObject cardObj = Instantiate(cardPrefab, handPanel);
            CardUI cardUI = cardObj.GetComponent<CardUI>();
            cardUI.SetupCard(cardData);
        }
    }

    /// <summary>
    /// 開始選擇攻擊目標：檢查能量 → 高亮範圍內的敵人
    /// </summary>
    public void StartAttackSelect(CardBase attackCard)
    {
        int finalCost = attackCard.cost + player.buffs.nextAttackCostModify;
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

        // 棄掉已使用的攻擊卡
        player.hand.Remove(currentAttackCard);
        player.discardPile.Add(currentAttackCard);

        int finalCost = currentAttackCard.cost + player.buffs.nextAttackCostModify;
        finalCost = Mathf.Max(0, finalCost);
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
}
