using System.Collections;                     // 引用非泛型集合命名空間（如 ArrayList、Hashtable），雖然本檔案未直接使用，但常見於 Unity 範本
using System.Collections.Generic;             // 引用泛型集合命名空間（如 List<T>、Dictionary<TKey,TValue>）
using UnityEngine;                            // 引用 Unity 的核心 API（如 MonoBehaviour、GameObject、Mathf 等）

internal static class ElementReactionOrderHelper
{
    public static ElementType? GetLatestReactiveTag(Enemy defender, ElementType[] candidates)
    {
        if (defender == null || candidates == null || candidates.Length == 0)
        {
            return null;
        }

        foreach (var tag in defender.GetElementTagsByRecentOrder())
        {
            if (Contains(candidates, tag))
            {
                return tag;
            }
        }

        return null;
    }

    private static bool Contains(ElementType[] candidates, ElementType tag)
    {
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] == tag)
            {
                return true;
            }
        }

        return false;
    }
}

public interface IElementalStrategy            // 宣告元素策略介面：定義所有元素計算傷害時必須實作的方法
{                                              // 介面區塊開始
    int CalculateDamage(Player attacker, Enemy defender, int baseDamage); // 計算傷害的方法：輸入攻擊者、被攻擊者與基礎傷害，回傳實際傷害值
}                                              // 介面區塊結束
public interface IPlayerEndTurnEffect          // 宣告玩家回合結束效果介面：某些元素有「玩家回合結束時」要處理的效果
{                                              // 介面區塊開始
    void OnPlayerEndTurn(Enemy enemy);         // 在玩家回合結束時呼叫，用來處理持續性狀態（如燃燒扣血）
}                                              // 介面區塊結束

public class DefaultElementalStrategy : IElementalStrategy // 預設元素策略實作：不做任何加成或特效，單純回傳原傷害
{                                              // 類別區塊開始
    public virtual int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // virtual 允許子類別覆寫
    {                                          // 方法區塊開始
        return baseDamage;                     // 直接回傳基礎傷害，沒有任何變動
    }                                          // 方法區塊結束
}                                              // 類別區塊結束

public class FireStrategy : DefaultElementalStrategy, IPlayerEndTurnEffect // 火元素策略：繼承預設策略並實作玩家回合結束效果
{                                              // 類別區塊開始
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 覆寫傷害計算：火元素的特殊反應處理
    {                                          // 方法區塊開始
        int dmg = baseDamage;                  // 先以基礎傷害為起點，之後依反應再修正

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
        defender,
        new[] { ElementType.Water, ElementType.Ice, ElementType.Wood, ElementType.Thunder });

