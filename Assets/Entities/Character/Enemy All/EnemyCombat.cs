using System.Collections;
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    private Enemy enemy;
    private Coroutine destroyAfterDeathRoutine;

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

        // Stack-based elemental debuffs naturally decay at the end of the enemy turn.
        if (enemy.frostStacks > 0)
        {
            enemy.SetFrostStacks(enemy.frostStacks - 1);
        }

        if (enemy.chargedCount > 0)
        {
            enemy.SetChargedCount(enemy.chargedCount - 1);
        }
    }

    public void Die()
    {
        if (enemy == null || enemy.IsDead) return;
        enemy.MarkDead();
        enemy.SetForceHideIntent(true);

        Debug.Log(enemy.enemyName + " died!");

        enemy.Visual.PlayDeadAnimation();

        BattleManager bm = BattleRuntimeContext.Active != null ? BattleRuntimeContext.Active.Manager : null;
        if (bm != null)
        {
            bm.OnEnemyDefeated(enemy);
        }

        BattleEndSummaryStore.RegisterEnemyDefeated(enemy);

        if (destroyAfterDeathRoutine != null)
        {
            StopCoroutine(destroyAfterDeathRoutine);
        }
        destroyAfterDeathRoutine = StartCoroutine(DestroyAfterDeathAnimationRoutine());
    }

    private IEnumerator DestroyAfterDeathAnimationRoutine()
    {
        if (enemy == null)
        {
            yield break;
        }

        if (enemy.Visual != null)
        {
            yield return enemy.Visual.WaitForDeathAnimationToFinish(enemy.DeathDestroyDelay);
        }
        else if (enemy.DeathDestroyDelay > 0f)
        {
            yield return new WaitForSeconds(enemy.DeathDestroyDelay);
        }

        if (enemy != null)
        {
            Destroy(enemy.gameObject);
        }
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

        return dmg + Enemy.GetFrostDamageBonus(frostStacksForThisHit);
    }
}
