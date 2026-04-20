using UnityEngine;

[CreateAssetMenu(fileName = "Relic_HeiGouXue", menuName = "Cards/Relic/黑狗血")]
public class Relic_HeiGouXue : RelicBase
{
    [Header("Hei Gou Xue Settings")]
    [Min(0)] public int immediateBurningDamage = Enemy.BurningDamagePerTurn;
    [Min(1)] public int burningTurnsConsumed = 1;

    public override void OnAttackCardHitEnemy(Player player, AttackCardBase card, Enemy target, int attemptedDamage, int hpDamage, AttackCardHitContext context)
    {
        if (player == null || card == null || target == null || !context.TargetWasBurningBeforeHit)
        {
            return;
        }

        if (target.currentHP <= 0 || target.IsDead)
        {
            return;
        }

        int damage = Mathf.Max(0, immediateBurningDamage);
        if (damage > 0)
        {
            target.TakeDamage(damage);
        }

        int remainingBurningTurns = Mathf.Max(0, target.burningTurns - Mathf.Max(1, burningTurnsConsumed));
        target.SetBurningTurns(remainingBurningTurns);

        if (remainingBurningTurns == 0)
        {
            target.RemoveElementTag(ElementType.Wood);
        }
    }
}