        if (latestReactive == ElementType.Water)              // 若防守者身上有水元素
        {                                                        // if 區塊開始
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);            // 火遇水：提高傷害 1.5 倍並無條件進位
            defender.RemoveElementTag(ElementType.Water);        // 移除水標記
            defender.AddElementTag(ElementType.Fire);            // 新增火標記（表示被火附著）
        }                                                        // if 區塊結束
        else if (latestReactive == ElementType.Ice)           // 否則若有冰元素
        {                                                        // else if 區塊開始
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);            // 火融冰：同樣 1.5 倍傷害
            defender.RemoveElementTag(ElementType.Ice);          // 移除冰標記
            defender.AddElementTag(ElementType.Fire);            // 加上火標記
        }                                                        // else if 區塊結束
        else if (latestReactive == ElementType.Wood)          // 若有木元素
        {                                                        // else if 區塊開始
            defender.SetBurningTurns(5);                         // 點燃木頭：設定燃燒持續 5 回合
            defender.RemoveElementTag(ElementType.Wood);         // 燃燒後木材被消耗
            defender.AddElementTag(ElementType.Fire);            // 加上火標記（燃燒來源）
        }                                                        // else if 區塊結束
        else if (latestReactive == ElementType.Thunder)       // 若有雷元素
        {                                                        // else if 區塊開始
            ElementType keep = ElementType.Fire;                 // 反應後保留的元素：火
            ElementType remove = ElementType.Thunder;            // 反應後要移除的元素：雷
            Board board = GameObject.FindObjectOfType<Board>();  // 在場景中尋找 Board 實例（棋盤/格子管理）
            if (board != null)                                   // 若成功找到 Board
            {                                                    // if 區塊開始
                foreach (var en in GameObject.FindObjectsOfType<Enemy>()) // 迴圈遍歷場景中所有 Enemy
                {                                                // foreach 區塊開始
                    if (en == defender) continue;                // 跳過本體（不處理自己）
                    if (Vector2Int.Distance(en.gridPosition, defender.gridPosition) <= 2.3f) // 若距離小於等於 2.3（視為相鄰）
                    {                                            // if 區塊開始
                        int spreadDmg = Mathf.CeilToInt(baseDamage * 0.5f); // 相鄰敵人受到 0.5 倍基礎傷害
                        ApplyFireSpreadReaction(en, ref spreadDmg); // 依照現有元素觸發對應反應
                        if (spreadDmg > 0) en.TakeDamage(spreadDmg); // 造成最終傷害
                    }                                            // if 區塊結束
                }                                                // foreach 區塊結束
            }                                                    // if 區塊結束
            defender.RemoveElementTag(remove);                   // 從本體移除雷元素標記
            defender.AddElementTag(keep);                        // 給本體加上火元素標記
        }                                                        // else if 區塊結束
        else                                                     // 若沒有任何特定相剋元素
        {                                                        // else 區塊開始
            defender.AddElementTag(ElementType.Fire);            // 單純附著火元素
        }                                                        // else 區塊結束

        if (defender.thunderstrike)                              // 若防守者有「雷擊加倍」狀態（thunderstrike）
        {                                                        // if 區塊開始
            dmg *= 2;                                            // 傷害加倍
            defender.thunderstrike = false;                      // 使用後清除狀態，避免下次再觸發
        }                                                        // if 區塊結束

        return dmg;                                              // 回傳最終傷害
    }                                                            // 方法區塊結束

    private void ApplyFireSpreadReaction(Enemy enemy, ref int dmg)
    {
        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
        enemy,
        new[] { ElementType.Water, ElementType.Ice, ElementType.Wood, ElementType.Thunder });

        if (latestReactive == ElementType.Water)
        {
            dmg = Mathf.CeilToInt(dmg * 1.5f);
            enemy.RemoveElementTag(ElementType.Water);
            enemy.AddElementTag(ElementType.Fire);
        }
        else if (latestReactive == ElementType.Ice)
        {
            dmg = Mathf.CeilToInt(dmg * 1.5f);
            enemy.RemoveElementTag(ElementType.Ice);
            enemy.AddElementTag(ElementType.Fire);
        }
        else if (latestReactive == ElementType.Wood)
        {
            enemy.SetBurningTurns(5);
            enemy.RemoveElementTag(ElementType.Wood);
            enemy.AddElementTag(ElementType.Fire);
        }
        else if (latestReactive == ElementType.Thunder)
        {
            enemy.RemoveElementTag(ElementType.Thunder);
            enemy.AddElementTag(ElementType.Fire);
        }
        else
        {
            enemy.AddElementTag(ElementType.Fire);
        }
    }
    
    public void OnPlayerEndTurn(Enemy enemy)                     // 玩家回合結束效果：火的燃燒 DOT（持續傷害）
    {                                                            // 方法區塊開始
        if (enemy.burningTurns > 0)                              // 若仍有燃燒回合數
        {                                                        // if 區塊開始
            enemy.TakeDamage(2);                                 // 每回合固定扣 2 點傷害
            enemy.burningTurns--;                                // 燃燒回合數 -1
            enemy.RaiseStatusChanged();
            if (enemy.burningTurns == 0)                         // 若燃燒剛好結束
            {                                                    // if 區塊開始
                enemy.RemoveElementTag(ElementType.Wood);        // 燃燒結束後只清除木標記
            }                                                    // if 區塊結束
        }                                                        // if 區塊結束
    }                                                            // 方法區塊結束
}                                                                // 類別區塊結束

