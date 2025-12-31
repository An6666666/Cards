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

        enemy.Visual.PlayHitShake();

        int remain = dmg - enemy.block;
        if (remain > 0)
        {
            enemy.block = 0;
            enemy.currentHP -= remain;

            if (enemy.currentHP <= 0)
            {
                enemy.currentHP = 0;
                Die();
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
    }

    public void TakeTrueDamage(int dmg)
    {
        if (enemy == null) return;

        enemy.Visual.PlayHitShake();

        enemy.currentHP -= dmg;
        if (enemy.currentHP <= 0)
        {
            enemy.currentHP = 0;
            Die();
        }
        else
        {
            enemy.Visual.PlayHitAnimation();
        }
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

    public void Die()
    {
        if (enemy == null || enemy.IsDead) return;
        enemy.MarkDead();

        Debug.Log(enemy.enemyName + " died!");

        enemy.Visual.PlayDeadAnimation();

        BattleManager bm = FindObjectOfType<BattleManager>();
        if (bm != null)
        {
            bm.OnEnemyDefeated(enemy);
        }

        Destroy(enemy.gameObject, enemy.DeathDestroyDelay);
    }
}
