using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#region 5 �ؿ�

/// <summary>
/// 輪迴主劍 - 當玩家丟棄手牌時，每張牌可獲得1點格擋；
/// 若一次丟棄達2張或以上，則額外抽1張牌。
/// </summary>
[CreateAssetMenu(fileName = "Relic_LunHuiZhuJian", menuName = "Cards/Relic/輪迴主劍")]
public class Relic_LunHuiZhuJian : CardBase   // 此類別繼承自 CardBase，屬於「遺物」類卡牌
{
    public int blockPerDiscard = 1;   // 每棄一張牌獲得的格擋值（可在 Inspector 調整）
    public int extraDraw = 1;         // 若達條件（一次丟棄≥2），則額外抽幾張牌（預設為1）

    private void OnEnable()
    {
        cardType = CardType.Relic;    // 啟用時設定此卡牌類型為遺物（Relic）
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        // 遺物卡不在戰鬥中主動使用，因此這裡不執行任何效果
    }

    // 當玩家丟棄手牌時由 Player 觸發
    public void OnPlayerDiscard(Player player, int discardCount)
    {
        int totalBlock = discardCount * blockPerDiscard;   // 計算總格擋量（每棄一張牌增加對應格擋）
        player.AddBlock(totalBlock);                       // 為玩家增加格擋

        if (discardCount >= 2)                             // 若一次丟棄2張或以上
        {
            player.DrawCards(extraDraw);                   // 觸發額外抽牌效果
        }
    }
}

/// <summary>
/// ���_�n - �԰��}�l��1, �C�^�X�Y���X�ܤ�2�i�����A��1�ޯ�ɩ�1
/// </summary>
[CreateAssetMenu(fileName = "Relic_BaiBaoNang", menuName = "Cards/Relic/���_�n")]
public class Relic_BaiBaoNang : CardBase
{
    private void OnEnable()
    {
        cardType = CardType.Relic;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
    }

    public void OnBattleStart(Player player)
    {
        player.DrawCards(1);
    }

    /// <summary>
    /// �i�bBattleManager���ˬd�G�Y���^�X�w���X2�i�����A�A���X�ޯ�h��1
    /// </summary>
    public void OnPlayerUseSkillAfterTwoAttacks(Player player)
    {
        player.DrawCards(1);
    }
}

/// <summary>
/// ���q�� - �C����o>=6��״N��U������+2�ˮ`(�Y���>=12�A+2=+4)
/// </summary>
[CreateAssetMenu(fileName = "Relic_ZiDianJiao", menuName = "Cards/Relic/���q��")]
public class Relic_ZiDianJiao : CardBase
{
    private void OnEnable()
    {
        cardType = CardType.Relic;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
    }

    public void OnAddBlock(Player player, int blockAdded)
    {
        if (blockAdded >= 6 && blockAdded < 12)
        {
            player.buffs.nextAttackPlus += 2;
        }
        else if (blockAdded >= 12)
        {
            player.buffs.nextAttackPlus += 4;
        }
    }
}

/// <summary>
/// �\����� - �^�X�}�l�i��ܱ�1�i�P, �Y��h���^�X���ʵP�O��-1
/// </summary>
[CreateAssetMenu(fileName = "Relic_KuMuShuQian", menuName = "Cards/Relic/������")]
public class Relic_KuMuShuQian : CardBase
{
    private void OnEnable()
    {
        cardType = CardType.Relic;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
    }

    public void OnTurnStart(Player player)
    {
        // ��UI�ݪ��a "�n���n��1�i�P"?
        // �o��²��
        bool wantDiscard = true;
        if (wantDiscard && player.Hand.Count > 0)
        {
            player.DiscardOneCard();
            // ���ʥd�O��-1
            player.buffs.movementCostModify -= 1;
        }
    }
}

/// <summary>
/// �}�]­ - �C�^�X�Y�ϥ�>=3�i�����P, �^�X�����ɩ�1��1, �Y�󱼪��O�����P�h�U�^�X����+1(�ֿn)
/// </summary>
[CreateAssetMenu(fileName = "Relic_PoMoXiao", menuName = "Cards/Relic/�}�]­")]
public class Relic_PoMoXiao : CardBase
{
    private void OnEnable()
    {
        cardType = CardType.Relic;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
    }

    public void OnEndTurn(Player player)
    {
        // TODO: 重新設計觸發條件（原條件依賴「本回合攻擊卡次數」已被移除）
    }
}

#endregion
