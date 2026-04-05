using UnityEngine;

[CreateAssetMenu(fileName = "Relic_LongJiao", menuName = "Cards/Relic/\u9F8D\u89D2")]
public class Relic_LongJiao : RelicBase
{
    [Header("Long Jiao Settings")]
    [Min(0)] public int followUpDamage = 3;

    public override void OnAttackCardHitEnemy(Player player, AttackCardBase card, Enemy target, int attemptedDamage, int hpDamage)
    {
        if (player == null || card == null || target == null)
        {
            return;
        }

        if (target.currentHP <= 0 || target.IsDead)
        {
            return;
        }

        int damage = Mathf.Max(0, followUpDamage);
        if (damage <= 0)
        {
            return;
        }

        target.TakeDamage(damage);
    }
}
