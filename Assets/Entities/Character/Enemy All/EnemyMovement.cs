using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private Enemy enemy;
    private Tween moveTween;

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

    public virtual bool IsPlayerInRange(Player player)
    {
        foreach (Vector2Int off in enemy.attackRangeOffsets)
        {
            if (enemy.gridPosition + off == player.position) return true;
        }

        return false;
    }

    public virtual void MoveOneStepTowards(Player player)
    {
        Board board = ResolveBoard();
        if (board == null || player == null)
        {
            return;
        }

        List<BoardTile> adjacentTiles = board.GetAdjacentTiles(enemy.gridPosition);
        Vector2Int bestPos = enemy.gridPosition;
        float bestDistance = Vector2Int.Distance(enemy.gridPosition, player.position);

        for (int i = 0; i < adjacentTiles.Count; i++)
        {
            BoardTile tile = adjacentTiles[i];
            Vector2Int pos = tile.gridPosition;
            if (board.IsTileOccupied(pos) || player.position == pos)
            {
                continue;
            }

            float distance = Vector2Int.Distance(pos, player.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPos = pos;
            }
        }

        if (bestPos != enemy.gridPosition)
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