public class WaterStrategy : DefaultElementalStrategy            // 水元素策略：只覆寫傷害，沒有回合開始效果
{                                                                // 類別區塊開始
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 覆寫水元素的傷害計算
    {                                                            // 方法區塊開始
        int dmg = baseDamage;                                    // 基礎傷害起點

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
        defender,
        new[] { ElementType.Fire, ElementType.Ice });

        if (latestReactive == ElementType.Fire)               // 水剋火：若對方有火
        {                                                        // if 區塊開始
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);            // 傷害 1.5 倍
            defender.RemoveElementTag(ElementType.Fire);         // 移除火標記
            defender.SetBurningTurns(0);                         // 受到水傷害時清除燃燒
            defender.AddElementTag(ElementType.Water);           // 加上水標記
        }                                                        // if 區塊結束
        else if (latestReactive == ElementType.Ice)           // 水 + 冰：凍結判定
        {                                                        // else if 區塊開始
            bool freeze = true;                                  // 預設會凍結
            if (defender.isBoss && UnityEngine.Random.value < 0.5f) // 若是 Boss，有 50% 免疫凍結（隨機）
                freeze = false;                                  // 設定不凍結
            if (freeze) defender.SetFrozenTurns(1);              // 若要凍結，設定凍結 1 回合
            defender.RemoveElementTag(ElementType.Ice);          // 清除冰標記
            defender.RemoveElementTag(ElementType.Water);        // 清除水標記（反應後兩者皆消失）
        }                                                        // else if 區塊結束
        else                                                     // 其餘情況：只是附著水
        {                                                        // else 區塊開始
            defender.AddElementTag(ElementType.Water);           // 加上水標記
        }                                                        // else 區塊結束

        ApplyElementToTiles(defender, ElementType.Water);        // 將水元素標記擴散到格子

        return dmg;                                              // 回傳最終傷害
    }                                                            // 方法區塊結束

    private void ApplyElementToTiles(Enemy defender, ElementType element) // 協助方法：在格子上附著元素
    {
        Board board = GameObject.FindObjectOfType<Board>();      // 尋找場上的棋盤
        if (board == null) return;                               // 若沒有棋盤則不處理

        BoardTile current = board.GetTileAt(defender.gridPosition); // 取得敵人所在格子
        if (current != null) current.AddElement(element);        // 在該格子加入元素標籤

        foreach (var adj in board.GetAdjacentTiles(defender.gridPosition)) // 迭代相鄰格子
        {
            adj.AddElement(element);                             // 相鄰格子也加入相同元素標籤
        }
    }
}                                                                // 類別區塊結束

public class ThunderStrategy : DefaultElementalStrategy          // 雷元素策略：多種連鎖、傳導反應
{                                                                // 類別區塊開始
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 覆寫雷元素傷害計算
    {                                                            // 方法區塊開始
        int dmg = baseDamage;                                    // 基礎傷害
        Board board = GameObject.FindObjectOfType<Board>();      // 快取場上的棋盤
        Enemy[] allEnemies = GameObject.FindObjectsOfType<Enemy>(); // 快取所有敵人，避免重複搜尋

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
        defender,
        new[] { ElementType.Fire, ElementType.Water, ElementType.Ice, ElementType.Wood });
        bool defenderHasWaterTag = defender.HasElement(ElementType.Water); // 防守者是否帶有水元素標籤
        bool defenderOnWaterTile = false;                        // 防守者腳下的格子是否帶有水元素

        if (board != null)                                       // 需要棋盤資訊才能檢查格子元素
        {
            BoardTile defenderTile = board.GetTileAt(defender.gridPosition); // 取得當前格子
            defenderOnWaterTile = defenderTile != null && defenderTile.HasElement(ElementType.Water); // 判斷格子是否帶水
        }

