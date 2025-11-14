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
        maxHP = 40;
        BaseAttackDamage = 0;
        base.Awake();
    }

    protected override void MoveOneStepTowards(Player player)
    {
        // 飛顱妖不會移動
    }

    public override void EnemyAction(Player player)
    {
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
}