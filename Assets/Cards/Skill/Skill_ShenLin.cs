using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 神臨（消耗兩點能量，抽 3 張牌，且本回合不能再抽牌）
/// </summary>
[CreateAssetMenu(fileName = "Skill_ShenLin", menuName = "Cards/Skill/神臨")]
public class Skill_ShenLin : CardBase
{
    public int drawCount = 3;

    private void OnEnable()
    {
        cardType = CardType.Skill;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        player.DrawCards(drawCount);
        player.buffs.drawBlockedThisTurn = true;
    }
}
