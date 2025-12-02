using System.Collections.Generic;                     // 使用泛型集合（List<T>）
using UnityEngine;                                    // 使用 Unity 核心 API（Vector2Int、Debug、Object 等）

public class MovementSelectionController              // 移動選擇控制器：負責處理「移動卡」選格與移動邏輯
{
    private readonly BattleManager battleManager;     // 戰鬥管理器參考，用來查詢狀態機與工具方法
    private readonly Player player;                   // 玩家物件參考
    private readonly Board board;                     // 棋盤物件，負責格子查詢與佔用檢查
    private readonly BattleHandUIController handUIController; // 手牌 UI 控制器，用來更新 UI
    private BattleEncounterLoader encounterLoader;    // 開局選起始格用的 Encounter Loader（可選）

    private bool isSelectingMovementTile = false;     // 是否正在選擇移動目的地格子的旗標
    private CardBase currentMovementCard = null;      // 目前正在使用的「移動卡」資料
    private readonly List<BoardTile> highlightedTiles = new List<BoardTile>();
    // 已被標示為可選（可移動）的格子清單

    public MovementSelectionController(
        BattleManager battleManager,
        Player player,
        Board board,
        BattleHandUIController handUIController,
        BattleEncounterLoader encounterLoader = null) // encounterLoader 預設可為 null
    {
        this.battleManager = battleManager;           // 指定戰鬥管理器
        this.player = player;                         // 指定玩家
        this.board = board;                           // 指定棋盤
        this.handUIController = handUIController;     // 指定手牌 UI 控制器
        this.encounterLoader = encounterLoader;       // 指定 EncounterLoader（可為 null）
    }

    public void SetEncounterLoader(BattleEncounterLoader loader)
    {
        encounterLoader = loader;                     // 之後可以從外部補上/更新 encounterLoader
    }

    public void UseMovementCard(CardBase movementCard)
    {
        if (movementCard == null)                     // 傳入卡片為 null，直接忽略
        {
            return;
        }
        if (!(battleManager.StateMachine.Current is PlayerTurnState))
        {
            return;                                   // 若當前狀態不是玩家回合，不允許使用移動卡
        }
        if (player == null)
        {
            Debug.LogWarning("Player reference not assigned."); // 沒有玩家參考，印警告並中止
            return;
        }
        if (!player.buffs.CanMove())                  // 若 Buff 狀態禁止移動
        {
            Debug.Log("Cannot use movement: movement is currently restricted.");
            // 顯示「目前無法移動」訊息
            return;
        }
        int previewCost = movementCard.cost + player.GetCardCostModifier(movementCard);
        // 計算卡片費用（基礎費用 + 個別卡片費用修正）
        previewCost += player.buffs.movementCostModify;
        // 再加上 Buff 中的移動卡費用修正
        previewCost = Mathf.Max(0, previewCost);      // 費用最低為 0

        if (player.energy < previewCost)              // 玩家能量不足以支付預覽費用
        {
            Debug.Log("Not enough energy for movement");
            // 顯示能量不足訊息
            return;
        }
        if (isSelectingMovementTile)                  // 已經在選移動格
        {
            Debug.Log("Already selecting movement tile");
            // 避免重複觸發選格流程
            return;
        }

        isSelectingMovementTile = true;               // 進入「選擇移動格子」模式
        currentMovementCard = movementCard;           // 記住這張正在使用的移動卡

        MovementCardBase mCard = movementCard as MovementCardBase;
        // 嘗試將 CardBase 轉型為 MovementCardBase（有自訂 rangeOffsets）

        List<Vector2Int> offs = (mCard != null && mCard.rangeOffsets?.Count > 0)
            ? mCard.rangeOffsets                      // 若有定義 rangeOffsets，使用該自訂範圍
            : new List<Vector2Int>                    // 否則使用預設的四方向（上下左右）
            {
                new Vector2Int(0,1), new Vector2Int(0,-1),
                new Vector2Int(-1,0), new Vector2Int(1,0)
            };

        highlightedTiles.Clear();                     // 先清空舊的可選格子清單
        HighlightTilesWithOffsets(player.position, offs);
        // 依照玩家當前位置 + 偏移列表，高亮所有可移動格子
    }

