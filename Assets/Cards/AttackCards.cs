using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#region P1-P4:基礎攻擊卡

/// <summary>
/// 振迅（若本回合有防禦/有格擋，則追加傷害）
/// </summary>
[CreateAssetMenu(fileName = "Attack_ZhenXun", menuName = "Cards/Attack/振迅")]
public class Attack_ZhenXun : CardBase
{
    [Header("數值設定")]
    public int baseDamage = 10;
    public int bonusDamage = 4;

    private void OnEnable()
    {
        cardType = CardType.Attack;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        // 判定玩家本回合是否使用過防禦（此處以 block>0 作為簡化判斷）
        bool usedDefense = (player.block > 0);
        int totalDamage = baseDamage;
        if (usedDefense)
        {
            totalDamage += bonusDamage;
        }

        int dmg = player.CalculateAttackDamage(totalDamage);
        enemy.TakeDamage(dmg);
    }
}

#endregion

#region P5-P10:進階攻擊卡

/// <summary>
/// 燃勁斬（先造成一次傷害；若能成功棄 1 張牌，再追加一次同等傷害）
/// </summary>
[CreateAssetMenu(fileName = "Attack_RanJinZhan", menuName = "Cards/Attack/燃勁斬")]
public class Attack_RanJinZhan : CardBase
{
    [Header("數值設定")]
    public int damage = 5;

    private void OnEnable()
    {
        cardType = CardType.Attack;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        int dmg1 = player.CalculateAttackDamage(damage);
        enemy.TakeDamage(dmg1);

        // 嘗試棄 1 張牌；成功則再打一次
        bool hasDiscard = player.DiscardOneCard();
        if (hasDiscard)
        {
            int dmg2 = player.CalculateAttackDamage(damage);
            enemy.TakeDamage(dmg2);
        }
    }
}

/// <summary>
/// 碎甲衝擊（先削減目標格擋，再造成傷害）
/// </summary>
[CreateAssetMenu(fileName = "Attack_SuiJiaChongJi", menuName = "Cards/Attack/碎甲衝擊")]
public class Attack_SuiJiaChongJi : CardBase
{
    [Header("數值設定")]
    public int damage = 8;
    [Tooltip("先削減目標的 Block 數值")]
    public int reduceBlock = 5;

    private void OnEnable()
    {
        cardType = CardType.Attack;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        // 先降低目標的格擋
        enemy.ReduceBlock(reduceBlock);
        // 再進行傷害結算
        int dmg = player.CalculateAttackDamage(damage);
        enemy.TakeDamage(dmg);
    }
}

/// <summary>
/// 盾擊（若目前 Block ≥ 門檻，則額外加傷）
/// </summary>
[CreateAssetMenu(fileName = "Attack_DunJi", menuName = "Cards/Attack/盾擊")]
public class Attack_DunJi : CardBase
{
    [Header("數值設定")]
    public int baseDamage = 4;
    public int blockThreshold = 6;
    public int bonusDamage = 4;

    private void OnEnable()
    {
        cardType = CardType.Attack;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        int totalDamage = baseDamage;
        if (player.block >= blockThreshold)
        {
            totalDamage += bonusDamage;
        }
        int dmg = player.CalculateAttackDamage(totalDamage);
        enemy.TakeDamage(dmg);
    }
}

/// <summary>
/// 亂流手裡劍（若無消耗 → 固定傷害；若有消耗 → 依消耗數×每次傷害）
/// </summary>
[CreateAssetMenu(fileName = "Attack_LuanLiuShuriken", menuName = "Cards/Attack/亂流手裡劍")]
public class Attack_LuanLiuShuriken : CardBase
{
    [Header("數值設定")]
    public int baseDamagePerDiscard = 2;
    [Tooltip("當回合沒有任何棄牌時造成的基礎傷害")]
    public int baseDamageIfNoDiscard = 2;

    private void OnEnable()
    {
        cardType = CardType.Attack;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        // exhaustCountThisTurn：本回合卡片被消耗的次數
        int exhaustCount = player.exhaustCountThisTurn;
        if (exhaustCount < 0) exhaustCount = 0;

        int totalDamage = 0;
        if (exhaustCount == 0)
        {
            // 無棄牌 → 固定傷害
            totalDamage = baseDamageIfNoDiscard;
        }
        else
        {
            // 有棄牌 → 次數 × 每次傷害（若計算結果 ≤0，退回固定傷害）
            totalDamage = exhaustCount * baseDamagePerDiscard;
            if (totalDamage <= 0) totalDamage = baseDamageIfNoDiscard;
        }

        int dmg = player.CalculateAttackDamage(totalDamage);
        enemy.TakeDamage(dmg);
    }
}

/// <summary>
/// 騙術突襲（造成傷害 → 抽 1 棄 1；若棄掉的是技能牌，則對同一目標追加傷害）
/// </summary>
[CreateAssetMenu(fileName = "Attack_PianShuTuXi", menuName = "Cards/Attack/騙術突襲")]
public class Attack_PianShuTuXi : CardBase
{
    [Header("數值設定")]
    public int baseDamage = 9;
    public int bonusDamage = 3;

    private void OnEnable()
    {
        cardType = CardType.Attack;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        // 先造成一次傷害
        int dmg = player.CalculateAttackDamage(baseDamage);
        enemy.TakeDamage(dmg);

        // 抽 1
        player.DrawCards(1);

        // 棄 1（盡量避開「保證位移」類的移動牌）
        CardBase lastCard = null;
        if (player.Hand.Count > 0)
        {
            BattleManager manager = FindObjectOfType<BattleManager>();
            for (int i = player.Hand.Count - 1; i >= 0; i--)
            {
                CardBase candidate = player.Hand[i];
                if (manager != null && manager.IsGuaranteedMovementCard(candidate))
                {
                    continue; // 跳過不應被丟棄的保底移動牌
                }

                lastCard = candidate;
                player.Hand.RemoveAt(i);
                player.discardPile.Add(lastCard);
                break;
            }
        }

        // 若棄掉的是技能牌 → 對同一目標追加傷害
        if (lastCard != null && lastCard.cardType == CardType.Skill)
        {
            int bonus = player.CalculateAttackDamage(bonusDamage);
            enemy.TakeDamage(bonus);
        }
    }
}

#endregion
