using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 攻擊卡基底，可自訂攻擊範圍設定。
/// </summary>
public abstract class AttackCardBase : CardBase
{
    [Header("攻擊範圍圖片")]
    public Sprite scopeImage;

    [Header("攻擊範圍偏移表")]
    public List<Vector2Int> rangeOffsets = new List<Vector2Int>();

    protected int GetRelicBonusDamage(Player player, Enemy target)
    {
        if (player == null || target == null)
        {
            return 0;
        }

        return Mathf.Max(0, player.GetAdditionalCardDamage(this, target));
    }

    protected int GetDamageWithRelicBonus(Player player, Enemy target, int baseDamage)
    {
        return Mathf.Max(0, baseDamage + GetRelicBonusDamage(player, target));
    }

    protected int DealDamageAndNotify(Player player, Enemy target, int damage, bool useTrueDamage = false)
    {
        if (target == null)
        {
            return 0;
        }

        int resolvedDamage = Mathf.Max(0, damage);
        target.SnapshotBurningTurnsForNextAttackHit();
        int hpBefore = Mathf.Max(0, target.currentHP);

        if (useTrueDamage)
        {
            target.TakeTrueDamage(resolvedDamage);
        }
        else
        {
            target.TakeDamage(resolvedDamage);
        }

        int hpAfter = Mathf.Max(0, target.currentHP);
        int hpDamage = Mathf.Max(0, hpBefore - hpAfter);

        try
        {
            if (player != null && resolvedDamage > 0)
            {
                player.NotifyAttackCardHitEnemy(this, target, resolvedDamage, hpDamage);
            }

            if (player != null && hpDamage > 0)
            {
                player.NotifyAttackCardDamagedEnemy(this, target, hpDamage);
            }
        }
        finally
        {
            target.ClearAttackHitSnapshots();
        }

        return hpDamage;
    }
}
