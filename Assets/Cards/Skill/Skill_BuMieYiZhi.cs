using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 不滅意志：本回合取得的護甲保留至下一次玩家回合開始。
/// </summary>
[CreateAssetMenu(fileName = "Skill_BuMieYiZhi", menuName = "Cards/Skill/不滅意志")]
public class Skill_BuMieYiZhi : CardBase
{
    private void OnEnable()
    {
        cardType = CardType.Skill;
    }
    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        if (player == null)
        {
            return;
        }
        player.buffs.retainBlockNextTurn = true;
    }
}
