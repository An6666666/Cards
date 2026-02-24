using System.Collections;                     // 撘??????征??憒?ArrayList?ashtable嚗???祆?獢?湔雿輻嚗?撣貉???Unity 蝭
using System.Collections.Generic;             // 撘瘜????賢?蝛粹?嚗? List<T>?ictionary<TKey,TValue>嚗?
using UnityEngine;                            // 撘 Unity ?敹?API嚗? MonoBehaviour?ameObject?athf 蝑?

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
public interface IElementalStrategy            // 摰????蝑隞嚗?蝢拇???蝝?蝞摰單?敹?撖虫??瘜?
{                                              // 隞?憛?憪?
    int CalculateDamage(Player attacker, Enemy defender, int baseDamage); // 閮??瑕拿?瘜?頛詨?餅??◤?餅????箇??瑕拿嚗??喳祕?摰喳?
}                                              // 隞?憛???
public interface IPlayerEndTurnEffect          // 摰???拙振??蝯???隞嚗?鈭?蝝??摰嗅?????????????
{                                              // 隞?憛?憪?
    void OnPlayerEndTurn(Enemy enemy);         // ?函摰嗅??????澆嚗靘???蝥抒???憒??銵嚗?
}                                              // 隞?憛???

public class DefaultElementalStrategy : IElementalStrategy // ?身??蝑撖虫?嚗??遙雿????寞?嚗蝝??喳??瑕拿
{                                              // 憿?憛?憪?
    public virtual int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // virtual ?迂摮??亥?撖?
    {                                          // ?寞??憛?憪?
        return baseDamage;                     // ?湔??箇??瑕拿嚗??遙雿???
    }                                          // ?寞??憛???
}                                              // 憿?憛???

public class FireStrategy : DefaultElementalStrategy // ?怠?蝝??伐?蝜潭?身蝑
{                                              // 憿?憛?憪?
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 閬神?瑕拿閮?嚗???畾?????
    {                                          // ?寞??憛?憪?
        int dmg = baseDamage;                  // ?誑?箇??瑕拿?箄絲暺?銋?靘???靽格迤

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
        defender,
        new[] { ElementType.Water, ElementType.Ice, ElementType.Wood, ElementType.Thunder });

        if (latestReactive == ElementType.Water)              // ?仿摰澈銝?瘞游?蝝?
        {                                                        // if ?憛?憪?
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);            // ?恍?瘞湛????瑕拿 1.5 ?蒂?⊥?隞園脖?
            defender.RemoveElementTag(ElementType.Water);        // 蝘駁瘞湔?閮?
            defender.AddElementTag(ElementType.Fire);            // ?啣??急?閮?銵函內鋡怎??嚗?
        }                                                        // if ?憛???
        else if (latestReactive == ElementType.Ice)           // ?血??交??啣?蝝?
        {                                                        // else if ?憛?憪?
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);            // ?怨??堆??見 1.5 ?摰?
            defender.RemoveElementTag(ElementType.Ice);          // 蝘駁?唳?閮?
            defender.AddElementTag(ElementType.Fire);            // ???急?閮?
        }                                                        // else if ?憛???
        else if (latestReactive == ElementType.Wood)          // ?交??典?蝝?
        {                                                        // else if ?憛?憪?
            defender.SetBurningTurns(5);                         // 暺??券嚗身摰???蝥?5 ??
            defender.RemoveElementTag(ElementType.Wood);         // ??敺?◤瘨?
            defender.AddElementTag(ElementType.Fire);            // ??敺??????
        }                                                        // else if ?憛???
        else if (latestReactive == ElementType.Thunder)       // ?交??瑕?蝝?
        {                                                        // else if ?憛?憪?
            ElementType keep = ElementType.Fire;                 // ??敺?????嚗
            ElementType remove = ElementType.Thunder;            // ??敺?蝘駁??蝝???
            Board board = BattleQueryResolver.ResolveBoard();  // ?典?臭葉撠 Board 撖虫?嚗????澆?蝞∠?嚗?
            if (board != null)                                   // ?交????Board
            {                                                    // if ?憛?憪?
                foreach (var en in BattleQueryResolver.ResolveEnemies()) // 餈游??風?湔銝剜???Enemy
                {                                                // foreach ?憛?憪?
                    if (en == defender) continue;                // 頝喲??祇?嚗????芸楛嚗?
                    if (Vector2Int.Distance(en.gridPosition, defender.gridPosition) <= 2.3f) // ?亥??Ｗ??潛???2.3嚗??箇?堆?
                    {                                            // if ?憛?憪?
                        int spreadDmg = Mathf.CeilToInt(baseDamage * 0.5f); // ?賊?萎犖? 0.5 ?蝷摰?
                        if (spreadDmg > 0) en.TakeDamage(spreadDmg); // ???蝯摰?
                    }                                            // if ?憛???
                }                                                // foreach ?憛???
            }                                                    // if ?憛???
            defender.RemoveElementTag(remove);                   // 敺擃宏?日??璅?
            defender.AddElementTag(keep);                        // 蝯行擃?銝??璅?
        }                                                        // else if ?憛???
        else                                                     // ?交??遙雿摰??蝝?
        {                                                        // else ?憛?憪?
            defender.AddElementTag(ElementType.Fire);            // ?桃????怠?蝝?
        }                                                        // else ?憛???

        return dmg;                                              // ??蝯摰?
    }                                                            // ?寞??憛???

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
}                                                                // 憿?憛???

