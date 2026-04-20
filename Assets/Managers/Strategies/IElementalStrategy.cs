using System.Collections;                     // 提供非泛型集合型別，例如 ArrayList 與 Hashtable。
using System.Collections.Generic;             // 提供泛型集合型別，例如 List<T> 與 Dictionary<TKey, TValue>。
using UnityEngine;                            // 提供 Unity API，例如 Mathf 與 Vector2Int。

// 依附著順序取得可觸發反應的元素。
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

// 從目前戰鬥環境取得棋盤與敵人快照。
internal static class BattleQueryResolver
{
    public static Board ResolveBoard()
    {
        BattleRuntimeContext context = BattleRuntimeContext.Active;
        if (context != null && context.Board != null)
        {
            return context.Board;
        }

        return null;
    }

    public static Enemy[] ResolveEnemies()
    {
        BattleRuntimeContext context = BattleRuntimeContext.Active;
        if (context != null && context.Enemies != null)
        {
            IReadOnlyList<Enemy> enemies = context.Enemies;
            Enemy[] snapshot = new Enemy[enemies.Count];
            for (int i = 0; i < enemies.Count; i++)
            {
                snapshot[i] = enemies[i];
            }

            return snapshot;
        }

        return new Enemy[0];
    }
}

// 元素傷害計算策略介面。
public interface IElementalStrategy
{
    int CalculateDamage(Player attacker, Enemy defender, int baseDamage); // 根據攻擊者、目標與基礎傷害計算本次實際傷害。
}

// 玩家回合結束時觸發的附加效果介面。
public interface IPlayerEndTurnEffect
{
    void OnPlayerEndTurn(Enemy enemy);         // 玩家回合結束時，對指定敵人執行效果。
}

// 預設元素策略，不改變傷害值。
public class DefaultElementalStrategy : IElementalStrategy
{
    public virtual int CalculateDamage(Player attacker, Enemy defender, int baseDamage)
    {
        return baseDamage;
    }
}

// 火屬性策略。
public class FireStrategy : DefaultElementalStrategy
{
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage)
    {
        int dmg = baseDamage;

        // 任何火屬性攻擊命中時，都會先解除凍結狀態。
        if (defender != null && defender.frozenTurns > 0)
        {
            defender.SetFrozenTurns(0);
        }

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
            defender,
            new[] { ElementType.Water, ElementType.Ice, ElementType.Wood, ElementType.Thunder });

        if (latestReactive == ElementType.Water)              // 火 + 水：蒸發，造成 1.5 倍傷害並轉為火附著。
        {
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);
            defender.RemoveElementTag(ElementType.Water);
            defender.AddElementTag(ElementType.Fire);
        }
        else if (latestReactive == ElementType.Ice)           // 火 + 冰：融解，造成 1.5 倍傷害並轉為火附著。
        {
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);
            defender.RemoveElementTag(ElementType.Ice);
            defender.AddElementTag(ElementType.Fire);
        }
        else if (latestReactive == ElementType.Wood)          // 火 + 木：點燃 5 回合。
        {
            defender.SetBurningTurns(5);
            defender.RemoveElementTag(ElementType.Wood);
            defender.AddElementTag(ElementType.Fire);
        }
        else if (latestReactive == ElementType.Thunder)       // 火 + 雷：爆散，對附近敵人造成 50% 傷害。
        {
            ElementType keep = ElementType.Fire;
            ElementType remove = ElementType.Thunder;
            Board board = BattleQueryResolver.ResolveBoard();
            if (board != null)
            {
                foreach (var en in BattleQueryResolver.ResolveEnemies())
                {
                    if (en == defender) continue;
                    if (Vector2Int.Distance(en.gridPosition, defender.gridPosition) <= 2.3f)
                    {
                        int spreadDmg = Mathf.CeilToInt(baseDamage * 0.5f);
                        if (spreadDmg > 0) en.TakeDamage(spreadDmg);
                    }
                }
            }
            defender.RemoveElementTag(remove);
            defender.AddElementTag(keep);
        }
        else                                                  // 其餘情況：單純附著火元素。
        {
            defender.AddElementTag(ElementType.Fire);
        }

        return dmg;
    }

    private void ApplyFireSpreadReaction(Enemy enemy, ref int dmg)
    {
        if (enemy != null && enemy.frozenTurns > 0)
        {
            enemy.SetFrozenTurns(0);
        }

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
}

