using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 奇門遁甲：立即將當前護甲值加倍
/// </summary>
[CreateAssetMenu(fileName = "Skill_QimenDunjia", menuName = "Cards/Skill/奇門遁甲")]
public class Skill_QimenDunjia : CardBase
{
    private void OnEnable()
    {
        cardType = CardType.Skill;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        int currentBlock = player.block;
        if (currentBlock <= 0)
        {
            return;
        }

        player.AddBlock(currentBlock);
    }
}
