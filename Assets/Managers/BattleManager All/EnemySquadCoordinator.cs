using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared tactical planner for baseline enemies.
/// It greedily assigns the best coordinated action among remaining allies,
/// reserving positions so later enemies adapt to the team's developing formation.
/// </summary>
public sealed class EnemySquadCoordinator
{
    private readonly BattleRuntimeContext context;
    private readonly Dictionary<Enemy, EnemyActionPlan> executionPlans = new Dictionary<Enemy, EnemyActionPlan>();
    private readonly List<Enemy> remainingEnemies = new List<Enemy>(8);
    private readonly HashSet<Vector2Int> simulatedOccupied = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> reservedThreatPositions = new HashSet<Vector2Int>();

    public EnemySquadCoordinator(BattleRuntimeContext context)
    {
        this.context = context;
    }

    public bool TryGetExecutionPlan(Enemy enemy, out EnemyActionPlan plan)
    {
        if (enemy != null && executionPlans.TryGetValue(enemy, out plan))
        {
            return true;
        }

        plan = default;
        return false;
    }

    public void ClearExecutionPlans()
    {
        executionPlans.Clear();
        remainingEnemies.Clear();
        simulatedOccupied.Clear();
        reservedThreatPositions.Clear();
    }

    public void RebuildExecutionPlans(Player player, IReadOnlyList<Enemy> orderedEnemies, int startIndex = 0)
    {
        ClearExecutionPlans();
        if (context == null || player == null || orderedEnemies == null)
        {
            return;
        }

        BuildInitialOccupiedSet();
        CollectEligibleEnemies(orderedEnemies, startIndex);

        while (remainingEnemies.Count > 0)
        {
            Enemy bestEnemy = null;
            EnemyActionPlan bestPlan = default;
            bool foundPlan = false;

            for (int i = 0; i < remainingEnemies.Count; i++)
            {
                Enemy candidate = remainingEnemies[i];
                if (candidate == null)
                {
                    continue;
                }

                simulatedOccupied.Remove(candidate.gridPosition);

                EnemyActionEvaluationContext evaluationContext =
                    new EnemyActionEvaluationContext(simulatedOccupied, reservedThreatPositions);
                EnemyActionPlan candidatePlan = EnemyActionScoringSystem.Evaluate(
                    candidate,
                    player,
                    candidate.Movement != null ? candidate.Movement.PreviousGridPosition : null,
                    evaluationContext);

                simulatedOccupied.Add(candidate.gridPosition);

                if (!foundPlan || IsBetterPlan(candidate, candidatePlan, bestEnemy, bestPlan, player))
                {
                    bestEnemy = candidate;
                    bestPlan = candidatePlan;
                    foundPlan = true;
                }
            }

            if (!foundPlan || bestEnemy == null)
            {
                break;
            }

            executionPlans[bestEnemy] = bestPlan;
            ReserveAssignedOutcome(bestEnemy, bestPlan, player);
            remainingEnemies.Remove(bestEnemy);
        }
    }

    private void BuildInitialOccupiedSet()
    {
        IReadOnlyList<Enemy> enemies = context.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (context.IsAliveEnemy(enemy))
            {
                simulatedOccupied.Add(enemy.gridPosition);
            }
        }
    }

    private void CollectEligibleEnemies(IReadOnlyList<Enemy> orderedEnemies, int startIndex)
    {
        int begin = Mathf.Max(0, startIndex);
        for (int i = begin; i < orderedEnemies.Count; i++)
        {
            Enemy enemy = orderedEnemies[i];
            if (enemy == null || !context.IsAliveEnemy(enemy) || !enemy.CanUseSharedSquadTactics)
            {
                continue;
            }

            remainingEnemies.Add(enemy);
        }
    }

    private void ReserveAssignedOutcome(Enemy enemy, EnemyActionPlan plan, Player player)
    {
        simulatedOccupied.Remove(enemy.gridPosition);

        Vector2Int occupiedPosition = enemy.gridPosition;
        if (plan.IntentType == EnemyIntentType.Move && plan.HasTargetPosition)
        {
            occupiedPosition = plan.TargetPosition;
        }

        simulatedOccupied.Add(occupiedPosition);

        bool threatensPlayer =
            plan.IntentType == EnemyIntentType.Attack ||
            (plan.IntentType == EnemyIntentType.Move &&
             plan.HasTargetPosition &&
             enemy.Movement != null &&
             enemy.Movement.CanAttackFrom(plan.TargetPosition, player.position));

        if (threatensPlayer)
        {
            reservedThreatPositions.Add(occupiedPosition);
        }
    }

    private static bool IsBetterPlan(
        Enemy candidateEnemy,
        EnemyActionPlan candidatePlan,
        Enemy currentBestEnemy,
        EnemyActionPlan currentBestPlan,
        Player player)
    {
        if (candidatePlan.Score != currentBestPlan.Score)
        {
            return candidatePlan.Score > currentBestPlan.Score;
        }

        if (candidatePlan.IntentType != currentBestPlan.IntentType)
        {
            return candidatePlan.IntentType == EnemyIntentType.Attack;
        }

        int candidateDamage = candidatePlan.IntentValue;
        int bestDamage = currentBestPlan.IntentValue;
        if (candidateDamage != bestDamage)
        {
            return candidateDamage > bestDamage;
        }

        if (candidateEnemy == null || currentBestEnemy == null || player == null)
        {
            return false;
        }

        return Vector2Int.Distance(candidateEnemy.gridPosition, player.position) <
               Vector2Int.Distance(currentBestEnemy.gridPosition, player.position);
    }
}
