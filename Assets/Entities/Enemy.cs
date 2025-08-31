using System.Collections;                          // 引用非泛型集合命名空間
using System.Collections.Generic;                 // 引用泛型集合命名空間
using UnityEngine;                                 // 引用 Unity 核心功能

public class Enemy : MonoBehaviour              // 敵人角色，繼承自 MonoBehaviour
{
    public string enemyName = "Slime";           // 敵人名稱，預設為 Slime
    public int maxHP = 30;                         // 最大生命值
    public int currentHP;                          // 當前生命值
    public int block = 0;                          // 格擋值，用於抵消傷害

    public int burningTurns = 0;

    public int frozenTurns = 0;

    public bool thunderstrike = false;
    public bool superconduct = false;

    public bool hasBerserk = false;                // 是否處於狂暴狀態
    public EnemyBuffs buffs = new EnemyBuffs();    // 敵人 Buff 結構
    public Vector2Int gridPosition;                // 在格子地圖中的座標

     [Header("攻擊範圍偏移")]
    public List<Vector2Int> attackRangeOffsets = new List<Vector2Int>
    {
        new Vector2Int(-2,0), new Vector2Int(2,0),
        new Vector2Int(-1,-2), new Vector2Int(1,-2),
        new Vector2Int(-1,2), new Vector2Int(1,2)
    };

    public bool isBoss = false;                    // 是否為首領級敵人

    private HashSet<ElementType> elementTags = new HashSet<ElementType>();  // 元素標籤

        // 移動到指定格子
    public void MoveToPosition(Vector2Int targetGridPos)
    {
        Board board = FindObjectOfType<Board>();
        if (board == null) return;
        BoardTile tile = board.GetTileAt(targetGridPos);
        if (tile == null) return;
        if (board.IsTileOccupied(targetGridPos)) return;
        Player p = FindObjectOfType<Player>();
        if (p != null && p.position == targetGridPos) return;

        gridPosition = targetGridPos;
        transform.position = tile.transform.position;
    }

    private bool IsPlayerInRange(Player player)
    {
        foreach (var off in attackRangeOffsets)
        {
            if (gridPosition + off == player.position) return true;
        }
        return false;
    }

    private void MoveOneStepTowards(Player player)
    {
        Board board = FindObjectOfType<Board>();
        if (board == null) return;
        var adjs = board.GetAdjacentTiles(gridPosition);
        Vector2Int bestPos = gridPosition;
        float bestDist = Vector2Int.Distance(gridPosition, player.position);
        foreach (var t in adjs)
        {
            Vector2Int pos = t.gridPosition;
            if (board.IsTileOccupied(pos)) continue;
            if (player.position == pos) continue;
            float d = Vector2Int.Distance(pos, player.position);
            if (d < bestDist)
            {
                bestDist = d;
                bestPos = pos;
            }
        }
        if (bestPos != gridPosition)
            MoveToPosition(bestPos);
    }
    private void Awake()                           // Awake 在物件建立時呼叫
    {
        currentHP = maxHP;                         // 同步當前生命值為最大值
    }

    [SerializeField] private GameObject highlightFx;  // 高亮特效物件

    public void SetHighlight(bool on)               // 控制高亮顯示
    {
        if (highlightFx) highlightFx.SetActive(on);
    }

    private void OnMouseDown()                     // 滑鼠點擊時呼叫
    {
        BattleManager bm = FindObjectOfType<BattleManager>();  // 找到 BattleManager
        bm.OnEnemyClicked(this);                   // 通知 BattleManager 有敵人被點擊
    }

    public void TakeDamage(int dmg)                // 受到傷害 (考慮格擋)
    {
        int remain = dmg - block;                 // 計算剩餘傷害
        if (remain > 0)
        {
            block = 0;                            // 格擋用完歸零
            currentHP -= remain;                  // 扣除剩餘傷害
            if (currentHP <= 0)
            {
                currentHP = 0;                   // 生命不低於 0
                Die();                           // 生命歸零觸發死亡
            }
        }
        else
        {
            block -= dmg;                         // 僅扣除格擋
        }
    }

    public void TakeTrueDamage(int dmg)           // 真實傷害 (無視格擋)
    {
        currentHP -= dmg;                         // 直接扣除生命
        if (currentHP <= 0)
        {
            currentHP = 0;
            Die();                                // 扣到 0 則死亡
        }
    }

    public void AddBlock(int amount)              // 增加格擋值
    {
        block += amount;
    }

    public void ReduceBlock(int amount)           // 減少格擋值
    {
        block -= amount;
        if (block < 0) block = 0;                // 格擋不低於 0
    }

    public void DispelBuff(int count)             // 清除指定數量的 Buff
    {
        buffs.ClearSomeBuff(count);
    }

    public bool HasElement(ElementType e)          // 檢查是否有指定元素標籤
    {
        return elementTags.Contains(e);
    }

    public void AddElementTag(ElementType e)      // 添加元素標籤
    {
        elementTags.Add(e);
    }

    public void RemoveElementTag(ElementType e)   // 移除元素標籤
    {
        elementTags.Remove(e);
    }

    public int ApplyElementalAttack(ElementType e, int baseDamage, Player player)
    {
        var strat = ElementalStrategyProvider.Get(e);
        return strat.CalculateDamage(player, this, baseDamage);
    }

    public void ProcessTurnStart()
    {
        var tagsCopy = new List<ElementType>(elementTags);
        foreach (var tag in tagsCopy)
        {
            var strat = ElementalStrategyProvider.Get(tag);

            // 如果該策略有持續效果的介面實作
            if (strat is IStartOfTurnEffect effect)
            {
                effect.OnStartOfTurn(this);  // 執行持續效果
            }
        }

        // 成長陷阱效果照舊
        Board board = FindObjectOfType<Board>();
        if (board != null)
        {
            var tile = board.GetTileAt(gridPosition);
            tile?.TriggerGrowthTrap(this);
        }
    }



    public void EnemyAction(Player player)        // 敵人執行動作
    {
        if (frozenTurns > 0)                     // 冰凍回合中不能行動
        {
            frozenTurns--;
            return;
        }
        if (buffs.stun > 0)                       // 暈眩回合中不能行動
        {
            buffs.stun--;
            return;
        }
        if (IsPlayerInRange(player))
        {
            int atkValue = 10;                    // 基礎攻擊
            if (hasBerserk) atkValue += 5;       // 狂暴狀態加攻擊
            player.TakeDamage(atkValue);         // 對玩家造成傷害
        }
        else
        {
            MoveOneStepTowards(player);           // 移動一格接近玩家
        }
    }

    void Die()                                    // 死亡處理
    {
        Debug.Log(enemyName + " died!");
         BattleManager bm = FindObjectOfType<BattleManager>();
        if (bm != null)
        {
            bm.OnEnemyDefeated(this);
        }
        Destroy(gameObject);                     // 刪除自身
    }
}

[System.Serializable]
public class EnemyBuffs                        // 敵人 Buff 結構
{
    public int stun = 0;                         // 暈眩回合數
    public void ClearSomeBuff(int count)         // 清除指定層數的 Buff
    {
        stun = Mathf.Max(0, stun - count);
    }
}