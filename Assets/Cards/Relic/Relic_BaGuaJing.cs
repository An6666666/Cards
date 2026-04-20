using UnityEngine;

[CreateAssetMenu(fileName = "Relic_BaGuaJing", menuName = "Cards/Relic/八卦鏡")]
public class Relic_BaGuaJing : RelicBase
{
    public override bool TryGetConductiveDamageMultiplier(Player player, out float multiplier)
    {
        multiplier = 1f;
        return true;
    }
}
