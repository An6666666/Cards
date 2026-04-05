using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 星芒破陣：
/// 移動 2 格，對路徑上的妖怪造成傷害，並將存活目標擊退。
/// </summary>
[CreateAssetMenu(fileName = "Move_XingMangPoZhen", menuName = "Cards/Movement/星芒破陣")]
public class Move_XingMangPoZhen : MovementCardBase
{
    private static readonly Vector2Int[] HexDirections =
    {
        new Vector2Int(2, 0),
        new Vector2Int(-2, 0),
        new Vector2Int(-1, -2),
        new Vector2Int(1, -2),
        new Vector2Int(-1, 2),
        new Vector2Int(1, 2)
    };

    [Min(1)] public int moveSteps = 2;
    [Min(0)] public int pathDamage = 5;
    [Min(0)] public int knockbackSteps = 1;

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
    }

    private void OnEnable()
    {
        cardType = CardType.Movement;

        if (cost <= 0)
        {
            cost = 1;
        }

        RebuildRangeOffsets();
    }

    private void OnValidate()
    {
        RebuildRangeOffsets();
    }

    private void RebuildRangeOffsets()
    {
        moveSteps = Mathf.Max(1, moveSteps);
        pathDamage = Mathf.Max(0, pathDamage);
        knockbackSteps = Mathf.Max(0, knockbackSteps);

        List<Vector2Int> offsets = new List<Vector2Int>(HexDirections.Length * moveSteps);
        for (int i = 0; i < HexDirections.Length; i++)
        {
            for (int step = 1; step <= moveSteps; step++)
            {
                offsets.Add(HexDirections[i] * step);
            }
        }

        rangeOffsets = offsets;
    }

    public override void ExecuteOnPosition(Player player, Vector2Int targetGridPos)
    {
        if (player == null)
        {
            return;
        }

        Vector2Int startPos = player.position;
        if (!TryGetDashDirection(startPos, targetGridPos, out Vector2Int dashDirection, out int dashSteps))
        {
            return;
        }

        Board board = BattleRuntimeContext.Active?.Board ?? Object.FindObjectOfType<Board>();
        IReadOnlyList<Enemy> enemies = BattleRuntimeContext.Active?.Enemies;
        List<Enemy> hitEnemies = CollectPathEnemies(startPos, dashDirection, dashSteps, enemies);

        player.MoveToPosition(targetGridPos, allowOccupiedTileRelic: true);
        if (player.position != targetGridPos)
        {
            return;
        }

        for (int i = 0; i < hitEnemies.Count; i++)
        {
            Enemy hitEnemy = hitEnemies[i];
            if (hitEnemy == null || hitEnemy.currentHP <= 0)
            {
                continue;
            }

            hitEnemy.TakeDamage(pathDamage);

            if (hitEnemy.currentHP > 0 && knockbackSteps > 0)
            {
                TryKnockbackEnemy(hitEnemy, dashDirection, knockbackSteps, board, player);
            }
        }
    }

    private bool TryGetDashDirection(
        Vector2Int startPos,
        Vector2Int targetPos,
        out Vector2Int dashDirection,
        out int dashSteps)
    {
        for (int i = 0; i < HexDirections.Length; i++)
        {
            Vector2Int dir = HexDirections[i];
            for (int step = 1; step <= moveSteps; step++)
            {
                if (startPos + dir * step == targetPos)
                {
                    dashDirection = dir;
                    dashSteps = step;
                    return true;
                }
            }
        }

        dashDirection = Vector2Int.zero;
        dashSteps = 0;
        return false;
    }

    private List<Enemy> CollectPathEnemies(
        Vector2Int startPos,
        Vector2Int dashDirection,
        int dashSteps,
        IReadOnlyList<Enemy> enemies)
    {
        List<Enemy> results = new List<Enemy>();
        if (enemies == null || enemies.Count == 0)
        {
            return results;
        }

        HashSet<Enemy> unique = new HashSet<Enemy>();
        for (int step = 1; step <= Mathf.Max(0, dashSteps); step++)
        {
            Vector2Int checkPos = startPos + dashDirection * step;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || enemy.currentHP <= 0)
                {
                    continue;
                }

                if (enemy.gridPosition == checkPos && unique.Add(enemy))
                {
                    results.Add(enemy);
                }
            }
        }

        return results;
    }

    private static void TryKnockbackEnemy(
        Enemy enemy,
        Vector2Int dashDirection,
        int steps,
        Board board,
        Player player)
    {
        if (enemy == null || board == null || steps <= 0)
        {
            return;
        }

        for (int step = 0; step < steps; step++)
        {
            Vector2Int currentPos = enemy.gridPosition;
            if (!TryPickAdjacentKnockbackTarget(currentPos, dashDirection, board, player, out Vector2Int nextPos))
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