public class WaterStrategy : DefaultElementalStrategy            // 瘞游?蝝??伐??芾?撖怠摰喉?瘝???????
{                                                                // 憿?憛?憪?
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 閬神瘞游?蝝??瑕拿閮?
    {                                                            // ?寞??憛?憪?
        int dmg = baseDamage;                                    // ?箇??瑕拿韏琿?

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
        defender,
        new[] { ElementType.Fire, ElementType.Ice });

        if (latestReactive == ElementType.Fire)               // 瘞游??恬??亙??寞???
        {                                                        // if ?憛?憪?
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);            // ?瑕拿 1.5 ??
            defender.RemoveElementTag(ElementType.Fire);         // 蝘駁?急?閮?
            defender.SetBurningTurns(0);                         // ?瘞游摰單?皜??
            defender.AddElementTag(ElementType.Water);           // ??瘞湔?閮?
        }                                                        // if ?憛???
        else if (latestReactive == ElementType.Ice)           // 瘞?+ ?堆????文?
        {                                                        // else if ?憛?憪?
            bool freeze = true;                                  // ?身??蝯?
            if (defender.isBoss && UnityEngine.Random.value < 0.5f) // ?交 Boss嚗? 50% ???嚗璈?
                freeze = false;                                  // 閮剖?銝?蝯?
            if (freeze) defender.SetFrozenTurns(1);              // ?亥???嚗身摰?蝯?1 ??
            defender.RemoveElementTag(ElementType.Ice);          // 皜?唳?閮?
            defender.RemoveElementTag(ElementType.Water);        // 皜瘞湔?閮???敺??瘨仃嚗?
        }                                                        // else if ?憛???
        else                                                     // ?園???嚗?舫??偌
        {                                                        // else ?憛?憪?
            defender.AddElementTag(ElementType.Water);           // ??瘞湔?閮?
        }                                                        // else ?憛???

        ApplyElementToTiles(defender, ElementType.Water);        // 撠偌??璅??湔?唳摮?

        return dmg;                                              // ??蝯摰?
    }                                                            // ?寞??憛???

    private void ApplyElementToTiles(Enemy defender, ElementType element) // ??寞?嚗?澆?銝???蝝?
    {
        Board board = BattleQueryResolver.ResolveBoard();      // 撠?港?????
        if (board == null) return;                               // ?交????文?銝???

        BoardTile current = board.GetTileAt(defender.gridPosition); // ???萎犖??冽摮?
        if (current != null) current.AddElement(element);        // ?刻府?澆????璅惜

        foreach (var adj in board.GetAdjacentTiles(defender.gridPosition)) // 餈凋誨?賊?澆?
        {
            adj.AddElement(element);                             // ?賊?澆?銋??亦??蝝?蝐?
        }
    }
}                                                                // 憿?憛???

