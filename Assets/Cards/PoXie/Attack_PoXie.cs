using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 破邪：1 能量，造成 5 點真實元素傷害（無視妖怪護甲）。
/// </summary>
[CreateAssetMenu(fileName = "Attack_PoXie", menuName = "Cards/Attack/破邪")]
public class Attack_PoXie : AttackCardBase
{
    [Header("數值設定")]
    public int damage = 5;

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
        int calculatedDamage = player.CalculateAttackDamage(damage);
        bool hasHit = false;

        foreach (Enemy target in GetEnemiesOnLine(player.position, enemy.gridPosition))
        {
            if (target == null) continue;

            int elementalDamage = target.ApplyElementalAttack(element, calculatedDamage, player);
            target.TakeTrueDamage(elementalDamage);

            if (hitEffectPrefab != null)
            {
                GameObject.Instantiate(hitEffectPrefab, target.transform.position, Quaternion.identity);
            }

            hasHit = true;
        }

        if (hasHit && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAttackSFX(element);
        }
    }

    private IEnumerable<Enemy> GetEnemiesOnLine(Vector2Int from, Vector2Int to)
    {
        Enemy[] allEnemies = GameObject.FindObjectsOfType<Enemy>();
        if (allEnemies == null || allEnemies.Length == 0) yield break;

        Vector2Int diff = to - from;
        int steps = Mathf.Max(1, GreatestCommonDivisor(Mathf.Abs(diff.x), Mathf.Abs(diff.y)));
        Vector2Int step = new Vector2Int(diff.x / steps, diff.y / steps);

        for (int i = 1; i <= steps; i++)
        {
            Vector2Int pos = from + step * i;
            foreach (Enemy e in allEnemies)
            {
                if (e != null && e.gridPosition == pos)
                {
                    yield return e;
                }
            }
        }
    }

    private int GreatestCommonDivisor(int a, int b)
    {
        while (b != 0)
        {
            int temp = b;
            b = a % b;
            a = temp;
        }

        return a == 0 ? 1 : a;
    }
}