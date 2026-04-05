using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private Enemy enemy;
    private Tween moveTween;
    private Vector2Int? previousGridPosition;

    public void Init(Enemy owner)
    {
        enemy = owner;
    }

    public Vector2Int GridPosition
    {
        get => enemy.gridPosition;
        set => enemy.gridPosition = value;
    }

    public List<Vector2Int> AttackRangeOffsets => enemy.attackRangeOffsets;
    public Vector2Int? PreviousGridPosition => previousGridPosition;
    public bool IsMoving => moveTween != null && moveTween.IsActive();

    public virtual bool IsPlayerInRange(Player player)
    {
        return player != null && CanAttackFrom(enemy.gridPosition, player.position);
    }

    public bool CanAttackFrom(Vector2Int origin, Vector2Int target)
    {
        List<Vector2Int> offsets = AttackRangeOffsets;
        for (int i = 0; i < offsets.Count; i++)
        {
            if (origin + offsets[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    public virtual void MoveOneStepTowards(Player player)
    {
        if (enemy == null || player == null)
        {
            return;
        }

        if (EnemyActionScoringSystem.TryEvaluateBestMove(enemy, player, previousGridPosition, out Vector2Int bestPos, out _))
        {
            MoveToPosition(bestPos);
        }
    }

    public void MoveToPosition(Vector2Int targetGridPos)
    {
        Board board = ResolveBoard();
        if (board == null)
        {
            return;
        }

        BoardTile tile = board.GetTileAt(targetGridPos);
        if (tile == null || board.IsTileOccupied(targetGridPos))
        {
            return;
        }

        Player player = ResolvePlayer();
        if (player != null && player.position == targetGridPos)
        {
            return;
        }

        previousGridPosition = enemy.gridPosition;
        enemy.gridPosition = targetGridPos;
        enemy.Visual.SetMoveBool(true);

        // Kill previous move tween first to avoid overlapping movement animations.
        if (moveTween != null && moveTween.IsActive())
        {
            moveTween.Kill(false);
            moveTween = null;
        }

        moveTween = enemy.transform.DOMove(tile.transform.position, 0.2f)
            .SetEase(Ease.Linear)
            .OnUpdate(() => enemy.Sorting.UpdateNow())
            .OnComplete(() =>
            {
                moveTween = null;
                enemy.Visual.SetMoveBool(false);
                enemy.Sorting.UpdateNow();
                // Do not overwrite sprite defaults here; just snap visual back to baseline.
                enemy.Visual.ResetSpriteVisual();
            });
    }

    private static Board ResolveBoard()
    {
        BattleRuntimeContext context = BattleRuntimeContext.Active;
        return context != null ? context.Board : null;
    }

    private static Player ResolvePlayer()
    {
        BattleRuntimeContext context = BattleRuntimeContext.Active;
        return context != null ? context.Player : null;
    }
}