        if (latestReactive == ElementType.Fire)               // 雷 + 火：擴散到周圍（與火類似，但元素交換不同）
        {                                                        // if 區塊開始
            ElementType keep = ElementType.Thunder;              // 保留雷
            ElementType remove = ElementType.Fire;               // 移除火
            
            if (board != null)                                   // 若找到
            {                                                    // if 區塊開始
                foreach (var en in allEnemies)                   // 迭代所有敵人
                {                                                // foreach 區塊開始
                    if (en == defender) continue;                // 跳過自己
                    if (Vector2Int.Distance(en.gridPosition, defender.gridPosition) <= 2.3f) // 相鄰判定
                    {                                            // if 區塊開始
                        int spreadDmg = Mathf.CeilToInt(baseDamage * 0.5f); // 相鄰扣 0.5 倍
                        ApplyThunderSpreadReaction(en, ref spreadDmg);
                        if (spreadDmg > 0) en.TakeDamage(spreadDmg);
                    }                                            // if 區塊結束
                }                                                // foreach 區塊結束
            }                                                    // if 區塊結束
            defender.RemoveElementTag(remove);                   // 移除本體火
            defender.AddElementTag(keep);                        // 本體加雷
        }                                                        // if 區塊結束
        else if (latestReactive == ElementType.Water || (latestReactive == null && defenderOnWaterTile))     // 雷 + 水：導電擴散，判斷敵人是否帶水或站在水格
        {                                                        // else if 區塊開始
            if (board != null)                                   // 需要棋盤資訊才能追蹤水元素連鎖
            {                                                    // if 區塊開始
                var enemyByPos = new Dictionary<Vector2Int, Enemy>(); // 建立座標到敵人的映射
                foreach (var en in allEnemies)                   // 迭代所有敵人
                {                                                // foreach 區塊開始
                    if (!enemyByPos.ContainsKey(en.gridPosition)) // 避免重複加入同座標
                        enemyByPos[en.gridPosition] = en;        // 建立映射
                }                                                // foreach 區塊結束

                Queue<Vector2Int> pending = new Queue<Vector2Int>(); // 佇列：待處理的格子座標
                HashSet<Vector2Int> visited = new HashSet<Vector2Int>(); // 紀錄已處理過的格子
                HashSet<Enemy> chainTargets = new HashSet<Enemy>(); // 需要承受連鎖傷害的敵人

                pending.Enqueue(defender.gridPosition);          // 以被攻擊者所在格子為起點
                visited.Add(defender.gridPosition);              // 標記起點已拜訪

                while (pending.Count > 0)                        // BFS 掃描所有相連水元素格子
                {                                                // while 區塊開始
                    Vector2Int current = pending.Dequeue();      // 取出目前處理的格子
                    var neighbors = board.GetAdjacentTiles(current); // 取得相鄰格子
                    foreach (var tile in neighbors)              // 檢查每個鄰居
                    {                                            // foreach 區塊開始
                        Vector2Int pos = tile.gridPosition;      // 鄰居座標
                        if (visited.Contains(pos)) continue;     // 已處理則跳過

                        enemyByPos.TryGetValue(pos, out Enemy occupant); // 試著找到該格子的敵人
                        bool tileHasWater = tile.HasElement(ElementType.Water); // 格子是否有水標籤
                        bool enemyHasWater = occupant != null && occupant.HasElement(ElementType.Water); // 敵人是否有水標籤

                        if (!tileHasWater && !enemyHasWater)     // 若格子與敵人都沒有水標籤
                            continue;                            // 不進入連鎖

                        visited.Add(pos);                        // 標記為已拜訪
                        pending.Enqueue(pos);                    // 加入待處理佇列，繼續擴散

                        if (occupant != null && occupant != defender) // 只對其他敵人造成傷害
                            chainTargets.Add(occupant);          // 加入連鎖傷害目標
                    }                                            // foreach 區塊結束
                }                                                // while 區塊結束

                foreach (var target in chainTargets)             // 對所有連鎖目標造成傷害
                {                                                // foreach 區塊開始
                    target.TakeDamage(baseDamage);               // 造成等同基礎值的傷害
                }                                                // foreach 區塊結束
            }                                                    // if 區塊結束
            else                                                 // 若場上無棋盤資訊
            {                                                    // else 區塊開始
                foreach (var en in allEnemies)                   // 退回舊邏輯：僅處理相鄰且帶水的敵人
                {                                                // foreach 區塊開始
                    if (en == defender) continue;                // 跳過自己
                    bool adjacent = Vector2Int.Distance(en.gridPosition, defender.gridPosition) <= 2.3f; // 是否相鄰
                    if (adjacent && en.HasElement(ElementType.Water)) // 僅當敵人身上有水標籤
                        en.TakeDamage(baseDamage);               // 造成基礎傷害
                }                                                // foreach 區塊結束
            }                                                    // else 區塊結束
            defender.AddElementTag(ElementType.Thunder); // 以導電結果為主，若沒有其他反應則附著雷
        }                                                        // else if 區塊結束
        else if (TryApplyThunderReactionWithoutWater(defender, false, latestReactive, ref dmg)) // 先檢查冰/木組合（依最新附著順序）
        {                                                        // else if 區塊開始
            // 反應處理已在 helper 內完成
        }                                                        // else if 區塊結束
        else                                                     // 沒有特別反應
        {                                                        // else 區塊開始
            defender.AddElementTag(ElementType.Thunder);         // 單純附著雷
        }                                                        // else 區塊結束

