using System.Collections.Generic;
using UnityEngine;

public class FeiLuYao : Enemy
{
    private enum DebuffType
    {
        Weak,
        Bleed,
        Imprison
    }

    [Header("Fei Lu Yao Settings")]
    [SerializeField] private int weakDuration = 2;
    [SerializeField] private int bleedDuration = 2;
    [SerializeField] private int imprisonDuration = 1;

    protected override void Awake()
    {
        enemyName = "飛顱妖";
        base.Awake();
    }

    protected internal override void MoveOneStepTowards(Player player)
    {
        // 飛顱妖不會移動
    }

    public override void EnemyAction(Player player)
    {
        if (frozenTurns > 0)
        {
            SetFrozenTurns(Mathf.Max(0, frozenTurns - 1));
            return;
        }

        if (player == null)
        {
            return;
        }

        ApplyRandomDebuff(player);
    }

    private void ApplyRandomDebuff(Player player)
    {
        List<DebuffType> available = new List<DebuffType>(3);

        if (player.buffs.weak <= 0)
        {
            available.Add(DebuffType.Weak);
        }

        if (player.buffs.bleed <= 0)
        {
            available.Add(DebuffType.Bleed);
        }

        if (player.buffs.imprison <= 0)
        {
            available.Add(DebuffType.Imprison);
        }

        if (available.Count == 0)
        {
            return;
        }

        DebuffType choice = available[Random.Range(0, available.Count)];
        switch (choice)
        {
            case DebuffType.Weak:
                player.buffs.ApplyWeakFromEnemy(weakDuration);
                break;
            case DebuffType.Bleed:
                player.buffs.ApplyBleedFromEnemy(bleedDuration);
                break;
            case DebuffType.Imprison:
                player.buffs.ApplyImprisonFromEnemy(imprisonDuration);
                break;
        }
    }

    // ⭐ 飛顱妖專用意圖：主要行動永遠是放 debuff 技能
    public override void DecideNextIntent(Player player)
    {
        if (player == null)
        {
            nextIntent.type = EnemyIntentType.Idle;
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        // 被冰凍 → Idle
        if (frozenTurns > 0)
        {
            nextIntent.type = EnemyIntentType.Idle;
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        // 檢查目前玩家是否還有可以上的 debuff
        bool hasDebuffAvailable =
            player.buffs.weak <= 0 ||
            player.buffs.bleed <= 0 ||
            player.buffs.imprison <= 0;

        if (hasDebuffAvailable)
        {
            // 可以上 debuff → 顯示技能意圖
            nextIntent.type = EnemyIntentType.Skill;
            nextIntent.value = 0;   // 先不顯示數字
        }
        else
        {
            // 全部 debuff 都在身上了，就當成發呆
            nextIntent.type = EnemyIntentType.Idle;
            nextIntent.value = 0;
        }

        UpdateIntentIcon();
    }
}
