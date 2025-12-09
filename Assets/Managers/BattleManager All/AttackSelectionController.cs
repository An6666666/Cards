using System.Collections.Generic;              // 引用泛型集合命名空間，這裡會用到 List<T>
using UnityEngine;                             // 引用 Unity 引擎相關類別（例如 Vector2Int、Object 等）

public class AttackSelectionController          // 負責「攻擊目標選取」邏輯的控制類
{
    private readonly Player player;                         // 玩家物件參考
    private readonly Board board;                           // 棋盤參考，用來顯示攻擊範圍
    private readonly BattleHandUIController handUIController; // 管理手牌 UI 的控制器參考

    private bool isSelectingAttackTarget = false;           // 是否正在選擇攻擊目標的狀態旗標
    private CardBase currentAttackCard = null;              // 目前準備要執行攻擊的那張卡牌資料
    private readonly List<Enemy> validEnemies = new List<Enemy>();
    // 可作為攻擊目標的敵人列表（不主動高亮）
    private Enemy currentHighlightedEnemy = null;           // 當前滑鼠瞄準並高亮的敵人
    private readonly List<Enemy> areaHighlightedEnemies = new List<Enemy>();
    private readonly List<BoardTile> highlightedTiles = new List<BoardTile>();
    // 顯示攻擊範圍的棋盤格子高亮

    public AttackSelectionController(
        Player player,
        Board board,
        BattleHandUIController handUIController)
    {
        this.player = player;                               // 注入玩家
        this.board = board;                                 // 注入棋盤
        this.handUIController = handUIController;           // 注入手牌 UI 控制器
    }

    public void StartAttackSelect(CardBase attackCard)
    {
        // 計算這張攻擊卡的最終費用：基礎 cost + 玩家對此卡的費用修正 + 下一張攻擊卡的費用修正 buff
        int finalCost = attackCard.cost + player.GetCardCostModifier(attackCard) + player.buffs.nextAttackCostModify;
        finalCost = Mathf.Max(0, finalCost);               // 確保最終費用不會小於 0

        if (player.energy < finalCost)                     // 若當前能量不足以支付這張卡
        {
            Debug.Log("Not enough energy");                // 在 Console 顯示能量不足訊息
            return;                                        // 直接取消選取流程
        }

        isSelectingAttackTarget = true;                    // 進入「正在選擇攻擊目標」狀態
        currentAttackCard = attackCard;                    // 記住目前準備要打出去的攻擊卡

        AttackCardBase aCard = attackCard as AttackCardBase; // 嘗試把一般 CardBase 轉型為 AttackCardBase
        List<Vector2Int> offs = (aCard != null && aCard.rangeOffsets?.Count > 0)
            ? aCard.rangeOffsets                           // 若這張攻擊卡有自訂 rangeOffsets，就用它
            : new List<Vector2Int>                         // 否則使用預設的近戰八方向 (上下左右+斜角)
            {
                new Vector2Int(1,0), new Vector2Int(-1,0), // 右、左
                new Vector2Int(0,1), new Vector2Int(0,-1), // 上、下
                new Vector2Int(1,1), new Vector2Int(1,-1), // 右上、右下
                new Vector2Int(-1,1), new Vector2Int(-1,-1)// 左上、左下
            };

        CacheValidEnemiesWithOffsets(player.position, offs); // 依照偏移列表，蒐集可攻擊的敵人
        HighlightTilesWithOffsets(player.position, offs);    // 以棋盤格高亮顯示攻擊範圍
    }

    public bool OnEnemyClicked(Enemy e)
    {
        if (!isSelectingAttackTarget) return false;        // 若目前不是在選擇攻擊目標流程中，直接忽略
        if (!validEnemies.Contains(e)) return false;       // 若點到的敵人不在可攻擊列表中，也忽略

        // 執行目前攻擊卡對該敵人產生的效果（扣血、上狀態等）
        currentAttackCard.ExecuteEffect(player, e);

        // 再次計算這張卡的最終實際費用（避免選取後 buff 有變化時不一致）
        int finalCost = currentAttackCard.cost + player.GetCardCostModifier(currentAttackCard) + player.buffs.nextAttackCostModify;
        finalCost = Mathf.Max(0, finalCost);              // 保底不能小於 0

        // 從玩家手牌中移除這張卡，如成功移除，順便清掉對這張卡的費用修正紀錄
        if (player.Hand.Remove(currentAttackCard))
        {
            player.ClearCardCostModifier(currentAttackCard);
        }

        if (currentAttackCard.exhaustOnUse)               // 若這張卡標記為「使用後消耗」（放進 Exhaust 區）
        {
            player.ExhaustCard(currentAttackCard);        // 進 Exhaust，戰鬥中不再回來
        }
        else                                              // 否則進棄牌堆
        {
            player.discardPile.Add(currentAttackCard);    // 將此卡加入玩家棄牌堆
        }
        player.UseEnergy(finalCost);                      // 扣除此次使用花費的能量

        EndAttackSelect();                                // 結束攻擊選取流程（清高亮與狀態）
        handUIController.RefreshHandUI();                 // 更新手牌 UI（重建卡牌顯示、更新能量文字等）
        return true;                                      // 回傳 true 表示有成功處理這次點擊
    }

