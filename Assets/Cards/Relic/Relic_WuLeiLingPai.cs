using UnityEngine;

[CreateAssetMenu(fileName = "Relic_WuLeiLingPai", menuName = "Cards/Relic/\u4E94\u96F7\u4EE4\u724C")]
public class Relic_WuLeiLingPai : RelicBase
{
    public override void OnCardPlayed(Player player, CardBase card)
    {
        if (player == null || card == null || card.cardType != CardType.Skill)
        {
            return;
        }

        player.TryRemoveRandomNegativeEffect();
    }
}