// 水屬性策略。
public class WaterStrategy : DefaultElementalStrategy
{
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage)
    {
        int dmg = baseDamage;

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
            defender,
            new[] { ElementType.Fire, ElementType.Ice });

        if (latestReactive == ElementType.Fire)               // 水 + 火：蒸發，1.5 倍傷害，清除燃燒並轉為水附著。
        {
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);
            defender.RemoveElementTag(ElementType.Fire);
            defender.SetBurningTurns(0);
            defender.AddElementTag(ElementType.Water);
        }
        else if (latestReactive == ElementType.Ice)           // 水 + 冰：嘗試凍結 1 回合，Boss 有 50% 機率抵抗。
        {
            bool freeze = true;
            if (defender.isBoss && UnityEngine.Random.value < 0.5f)
                freeze = false;
            if (freeze) defender.SetFrozenTurns(1);
            attacker?.NotifyFreezeReactionResolved(defender, freeze);
            defender.RemoveElementTag(ElementType.Ice);
            defender.RemoveElementTag(ElementType.Water);
        }
        else                                                  // 其餘情況：單純附著水元素。
        {
            defender.AddElementTag(ElementType.Water);
        }

        ApplyElementToTiles(defender, ElementType.Water);     // 把水元素擴散到目標腳下與相鄰地格。

        return dmg;
    }

    private void ApplyElementToTiles(Enemy defender, ElementType element) // 將元素套用到目標所在格與鄰近格。
    {
        Board board = BattleQueryResolver.ResolveBoard();
        if (board == null) return;

        BoardTile current = board.GetTileAt(defender.gridPosition);
        if (current != null) current.AddElement(element);

        foreach (var adj in board.GetAdjacentTiles(defender.gridPosition))
        {
            adj.AddElement(element);
        }
    }
}

