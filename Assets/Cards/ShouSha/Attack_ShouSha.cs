using UnityEngine;

/// <summary>
/// 收煞：消耗 1 能量，對敵人造成等同自身護甲值的元素傷害。
/// </summary>
[CreateAssetMenu(fileName = "Attack_ShouSha", menuName = "Cards/Attack/收煞")]
public class Attack_ShouSha : AttackCardBase
{
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
        int blockValue = Mathf.Max(0, player.block);
        int damage = enemy.ApplyElementalAttack(element, blockValue, player);
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