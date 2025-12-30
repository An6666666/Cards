using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#region �ޯ�P1-5�G�����]�p

/// <summary>
/// ���� (���ۤv��� + �ĤH�U������ˮ` +2)
/// </summary>
[CreateAssetMenu(fileName = "Skill_ShenWei", menuName = "Cards/Skill/����")]
public class Skill_ShenWei : CardBase
{
    public int blockValue = 5;
    public int enemyNextDamageUp = 2;

    private void OnEnable()
    {
        cardType = CardType.Skill;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        // �W�[�ۨ����
        player.AddBlock(blockValue);

        // �ĤH�U�������ˮ`+2 => �b Enemy �̥i�� e.buffs.nextDamageTakenUp = 2;
        //enemy.buffs.nextDamageTakenUp = enemyNextDamageUp;

    }
}

/// <summary>
/// �F��_�� (��o1��q, �����J��z1�^�X + �I�[�p�q�ĪG)
/// </summary>
[CreateAssetMenu(fileName = "Skill_LingHunZhenDang", menuName = "Cards/Skill/�F��_��")]
public class Skill_LingHunZhenDang : CardBase
{
    private void OnEnable()
    {
        cardType = CardType.Skill;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        // ��o1��q (��q�W���ݦۦ�޲z)
        player.energy += 1;
        // �O���a��z 1 �^�X => 改以方法增加玩家自身造成的虛弱層數
        player.buffs.IncreaseWeakFromPlayer(1);


        // ����W�I�[�p�q�ĪG => �i��� BattleManager �� GameManager ����
        // GameManager.instance.isThunderPresent = true; (�̧A�]�p)

    }

    /// <summary>
    /// ���� (���^�X�ڤ���˴�b, �Y�h�H�Ҧ��i�@�Υ���)
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_ShenJiang", menuName = "Cards/Skill/����")]
    public class Skill_ShenJiang : CardBase
    {
        private void OnEnable()
        {
            cardType = CardType.Skill;
        }

        public override void ExecuteEffect(Player player, Enemy enemy)
        {
            // ���^�X���˴�b
            player.buffs.damageTakenRatio = 0.5f; // �����a���^�X�u��50%�ˮ`
                                                  // �U�^�X�i���m
        }
    }

    #endregion

    #region �ޯ�P6-10�G�s�W�]�p

    /// <summary>
    /// �j���@�� (��¦���m, �Y��P���p�e��P/�Τw��P, �h�B�~���m)
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_QiangHuaHuDun", menuName = "Cards/Skill/�j���@��")]
    public class Skill_QiangHuaHuDun : CardBase
    {
        public int baseBlock = 7;
        public int bonusBlock = 3;

        private void OnEnable()
        {
            cardType = CardType.Skill;
        }

        public override void ExecuteEffect(Player player, Enemy enemy)
        {
            bool isPlanToExhaust = player.CheckExhaustPlan();
            int totalBlock = baseBlock;
            if (isPlanToExhaust)
            {
                totalBlock += bonusBlock;
            }
            player.AddBlock(totalBlock);
        }
    }

    /// <summary>
    /// ���Ĥ��� (0�O�G��1�i�P, �U1�i�����d�O��-1)
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_YouDiZhiCe", menuName = "Cards/Skill/���Ĥ���")]
    public class Skill_YouDiZhiCe : CardBase
    {
        private void OnEnable()
        {
            cardType = CardType.Skill;
        }

        public override void ExecuteEffect(Player player, Enemy enemy)
        {
            // ��1�i�P
            bool success = player.DiscardOneCard();
            if (success)
            {
                // �ϤU1�i�����d cost-1 => ��Player���� nextAttackCostModify = -1
                player.buffs.nextAttackCostModify -= 1;
            }
        }
    }

    /// <summary>
    /// ½�c���d (��3, �^�X�����H����2)
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_FanXiangDaoGui", menuName = "Cards/Skill/½�c���d")]
    public class Skill_FanXiangDaoGui : CardBase
    {
        private void OnEnable()
        {
            cardType = CardType.Skill;
        }

        public override void ExecuteEffect(Player player, Enemy enemy)
        {
            // ��3
            player.DrawCards(3);

            // �b BattleManager �� Player �ݪ� "EndTurn" ���ˬd:
            // if(���^�X�ιL½�c���d) => �H����2
            // �o�̶ȰO���@��flag
            player.buffs.needRandomDiscardAtEnd = 2; // �^�X�����H����2
        }
    }

    /// <summary>
    /// �������m (���B���m, ���U�^�X��P-1)
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_CheDiFangYu", menuName = "Cards/Skill/�������m")]
    public class Skill_CheDiFangYu : CardBase
    {
        public int blockValue = 12;

        private void OnEnable()
        {
            cardType = CardType.Skill;
        }

        public override void ExecuteEffect(Player player, Enemy enemy)
        {
            player.AddBlock(blockValue);
            // �U�^�X��P -1
            player.buffs.nextTurnDrawChange -= 1;
        }
    }
}
#endregion

