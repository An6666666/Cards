using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 燃盡術（造成 10 點元素傷害，使用後消耗）
/// </summary>
[CreateAssetMenu(fileName = "Attack_RanJinShu", menuName = "Cards/Attack/燃盡術")]
public class Attack_RanJinShu : AttackCardBase
{
    [Header("數值設定")]
    [Tooltip("對主要目標造成的基礎傷害。")]
    public int damage = 10;

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
        exhaustOnUse = true;
    }

    private void OnValidate()
    {
        elementType = Element;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        if (enemy == null) return;

        ElementType element = Element;
        int appliedDamage = enemy.ApplyElementalAttack(element, damage, player);
        enemy.TakeDamage(appliedDamage);

        if (hitEffectPrefab != null)
        {
            GameObject.Instantiate(hitEffectPrefab, enemy.transform.position, Quaternion.identity);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAttackSFX(element);
        }
    }
}