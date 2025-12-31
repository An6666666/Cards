using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class EnemyMovement : MonoBehaviour
{
    private Enemy enemy;

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
        foreach (var off in enemy.attackRangeOffsets)
        {
            if (enemy.gridPosition + off == player.position) return true;
        }
        return false;
    }

    public virtual void MoveOneStepTowards(Player player)
    {
        Board board = FindObjectOfType<Board>();
        if (board == null) return;
        var adjs = board.GetAdjacentTiles(enemy.gridPosition);
        Vector2Int bestPos = enemy.gridPosition;
        float bestDist = Vector2Int.Distance(enemy.gridPosition, player.position);
        foreach (var t in adjs)
        {
            Vector2Int pos = t.gridPosition;
            if (board.IsTileOccupied(pos)) continue;
            if (player.position == pos) continue;
            float d = Vector2Int.Distance(pos, player.position);
            if (d < bestDist)
            {
                bestDist = d;
                bestPos = pos;
            }
        }
        if (bestPos != enemy.gridPosition)
            MoveToPosition(bestPos);
    }

    public void MoveToPosition(Vector2Int targetGridPos)
    {
        Board board = FindObjectOfType<Board>();
        if (board == null) return;
        BoardTile tile = board.GetTileAt(targetGridPos);
        if (tile == null) return;
        if (board.IsTileOccupied(targetGridPos)) return;
        Player p = FindObjectOfType<Player>();
        if (p != null && p.position == targetGridPos) return;

        enemy.gridPosition = targetGridPos;

        enemy.Visual.SetMoveBool(true);

        enemy.transform.DOMove(tile.transform.position, 0.2f)
            .SetEase(Ease.Linear)
            .OnUpdate(() =>
            {
                enemy.Sorting.UpdateNow();
            })
            .OnComplete(() =>
            {
                enemy.Visual.SetMoveBool(false);
                enemy.Sorting.UpdateNow();
                enemy.Visual.CaptureSpriteDefaults();
            });
    }
}
