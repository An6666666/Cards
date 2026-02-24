using System.Collections.Generic;
using UnityEngine;

public interface IEnemyQueryService
{
    IReadOnlyList<Enemy> AliveEnemies { get; }
    bool IsAlive(Enemy enemy);
    void EnemiesInOffsets(Vector2Int center, IReadOnlyList<Vector2Int> offsets, List<Enemy> results);
    void EnemiesInRadius(Vector2Int center, float radius, List<Enemy> results, bool includeCenter = true);
}

public sealed class BattleEnemyQueryService : IEnemyQueryService
{
    private readonly BattleRuntimeContext context;
    private readonly List<Enemy> aliveCache = new List<Enemy>(8);

    public BattleEnemyQueryService(BattleRuntimeContext context)
    {
        this.context = context;
    }

    public IReadOnlyList<Enemy> AliveEnemies
    {
        get
        {
            RebuildAliveCache();
            return aliveCache;
        }
    }

    public bool IsAlive(Enemy enemy)
    {
        if (enemy == null)
        {
            return false;
        }

        if (context != null)
        {
            return context.IsAliveEnemy(enemy);
        }

        return enemy.currentHP > 0 && !enemy.IsDead;
    }

    public void EnemiesInOffsets(Vector2Int center, IReadOnlyList<Vector2Int> offsets, List<Enemy> results)
    {
        results?.Clear();
        if (results == null || offsets == null || offsets.Count == 0)
        {
            return;
        }

        IReadOnlyList<Enemy> enemies = AliveEnemies;
        for (int i = 0; i < offsets.Count; i++)
        {
            Vector2Int targetPos = center + offsets[i];
            for (int j = 0; j < enemies.Count; j++)
            {
                Enemy enemy = enemies[j];
                if (enemy != null && enemy.gridPosition == targetPos && !results.Contains(enemy))
                {
                    results.Add(enemy);
                }
            }
        }
    }

    public void EnemiesInRadius(Vector2Int center, float radius, List<Enemy> results, bool includeCenter = true)
    {
        results?.Clear();
        if (results == null)
        {
            return;
        }

        IReadOnlyList<Enemy> enemies = AliveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == null)
            {
                continue;
            }

            float distance = Vector2Int.Distance(center, enemy.gridPosition);
            if (!includeCenter && distance <= Mathf.Epsilon)
            {
                continue;
            }

            if (distance <= radius)
            {
                results.Add(enemy);
            }
        }
    }

    private void RebuildAliveCache()
    {
        aliveCache.Clear();

        if (context == null || context.Enemies == null)
        {
            return;
        }

        IReadOnlyList<Enemy> enemies = context.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (context.IsAliveEnemy(enemy))
            {
                aliveCache.Add(enemy);
            }
        }
    }
}

