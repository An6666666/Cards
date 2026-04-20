using UnityEngine;

[CreateAssetMenu(fileName = "Relic_DaDiSuiPian", menuName = "Cards/Relic/大地碎片")]
public class Relic_DaDiSuiPian : RelicBase
{
    public override bool TryGetGrowthTrapBonusElement(Player player, out ElementType element)
    {
        element = ElementType.Wood;
        return true;
    }
}
