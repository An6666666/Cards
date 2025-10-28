using System.Collections.Generic;
using UnityEngine;

public class DiNiu : Enemy
{
    [Header("Di Niu Settings")]
    [SerializeField] private int sleepDuration = 3;
    [SerializeField] private int fixedDamage = 1;

    private int sleepTurnsRemaining;

    protected override void Awake()
    {
        enemyName = "地牛";
        maxHP = 40;
        BaseAttackDamage = 0;
        base.Awake();
        sleepTurnsRemaining = Mathf.Max(0, sleepDuration);
    }

    public override void EnemyAction(Player player)
    {
        bool sleepingThisTurn = sleepTurnsRemaining > 0;
        if (sleepingThisTurn)
        {
            sleepTurnsRemaining--;
        }

        if (frozenTurns > 0)
        {
            frozenTurns--;
            return;
        }

        if (buffs.stun > 0)
        {
            buffs.stun--;
            return;
        }

        if (sleepingThisTurn)
        {
            return;
        }

        DealDamageToAllCombatants(player);
    }

    private void DealDamageToAllCombatants(Player player)
    {
        if (fixedDamage <= 0)
        {
            return;
        }

        if (player != null)
        {
            player.TakeDamage(fixedDamage);
        }

        BattleManager battleManager = FindObjectOfType<BattleManager>();
        if (battleManager == null)
        {
            return;
        }

        foreach (Enemy enemy in battleManager.enemies)
        {
            if (enemy == null || enemy == this)
            {
                continue;
            }

            enemy.TakeDamage(fixedDamage);
        }
    }
}