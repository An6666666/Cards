using UnityEngine;

/// <summary>
/// 真訣：造成基礎傷害，若自身有護甲則額外提升傷害，並附帶元素屬性。
/// </summary>
[CreateAssetMenu(fileName = "Attack_ZhenJue", menuName = "Cards/Attack/真訣")]
public class Attack_ZhenJue : AttackCardBase
{
    [Header("數值設定")]
    [Tooltip("基礎傷害。")]
    public int baseDamage = 10;

    [Tooltip("當自身有護甲時增加的額外傷害。")]
    public int bonusDamageWithArmor = 4;

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
        if (enemy == null || player == null)
        {
            return;
        }

        ElementType element = Element;
        int totalDamage = baseDamage;

        if (player.block > 0)
        {
            totalDamage += bonusDamageWithArmor;
        }

        int damage = enemy.ApplyElementalAttack(element, totalDamage, player);
        enemy.TakeDamage(damage);

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