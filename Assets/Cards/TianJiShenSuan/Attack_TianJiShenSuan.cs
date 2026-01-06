using UnityEngine;

/// <summary>
/// 天機神算：造成元素傷害、抽牌，並在使用後消耗。
/// </summary>
[CreateAssetMenu(fileName = "Attack_TianJiShenSuan", menuName = "Cards/Attack/天機神算")]
public class Attack_TianJiShenSuan : AttackCardBase
{
    [Header("數值設定")]
    [Tooltip("對目標造成的基礎傷害。")]
    public int damage = 9;

    [Tooltip("命中後抽取的牌數。")]
    public int drawCount = 1;

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
        cost = 2;
        exhaustOnUse = true;
        elementType = Element;
    }

    private void OnValidate()
    {
        cost = 2;
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

        player?.DrawCards(drawCount);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAttackSFX(element);
        }
    }
}