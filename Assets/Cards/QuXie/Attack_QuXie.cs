using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 驅邪（基礎單體攻擊 + 解除增益）
/// </summary>
[CreateAssetMenu(fileName = "Attack_QuXie", menuName = "Cards/Attack/驅邪")]
public class Attack_QuXie : CardBase
{
    [Header("數值設定")]
    public int damage = 4;
    [Tooltip("可驅散目標身上的增益層數(Dispel次數)")]
    public int dispelCount = 1;

    private void OnEnable()
    {
        cardType = CardType.Attack;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        int dmg = player.CalculateAttackDamage(damage);
        enemy.TakeDamage(dmg);
        // 攻擊後，驅散目標身上的 buff
        enemy.DispelBuff(dispelCount);
    }
}
