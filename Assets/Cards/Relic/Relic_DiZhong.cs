using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Relic_DiZhong", menuName = "Cards/Relic/帝鐘")]
public class Relic_DiZhong : RelicBase
{
    [Header("DiZhong Settings")]
    [Min(0)] public int damageToAllEnemies = 5;
    [Min(0)] public int knockbackSteps = 1;

    public override void OnCardExhausted(Player player, CardBase card)
    {
        if (player == null || card == null)
        {
            return;
        }

        BattleRuntimeContext context = BattleRuntimeContext.Active;
        if (context == null || context.Enemies == null)
        {
            return;
        }

        List<Enemy> targets = CollectAliveEnemies(context);
        if (targets.Count == 0)
        {
            return;
        }

        int damage = Mathf.Max(0, damageToAllEnemies);
        int knockback = Mathf.Max(0, knockbackSteps);
        Board board = context.Board;

        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy == null)
            {
                continue;
            }

            if (damage > 0)
            {
                enemy.TakeDamage(damage);
            }

            if (enemy.currentHP <= 0 || enemy.IsDead || enemy.isBoss || knockback <= 0 || board == null)
            {
                continue;
            }

            TryKnockbackEnemy(enemy, player, board, knockback);
        }
    }

    private static List<Enemy> CollectAliveEnemies(BattleRuntimeContext context)
    {
        List<Enemy> results = new List<Enemy>();
        IReadOnlyList<Enemy> enemies = context.Enemies;
        if (enemies == null)
        {
            return results;
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (!context.IsAliveEnemy(enemy))
            {
                continue;
            }

            results.Add(enemy);
        }

        return results;
    }

    private static void TryKnockbackEnemy(Enemy enemy, Player player, Board board, int steps)
    {
        if (enemy == null || board == null || steps <= 0)
        {
            return;
        }

        for (int step = 0; step < steps; step++)
        {
            Vector2Int currentPos = enemy.gridPosition;
            Vector2Int preferredDirection = GetPreferredKnockbackDirection(player, currentPos);

            if (!TryPickAdjacentKnockbackTarget(currentPos, preferredDirection, board, player, out Vector2Int nextPos))
            {
                return;
            }

            enemy.MoveToPosition(nextPos);
            if (enemy.gridPosition != nextPos)
            {
                return;
            }
        }
    }

    private static Vector2Int GetPreferredKnockbackDirection(Player player, Vector2Int enemyPosition)
    {
        if (player == null)
        {
            return Vector2Int.zero;
        }

        return enemyPosition - player.position;
    }

    private static bool TryPickAdjacentKnockbackTarget(
        Vector2Int currentPos,
        Vector2Int preferredDirection,
        Board board,
        Player player,
        out Vector2Int targetPos)
    {
        targetPos = Vector2Int.zero;

        List<BoardTile> adjacentTiles = board.GetAdjacentTiles(currentPos);
        if (adjacentTiles == null || adjacentTiles.Count == 0)
        {
            return false;
        }

        Vector2 preferred = new Vector2(preferredDirection.x, preferredDirection.y);
        bool hasPreferredDirection = preferred.sqrMagnitude > 0.0001f;
        if (hasPreferredDirection)
        {
            preferred.Normalize();
        }

        bool found = false;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < adjacentTiles.Count; i++)
        {
            BoardTile tile = adjacentTiles[i];
            if (tile == null)
            {
                continue;
            }

            Vector2Int candidatePos = tile.gridPosition;
            if (board.IsTileOccupied(candidatePos))
            {
                continue;
            }

            if (player != null && player.position == candidatePos)
            {
                continue;
            }

            float score = 0f;
            if (hasPreferredDirection)
            {
                Vector2 candidateDirection = new Vector2(candidatePos.x - currentPos.x, candidatePos.y - currentPos.y);
                if (candidateDirection.sqrMagnitude > 0.0001f)
                {
                    candidateDirection.Normalize();
                    score = Vector2.Dot(preferred, candidateDirection);
                }
            }

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                targetPos = candidatePos;
            }
        }

        return found;
    }
}
