using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class Player : MonoBehaviour
{
    [Header("基本屬性")]
    public int maxHP = 50;
    public int currentHP;
    public int maxEnergy = 4;
    // 當前能量
    public int energy;
    public int block = 0;
    public int gold = 0;

    [Header("手牌設定")]
    [Tooltip("每回合起始抽牌數量，會在 Inspector 中顯示，可直接調整。")]
    public int baseHandCardCount = 5;

    [Header("牌堆管理")]
    public List<CardBase> deck = new List<CardBase>();      // 牌庫
    private List<CardBase> hand = new List<CardBase>();     // 手牌（私有，請透過 Hand 取用）
    [System.NonSerialized] public List<CardBase> discardPile = new List<CardBase>(); // 棄牌堆（不序列化）

    public List<CardBase> relics = new List<CardBase>();  // 遺物 / 聖物清單

    [Header("回合統計")]
    public bool hasDiscardedThisTurn = false;  // 本回合是否曾經棄牌
    public int discardCountThisTurn = 0;       // 本回合累計棄了幾張
    public int attackUsedThisTurn = 0;         // 本回合使用攻擊卡次數

    // 棋盤座標（若使用 2D 格子）
    public Vector2Int position = new Vector2Int(0, 0);

    // Buff 狀態
    public PlayerBuffs buffs = new PlayerBuffs();

    // 只讀外部介面：取得手牌清單
    public List<CardBase> Hand => hand;

    private void Awake()
    {
        currentHP = maxHP;
        // 遊戲開始時將能量補滿
        energy = maxEnergy;
        // 遊戲開始時隨機洗牌
        ShuffleDeck();
    }

    /// <summary>
    /// 回合開始時的處理
    /// </summary>
    public void StartTurn()
    {
        block = 0;  // 清空格擋（若要沿用 Slay the Spire，可在此重置 block）
        // 每回合開始時回滿能量
        energy = maxEnergy;
        hasDiscardedThisTurn = false;
        discardCountThisTurn = 0;
        attackUsedThisTurn = 0;

        int initialDrawCount = Mathf.Max(0, baseHandCardCount);
        DrawCards(initialDrawCount); // 依設定的基礎手牌數量抽牌

        // 回合開始的 Buff 重置（例如 movementCostModify 歸零、damageTakenRatio 邏輯等）
        buffs.OnTurnStartReset();

        // 若有具有「回合開始觸發」的遺物，逐一觸發
        foreach (CardBase r in relics)
        {
            if (r is Relic_KuMuShuQian kk)
            {
                kk.OnTurnStart(this);
            }
        }
    }

    /// <summary>
    /// 回合結束時的處理（一般由 BattleManager 觸發）
    /// </summary>
    public void EndTurn()
    {
        // 遺物的「回合結束」觸發
        foreach (CardBase r in relics)
        {
            if (r is Relic_PoMoXiao pmx)
            {
                pmx.OnEndTurn(this, attackUsedThisTurn);
            }
        }

        // 若需要在回合結束時隨機棄牌（由 Buff 指示），此處執行
        if (buffs.needRandomDiscardAtEnd > 0)
        {
            int n = buffs.needRandomDiscardAtEnd;
            buffs.needRandomDiscardAtEnd = 0;
            BattleManager manager = FindObjectOfType<BattleManager>();
            for (int i = 0; i < n; i++)
            {
                if (TryRemoveDiscardableCardFromHand(manager, true, out CardBase c))
                {
                    discardPile.Add(c);
                    hasDiscardedThisTurn = true;
                    discardCountThisTurn++;
                }
                else
                {
                    break;
                }
            }
        }

        // 回合結束的 Buff 重置（例如將 damageTakenRatio 歸回 1.0f 等）
        buffs.OnTurnEndReset();
    }

    /// <summary>
    /// 消耗能量
    /// </summary>
    public void UseEnergy(int cost)
    {
        Debug.Log($"UseEnergy: deducting {cost} energy. Energy before={energy}");
        // 有關 nextAttackCostModify / movementCostModify 的影響已在 PlayCard 計算
        energy -= cost;
        if (energy < 0) energy = 0;
    }

    /// <summary>
    /// 計算攻擊傷害（套用 buff 的加成，並清空一次性加傷）
    /// </summary>
    public int CalculateAttackDamage(int baseDamage)
    {
        int dmg = baseDamage + buffs.nextAttackPlus + buffs.nextTurnAllAttackPlus;
        if (dmg < 0) dmg = 0;
        buffs.nextAttackPlus = 0;
        return dmg;
    }

    /// <summary>
    /// 增加格擋值
    /// </summary>
    public void AddBlock(int amount)
    {
        block += amount;
        // 觸發可能關注加格擋的遺物（例如反擊、能量、抽牌等）
        foreach (CardBase r in relics)
        {
            if (r is Relic_ZiDianJiao z)
            {
                z.OnAddBlock(this, amount);
            }
        }
    }

    /// <summary>
    /// 承受傷害（考慮 buff：damageTakenRatio、近戰減傷等）
    /// </summary>
    public void TakeDamage(int dmg)
    {
        int reduced = dmg - buffs.meleeDamageReduce;
        if (reduced < 0) reduced = 0;
        float realDmgF = reduced * buffs.damageTakenRatio;
        int realDmg = Mathf.CeilToInt(realDmgF);

        int remain = realDmg - block;
        if (remain > 0)
        {
            block = 0;
            currentHP -= remain;
            if (currentHP <= 0)
            {
                currentHP = 0;
                // TODO: Player Die 的後續處理
            }
        }
        else
        {
            block -= realDmg;
        }
    }

    /// <summary>
    /// 直接扣血（不計格擋）— 用於某些特例
    /// </summary>
    public void TakeDamageDirect(int dmg)
    {
        currentHP -= dmg;
        if (currentHP <= 0) currentHP = 0;
    }

    public void AddGold(int amount)
    {
        gold += amount;
    }

    /// <summary>
    /// 抽 n 張牌
    /// </summary>
    public void DrawCards(int n)
    {
        for (int i = 0; i < n; i++)
        {
            if (deck.Count == 0)
            {
                // 牌庫為空 → 將棄牌堆洗回牌庫
                ReshuffleDiscardIntoDeck();
                // 若仍沒有牌 → 結束抽牌
                if (deck.Count == 0) break;
            }
            CardBase top = deck[0];
            deck.RemoveAt(0);
            hand.Add(top);
        }
        FindObjectOfType<BattleManager>()?.RefreshHandUI(true);
    }

    /// <summary>
    /// 抽指定數量的新手牌（不強制刷新 UI；交由外部流程控制）
    /// </summary>
    public void DrawNewHand(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (deck.Count == 0)
            {
                if (discardPile.Count > 0)
                {
                    deck.AddRange(discardPile);
                    discardPile.Clear();
                    ShuffleDeck();
                }
                else
                {
                    break;
                }
            }

            if (deck.Count > 0)
            {
                CardBase drawn = deck[0];
                deck.RemoveAt(0);
                hand.Add(drawn);
            }
        }
    }

    /// <summary>
    /// 將棄牌堆洗回牌庫並洗牌
    /// </summary>
    public void ReshuffleDiscardIntoDeck()
    {
        // 併回
        deck.AddRange(discardPile);
        discardPile.Clear();
        // 洗牌
        ShuffleDeck();
    }

    /// <summary>
    /// 費雪耶茲洗牌
    /// </summary>
    public void ShuffleDeck()
    {
        System.Random rnd = new System.Random();
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int r = rnd.Next(0, i + 1);
            CardBase temp = deck[i];
            deck[i] = deck[r];
            deck[r] = temp;
        }
    }

    /// <summary>
    /// 從手牌移除一張「可被棄掉」的卡（可隨機 / 從尾端），並回傳該卡
    /// </summary>
    private bool TryRemoveDiscardableCardFromHand(BattleManager manager, bool randomIndex, out CardBase removedCard)
    {
        removedCard = null;
        if (hand.Count == 0) return false;

        if (manager == null)
        {
            int index = randomIndex ? Random.Range(0, hand.Count) : hand.Count - 1;
            removedCard = hand[index];
            hand.RemoveAt(index);
            return true;
        }

        if (randomIndex)
        {
            List<int> candidateIndexes = new List<int>();
            for (int i = 0; i < hand.Count; i++)
            {
                if (!manager.IsGuaranteedMovementCard(hand[i]))
                {
                    candidateIndexes.Add(i);
                }
            }

            if (candidateIndexes.Count == 0) return false;

            int selectedIndex = candidateIndexes[Random.Range(0, candidateIndexes.Count)];
            removedCard = hand[selectedIndex];
            hand.RemoveAt(selectedIndex);
            return true;
        }

        for (int i = hand.Count - 1; i >= 0; i--)
        {
            CardBase candidate = hand[i];
            if (!manager.IsGuaranteedMovementCard(candidate))
            {
                removedCard = candidate;
                hand.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 棄 n 張牌（依序處理：優先移除可棄的手牌；過程中更新計數與觸發遺物）
    /// </summary>
    public void DiscardCards(int n)
    {
        if (hand.Count < n) n = hand.Count;
        BattleManager manager = FindObjectOfType<BattleManager>();
        int actualDiscarded = 0;
        for (int i = 0; i < n; i++)
        {
            if (TryRemoveDiscardableCardFromHand(manager, false, out CardBase c))
            {
                discardPile.Add(c);
                actualDiscarded++;
            }
            else
            {
                break;
            }
        }
        if (actualDiscarded > 0)
        {
            hasDiscardedThisTurn = true;
            discardCountThisTurn += actualDiscarded;

            // 觸發與棄牌相關的遺物
            foreach (CardBase r in relics)
            {
                if (r is Relic_LunHuiZhuJian zhujian)
                {
                    zhujian.OnPlayerDiscard(this, actualDiscarded);
                }
            }
        }
    }

    /// <summary>
    /// 棄 1 張牌（常用於即時消耗）
    /// </summary>
    public bool DiscardOneCard()
    {
        BattleManager manager = FindObjectOfType<BattleManager>();
        if (!TryRemoveDiscardableCardFromHand(manager, false, out CardBase c))
            return false;
        discardPile.Add(c);

        hasDiscardedThisTurn = true;
        discardCountThisTurn++;

        // 觸發與棄牌相關的遺物
        foreach (CardBase r in relics)
        {
            if (r is Relic_LunHuiZhuJian zhujian)
            {
                zhujian.OnPlayerDiscard(this, 1);
            }
        }

        return true;
    }

    /// <summary>
    /// 查詢本回合是否有棄牌（供其他效果判斷）
    /// </summary>
    public bool CheckDiscardPlan()
    {
        return hasDiscardedThisTurn;
    }

    /// <summary>
    /// 以棋盤格為目標移動（若有 2D 棋盤）
    /// </summary>
    public void MoveToPosition(Vector2Int targetGridPos)
    {
        // 1. 取得棋盤管理器
        Board board = FindObjectOfType<Board>();
        if (board == null)
        {
            Debug.LogWarning("Board not found!");
            return;
        }

        // 2. 檢查是否有敵人佔據該格
        if (board.IsTileOccupied(targetGridPos))
        {
            Debug.Log("Cannot move: tile occupied by enemy.");
            return;
        }

        // 3. 更新邏輯座標
        position = targetGridPos;

        // 4. 拿到這個格子的 BoardTile
        BoardTile tile = board.GetTileAt(targetGridPos);
        if (tile == null)
        {
            Debug.LogWarning($"No tile at {targetGridPos}");
            return;
        }

        // 5. 將玩家的世界座標設成該格子的 transform.position
        transform.position = tile.transform.position;
    }

    /// <summary>
    /// 瞬移（無視路徑檢查，但仍可檢查目標是否被佔據）
    /// </summary>
    public void TeleportToPosition(Vector2Int targetPos)
    {
        Board board = FindObjectOfType<Board>();
        if (board != null && board.IsTileOccupied(targetPos))
        {
            Debug.Log("Cannot teleport: tile occupied by enemy.");
            return;
        }

        position = targetPos;
        transform.position = new Vector3(targetPos.x, targetPos.y, 0f);
    }
}

/// <summary>
/// 玩家身上的 Buff 與回合邏輯
/// </summary>
[System.Serializable]
public class PlayerBuffs
{
    public float damageTakenRatio = 1.0f;   // 承傷比例（例如易傷 = 1.5、減傷 = 0.5）
    public int nextAttackPlus = 0;          // 下一次攻擊額外加成
    public int nextDamageTakenUp = 0;       // 下一次承傷 +X（也可能用在 Enemy 身上）
    public int nextAttackCostModify = 0;    // 下一張攻擊卡費用修正
    public int movementCostModify = 0;      // 移動卡費用修正
    public int nextTurnDrawChange = 0;      // 下回合抽牌數量增減
    public int needRandomDiscardAtEnd = 0;  // 回合結束時需要隨機棄牌的張數
    public int meleeDamageReduce = 0;       // 近戰傷害固定減免
    public int weak = 0;                    // 虛弱回合數
    public int stun = 0;                    // 暈眩回合數（無法行動）
    public int nextTurnAllAttackPlus = 0;   // 下回合所有攻擊 +X

    /// <summary>
    /// 回合開始時的重置與遞減
    /// </summary>
    public void OnTurnStartReset()
    {
        // 例如：回合開始前，將「下回合全攻 +X」在這時生效完後再視需要重置
        // damageTakenRatio 是否需要在此回復為 1.0f，依你的設計調整

        if (stun > 0) stun--;   // 暈眩回合數往下扣
        if (weak > 0) weak--;   // 虛弱回合數往下扣

        // 將「本回合內」的費用修正歸零
        movementCostModify = 0;
        nextAttackCostModify = 0;
    }

    /// <summary>
    /// 回合結束時的重置
    /// </summary>
    public void OnTurnEndReset()
    {
        // 回復承傷比例為 1.0f（若只在本回合有效）
        damageTakenRatio = 1.0f;
        // nextAttackPlus 依你的流程決定是否在此清除（此程式在 CalculateAttackDamage 時已清 0）
        // needRandomDiscardAtEnd 已在 EndTurn 中用掉，這裡不動即可
    }
}
