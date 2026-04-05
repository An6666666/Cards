using System.Collections.Generic;
using UnityEngine;

public readonly struct EnemyActionPlan
{
    public EnemyActionPlan(EnemyIntentType intentType, int intentValue, int score, Vector2Int targetPosition, bool hasTargetPosition)
    {
        IntentType = intentType;
        IntentValue = intentValue;
        Score = score;
        TargetPosition = targetPosition;
        HasTargetPosition = hasTargetPosition;
    }

    public EnemyIntentType IntentType { get; }
    public int IntentValue { get; }
    public int Score { get; }
    public Vector2Int TargetPosition { get; }
    public bool HasTargetPosition { get; }
}

public sealed class EnemyActionEvaluationContext
{
    public EnemyActionEvaluationContext(
        HashSet<Vector2Int> occupiedPositions,
        HashSet<Vector2Int> reservedThreatPositions = null)
    {
        OccupiedPositions = occupiedPositions;
        ReservedThreatPositions = reservedThreatPositions;
    }

    public HashSet<Vector2Int> OccupiedPositions { get; }
    public HashSet<Vector2Int> ReservedThreatPositions { get; }
}

/// <summary>
/// Centralized scoring for baseline enemy AI.
/// Enemies compare attack and candidate move positions instead of always moving to the closest tile.
/// </summary>
public static class EnemyActionScoringSystem
{
    private const int AttackBaseScore = 10000;
    private const int AttackDamageWeight = 100;
    private const int ImmediateThreatBonus = 450;
    private const int ThreatDistanceWeight = 120;
    private const int ProgressWeight = 90;
    private const int MobilityWeight = 18;
    private const int AllyCrowdingPenalty = 28;
    private const int BacktrackPenalty = 80;
    private const int LostPressurePenalty = 140;
    private const int UnreachableThreatPenalty = 300;
    private const int UniqueThreatSlotBonus = 220;
    private const int DefaultIdleScore = -1000;

    private static readonly List<Enemy> EnemyBuffer = new List<Enemy>(8);

    public static EnemyActionPlan Evaluate(
        Enemy enemy,
        Player player,
        Vector2Int? previousGridPosition = null,
        EnemyActionEvaluationContext evaluationContext = null)
    {
        if (enemy == null || player == null)
        {
            return CreateIdlePlan();
        }

        if (enemy.frozenTurns > 0)
        {
            return CreateIdlePlan();
        }

        if (enemy.Movement != null && enemy.Movement.CanAttackFrom(enemy.gridPosition, player.position))
        {
            int damage = enemy.CalculateAttackDamage();
            if (damage > 0)
            {
                int attackScore = AttackBaseScore + damage * AttackDamageWeight;
                if (evaluationContext != null &&
                    evaluationContext.ReservedThreatPositions != null &&
                    !evaluationContext.ReservedThreatPositions.Contains(enemy.gridPosition))
                {
                    attackScore += UniqueThreatSlotBonus;
                }

                return new EnemyActionPlan(
                    EnemyIntentType.Attack,
                    damage,
                    attackScore,
                    enemy.gridPosition,
                    hasTargetPosition: false);
            }
        }

        if (enemy.CanMoveThisTurn() &&
            TryEvaluateBestMove(enemy, player, previousGridPosition, out Vector2Int bestMove, out int moveScore, evaluationContext))
        {
            return new EnemyActionPlan(
                EnemyIntentType.Move,
                0,
                moveScore,
                bestMove,
                hasTargetPosition: true);
        }

        return CreateIdlePlan();
    }

    public static bool TryEvaluateBestMove(
        Enemy enemy,
        Player player,
        Vector2Int? previousGridPosition,
        out Vector2Int bestMove,
        out int bestScore,
        EnemyActionEvaluationContext evaluationContext = null)
    {
        bestMove = default;
        bestScore = int.MinValue;

        if (enemy == null || player == null)
        {
            return false;
        }

        Board board = ResolveBoard();
        if (board == null || enemy.Movement == null)
        {
            return false;
        }

        HashSet<Vector2Int> occupiedByEnemies = evaluationContext != null && evaluationContext.OccupiedPositions != null
            ? evaluationContext.OccupiedPositions
            : BuildEnemyOccupancy(enemy);
        int currentThreatDistance = ComputeThreatDistance(board, enemy, enemy.gridPosition, player.position, occupiedByEnemies);
        int currentDirectDistance = ComputeDirectStepDistance(board, enemy.gridPosition, player.position, occupiedByEnemies);
        bool found = false;

        List<BoardTile> adjacentTiles = board.GetAdjacentTiles(enemy.gridPosition);
        for (int i = 0; i < adjacentTiles.Count; i++)
        {
            BoardTile tile = adjacentTiles[i];
            if (tile == null)
            {
                continue;
            }

            Vector2Int candidate = tile.gridPosition;
            if (candidate == player.position || occupiedByEnemies.Contains(candidate))
            {
                continue;
            }

            int score = ScoreMoveCandidate(
                board,
                enemy,
                player,
                candidate,
                previousGridPosition,
                occupiedByEnemies,
                evaluationContext,
                currentThreatDistance,
                currentDirectDistance);

            if (!found || score > bestScore || (score == bestScore && IsBetterTieBreak(enemy, player, candidate, bestMove, occupiedByEnemies)))
            {
                bestMove = candidate;
                bestScore = score;
                found = true;
            }
        }

        return found;
    }