    public void EndAttackSelect()
    {
        isSelectingAttackTarget = false;                  // 不再處於「選擇攻擊目標」狀態
        currentAttackCard = null;                         // 清除目前攻擊卡的引用
        ClearCurrentEnemyHighlight();                     // 關閉目前瞄準敵人的高亮
        validEnemies.Clear();                             // 清空可攻擊敵人列表
        ClearRangeHighlights();                           // 關閉棋盤攻擊範圍高亮
    }

    public void UpdateAttackHover(Vector2 worldPosition)
    {
        if (!isSelectingAttackTarget) return;             // 若不在攻擊選取狀態，不處理 hover

        Collider2D hit = Physics2D.OverlapPoint(worldPosition); // 取得目前滑鼠指向的碰撞體
        Enemy targetEnemy = hit != null ? hit.GetComponentInParent<Enemy>() : null; // 找到敵人元件

        if (targetEnemy != null && validEnemies.Contains(targetEnemy))
        {
            SetCurrentEnemyHighlight(targetEnemy);        // 只有在有效目標範圍內才高亮
        }
        else
        {
            ClearCurrentEnemyHighlight();                 // 沒有有效目標就清除高亮
        }
    }

    private void CacheValidEnemiesWithOffsets(Vector2Int center, List<Vector2Int> offsets)
    {
        validEnemies.Clear();                             // 先清空可攻擊敵人列表
        Enemy[] all = Object.FindObjectsOfType<Enemy>();  // 場上所有 Enemy（從場景中搜尋）

        foreach (var off in offsets)                      // 對每一個攻擊偏移量
        {
            Vector2Int targetPos = center + off;          // 計算「玩家位置 + 偏移」得到可攻擊格子位置
            foreach (var e in all)                        // 檢查所有敵人
            {
                if (e.gridPosition == targetPos && !validEnemies.Contains(e))
                {
                    validEnemies.Add(e);                  // 加入可攻擊列表，避免重複
                }
            }
        }
    }

    private void HighlightTilesWithOffsets(Vector2Int center, List<Vector2Int> offsets)
    {
        ClearRangeHighlights();                           // 清掉舊的範圍高亮
        if (board == null) return;                        // 沒有棋盤參考就不處理

        foreach (var off in offsets)
        {
            BoardTile tile = board.GetTileAt(center + off);
            if (tile != null)
            {
                tile.SetHighlight(true);                  // 使用與移動卡相同的格子高亮
                highlightedTiles.Add(tile);               // 記錄以便之後關閉
            }
        }
    }

    private void ClearRangeHighlights()
    {
        foreach (var tile in highlightedTiles)
            tile.SetHighlight(false);                     // 關閉所有範圍格子的高亮

        highlightedTiles.Clear();
    }

    private void SetCurrentEnemyHighlight(Enemy enemy)
    {
        if (enemy == currentHighlightedEnemy) return;     // 已經高亮同一個敵人就不重複

        ClearCurrentEnemyHighlight();                     // 先清除舊的高亮
        currentHighlightedEnemy = enemy;                  // 更新目前高亮的敵人
        Attack_TianFa tianFaCard = currentAttackCard as Attack_TianFa; // 判斷是否為天罰範圍攻擊
        if (tianFaCard != null)
        {
            HighlightAreaTargets(enemy, tianFaCard.effectRadius); // 高亮所有會被範圍命中的敵人
        }
        else
        {
            currentHighlightedEnemy.SetHighlight(true);  // 單體攻擊只高亮目前瞄準的敵人
            areaHighlightedEnemies.Add(currentHighlightedEnemy);
        }
    }

    private void ClearCurrentEnemyHighlight()
    {
        foreach (Enemy highlightedEnemy in areaHighlightedEnemies)
        {
            if (highlightedEnemy != null)
            {
                highlightedEnemy.SetHighlight(false);    // 關閉高亮
            }
        }

        areaHighlightedEnemies.Clear();
        currentHighlightedEnemy = null;                   // 清空記錄
    }

    private void HighlightAreaTargets(Enemy centerEnemy, float radius)
    {
        if (centerEnemy == null) return;

        Vector2Int center = centerEnemy.gridPosition;
        Enemy[] allEnemies = Object.FindObjectsOfType<Enemy>();

        foreach (Enemy target in allEnemies)
        {
            if (target == null) continue;

            float distance = Vector2Int.Distance(center, target.gridPosition);
            if (target != centerEnemy && distance > radius) continue; // 同 ExecuteEffect 判斷方式

            target.SetHighlight(true);
            areaHighlightedEnemies.Add(target);
        }
    }
}
