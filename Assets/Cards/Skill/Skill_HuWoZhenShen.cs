using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 立刻獲得指定量的護盾
/// </summary>
[CreateAssetMenu(fileName = "Skill_HuWoZhenShen", menuName = "Cards/Skill/護我真身")]
public class Skill_HuWoZhenShen : CardBase
{
    public int blockValue = 8;

    private void OnEnable()
    {
        cardType = CardType.Skill;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        player.AddBlock(blockValue);
    }
}
