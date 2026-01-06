using UnityEngine;

/// <summary>
/// 神靈庇佑（消耗 2 點能量，獲得 30 點護甲）
/// </summary>
[CreateAssetMenu(fileName = "Skill_ShenLingBiYou", menuName = "Cards/Skill/神靈庇佑")]
public class Skill_ShenLingBiYou : CardBase
{
    [Header("數值設定")]
    public int blockValue = 30;

    private void OnEnable()
    {
        cardType = CardType.Skill;
        exhaustOnUse = true;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        player.AddBlock(blockValue);
    }
}