        if (defender.thunderstrike)                              // 若雷擊狀態生效
        {                                                        // if 區塊開始
            dmg *= 2;                                            // 傷害加倍
            defender.thunderstrike = false;                      // 清除雷擊狀態
        }                                                        // if 區塊結束

        if (defender.superconduct)                               // 若超導狀態生效
        {                                                        // if 區塊開始
            dmg += 6;                                            // 額外加 6 點固定傷害
            defender.superconduct = false;                       // 清除超導狀態
        }                                                        // if 區塊結束

        return dmg;                                              // 回傳最終傷害
    }                                                            // 方法區塊結束

    private void ApplyThunderSpreadReaction(Enemy enemy, ref int dmg)
    {
        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
        enemy,
        new[] { ElementType.Fire, ElementType.Water, ElementType.Ice, ElementType.Wood });

        if (latestReactive == ElementType.Water)
        {
            enemy.TakeDamage(dmg);
            dmg = 0; // 已經處理傷害，避免重複
            bool reacted = TryApplyThunderReactionWithoutWater(enemy, true, latestReactive, ref dmg); // 水導電後檢查冰/木
            if (!reacted) enemy.AddElementTag(ElementType.Thunder); // 無反應才附著雷
            return;
        }
        if (latestReactive == ElementType.Fire)
        {
            enemy.RemoveElementTag(ElementType.Fire);
            enemy.AddElementTag(ElementType.Thunder);
            return;
        }
        if (TryApplyThunderReactionWithoutWater(enemy, true, latestReactive, ref dmg))
        {
            return;
        }

        if (dmg == 0) return;

        enemy.AddElementTag(ElementType.Thunder);
    }

    /// <summary>
    /// 處理雷元素與非水元素（冰或木）的組合反應。
    /// </summary>
    /// <param name="target">要處理的目標敵人。</param>
    /// <param name="zeroDamageOnReact">若發生反應，是否將當前傷害歸零（spread 版本需要）。</param>
    /// <param name="latestReactive">外部已計算好的最新可反應元素，若為 null 則在此計算。</param>
    /// <param name="dmg">傳入的傷害引用，視情況可能被重設。</param>
    /// <returns>若有發生反應則回傳 true。</returns>
    private static bool TryApplyThunderReactionWithoutWater(Enemy target, bool zeroDamageOnReact, ElementType? latestReactive, ref int dmg)
    {
        ElementType? reactive = latestReactive ?? ElementReactionOrderHelper.GetLatestReactiveTag(
        target,
        new[] { ElementType.Ice, ElementType.Wood });

        if (reactive == ElementType.Ice)
        {
            target.superconduct = true;
            target.RemoveElementTag(ElementType.Thunder);
            target.RemoveElementTag(ElementType.Ice);
            if (zeroDamageOnReact) dmg = 0;
            return true;
        }

        if (reactive == ElementType.Wood)
        {
            target.thunderstrike = true;
            target.RemoveElementTag(ElementType.Wood);
            target.RemoveElementTag(ElementType.Thunder);
            if (zeroDamageOnReact) dmg = 0;
            return true;
        }

        return false;
    }
}                                                                // 類別區塊結束

