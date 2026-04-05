using UnityEngine;

[CreateAssetMenu(fileName = "Relic_TaoMuJian", menuName = "Cards/Relic/桃木劍")]
public class Relic_TaoMuJian : RelicBase
{
    [Header("TaoMuJian Settings")]
    [Min(0)] public int bonusDamageAgainstDebuffedEnemy = 4;

    public override int GetAdditionalDamage(Player player, CardBase card, Enemy target)
    {
        if (player == null || target == null || !(card is AttackCardBase))
        {
            return 0;
        }

        if (!target.HasNegativeStatusEffect())
        {
            return 0;
        }

        return Mathf.Max(0, bonusDamageAgainstDebuffedEnemy);
    }
}