public class ThunderStrategy : DefaultElementalStrategy          // ?瑕?蝝??伐?憭車????撠???
{                                                                // 憿?憛?憪?
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 閬神?瑕?蝝摰唾?蝞?
    {                                                            // ?寞??憛?憪?
        Board board = BattleQueryResolver.ResolveBoard();      // 敹怠??港?????
        Enemy[] allEnemies = BattleQueryResolver.ResolveEnemies(); // 敹怠???鈭綽??踹?????
        int dmg = ResolveThunderHit(attacker, defender, baseDamage, HitContext.Direct, board, allEnemies); // ?梁?摩

        return dmg;                                              // ??蝯摰?
    }                                                            // ?寞??憛???

    private enum HitContext
    {
        Direct,
        Chain
    }

    private int ResolveThunderHit(Player attacker, Enemy defender, int baseDamage, HitContext context, Board board, Enemy[] allEnemies)
    {
        int dmg = baseDamage;                                    // ?箇??瑕拿
        bool isChain = context == HitContext.Chain;              // ?臬?粹???賭葉

        if (isChain)                                             // ????賭葉?宏?斗偌嚗??BFS 憟?BFS
        defender.RemoveElementTag(ElementType.Water);        // 撠敺宏?斗偌嚗?????

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
        defender,
        new[] { ElementType.Fire, ElementType.Water, ElementType.Ice, ElementType.Wood });
        bool defenderOnWaterTile = false;                        // ?脣??銝??澆??臬撣嗆?瘞游?蝝?

        if (board != null && !isChain)                           // ?閬??方?閮??賣炎?交摮?蝝?
        {
            BoardTile defenderTile = board.GetTileAt(defender.gridPosition); // ???嗅??澆?
            defenderOnWaterTile = defenderTile != null && defenderTile.HasElement(ElementType.Water); // ?斗?澆??臬撣嗆偌
        }

