using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 天罰 - 以指定敵人為中心對周圍敵人造成元素傷害。
/// 可以透過 effectRadius 控制範圍，並依元素型別觸發對應的元素反應。
/// </summary>
[CreateAssetMenu(fileName = "Attack_TianFa", menuName = "Cards/Attack/天罰")]
public class Attack_TianFa : AttackCardBase
{
    [Header("基本設定")]
    [Tooltip("對每個命中的敵人造成的基礎傷害。")]
    public int baseDamage = 6;

    [Tooltip("以被選定的敵人為中心，影響其他敵人的半徑。單位為格子距離。")]
    public float effectRadius = 2.5f;

    [Tooltip("命中時產生的特效 (選填)。")] public GameObject hitEffectPrefab;

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
        Vector2Int center = enemy.gridPosition;

        Enemy[] enemies = GameObject.FindObjectsOfType<Enemy>();
        foreach (Enemy target in enemies)
        {
            if (target == null) continue;

            float distance = Vector2Int.Distance(center, target.gridPosition);
            if (target != enemy && distance > effectRadius) continue;

            int damage = target.ApplyElementalAttack(element, baseDamage, player);
            target.TakeDamage(damage);

            if (hitEffectPrefab != null)
            {
                GameObject.Instantiate(hitEffectPrefab, target.transform.position, Quaternion.identity);
            }
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAttackSFX(element);
        }
    }
}