// 雷屬性策略。
public class ThunderStrategy : DefaultElementalStrategy
{
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage)
    {
        Board board = BattleQueryResolver.ResolveBoard();
        Enemy[] allEnemies = BattleQueryResolver.ResolveEnemies();
        int dmg = ResolveThunderHit(attacker, defender, baseDamage, HitContext.Direct, board, allEnemies);

        return dmg;
    }

    private enum HitContext
    {
        Direct,
        Chain
    }

    private int ResolveThunderHit(Player attacker, Enemy defender, int baseDamage, HitContext context, Board board, Enemy[] allEnemies)
    {
        int dmg = baseDamage;
        bool isChain = context == HitContext.Chain;

        if (isChain)                                           // 連鎖命中時先移除水附著，避免無限傳導。
            defender.RemoveElementTag(ElementType.Water);

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
            defender,
            new[] { ElementType.Fire, ElementType.Water, ElementType.Ice, ElementType.Wood });
        bool defenderOnWaterTile = false;                      // 只在直接命中時檢查腳下是否有水地板。

        if (board != null && !isChain)
        {
            BoardTile defenderTile = board.GetTileAt(defender.gridPosition);
            defenderOnWaterTile = defenderTile != null && defenderTile.HasElement(ElementType.Water);
        }

        if (latestReactive == ElementType.Fire)               // 雷 + 火：爆散，對附近敵人造成 50% 傷害。
        {
            ElementType keep = ElementType.Thunder;
            ElementType remove = ElementType.Fire;

            if (board != null)
            {
                foreach (var en in allEnemies)
                {
                    if (en == defender) continue;
                    if (Vector2Int.Distance(en.gridPosition, defender.gridPosition) <= 2.3f)
                    {
                        int spreadDmg = Mathf.CeilToInt(baseDamage * 0.5f);
                        if (spreadDmg > 0) en.TakeDamage(spreadDmg);
                    }
                }
            }
            defender.RemoveElementTag(remove);
            defender.AddElementTag(keep);
        }
        else if (latestReactive == ElementType.Water || (latestReactive == null && defenderOnWaterTile)) // 雷 + 水：沿著水地板與水附著單位傳導。
        {
            float conductiveMultiplier = attacker != null ? attacker.GetConductiveDamageMultiplier() : 0.5f;
            int conductiveDamage = Mathf.CeilToInt(baseDamage * conductiveMultiplier);
            if (board != null)
            {
                var enemyByPos = new Dictionary<Vector2Int, Enemy>();
                foreach (var en in allEnemies)
                {
                    if (!enemyByPos.ContainsKey(en.gridPosition))
                        enemyByPos[en.gridPosition] = en;
                }

                Queue<Vector2Int> pending = new Queue<Vector2Int>();       // BFS 走訪所有連通的水地格。
                HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
                HashSet<Enemy> chainTargets = new HashSet<Enemy>();        // 避免同一敵人被重複加入連鎖目標。

                pending.Enqueue(defender.gridPosition);
                visited.Add(defender.gridPosition);

                while (pending.Count > 0)
                {
                    Vector2Int current = pending.Dequeue();
                    var neighbors = board.GetAdjacentTiles(current);
                    foreach (var tile in neighbors)
                    {
                        Vector2Int pos = tile.gridPosition;

                        enemyByPos.TryGetValue(pos, out Enemy occupant);
                        bool tileHasWater = tile.HasElement(ElementType.Water);
                        bool enemyHasWater = occupant != null && occupant.HasElement(ElementType.Water);

                        if (!tileHasWater && !enemyHasWater)
                            continue;

                        if (!visited.Add(pos)) continue;
                        pending.Enqueue(pos);

                        if (occupant != null && occupant != defender)
                            chainTargets.Add(occupant);
                    }
                }

                foreach (var target in chainTargets)
                {
                    ResolveThunderHit(attacker, target, conductiveDamage, HitContext.Chain, board, allEnemies);
                }
            }
            else
            {
                foreach (var en in allEnemies)
                {
                    if (en == defender) continue;
                    bool adjacent = Vector2Int.Distance(en.gridPosition, defender.gridPosition) <= 2.3f;
                    if (adjacent && en.HasElement(ElementType.Water))
                    {
                        en.TakeDamage(conductiveDamage);
                        en.RemoveElementTag(ElementType.Water);
                    }
                }
            }
            defender.RemoveElementTag(ElementType.Water);
            defender.AddElementTag(ElementType.Thunder);
        }
        else if (TryApplyThunderReactionWithoutWater(defender, false, null, ref dmg))
        {
            // 冰、木的非導電反應已在 helper 內處理。
        }
        else
        {
            defender.AddElementTag(ElementType.Thunder);
        }

        if (defender.superconduct)                             // 觸發超導後，額外造成 6 點傷害並清除狀態。
        {
            dmg += 6;
            defender.superconduct = false;
        }

        if (isChain && dmg > 0)                                // 連鎖命中在這裡實際扣血。
            defender.TakeDamage(dmg);

        return dmg;
    }

    private void ApplyThunderSpreadReaction(Enemy enemy, ref int dmg)
    {
        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
            enemy,
            new[] { ElementType.Fire, ElementType.Water, ElementType.Ice, ElementType.Wood });

        if (latestReactive == ElementType.Water)
        {
            enemy.RemoveElementTag(ElementType.Water);
            bool reacted = TryApplyThunderReactionWithoutWater(enemy, false, null, ref dmg);
            if (!reacted) enemy.AddElementTag(ElementType.Thunder);
            return;
        }
        if (latestReactive == ElementType.Fire)
        {
            enemy.RemoveElementTag(ElementType.Fire);
            enemy.AddElementTag(ElementType.Thunder);
            return;
        }
        if (TryApplyThunderReactionWithoutWater(enemy, false, null, ref dmg))
        {
            return;
        }

        if (dmg == 0) return;

        enemy.AddElementTag(ElementType.Thunder);
    }

    /// <summary>
    /// 處理雷元素在沒有水參與時，與冰或木的反應。
    /// </summary>
    /// <param name="target">要處理反應的目標敵人。</param>
    /// <param name="zeroDamageOnReact">若反應成功，是否將本次傷害歸零。</param>
    /// <param name="latestReactive">可選，外部已解析出的最近可反應元素；為 null 時會自行查詢。</param>
    /// <param name="dmg">以參考方式傳入的傷害值，必要時會被修改。</param>
    /// <returns>若成功觸發反應則回傳 true。</returns>
    private static bool TryApplyThunderReactionWithoutWater(Enemy target, bool zeroDamageOnReact, ElementType? latestReactive, ref int dmg)
    {
        ElementType? reactive = latestReactive ?? ElementReactionOrderHelper.GetLatestReactiveTag(
            target,
            new[] { ElementType.Ice, ElementType.Wood });

        if (reactive == ElementType.Ice)                       // 雷 + 冰：超導，下次再受元素傷害時額外 +6。
        {
            target.superconduct = true;
            target.RemoveElementTag(ElementType.Thunder);
            target.RemoveElementTag(ElementType.Ice);
            if (zeroDamageOnReact) dmg = 0;
            return true;
        }

        if (reactive == ElementType.Wood)                      // 雷 + 木：激化，累積 2 層充能。
        {
            target.RemoveElementTag(ElementType.Wood);
            target.RemoveElementTag(ElementType.Thunder);
            target.SetChargedCount(2);
            if (zeroDamageOnReact) dmg = 0;
            return true;
        }

        return false;
    }
}