        if (latestReactive == ElementType.Fire)               // ??+ ?恬??湔?啣???憿撮嚗???鈭斗?銝?嚗?
        {                                                        // if ?憛?憪?
            ElementType keep = ElementType.Thunder;              // 靽???
            ElementType remove = ElementType.Fire;               // 蝘駁??
            
            if (board != null)                                   // ?交??
            {                                                    // if ?憛?憪?
                foreach (var en in allEnemies)                   // 餈凋誨??鈭?
                {                                                // foreach ?憛?憪?
                    if (en == defender) continue;                // 頝喲??芸楛
                    if (Vector2Int.Distance(en.gridPosition, defender.gridPosition) <= 2.3f) // ?賊?文?
                    {                                            // if ?憛?憪?
                        int spreadDmg = Mathf.CeilToInt(baseDamage * 0.5f); // ?賊??0.5 ??
                        if (spreadDmg > 0) en.TakeDamage(spreadDmg);
                    }                                            // if ?憛???
                }                                                // foreach ?憛???
            }                                                    // if ?憛???
            defender.RemoveElementTag(remove);                   // 蝘駁?祇???
            defender.AddElementTag(keep);                        // ?祇??
        }                                                        // if ?憛???
        else if (latestReactive == ElementType.Water || (latestReactive == null && defenderOnWaterTile))     // ??+ 瘞湛?撠?湔嚗?瑟鈭箸?血葆瘞湔?蝡瘞湔
        {                                                        // else if ?憛?憪?
            if (board != null)                                   // ?閬??方?閮??質蕭頩斗偌?????
            {                                                    // if ?憛?憪?
                var enemyByPos = new Dictionary<Vector2Int, Enemy>(); // 撱箇?摨扳??唳鈭箇???
                foreach (var en in allEnemies)                   // 餈凋誨??鈭?
                {                                                // foreach ?憛?憪?
                    if (!enemyByPos.ContainsKey(en.gridPosition)) // ?踹?????漣璅?
                        enemyByPos[en.gridPosition] = en;        // 撱箇???
                }                                                // foreach ?憛???

                Queue<Vector2Int> pending = new Queue<Vector2Int>(); // 雿?嚗????摮漣璅?
                HashSet<Vector2Int> visited = new HashSet<Vector2Int>(); // 蝝?歇?????澆?
                HashSet<Enemy> chainTargets = new HashSet<Enemy>(); // ?閬????瑕拿?鈭?

                pending.Enqueue(defender.gridPosition);          // 隞亥◤?餅????冽摮韏琿?
                visited.Add(defender.gridPosition);              // 璅?韏琿?撌脫?閮?

                while (pending.Count > 0)                        // BFS ??????偌???澆?
                {                                                // while ?憛?憪?
                    Vector2Int current = pending.Dequeue();      // ??桀????摮?
                    var neighbors = board.GetAdjacentTiles(current); // ???賊?澆?
                    foreach (var tile in neighbors)              // 瑼Ｘ瘥撅?
                    {                                            // foreach ?憛?憪?
                        Vector2Int pos = tile.gridPosition;      // ?啣?摨扳?

                        enemyByPos.TryGetValue(pos, out Enemy occupant); // 閰西??曉閰脫摮??萎犖
                        bool tileHasWater = tile.HasElement(ElementType.Water); // ?澆??臬?偌璅惜
                        bool enemyHasWater = occupant != null && occupant.HasElement(ElementType.Water); // ?萎犖?臬?偌璅惜

                        if (!tileHasWater && !enemyHasWater)     // ?交摮??萎犖?賣??偌璅惜
                            continue;                            // 銝脣???

                        if (!visited.Add(pos)) continue;         // 撌脰???頝喲?
                        pending.Enqueue(pos);                    // ?敺?????蝜潛??湔

                        if (occupant != null && occupant != defender) // ?芸??嗡??萎犖???瑕拿
                            chainTargets.Add(occupant);          // ?????瑕拿?格?
                    }                                            // foreach ?憛???
                }                                                // while ?憛???

                foreach (var target in chainTargets)             // 撠?????格????瑕拿
                {                                                // foreach ?憛?憪?
                    ResolveThunderHit(attacker, target, baseDamage, HitContext.Chain, board, allEnemies);
                }                                                // foreach ?憛???
            }                                                    // if ?憛???
            else                                                 // ?亙銝璉鞈?
            {                                                    // else ?憛?憪?
                foreach (var en in allEnemies)                   // ????摩嚗????賊銝葆瘞渡??萎犖
                {                                                // foreach ?憛?憪?
                    if (en == defender) continue;                // 頝喲??芸楛
                    bool adjacent = Vector2Int.Distance(en.gridPosition, defender.gridPosition) <= 2.3f; // ?臬?賊
                    if (adjacent && en.HasElement(ElementType.Water)) // ??萎犖頨思??偌璅惜
                    {
                        en.TakeDamage(baseDamage);               // ???箇??瑕拿
                        en.RemoveElementTag(ElementType.Water);  // 撠敺宏?斗偌嚗?????
                    }
                }                                                // foreach ?憛???
            }                                                    // else ?憛???
            defender.RemoveElementTag(ElementType.Water);         // 撠蝯?敺??斗偌璅?
            defender.AddElementTag(ElementType.Thunder); // 隞亙??餌??銝鳴??交??隞???????
        }                                                        // else if ?憛???
        else if (TryApplyThunderReactionWithoutWater(defender, false, null, ref dmg)) // ?炎?亙/?函???靘??圈???摨?
        {                                                        // else if ?憛?憪?
            // ????撌脣 helper ?批???
        }                                                        // else if ?憛???
        else                                                     // 瘝??孵??
        {                                                        // else ?憛?憪?
            defender.AddElementTag(ElementType.Thunder);         // ?桃?????
        }                                                        // else ?憛???
        
        if (defender.superconduct)                               // ?亥?撠?????
        {                                                        // if ?憛?憪?
            dmg += 6;                                            // 憿???6 暺摰摰?
            defender.superconduct = false;                       // 皜頞????
        }                                                        // if ?憛???

