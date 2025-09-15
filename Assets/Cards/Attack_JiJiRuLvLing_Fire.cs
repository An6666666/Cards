using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Attack_JiJiRuLvLing_Fire", menuName = "Cards/Attack/急急如律令(火)")]
public class Attack_JiJiRuLvLing_Fire : AttackCardBase
{
    public int baseDamage = 6;

    public GameObject fireEffectPrefab;

    private void OnEnable() { cardType = CardType.Attack; }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        int dmg = enemy.ApplyElementalAttack(ElementType.Fire, baseDamage, player);
        enemy.TakeDamage(dmg);
        
         if (fireEffectPrefab != null)
            GameObject.Instantiate(fireEffectPrefab, enemy.transform.position, Quaternion.identity);

        AudioManager.Instance.PlayAttackSFX(ElementType.Fire);
    }
}
