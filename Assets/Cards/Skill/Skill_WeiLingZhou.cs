using UnityEngine;

/// <summary>
/// 衛靈咒：當生命值將降至 0 時改為降至 1，並獲得護甲，之後移除這張牌。
/// </summary>
[CreateAssetMenu(fileName = "Skill_WeiLingZhou", menuName = "Cards/Skill/衛靈咒")]
public class Skill_WeiLingZhou : CardBase
{
    [Header("效果設定")]
    [Tooltip("觸發時獲得的護甲量。")]
    public int blockOnTrigger = 10;

    private void OnEnable()
    {
        cardType = CardType.Skill;
        exhaustOnUse = true;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        if (player == null)
        {
            return;
        }

        player.buffs.AddGuardianSpiritCharge(this, blockOnTrigger);
        player.ClearCardCostModifier(this);
        player.buffs.RemoveCardFromAllPiles(player, this);
    }
}
