using UnityEngine;

[CreateAssetMenu(fileName = "Relic_ZhaoHunFan", menuName = "Cards/Relic/招魂幡")]
public class Relic_ZhaoHunFan : RelicBase
{
    [Header("ZhaoHunFan Settings")]
    [Min(0)] public int energyGainPerKill = 1;

    public override void OnEnemyDefeated(Player player, Enemy enemy)
    {
        if (player == null || enemy == null)
        {
            return;
        }

        int energyGain = Mathf.Max(0, energyGainPerKill);
        if (energyGain > 0)
        {
            player.GainEnergy(energyGain);
        }
    }
}
