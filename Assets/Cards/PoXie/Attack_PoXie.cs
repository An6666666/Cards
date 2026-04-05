using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deals line damage to every enemy between the player and the selected target.
/// </summary>
[CreateAssetMenu(fileName = "Attack_PoXie", menuName = "Cards/Attack/破邪")]
public class Attack_PoXie : AttackCardBase, IAreaTargetingCard
{
    [Header("Damage")]
    public int damage = 5;

    [Header("FX")]
    [Tooltip("Optional hit effect prefab.")]
    public GameObject hitEffectPrefab;

    [Header("Element")]
    [SerializeField]
    [Tooltip("Element applied by this card.")]
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
        List<Enemy> targets = new List<Enemy>();
        GetResolveTargets(enemy, GetAliveEnemyCandidates(), targets);

        for (int i = 0; i < targets.Count; i++)
        {
            Enemy target = targets[i];
            if (!IsAliveEnemy(target))
            {
                continue;
            }

            int totalDamage = Mathf.Max(0, calculatedDamage + GetRelicBonusDamage(player, target));
            int elementalDamage = target.ApplyElementalAttack(element, totalDamage, player);
            DealDamageAndNotify(player, target, elementalDamage, useTrueDamage: true);

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

    public void GetPreviewTargets(Enemy primaryTarget, IReadOnlyList<Enemy> aliveEnemies, List<Enemy> results)
    {
        CollectTargets(primaryTarget, aliveEnemies, results);
    }

    public void GetResolveTargets(Enemy primaryTarget, IReadOnlyList<Enemy> aliveEnemies, List<Enemy> results)
    {
        CollectTargets(primaryTarget, aliveEnemies, results);
    }

    private void CollectTargets(Enemy primaryTarget, IReadOnlyList<Enemy> aliveEnemies, List<Enemy> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        Player activePlayer = BattleRuntimeContext.Active?.Player;
        if (activePlayer == null || primaryTarget == null || aliveEnemies == null)
        {
            return;
        }

        Vector2Int from = activePlayer.position;
        Vector2Int to = primaryTarget.gridPosition;
        Vector2Int diff = to - from;
        int steps = Mathf.Max(1, GreatestCommonDivisor(Mathf.Abs(diff.x), Mathf.Abs(diff.y)));
        Vector2Int step = new Vector2Int(diff.x / steps, diff.y / steps);
        HashSet<Enemy> uniqueTargets = new HashSet<Enemy>();

        for (int i = 1; i <= steps; i++)
        {
            Vector2Int pos = from + step * i;
            for (int j = 0; j < aliveEnemies.Count; j++)
            {
                Enemy target = aliveEnemies[j];
                if (!IsAliveEnemy(target) || target.gridPosition != pos || !uniqueTargets.Add(target))
                {
                    continue;
                }

                results.Add(target);
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

    public override bool TryGetElementType(out ElementType elementType)
    {
        elementType = Element;
        return true;
    }

    private static IReadOnlyList<Enemy> GetAliveEnemyCandidates()
    {
        BattleRuntimeContext context = BattleRuntimeContext.Active;
        if (context != null && context.Enemies != null)
        {
            return context.Enemies;
        }

        return System.Array.Empty<Enemy>();
    }

    private static bool IsAliveEnemy(Enemy enemy)
    {
        return enemy != null && enemy.currentHP > 0 && !enemy.IsDead;
    }
}
