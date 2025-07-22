using System.Collections;                     // 引用非泛型集合命名空間（如 ArrayList、Hashtable），雖然本檔案未直接使用，但常見於 Unity 範本
using System.Collections.Generic;             // 引用泛型集合命名空間（如 List<T>、Dictionary<TKey,TValue>）
using UnityEngine;                            // 引用 Unity 的核心 API（如 MonoBehaviour、GameObject、Mathf 等）

public interface IElementalStrategy            // 宣告元素策略介面：定義所有元素計算傷害時必須實作的方法
{                                              // 介面區塊開始
    int CalculateDamage(Player attacker, Enemy defender, int baseDamage); // 計算傷害的方法：輸入攻擊者、被攻擊者與基礎傷害，回傳實際傷害值
}                                              // 介面區塊結束
public interface IStartOfTurnEffect            // 宣告回合開始效果介面：某些元素有「回合開始時」要處理的效果
{                                              // 介面區塊開始
    void OnStartOfTurn(Enemy enemy);           // 在敵人回合開始時呼叫，用來處理持續性狀態（如燃燒扣血）
}                                              // 介面區塊結束

public class DefaultElementalStrategy : IElementalStrategy // 預設元素策略實作：不做任何加成或特效，單純回傳原傷害
{                                              // 類別區塊開始
    public virtual int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // virtual 允許子類別覆寫
    {                                          // 方法區塊開始
        return baseDamage;                     // 直接回傳基礎傷害，沒有任何變動
    }                                          // 方法區塊結束
}                                              // 類別區塊結束

