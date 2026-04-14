using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    private Enemy enemy;

    public void Init(Enemy owner)
    {
        enemy = owner;
    }

    public void HandleAwake()
    {
        if (enemy == null) return;
        enemy.currentHP = enemy.maxHP;
    }

    public void TakeDamage(int dmg)
    {
        if (enemy == null) return;

        dmg = ApplyFrostBonus(dmg);
        enemy.Visual.PlayHitShake();
        int previousHp = enemy.currentHP;

        int remain = dmg - enemy.block;
        if (remain > 0)
        {
            enemy.block = 0;
            enemy.currentHP -= remain;

            if (enemy.currentHP <= 0)
            {
                enemy.currentHP = 0;
                enemy.Die();
            }
            else
            {
                enemy.Visual.PlayHitAnimation();
            }
        }
        else
        {
            enemy.block -= dmg;
            if (enemy.block < 0) enemy.block = 0;
        }

        BattleEndSummaryStore.RegisterPlayerDamageDealt(Mathf.Max(0, previousHp - enemy.currentHP));
    }

    public void TakeTrueDamage(int dmg)
    {
        if (enemy == null) return;

        dmg = ApplyFrostBonus(dmg);
        enemy.Visual.PlayHitShake();
        int previousHp = enemy.currentHP;

        enemy.currentHP -= dmg;
        if (enemy.currentHP <= 0)
        {
            enemy.currentHP = 0;
            enemy.Die();
        }
        else
        {
            enemy.Visual.PlayHitAnimation();
        }

        BattleEndSummaryStore.RegisterPlayerDamageDealt(Mathf.Max(0, previousHp - enemy.currentHP));
    }

    public void AddBlock(int amount)
    {
        if (enemy == null) return;
        enemy.block += amount;
    }

    public void ReduceBlock(int amount)
    {
        if (enemy == null) return;
        enemy.block -= amount;
        if (enemy.block < 0) enemy.block = 0;
    }

    public void HandleEnemyTurnEnd()
    {
        if (enemy == null) return;
        if (enemy.frostStacks <= 0) return;
        enemy.SetFrostStacks(enemy.frostStacks - 1);
    }

    public void Die()
    {
        if (enemy == null || enemy.IsDead) return;
        enemy.MarkDead();

        Debug.Log(enemy.enemyName + " died!");

        enemy.Visual.PlayDeadAnimation();

        BattleManager bm = BattleRuntimeContext.Active != null ? BattleRuntimeContext.Active.Manager : null;
        if (bm != null)
        {
            bm.OnEnemyDefeated(enemy);
        }

        BattleEndSummaryStore.RegisterEnemyDefeated(enemy);

        Destroy(enemy.gameObject, enemy.DeathDestroyDelay);
    }

    private int ApplyFrostBonus(int dmg)
    {
        if (dmg <= 0 || enemy == null)
            return dmg;

        int frostStacksForThisHit = enemy.ConsumeFrostStacksForDamage();
        if (frostStacksForThisHit <= 0)
        {
            return dmg;
        }

        return dmg + frostStacksForThisHit * 2;
    }
}