    private static int ScoreMoveCandidate(
        Board board,
        Enemy enemy,
        Player player,
        Vector2Int candidate,
        Vector2Int? previousGridPosition,
        HashSet<Vector2Int> occupiedByEnemies,
        EnemyActionEvaluationContext evaluationContext,
        int currentThreatDistance,
        int currentDirectDistance)
    {
        bool threatensPlayerImmediately = enemy.Movement.CanAttackFrom(candidate, player.position);
        int candidateThreatDistance = threatensPlayerImmediately
            ? 0
            : ComputeThreatDistance(board, enemy, candidate, player.position, occupiedByEnemies);
        int candidateDirectDistance = ComputeDirectStepDistance(board, candidate, player.position, occupiedByEnemies);
        int openNeighbors = CountOpenNeighbors(board, candidate, occupiedByEnemies, player.position);
        int adjacentAllies = CountAdjacentAllies(board, candidate, occupiedByEnemies);

        int score = 0;

        if (threatensPlayerImmediately)
        {
            score += ImmediateThreatBonus;

            if (evaluationContext != null &&
                evaluationContext.ReservedThreatPositions != null &&
                !evaluationContext.ReservedThreatPositions.Contains(candidate))
            {
                score += UniqueThreatSlotBonus;
            }
        }

        if (candidateThreatDistance == int.MaxValue)
        {
            score -= UnreachableThreatPenalty;
        }
        else
        {
            score -= candidateThreatDistance * ThreatDistanceWeight;
        }

        if (currentThreatDistance != int.MaxValue && candidateThreatDistance != int.MaxValue)
        {
            score += (currentThreatDistance - candidateThreatDistance) * ProgressWeight;
        }
        else if (currentThreatDistance == int.MaxValue && candidateThreatDistance != int.MaxValue)
        {
            score += ProgressWeight;
        }

        if (currentThreatDistance != int.MaxValue &&
            candidateThreatDistance != int.MaxValue &&
            candidateThreatDistance > currentThreatDistance)
        {
            score -= LostPressurePenalty;
        }

        if (currentDirectDistance != int.MaxValue && candidateDirectDistance != int.MaxValue)
        {
            score += (currentDirectDistance - candidateDirectDistance) * 24;
        }

        score += openNeighbors * MobilityWeight;
        score -= adjacentAllies * AllyCrowdingPenalty;

        if (previousGridPosition.HasValue && candidate == previousGridPosition.Value)
        {
            score -= BacktrackPenalty;
        }

        return score;
    }

    private static bool IsBetterTieBreak(
        Enemy enemy,
        Player player,
        Vector2Int candidate,
        Vector2Int currentBest,
        HashSet<Vector2Int> occupiedByEnemies)
    {
        Board board = ResolveBoard();
        if (board == null || enemy == null || player == null)
        {
            return false;
        }

        int candidateThreatDistance = ComputeThreatDistance(board, enemy, candidate, player.position, occupiedByEnemies);
        int bestThreatDistance = ComputeThreatDistance(board, enemy, currentBest, player.position, occupiedByEnemies);
        if (candidateThreatDistance != bestThreatDistance)
        {
            return candidateThreatDistance < bestThreatDistance;
        }

        int candidateOpenNeighbors = CountOpenNeighbors(board, candidate, occupiedByEnemies, player.position);
        int bestOpenNeighbors = CountOpenNeighbors(board, currentBest, occupiedByEnemies, player.position);
        if (candidateOpenNeighbors != bestOpenNeighbors)
        {
            return candidateOpenNeighbors > bestOpenNeighbors;
        }

        return Vector2Int.Distance(candidate, player.position) < Vector2Int.Distance(currentBest, player.position);
    }

