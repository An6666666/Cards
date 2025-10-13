using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "Attack_JiJiRuLvLing_Wood", menuName = "Cards/Attack/急急如律令(木)")]
public class Attack_JiJiRuLvLing_Wood : AttackCardBase        // 木屬性版本
{
    public int baseDamage = 6;                                 // 基礎傷害值

    public GameObject woodEffectPrefab;

    private void OnEnable() { cardType = CardType.Attack; }    // 設定卡牌為攻擊類型

    public override void ExecuteEffect(Player player, Enemy enemy)  // 執行效果
    {
        int dmg = enemy.ApplyElementalAttack(ElementType.Wood, baseDamage, player);  // 計算木屬性傷害
        enemy.TakeDamage(dmg);                                        // 使敵人承受計算後傷害

        if (woodEffectPrefab != null)
            GameObject.Instantiate(woodEffectPrefab, enemy.transform.position, Quaternion.identity);

        AudioManager.Instance.PlayAttackSFX(ElementType.Wood);
    }
}