public class IceStrategy : DefaultElementalStrategy              // 冰元素策略
{                                                                // 類別區塊開始
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 覆寫冰的傷害計算
    {                                                            // 方法區塊開始
        int dmg = baseDamage;                                    // 基礎傷害

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
        defender,
        new[] { ElementType.Fire, ElementType.Water, ElementType.Thunder });

        if (latestReactive == ElementType.Fire)               // 冰 + 火：互剋（實作為 1.5 倍並覆蓋火）
        {                                                        // if 區塊開始
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);            // 1.5 倍傷害
            defender.RemoveElementTag(ElementType.Fire);         // 移除火
            defender.SetBurningTurns(0);                         // 受到冰傷害時清除燃燒
            defender.AddElementTag(ElementType.Ice);             // 附著冰
        }                                                        // if 區塊結束
        else if (latestReactive == ElementType.Water)         // 冰 + 水：凍結機率判定
        {                                                        // else if 區塊開始
            bool freeze = true;                                  // 預設凍結
            if (defender.isBoss && UnityEngine.Random.value < 0.5f) // Boss 有 50% 免疫
                freeze = false;                                  // 改為不凍結
            if (freeze) defender.SetFrozenTurns(1);              // 凍結 1 回合
            defender.RemoveElementTag(ElementType.Ice);          // 清除冰
            defender.RemoveElementTag(ElementType.Water);        // 清除水
        }                                                        // else if 區塊結束
        else if (latestReactive == ElementType.Thunder)       // 冰 + 雷：超導
        {                                                        // else if 區塊開始
            defender.superconduct = true;                        // 開啟超導
            defender.RemoveElementTag(ElementType.Thunder);      // 移除雷
            defender.RemoveElementTag(ElementType.Ice);          // 移除冰
        }                                                        // else if 區塊結束
        else                                                     // 無特殊反應
        {                                                        // else 區塊開始
            defender.AddElementTag(ElementType.Ice);             // 單純附著冰
        }                                                        // else 區塊結束

        ApplyIceTagToAdjacentEnemies(defender);                  // 將冰元素標記擴散到相鄰敵人

        if (defender.superconduct)                               // 若有超導狀態
        {                                                        // if 區塊開始
            dmg += 6;                                            // 額外加 6 傷害
            defender.superconduct = false;                       // 清除狀態
        }                                                        // if 區塊結束

        return dmg;                                              // 回傳傷害
    }                                                            // 方法區塊結束

    private void ApplyIceTagToAdjacentEnemies(Enemy defender)
    {
        Board board = GameObject.FindObjectOfType<Board>();
        if (board == null) return;

        foreach (var enemy in GameObject.FindObjectsOfType<Enemy>())
        {
            if (enemy == defender) continue;
            if (Vector2Int.Distance(enemy.gridPosition, defender.gridPosition) <= 2.3f)
            {
                ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
                enemy,
                new[] { ElementType.Fire, ElementType.Water, ElementType.Thunder });

                if (latestReactive == ElementType.Thunder)
                {
                    enemy.superconduct = true;
                    enemy.RemoveElementTag(ElementType.Thunder);
                    enemy.RemoveElementTag(ElementType.Ice);
                    continue;
                }
                if (latestReactive == ElementType.Water)
                {
                    bool freeze = true;
                    if (enemy.isBoss && UnityEngine.Random.value < 0.5f)
                        freeze = false;
                    if (freeze) enemy.SetFrozenTurns(1);
                    enemy.RemoveElementTag(ElementType.Ice);
                    enemy.RemoveElementTag(ElementType.Water);
                    continue;
                }
                if (latestReactive == ElementType.Fire)
                {
                    enemy.RemoveElementTag(ElementType.Fire);
                    enemy.AddElementTag(ElementType.Ice);
                    continue;
                }
                enemy.AddElementTag(ElementType.Ice);
            }
        }
    }
}                                                                // 類別區塊結束


