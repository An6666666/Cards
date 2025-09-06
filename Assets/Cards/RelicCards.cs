using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#region 5 �ؿ�

/// <summary>
/// ���j��² - ��P����o���, �@����2�i�H�W�ɦA��1
/// </summary>
[CreateAssetMenu(fileName = "Relic_LunHuiZhuJian", menuName = "Cards/Relic/���j��²")]
public class Relic_LunHuiZhuJian : CardBase
{
    public int blockPerDiscard = 1;
    public int extraDraw = 1;

    private void OnEnable()
    {
        cardType = CardType.Relic;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        // ���|�D�ʳQ���X
    }

    public void OnPlayerDiscard(Player player, int discardCount)
    {
        int totalBlock = discardCount * blockPerDiscard;
        player.AddBlock(totalBlock);
        if (discardCount >= 2)
        {
            player.DrawCards(extraDraw);
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

    public void OnEndTurn(Player player, int attackCardUsedThisTurn)
    {
        if (attackCardUsedThisTurn >= 3)
        {
            // ��1
            player.DrawCards(1);
            // ��1 (²�Ƭ���̫�@�i)
            CardBase last = null;
           if (player.Hand.Count > 0)
            {
                last = player.Hand[player.Hand.Count - 1];
                player.Hand.RemoveAt(player.Hand.Count - 1);
                player.discardPile.Add(last);
                player.hasDiscardedThisTurn = true;
                player.discardCountThisTurn++;
            }
            // �Y�󱼪��O�����P => �U�^�X����+1
            if (last != null && last.cardType == CardType.Attack)
            {
                player.buffs.nextTurnAllAttackPlus += 1;
            }
        }
    }
}

#endregion
