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

        // 地牛本身沒有普通攻擊
        BaseAttackDamage = 0;

        base.Awake();
        sleepTurnsRemaining = Mathf.Max(0, sleepDuration);
    }

    public override void EnemyAction(Player player)
    {
        bool sleepingThisTurn = sleepTurnsRemaining > 0;
        if (sleepingThisTurn)
        {
            // 還在睡覺，就把剩餘睡覺回合扣一
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
            // 這回合在睡，不做事
            return;
        }

        // 起床後：「全場傷害」技能
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

        List<Enemy> snapshot = new List<Enemy>(battleManager.enemies);

        foreach (Enemy enemy in battleManager.enemies)
        {
            if (enemy == null || enemy == this)
            {
                continue;
            }

            enemy.TakeDamage(fixedDamage);
        }
    }

    // ⭐ 地牛專用意圖：睡覺回合 → Idle，起床那回合 → Skill icon
    public override void DecideNextIntent(Player player)
    {
        if (player == null)
        {
            nextIntent.type = EnemyIntentType.Idle;
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        // 被冰凍 / 暈眩 → 一律 Idle
        if (frozenTurns > 0 || buffs.stun > 0)
        {
            nextIntent.type = EnemyIntentType.Idle;
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        if (sleepTurnsRemaining > 0)
        {
            // 還在睡覺：可以用 Idle 或 Charge，看你之後要不要做蓄力圖示
            nextIntent.type = EnemyIntentType.Idle;
            nextIntent.value = 0;
        }
        else
        {
            // 要放地裂技能的回合
            nextIntent.type = EnemyIntentType.Skill;

            // 你說傷害先不顯示，所以先給 0
            // 如果以後要顯示固定傷害，就改成：nextIntent.value = fixedDamage;
            nextIntent.value = 0;
        }

        UpdateIntentIcon();
    }
}
