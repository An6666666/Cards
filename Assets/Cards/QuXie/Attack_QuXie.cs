using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 驅邪（基礎單體攻擊 + 淨化自身負面狀態）
/// </summary>
[CreateAssetMenu(fileName = "Attack_QuXie", menuName = "Cards/Attack/驅邪")]
public class Attack_QuXie : AttackCardBase
{
    [Header("數值設定")]
    public int damage = 4;

    [Header("特效設定")]
    [Tooltip("命中時產生的特效 (選填)。")]
    public GameObject hitEffectPrefab;

    [Header("元素設定")]
    [SerializeField]
    [Tooltip("此卡片所使用的元素屬性。")]
    private ElementType elementType = ElementType.Fire;

    protected virtual ElementType Element => elementType;

    private void OnEnable()
    {
        cardType = CardType.Attack;
    }

    private void OnValidate()
    {
        elementType = Element;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        if (enemy == null) return;

        ElementType element = Element;
        int dmg = enemy.ApplyElementalAttack(element, damage, player);
        enemy.TakeDamage(dmg);

        if (hitEffectPrefab != null)
        {
            GameObject.Instantiate(hitEffectPrefab, enemy.transform.position, Quaternion.identity);
        }

        // 攻擊後，只淨化由妖怪造成的負面狀態
        player?.RemoveEnemyNegativeEffects();
        player.DrawCards(1);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAttackSFX(element);
        }
    }
}