// 冰屬性策略。
public class IceStrategy : DefaultElementalStrategy
{
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage)
    {
        int dmg = baseDamage;

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
            defender,
            new[] { ElementType.Fire, ElementType.Water, ElementType.Thunder, ElementType.Wood });

        if (latestReactive == ElementType.Fire)               // 冰 + 火：融解，造成 1.5 倍傷害並留下冰附著。
        {
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);
            defender.RemoveElementTag(ElementType.Fire);
            defender.SetBurningTurns(0);
            defender.AddElementTag(ElementType.Ice);
        }
        else if (latestReactive == ElementType.Water)         // 冰 + 水：嘗試凍結 1 回合，Boss 有 50% 機率抵抗。
        {
            bool freeze = true;
            if (defender.isBoss && UnityEngine.Random.value < 0.5f)
                freeze = false;
            if (freeze) defender.SetFrozenTurns(1);
            attacker?.NotifyFreezeReactionResolved(defender, freeze);
            defender.RemoveElementTag(ElementType.Ice);
            defender.RemoveElementTag(ElementType.Water);
        }
        else if (latestReactive == ElementType.Thunder)       // 冰 + 雷：觸發超導。
        {
            defender.superconduct = true;
            defender.RemoveElementTag(ElementType.Thunder);
            defender.RemoveElementTag(ElementType.Ice);
        }
        else if (latestReactive == ElementType.Wood)          // 冰 + 木：疊加 1 層霜蝕。
        {
            defender.AddFrostStacks(1);
            defender.RemoveElementTag(ElementType.Wood);
            defender.RemoveElementTag(ElementType.Ice);
        }
        else
        {
            defender.AddElementTag(ElementType.Ice);
        }

        ApplyIceTagToAdjacentEnemies(attacker, defender);      // 冰附著會擴散到鄰近敵人並立即處理反應。

        if (defender.superconduct)
        {
            dmg += 6;
            defender.superconduct = false;
        }

        return dmg;
    }

    private void ApplyIceTagToAdjacentEnemies(Player attacker, Enemy defender)
    {
        Board board = BattleQueryResolver.ResolveBoard();
        if (board == null) return;

        foreach (var enemy in BattleQueryResolver.ResolveEnemies())
        {
            if (enemy == defender) continue;
            if (Vector2Int.Distance(enemy.gridPosition, defender.gridPosition) <= 2.3f)
            {
                ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
                    enemy,
                    new[] { ElementType.Fire, ElementType.Water, ElementType.Thunder, ElementType.Wood });

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
                    attacker?.NotifyFreezeReactionResolved(enemy, freeze);
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
                if (latestReactive == ElementType.Wood)
                {
                    enemy.AddFrostStacks(1);
                    enemy.RemoveElementTag(ElementType.Wood);
                    enemy.RemoveElementTag(ElementType.Ice);
                    continue;
                }
                enemy.AddElementTag(ElementType.Ice);
            }
        }
    }
}

// 木屬性策略。
public class WoodStrategy : DefaultElementalStrategy
{
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage)
    {
        int dmg = baseDamage;

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
            defender,
            new[] { ElementType.Fire, ElementType.Thunder, ElementType.Ice });

        if (latestReactive == ElementType.Fire)               // 木 + 火：燃燒 5 回合。
        {
            defender.SetBurningTurns(5);
            defender.RemoveElementTag(ElementType.Wood);
            defender.AddElementTag(ElementType.Fire);
        }
        else if (latestReactive == ElementType.Thunder)       // 木 + 雷：激化，累積 2 層充能。
        {
            defender.RemoveElementTag(ElementType.Wood);
            defender.RemoveElementTag(ElementType.Thunder);
            defender.SetChargedCount(2);
        }
        else if (latestReactive == ElementType.Ice)           // 木 + 冰：疊加 1 層霜蝕。
        {
            defender.AddFrostStacks(1);
            defender.RemoveElementTag(ElementType.Wood);
            defender.RemoveElementTag(ElementType.Ice);
        }
        else
        {
            defender.AddElementTag(ElementType.Wood);
        }

        ApplyElementToTiles(defender, ElementType.Wood);      // 把木元素擴散到目標腳下與相鄰地格。

        return dmg;
    }

    private void ApplyElementToTiles(Enemy defender, ElementType element) // 將元素套用到目標所在格與鄰近格。
    {
        Board board = BattleQueryResolver.ResolveBoard();
        if (board == null) return;

        BoardTile current = board.GetTileAt(defender.gridPosition);
        if (current != null) current.AddElement(element);

        foreach (var adj in board.GetAdjacentTiles(defender.gridPosition))
        {
            adj.AddElement(element);
        }
    }
}

// 依元素型別提供對應的策略實例。
public static class ElementalStrategyProvider
{
    private static readonly System.Collections.Generic.Dictionary<ElementType, IElementalStrategy> map =
        new System.Collections.Generic.Dictionary<ElementType, IElementalStrategy>
        {
            { ElementType.Fire, new FireStrategy() },
            { ElementType.Water, new WaterStrategy() },
            { ElementType.Thunder, new ThunderStrategy() },
            { ElementType.Ice, new IceStrategy() },
            { ElementType.Wood, new WoodStrategy() }
        };

    public static IElementalStrategy Get(ElementType type)
    {
        if (map.TryGetValue(type, out var strat))
            return strat;
        return new DefaultElementalStrategy();
    }
}