        if (isChain && dmg > 0)                                  // ????賭葉??交銵
        defender.TakeDamage(dmg);                            // ??蝑??箇??潛??瑕拿

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
            bool reacted = TryApplyThunderReactionWithoutWater(enemy, false, null, ref dmg); // 瘞游??餃?瑼Ｘ????
            if (!reacted) enemy.AddElementTag(ElementType.Thunder); // ?∪???????
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
    /// ???瑕?蝝??偌??嚗?嚗?蝯?????
    /// </summary>
    /// <param name="target">閬????格??萎犖??/param>
    /// <param name="zeroDamageOnReact">?亦?????臬撠?摰單飛?嗚?/param>
    /// <param name="latestReactive">憭撌脰?蝞末???啣????嚗??null ?甇方?蝞?/param>
    /// <param name="dmg">?喳?摰喳??剁?閬?瘜?質◤?身??/param>
    /// <returns>?交??潛???????true??/returns>
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
            target.RemoveElementTag(ElementType.Wood);
            target.RemoveElementTag(ElementType.Thunder);
            target.SetChargedCount(2);
            if (zeroDamageOnReact) dmg = 0;
            return true;
        }

        return false;
    }
}                                                                // 憿?憛???

public class IceStrategy : DefaultElementalStrategy              // ?啣?蝝???
{                                                                // 憿?憛?憪?
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 閬神?啁??瑕拿閮?
    {                                                            // ?寞??憛?憪?
        int dmg = baseDamage;                                    // ?箇??瑕拿

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
        defender,
        new[] { ElementType.Fire, ElementType.Water, ElementType.Thunder, ElementType.Wood });

        if (latestReactive == ElementType.Fire)               // ??+ ?恬?鈭?嚗祕雿 1.5 ?蒂閬??恬?
        {                                                        // if ?憛?憪?
            dmg = Mathf.CeilToInt(baseDamage * 1.5f);            // 1.5 ?摰?
            defender.RemoveElementTag(ElementType.Fire);         // 蝘駁??
            defender.SetBurningTurns(0);                         // ??啣摰單?皜??
            defender.AddElementTag(ElementType.Ice);             // ????
        }                                                        // if ?憛???
        else if (latestReactive == ElementType.Water)         // ??+ 瘞湛???璈??文?
        {                                                        // else if ?憛?憪?
            bool freeze = true;                                  // ?身??
            if (defender.isBoss && UnityEngine.Random.value < 0.5f) // Boss ??50% ?
                freeze = false;                                  // ?寧銝?蝯?
            if (freeze) defender.SetFrozenTurns(1);              // ?? 1 ??
            defender.RemoveElementTag(ElementType.Ice);          // 皜??
            defender.RemoveElementTag(ElementType.Water);        // 皜瘞?
        }                                                        // else if ?憛???
        else if (latestReactive == ElementType.Thunder)       // ??+ ?瘀?頞?
        {                                                        // else if ?憛?憪?
            defender.superconduct = true;                        // ??頞?
            defender.RemoveElementTag(ElementType.Thunder);      // 蝘駁??
            defender.RemoveElementTag(ElementType.Ice);          // 蝘駁??
        }                                                        // else if ?憛???
        else if (latestReactive == ElementType.Wood)          // ??+ ?剁?蝯?
        {                                                        // else if ?憛?憪?
            defender.AddFrostStacks(1);                          // ??蝯?撅斗
            defender.RemoveElementTag(ElementType.Wood);         // 蝘駁??
            defender.RemoveElementTag(ElementType.Ice);          // 蝘駁??
        }                                                        // else if ?憛???
        else                                                     // ?∠畾???
        {                                                        // else ?憛?憪?
            defender.AddElementTag(ElementType.Ice);             // ?桃?????
        }                                                        // else ?憛???

        ApplyIceTagToAdjacentEnemies(defender);                  // 撠??璅??湔?啁?唳鈭?

        if (defender.superconduct)                               // ?交?頞????
        {                                                        // if ?憛?憪?
            dmg += 6;                                            // 憿???6 ?瑕拿
            defender.superconduct = false;                       // 皜???
        }                                                        // if ?憛???

        return dmg;                                              // ??瑕拿
    }                                                            // ?寞??憛???

    private void ApplyIceTagToAdjacentEnemies(Enemy defender)
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
}                                                                // 憿?憛???


