using UnityEngine;

/// <summary>
/// 靈巧穿刺（造成元素傷害並獲得護甲）。
/// </summary>
[CreateAssetMenu(fileName = "Attack_LingQiaoChuanCi", menuName = "Cards/Attack/靈巧穿刺")]
public class Attack_LingQiaoChuanCi : AttackCardBase
{
    [Header("數值設定")]
    public int damage = 5;
    public int blockValue = 4;

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

        player?.AddBlock(blockValue);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAttackSFX(element);
        }
    }
}