    private static int CountOpenNeighbors(Board board, Vector2Int origin, HashSet<Vector2Int> occupiedByEnemies, Vector2Int playerPosition)
    {
        int count = 0;
        List<BoardTile> adjacentTiles = board.GetAdjacentTiles(origin);
        for (int i = 0; i < adjacentTiles.Count; i++)
        {
            BoardTile tile = adjacentTiles[i];
            if (tile == null)
            {
                continue;
            }

            Vector2Int pos = tile.gridPosition;
            if (pos == playerPosition || occupiedByEnemies.Contains(pos))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static int CountAdjacentAllies(Board board, Vector2Int origin, HashSet<Vector2Int> occupiedByEnemies)
    {
        int count = 0;
        List<BoardTile> adjacentTiles = board.GetAdjacentTiles(origin);
        for (int i = 0; i < adjacentTiles.Count; i++)
        {
            BoardTile tile = adjacentTiles[i];
            if (tile != null && occupiedByEnemies.Contains(tile.gridPosition))
            {
                count++;
            }
        }

        return count;
    }

    private static int ComputeThreatDistance(Board board, Enemy enemy, Vector2Int start, Vector2Int playerPosition, HashSet<Vector2Int> occupiedByEnemies)
    {
        if (enemy.Movement == null)
        {
            return int.MaxValue;
        }

        if (enemy.Movement.CanAttackFrom(start, playerPosition))
        {
            return 0;
        }

        Queue<(Vector2Int pos, int dist)> pending = new Queue<(Vector2Int pos, int dist)>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int> { start };
        pending.Enqueue((start, 0));

        while (pending.Count > 0)
        {
            (Vector2Int currentPos, int currentDistance) = pending.Dequeue();
            List<BoardTile> adjacentTiles = board.GetAdjacentTiles(currentPos);

            for (int i = 0; i < adjacentTiles.Count; i++)
            {
                BoardTile tile = adjacentTiles[i];
                if (tile == null)
                {
                    continue;
                }

                Vector2Int next = tile.gridPosition;
                if (!visited.Add(next) || occupiedByEnemies.Contains(next) || next == playerPosition)
                {
                    continue;
                }

                int nextDistance = currentDistance + 1;
                if (enemy.Movement.CanAttackFrom(next, playerPosition))
                {
                    return nextDistance;
                }

                pending.Enqueue((next, nextDistance));
            }
        }

        return int.MaxValue;
    }

    private static int ComputeDirectStepDistance(Board board, Vector2Int start, Vector2Int playerPosition, HashSet<Vector2Int> occupiedByEnemies)
    {
        Queue<(Vector2Int pos, int dist)> pending = new Queue<(Vector2Int pos, int dist)>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int> { start };
        pending.Enqueue((start, 0));

        while (pending.Count > 0)
        {
            (Vector2Int currentPos, int currentDistance) = pending.Dequeue();
            List<BoardTile> adjacentTiles = board.GetAdjacentTiles(currentPos);

            for (int i = 0; i < adjacentTiles.Count; i++)
            {
                BoardTile tile = adjacentTiles[i];
                if (tile == null)
                {
                    continue;
                }

                Vector2Int next = tile.gridPosition;
                if (next == playerPosition)
                {
                    return currentDistance + 1;
                }

                if (!visited.Add(next) || occupiedByEnemies.Contains(next))
                {
                    continue;
                }

                pending.Enqueue((next, currentDistance + 1));
            }
        }

        return int.MaxValue;
    }

    private static HashSet<Vector2Int> BuildEnemyOccupancy(Enemy self)
    {
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        BattleRuntimeContext context = BattleRuntimeContext.Active;

        if (context != null)
        {
            IReadOnlyList<Enemy> enemies = context.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || enemy == self || !context.IsAliveEnemy(enemy))
                {
                    continue;
                }

                occupied.Add(enemy.gridPosition);
            }

            return occupied;
        }

        Enemy.FillActiveEnemies(EnemyBuffer);
        for (int i = 0; i < EnemyBuffer.Count; i++)
        {
            Enemy enemy = EnemyBuffer[i];
            if (enemy == null || enemy == self || enemy.currentHP <= 0 || enemy.IsDead)
            {
                continue;
            }

            occupied.Add(enemy.gridPosition);
        }

        return occupied;
    }

    private static Board ResolveBoard()
    {
        BattleRuntimeContext context = BattleRuntimeContext.Active;
        if (context != null)
        {
            return context.Board;
        }

        return Object.FindObjectOfType<Board>();
    }

    private static EnemyActionPlan CreateIdlePlan()
    {
        return new EnemyActionPlan(
            EnemyIntentType.Idle,
            0,
            DefaultIdleScore,
            Vector2Int.zero,
            hasTargetPosition: false);
    }
}
