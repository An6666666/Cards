using UnityEngine;

[CreateAssetMenu(fileName = "Relic_HuShenFu", menuName = "Cards/Relic/護身符")]
public class Relic_HuShenFu : RelicBase
{
    [Header("HuShenFu Settings")]
    [Min(0)] public int blockGainPerTurnStart = 8;

    public override void OnBattleStart(Player player)
    {
        if (player == null || player.buffs == null)
        {
            return;
        }

        int blockGain = Mathf.Max(0, blockGainPerTurnStart);
        if (blockGain > 0)
        {
            player.buffs.blockGainAtTurnStart += blockGain;
        }
    }
}
