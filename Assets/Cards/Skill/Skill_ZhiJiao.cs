using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 擲筊（消耗 1 點能量，抽 2 張牌，隨機消耗 1 張當前手牌）
/// </summary>
[CreateAssetMenu(fileName = "Skill_ZhiJiao", menuName = "Cards/Skill/擲筊")]
public class Skill_ZhiJiao : CardBase
{
    [Header("數值設定")]
    public int drawCount = 2;
    [FormerlySerializedAs("randomDiscardCount")]
    public int randomExhaustCount = 1;

    private void OnEnable()
    {
        cardType = CardType.Skill;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        player.DrawCards(drawCount);

        for (int i = 0; i < randomExhaustCount; i++)
        {
            if (!player.ExhaustRandomCardFromHand())
            {
                break;
            }
        }
    }
}