    public void ResetAllTilesSelectable()
    {
        board.ResetAllTilesSelectable();              // 呼叫 Board 重置所有格子的「可選」標記
    }

    public void CancelMovementSelection()
    {
        isSelectingMovementTile = false;              // 離開選格模式
        currentMovementCard = null;                   // 清空目前移動卡參考
        foreach (var t in highlightedTiles)           // 把之前標記為可選的格子
            t.SetSelectable(false);                   // 全部取消可選狀態
        highlightedTiles.Clear();                     // 清空清單
        board.ResetAllTilesSelectable();              // 再讓棋盤重置所有格子的可選狀態
    }

    public bool OnTileClicked(BoardTile tile)
    {
        if (encounterLoader != null && encounterLoader.HandleTileSelection(tile))
        {
            return true;                              // 若目前是開局選起始格，交給 encounterLoader 處理，並回傳 true
        }

        if (!(battleManager.StateMachine.Current is PlayerTurnState))
        {
            return false;                             // 若現在不是玩家回合，不處理點格
        }

        if (!isSelectingMovementTile) return false;   // 若不在選移動格模式，直接忽略
        if (!highlightedTiles.Contains(tile))         // 點到的格子不在可選列表
        {
            CancelMovementSelection();                // 直接取消這次選移動
            return false;
        }
        if (player == null || !player.buffs.CanMove())
        {
            Debug.Log("Cannot move: movement is currently restricted.");
            // 玩家不存在或目前無法移動
            CancelMovementSelection();                // 取消選格
            return false;
        }
        if (board.IsTileOccupied(tile.gridPosition))  // 目標格子上已經有敵人/其他單位
        {
            Debug.Log("Cannot move: tile occupied by enemy.");
            CancelMovementSelection();                // 無法移動到該格，取消
            return false;
        }

        // 以上條件都通過，真正執行移動卡效果
        currentMovementCard.ExecuteOnPosition(player, tile.gridPosition);
        // 將卡片的移動效果套用在指定格子位置上（通常會移動玩家）

        int finalCost = currentMovementCard.cost + player.GetCardCostModifier(currentMovementCard)
                        + player.buffs.movementCostModify;
        // 計算實際要扣的最終費用：基礎 + 修正 + Buff
        finalCost = Mathf.Max(0, finalCost);         // 不低於 0
        player.UseEnergy(finalCost);                 // 扣除玩家能量

        if (player.Hand.Contains(currentMovementCard))   // 若手牌中還有這張卡
        {
            player.Hand.Remove(currentMovementCard);     // 將這張卡從手牌移除

            if (!battleManager.IsGuaranteedMovementCard(currentMovementCard))
            {
                player.discardPile.Add(currentMovementCard);
                // 若不是「保證移動卡」，就丟入棄牌堆
            }
            else
            {
                battleManager.RemoveGuaranteedMovementCardFromPiles();
                // 若是保證移動卡，請 BattleManager 從牌庫/棄牌堆移除所有同種 YiDong
            }
        }

        isSelectingMovementTile = false;              // 結束選格模式
        currentMovementCard = null;                   // 清掉目前移動卡參考
        foreach (var t in highlightedTiles)           // 把所有可選格子
            t.SetSelectable(false);                   // 取消可選顯示
        highlightedTiles.Clear();                     // 清空列表
        board.ResetAllTilesSelectable();              // 重置棋盤所有格子的可選狀態
        handUIController.RefreshHandUI();             // 更新手牌 UI（反映卡片被使用、能量變化）
        return true;                                  // 回傳 true：這次點格有成功處理移動
    }

    private void HighlightTilesWithOffsets(Vector2Int centerPos, List<Vector2Int> offsets)
    {
        foreach (var off in offsets)                  // 針對每個偏移向量
        {
            Vector2Int tilePos = centerPos + off;     // 計算要高亮的格子座標
            BoardTile tile = board.GetTileAt(tilePos);// 從棋盤取得該座標的格子
            if (tile != null && !board.IsTileOccupied(tilePos))
            {
                tile.SetSelectable(true);             // 設這個格子為可選（通常會有高亮/邊框效果）
                highlightedTiles.Add(tile);           // 加到可選格子列表中
            }
        }
    }
}
