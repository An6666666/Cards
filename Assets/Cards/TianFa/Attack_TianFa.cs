using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Attack_TianFa", menuName = "Cards/Attack/憭拍蔑")]
public class Attack_TianFa : AttackCardBase, IAreaTargetingCard
{
    [Header("Damage")]
    public int baseDamage = 6;

    [Tooltip("AOE radius around the selected enemy.")]
    public float effectRadius = 2.5f;

    [Tooltip("Optional hit effect prefab.")]
    public GameObject hitEffectPrefab;

    [Header("Element")]
    [SerializeField] private ElementType elementType = ElementType.Fire;

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
        List<Enemy> targets = new List<Enemy>();
        GetResolveTargets(enemy, GetAliveEnemyCandidates(), targets);

        foreach (Enemy target in targets)
        {
            int damage = target.ApplyElementalAttack(element, baseDamage, player);
            target.TakeDamage(damage);

            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, target.transform.position, Quaternion.identity);
            }
        }

        if (AudioManager.Instance != null)
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

    public override bool TryGetElementType(out ElementType type)
    {
        type = Element;
        return true;
    }

    private void CollectTargets(Enemy primaryTarget, IReadOnlyList<Enemy> aliveEnemies, List<Enemy> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (primaryTarget == null || aliveEnemies == null)
        {
            return;
        }

        Vector2Int center = primaryTarget.gridPosition;
        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            Enemy candidate = aliveEnemies[i];
            if (!IsAliveEnemy(candidate))
            {
                continue;
            }

            float distance = Vector2Int.Distance(center, candidate.gridPosition);
            if (candidate != primaryTarget && distance > effectRadius)
            {
                continue;
            }

            results.Add(candidate);
        }
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
