using UnityEngine;

/// <summary>
/// 金身：使用後，玩家的回合結束時獲得指定量的護甲。
/// </summary>
[CreateAssetMenu(fileName = "Skill_JinShen", menuName = "Cards/Skill/金身")]
public class Skill_JinShen : CardBase
{
    [Header("數值設定")]
    public int armorGainPerTurn = 4;

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

        player.buffs.blockGainAtTurnEnd += armorGainPerTurn;
    }
}