public class WoodStrategy : DefaultElementalStrategy             // ?典?蝝???
{                                                                // 憿?憛?憪?
    public override int CalculateDamage(Player attacker, Enemy defender, int baseDamage) // 閬神?函??瑕拿閮?
    {                                                            // ?寞??憛?憪?
        int dmg = baseDamage;                                    // ?箇??瑕拿

        ElementType? latestReactive = ElementReactionOrderHelper.GetLatestReactiveTag(
        defender,
        new[] { ElementType.Fire, ElementType.Thunder, ElementType.Ice });

        if (latestReactive == ElementType.Fire)               // ??+ ?恬?撘??券嚗???
        {                                                        // if ?憛?憪?
            defender.SetBurningTurns(5);                         // 閮剖? 5 ????
            defender.RemoveElementTag(ElementType.Wood);         // ??敺?◤瘨?
            defender.AddElementTag(ElementType.Fire);            // ??敺??????
        }                                                        // if ?憛???
        else if (latestReactive == ElementType.Thunder)       // ??+ ?瘀??????
        {                                                        // else if ?憛?憪?
            defender.RemoveElementTag(ElementType.Wood);         // 蝘駁??
            defender.RemoveElementTag(ElementType.Thunder);      // 蝘駁??
            defender.SetChargedCount(2);                         // 閮剖???甈⊥
        }                                                        // else if ?憛???
        else if (latestReactive == ElementType.Ice)           // ??+ ?堆?蝯?
        {                                                        // else if ?憛?憪?
            defender.AddFrostStacks(1);                          // ??蝯?撅斗
            defender.RemoveElementTag(ElementType.Wood);         // 蝘駁??
            defender.RemoveElementTag(ElementType.Ice);          // 蝘駁??
        }                                                        // else if ?憛???
        else                                                     // 瘝??寞?蝯?
        {                                                        // else ?憛?憪?
            defender.AddElementTag(ElementType.Wood);            // ?桃?????
        }                                                        // else ?憛???

        ApplyElementToTiles(defender, ElementType.Wood);         // 撠??璅??湔?唳摮?

        return dmg;                                              // ??瑕拿
    }                                                            // ?寞??憛???

     private void ApplyElementToTiles(Enemy defender, ElementType element) // ??寞?嚗?澆?銝???蝝?
    {
        Board board = BattleQueryResolver.ResolveBoard();      // 撠?港?????
        if (board == null) return;                               // ?交????文?銝???

        BoardTile current = board.GetTileAt(defender.gridPosition); // ???萎犖??冽摮?
        if (current != null) current.AddElement(element);        // ?刻府?澆????璅惜

        foreach (var adj in board.GetAdjacentTiles(defender.gridPosition)) // 餈凋誨?賊?澆?
        {
            adj.AddElement(element);                             // ?賊?澆?銋??亦??蝝?蝐?
        }
    }
}                                                                // 憿?憛???

public static class ElementalStrategyProvider                    // ??撠???蝑???極撱??亥岷憿?
{                                                                // 憿?憛?憪?
    private static readonly System.Collections.Generic.Dictionary<ElementType, IElementalStrategy> map = // ??銵剁???憿? ??撠?蝑撖虫?
        new System.Collections.Generic.Dictionary<ElementType, IElementalStrategy> // 撱箇?摮撖虫?
        {                                                    // ?????
            { ElementType.Fire, new FireStrategy() },        // ????FireStrategy
            { ElementType.Water, new WaterStrategy() },      // 瘞???WaterStrategy
            { ElementType.Thunder, new ThunderStrategy() },  // ????ThunderStrategy
            { ElementType.Ice, new IceStrategy() },          // ????IceStrategy
            { ElementType.Wood, new WoodStrategy() }         // ????WoodStrategy
        };                                                   // ???蝯?銝虫誑??蝯??游

    public static IElementalStrategy Get(ElementType type)        // 撠???蝑?瘜?
    {                                                             // ?寞??憛?憪?
        if (map.TryGetValue(type, out var strat))                 // ?岫敺??詨?敺?????
            return strat;                                         // ?交?啁?亙???
        return new DefaultElementalStrategy();                    // ?亙??訾葉瘝?嚗??喲?閮剔???
    }                                                             // ?寞??憛???
}                                                                 // 憿?憛???