public class FireStrategy : DefaultElementalStrategy, IStartOfTurnEffect // 火元素策略：繼承預設策略並實作回合開始效果
{                                              // 類別區塊開始
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 覆寫傷害計算：火元素的特殊反應處理
    {                                          // 方法區塊開始
        int dmg = baseDamage;                  // 先以基礎傷害為起點，之後依反應再修正

        if (defender.HasElement(ElementType.Water))              // 若防守者身上有水元素
        {                                                        // if 區塊開始
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);            // 火遇水：提高傷害 1.5 倍並無條件進位
            defender.RemoveElementTag(ElementType.Water);        // 移除水標記
            defender.AddElementTag(ElementType.Fire);            // 新增火標記（表示被火附著）
        }                                                        // if 區塊結束
        else if (defender.HasElement(ElementType.Ice))           // 否則若有冰元素
        {                                                        // else if 區塊開始
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);            // 火融冰：同樣 1.5 倍傷害
            defender.RemoveElementTag(ElementType.Ice);          // 移除冰標記
            defender.AddElementTag(ElementType.Fire);            // 加上火標記
        }                                                        // else if 區塊結束
        else if (defender.HasElement(ElementType.Wood))          // 若有木元素
        {                                                        // else if 區塊開始
            defender.burningTurns = 5;                           // 點燃木頭：設定燃燒持續 5 回合
            defender.AddElementTag(ElementType.Fire);            // 加上火標記（燃燒來源）
            defender.AddElementTag(ElementType.Wood);            // 保留木標記（表示木仍存在，被火影響）
        }                                                        // else if 區塊結束
        else if (defender.HasElement(ElementType.Thunder))       // 若有雷元素
        {                                                        // else if 區塊開始
            ElementType keep = ElementType.Fire;                 // 反應後保留的元素：火
            ElementType remove = ElementType.Thunder;            // 反應後要移除的元素：雷
            Board board = GameObject.FindObjectOfType<Board>();  // 在場景中尋找 Board 實例（棋盤/格子管理）
            if (board != null)                                   // 若成功找到 Board
            {                                                    // if 區塊開始
                foreach (var en in GameObject.FindObjectsOfType<Enemy>()) // 迴圈遍歷場景中所有 Enemy
                {                                                // foreach 區塊開始
                    if (en == defender) continue;                // 跳過本體（不處理自己）
                    if (Vector2Int.Distance(en.gridPosition, defender.gridPosition) <= 1.1f) // 若距離小於等於 1.1（視為相鄰）
                    {                                            // if 區塊開始
                        en.TakeDamage(Mathf.CeilToInt(baseDamage * 0.5f)); // 相鄰敵人受到 0.5 倍基礎傷害
                        en.AddElementTag(keep);                  // 附加火元素標記到相鄰敵人
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
    public void OnStartOfTurn(Enemy enemy)                       // 回合開始效果：火的燃燒 DOT（持續傷害）
    {                                                            // 方法區塊開始
        if (enemy.burningTurns > 0)                              // 若仍有燃燒回合數
        {                                                        // if 區塊開始
            enemy.TakeDamage(2);                                 // 每回合固定扣 2 點傷害
            enemy.burningTurns--;                                // 燃燒回合數 -1
            if (enemy.burningTurns == 0)                         // 若燃燒剛好結束
            {                                                    // if 區塊開始
                enemy.RemoveElementTag(ElementType.Fire);        // 移除火標記
                enemy.RemoveElementTag(ElementType.Wood);        // 同時移除木標記（因為燃燒結束，木不再維持燃燒狀態）
            }                                                    // if 區塊結束
        }                                                        // if 區塊結束
    }                                                            // 方法區塊結束
}                                                                // 類別區塊結束

public class WaterStrategy : DefaultElementalStrategy            // 水元素策略：只覆寫傷害，沒有回合開始效果
{                                                                // 類別區塊開始
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 覆寫水元素的傷害計算
    {                                                            // 方法區塊開始
        int dmg = baseDamage;                                    // 基礎傷害起點

        if (defender.HasElement(ElementType.Fire))               // 水剋火：若對方有火
        {                                                        // if 區塊開始
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);            // 傷害 1.5 倍
            defender.RemoveElementTag(ElementType.Fire);         // 移除火標記
            defender.AddElementTag(ElementType.Water);           // 加上水標記
        }                                                        // if 區塊結束
        else if (defender.HasElement(ElementType.Ice))           // 水 + 冰：凍結判定
        {                                                        // else if 區塊開始
            bool freeze = true;                                  // 預設會凍結
            if (defender.isBoss && UnityEngine.Random.value < 0.5f) // 若是 Boss，有 50% 免疫凍結（隨機）
                freeze = false;                                  // 設定不凍結
            if (freeze) defender.frozenTurns = 1;                // 若要凍結，設定凍結 1 回合
            defender.RemoveElementTag(ElementType.Ice);          // 清除冰標記
            defender.RemoveElementTag(ElementType.Water);        // 清除水標記（反應後兩者皆消失）
        }                                                        // else if 區塊結束
        else                                                     // 其餘情況：只是附著水
        {                                                        // else 區塊開始
            defender.AddElementTag(ElementType.Water);           // 加上水標記
        }                                                        // else 區塊結束

        return dmg;                                              // 回傳最終傷害
    }                                                            // 方法區塊結束
}                                                                // 類別區塊結束

public class ThunderStrategy : DefaultElementalStrategy          // 雷元素策略：多種連鎖、傳導反應
{                                                                // 類別區塊開始
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 覆寫雷元素傷害計算
    {                                                            // 方法區塊開始
        int dmg = baseDamage;                                    // 基礎傷害

        if (defender.HasElement(ElementType.Fire))               // 雷 + 火：擴散到周圍（與火類似，但元素交換不同）
        {                                                        // if 區塊開始
            ElementType keep = ElementType.Thunder;              // 保留雷
            ElementType remove = ElementType.Fire;               // 移除火
            Board board = GameObject.FindObjectOfType<Board>();  // 找 Board
            if (board != null)                                   // 若找到
            {                                                    // if 區塊開始
                foreach (var en in GameObject.FindObjectsOfType<Enemy>()) // 迭代所有敵人
                {                                                // foreach 區塊開始
                    if (en == defender) continue;                // 跳過自己
                    if (Vector2Int.Distance(en.gridPosition, defender.gridPosition) <= 1.1f) // 相鄰判定
                    {                                            // if 區塊開始
                        en.TakeDamage(Mathf.CeilToInt(baseDamage * 0.5f)); // 相鄰扣 0.5 倍
                        en.AddElementTag(keep);                  // 相鄰附著雷元素
                    }                                            // if 區塊結束
                }                                                // foreach 區塊結束
            }                                                    // if 區塊結束
            defender.RemoveElementTag(remove);                   // 移除本體火
            defender.AddElementTag(keep);                        // 本體加雷
        }                                                        // if 區塊結束
        else if (defender.HasElement(ElementType.Water))         // 雷 + 水：導電擴散，會看相鄰或處於水格
        {                                                        // else if 區塊開始
            foreach (var en in GameObject.FindObjectsOfType<Enemy>()) // 檢查所有敵人
            {                                                    // foreach 區塊開始
                if (en == defender) continue;                    // 跳過自己
                bool adjacent = Vector2Int.Distance(en.gridPosition, defender.gridPosition) <= 1.1f; // 是否相鄰
                bool valid = false;                              // 是否有效導電對象
                if (adjacent && en.HasElement(ElementType.Water)) valid = true; // 相鄰且身上有水 ⇒ 有效
                if (!valid)                                      // 若還無效，檢查地板是否有水
                {                                                // if 區塊開始
                    Board board = GameObject.FindObjectOfType<Board>(); // 找 Board
                    if (board != null)                           // 找到才判斷
                    {                                            // if 區塊開始
                        BoardTile tile = board.GetTileAt(en.gridPosition); // 取得該敵人在的格子
                        if (tile != null && tile.HasElement(ElementType.Water)) valid = true; // 格子有水 ⇒ 有效
                    }                                            // if 區塊結束
                }                                                // if 區塊結束
                if (valid)                                       // 若有效導電
                {                                                // if 區塊開始
                    en.TakeDamage(baseDamage);                   // 給與完整基礎傷害（非 0.5 倍）
                }                                                // if 區塊結束
            }                                                    // foreach 區塊結束
            defender.AddElementTag(ElementType.Thunder);         // 本體附著雷
        }                                                        // else if 區塊結束
        else if (defender.HasElement(ElementType.Wood))          // 雷 + 木：觸發雷擊 double 狀態
        {                                                        // else if 區塊開始
            defender.thunderstrike = true;                       // 設定雷擊加倍旗標
            defender.RemoveElementTag(ElementType.Wood);         // 移除木
            defender.RemoveElementTag(ElementType.Thunder);      // 移除雷（反應後消失）
        }                                                        // else if 區塊結束
        else if (defender.HasElement(ElementType.Ice))           // 雷 + 冰：觸發超導（增加固定傷害）
        {                                                        // else if 區塊開始
            defender.superconduct = true;                        // 打開超導旗標
            defender.RemoveElementTag(ElementType.Thunder);      // 移除雷
            defender.RemoveElementTag(ElementType.Ice);          // 移除冰
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
}                                                                // 類別區塊結束

public class IceStrategy : DefaultElementalStrategy              // 冰元素策略
{                                                                // 類別區塊開始
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 覆寫冰的傷害計算
    {                                                            // 方法區塊開始
        int dmg = baseDamage;                                    // 基礎傷害

        if (defender.HasElement(ElementType.Fire))               // 冰 + 火：互剋（實作為 1.5 倍並覆蓋火）
        {                                                        // if 區塊開始
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);            // 1.5 倍傷害
            defender.RemoveElementTag(ElementType.Fire);         // 移除火
            defender.AddElementTag(ElementType.Ice);             // 附著冰
        }                                                        // if 區塊結束
        else if (defender.HasElement(ElementType.Water))         // 冰 + 水：凍結機率判定
        {                                                        // else if 區塊開始
            bool freeze = true;                                  // 預設凍結
            if (defender.isBoss && UnityEngine.Random.value < 0.5f) // Boss 有 50% 免疫
                freeze = false;                                  // 改為不凍結
            if (freeze) defender.frozenTurns = 1;                // 凍結 1 回合
            defender.RemoveElementTag(ElementType.Ice);          // 清除冰
            defender.RemoveElementTag(ElementType.Water);        // 清除水
        }                                                        // else if 區塊結束
        else if (defender.HasElement(ElementType.Thunder))       // 冰 + 雷：超導
        {                                                        // else if 區塊開始
            defender.superconduct = true;                        // 開啟超導
            defender.RemoveElementTag(ElementType.Thunder);      // 移除雷
            defender.RemoveElementTag(ElementType.Ice);          // 移除冰
        }                                                        // else if 區塊結束
        else                                                     // 無特殊反應
        {                                                        // else 區塊開始
            defender.AddElementTag(ElementType.Ice);             // 單純附著冰
        }                                                        // else 區塊結束

        if (defender.superconduct)                               // 若有超導狀態
        {                                                        // if 區塊開始
            dmg += 6;                                            // 額外加 6 傷害
            defender.superconduct = false;                       // 清除狀態
        }                                                        // if 區塊結束

        return dmg;                                              // 回傳傷害
    }                                                            // 方法區塊結束
}                                                                // 類別區塊結束

public class WoodStrategy : DefaultElementalStrategy             // 木元素策略
{                                                                // 類別區塊開始
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 覆寫木的傷害計算
    {                                                            // 方法區塊開始
        int dmg = baseDamage;                                    // 基礎傷害

        if (defender.HasElement(ElementType.Fire))               // 木 + 火：引燃木頭（燃燒）
        {                                                        // if 區塊開始
            defender.burningTurns = 5;                           // 設定 5 回合燃燒
            defender.AddElementTag(ElementType.Fire);            // 附著火
            defender.AddElementTag(ElementType.Wood);            // 保留木（表示燃燒木頭）
        }                                                        // if 區塊結束
        else if (defender.HasElement(ElementType.Thunder))       // 木 + 雷：雷擊加倍狀態
        {                                                        // else if 區塊開始
            defender.thunderstrike = true;                       // 開啟雷擊加倍
            defender.RemoveElementTag(ElementType.Wood);         // 移除木
            defender.RemoveElementTag(ElementType.Thunder);      // 移除雷
        }                                                        // else if 區塊結束
        else                                                     // 沒有特殊組合
        {                                                        // else 區塊開始
            defender.AddElementTag(ElementType.Wood);            // 單純附著木
        }                                                        // else 區塊結束

        if (defender.thunderstrike)                              // 若雷擊狀態存在
        {                                                        // if 區塊開始
            dmg *= 2;                                            // 傷害加倍
            defender.thunderstrike = false;                      // 清除狀態
        }                                                        // if 區塊結束

        return dmg;                                              // 回傳傷害
    }                                                            // 方法區塊結束
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