public class WoodStrategy : DefaultElementalStrategy             // 木元素策略
{                                                                // 類別區塊開始
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 覆寫木的傷害計算
    {                                                            // 方法區塊開始
        int dmg = baseDamage;                                    // 基礎傷害

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
        defender,
        new[] { ElementType.Fire, ElementType.Thunder });

        if (latestReactive == ElementType.Fire)               // 木 + 火：引燃木頭（燃燒）
        {                                                        // if 區塊開始
            defender.SetBurningTurns(5);                         // 設定 5 回合燃燒
            defender.RemoveElementTag(ElementType.Wood);         // 燃燒後木材被消耗
            defender.AddElementTag(ElementType.Fire);            // 附著火
        }                                                        // if 區塊結束
        else if (latestReactive == ElementType.Thunder)       // 木 + 雷：雷擊加倍狀態
        {                                                        // else if 區塊開始
            defender.thunderstrike = true;                       // 開啟雷擊加倍
            defender.RemoveElementTag(ElementType.Wood);         // 移除木
            defender.RemoveElementTag(ElementType.Thunder);      // 移除雷
        }                                                        // else if 區塊結束
        else                                                     // 沒有特殊組合
        {                                                        // else 區塊開始
            defender.AddElementTag(ElementType.Wood);            // 單純附著木
        }                                                        // else 區塊結束

        ApplyElementToTiles(defender, ElementType.Wood);         // 將木元素標記擴散到格子

        if (defender.thunderstrike)                              // 若雷擊狀態存在
        {                                                        // if 區塊開始
            dmg *= 2;                                            // 傷害加倍
            defender.thunderstrike = false;                      // 清除狀態
        }                                                        // if 區塊結束

        return dmg;                                              // 回傳傷害
    }                                                            // 方法區塊結束

     private void ApplyElementToTiles(Enemy defender, ElementType element) // 協助方法：在格子上附著元素
    {
        Board board = GameObject.FindObjectOfType<Board>();      // 尋找場上的棋盤
        if (board == null) return;                               // 若沒有棋盤則不處理

        BoardTile current = board.GetTileAt(defender.gridPosition); // 取得敵人所在格子
        if (current != null) current.AddElement(element);        // 在該格子加入元素標籤

        foreach (var adj in board.GetAdjacentTiles(defender.gridPosition)) // 迭代相鄰格子
        {
            adj.AddElement(element);                             // 相鄰格子也加入相同元素標籤
        }
    }
}                                                                // 類別區塊結束

public static class ElementalStrategyProvider                    // 提供對應元素策略的靜態工廠/查詢類
{                                                                // 類別區塊開始
    private static readonly System.Collections.Generic.Dictionary<ElementType, IElementalStrategy> map = // 映射表：元素類型 → 對應策略實例
        new System.Collections.Generic.Dictionary<ElementType, IElementalStrategy> // 建立字典實例
        {                                                    // 初始化器開始
            { ElementType.Fire, new FireStrategy() },        // 火 → FireStrategy
            { ElementType.Water, new WaterStrategy() },      // 水 → WaterStrategy
            { ElementType.Thunder, new ThunderStrategy() },  // 雷 → ThunderStrategy
            { ElementType.Ice, new IceStrategy() },          // 冰 → IceStrategy
            { ElementType.Wood, new WoodStrategy() }         // 木 → WoodStrategy
        };                                                   // 初始化器結束並以分號結束整句

    public static IElementalStrategy Get(ElementType type)        // 對外取得策略的方法
    {                                                             // 方法區塊開始
        if (map.TryGetValue(type, out var strat))                 // 嘗試從字典取得對應策略
            return strat;                                         // 若找到直接回傳
        return new DefaultElementalStrategy();                    // 若字典中沒有，回傳預設策略
    }                                                             // 方法區塊結束
}                                                                 // 類別區塊結束
