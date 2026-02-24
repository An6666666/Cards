using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deals elemental damage to all enemies in card range.
/// Final damage scales with the current exhaust pile count.
/// </summary>
public abstract class Attack_DaRanJinShuBase : AttackCardBase
{
    [Header("Damage")]
    [Tooltip("Damage gained per exhausted card.")]
    public int damagePerExhaust = 2;

    [Header("FX")]
    [Tooltip("Optional hit effect prefab.")]
    public GameObject hitEffectPrefab;

    [Header("Element")]
    [SerializeField] private ElementType elementType = ElementType.Fire;

    protected virtual ElementType Element => elementType;

    private void OnEnable()
    {
        cardType = CardType.Attack;
        cost = 0;
        elementType = Element;
    }

    private void OnValidate()
    {
        cost = 0;
        elementType = Element;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        if (player == null)
        {
            return;
        }

        int exhaustCount = player.exhaustPile != null ? player.exhaustPile.Count : 0;
        int totalDamage = Mathf.Max(0, exhaustCount) * damagePerExhaust;
        if (totalDamage <= 0)
        {
            return;
        }

        ElementType element = Element;
        bool hitAnyTarget = false;

        foreach (Enemy target in GetEnemiesInRange(player))
        {
            int appliedDamage = target.ApplyElementalAttack(element, totalDamage, player);
            target.TakeDamage(appliedDamage);
            hitAnyTarget = true;

            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, target.transform.position, Quaternion.identity);
            }
        }

        if (hitAnyTarget && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAttackSFX(element);
        }
    }

    public override bool TryGetElementType(out ElementType type)
    {
        type = Element;
        return true;
    }

    private IEnumerable<Enemy> GetEnemiesInRange(Player player)
    {
        IReadOnlyList<Vector2Int> offsets = (rangeOffsets != null && rangeOffsets.Count > 0)
            ? rangeOffsets
            : DefaultOffsets;

        Vector2Int center = player.position;
        IReadOnlyList<Enemy> allEnemies = BattleRuntimeContext.Active?.Enemies;
        if (allEnemies == null)
        {
            allEnemies = System.Array.Empty<Enemy>();
        }

        HashSet<Enemy> uniqueTargets = new HashSet<Enemy>();
        for (int i = 0; i < offsets.Count; i++)
        {
            Vector2Int targetPos = center + offsets[i];
            for (int j = 0; j < allEnemies.Count; j++)
            {
                Enemy target = allEnemies[j];
                if (!IsAliveEnemy(target))
                {
                    continue;
                }

                if (target.gridPosition == targetPos && uniqueTargets.Add(target))
                {
                    yield return target;
                }
            }
        }
    }

    private static bool IsAliveEnemy(Enemy enemy)
    {
        return enemy != null && enemy.currentHP > 0 && !enemy.IsDead;
    }

    private static readonly List<Vector2Int> DefaultOffsets = new List<Vector2Int>
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };
}
