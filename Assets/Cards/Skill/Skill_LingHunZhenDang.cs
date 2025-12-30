using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 靈魂震盪（消耗 1 能量：獲得 2 點能量，扣 6 點生命值）
/// </summary>
[CreateAssetMenu(fileName = "Skill_LingHunZhenDang", menuName = "Cards/Skill/靈魂震盪")]
public class Skill_LingHunZhenDang : CardBase
{
    [Header("效果設定")]
    [Tooltip("使用時獲得的能量。")]
    public int energyGain = 2;

    [Tooltip("使用時直接損失的生命值。")]
    public int selfDamage = 6;

    private void OnEnable()
    {
        cardType = CardType.Skill;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        if (player == null)
        {
            return;
        }

        player.energy += energyGain;
        player.TakeDamageDirect(selfDamage);
    }
}

