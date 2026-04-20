using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerBuffController))]
public class PlayerMovement : MonoBehaviour
{
    public Vector2Int position = new Vector2Int(0, 0);

    private PlayerBuffController buffController;

    private void Awake()
    {
        buffController = GetComponent<PlayerBuffController>();
    }

    public bool CanMoveToPosition(Vector2Int targetGridPos, bool allowOccupiedTileRelic = false)
    {
        Board board = ResolveBoard();
        if (board == null)
        {
            return false;
        }

        BoardTile tile = board.GetTileAt(targetGridPos);
        if (tile == null)
        {
            return false;
        }

        if (!board.IsTileOccupied(targetGridPos))
        {
            return true;
        }

        return allowOccupiedTileRelic && CanDisplaceEnemyFromTile(targetGridPos, board);
    }

    public void MoveToPosition(Vector2Int targetGridPos, bool allowOccupiedTileRelic = false)
    {
        if (!buffController.CanMove())
        {
            Debug.Log("Cannot move: movement is currently restricted.");
            return;
        }

        Board board = ResolveBoard();
        if (board == null)
        {
            Debug.LogWarning("Board not found!");
            return;
        }

        if (!PrepareDestinationTile(targetGridPos, board, allowOccupiedTileRelic, "move"))
        {
            return;
        }

        BoardTile tile = board.GetTileAt(targetGridPos);
        if (tile == null)
        {
            Debug.LogWarning($"No tile at {targetGridPos}");
            return;
        }

        position = targetGridPos;

        StopAllCoroutines();
        StartCoroutine(MoveRoutine(tile.transform.position, 0.2f, tile));
    }

    private IEnumerator MoveRoutine(Vector3 targetWorldPos, float duration, BoardTile tile)
    {
        Player player = GetComponent<Player>();
        player?.SetMovingAnim(true);

        Vector3 start = transform.position;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            transform.position = Vector3.Lerp(start, targetWorldPos, t);
            yield return null;
        }

        transform.position = targetWorldPos;
        player?.SetMovingAnim(false);

        tile?.HandlePlayerEntered(player);
    }

    public void TeleportToPosition(Vector2Int targetPos, bool allowOccupiedTileRelic = false)
    {
        if (!buffController.CanMove())
        {
            Debug.Log("Cannot teleport: movement is currently restricted.");
            return;
        }

        Board board = ResolveBoard();
        if (board == null)
        {
            Debug.LogWarning("Board not found!");
            return;
        }

        if (!PrepareDestinationTile(targetPos, board, allowOccupiedTileRelic, "teleport"))
        {
            return;
        }

        BoardTile tile = board.GetTileAt(targetPos);
        if (tile == null)
        {
            Debug.LogWarning($"No tile at {targetPos}");
            return;
        }

        position = targetPos;
        StopAllCoroutines();
        StartCoroutine(TeleportRoutine(tile.transform.position, tile));
    }

    private IEnumerator TeleportRoutine(Vector3 targetWorldPos, BoardTile tile)
    {
        yield return null;

        transform.position = targetWorldPos;

        Player player = GetComponent<Player>();
        player?.SetMovingAnim(false);
        player?.FinishTeleportVisual();

        tile?.HandlePlayerEntered(player);
    }

    private bool PrepareDestinationTile(Vector2Int targetGridPos, Board board, bool allowOccupiedTileRelic, string actionName)
    {
        if (!board.IsTileOccupied(targetGridPos))
        {
            return true;
        }

        if (!allowOccupiedTileRelic || !TryDisplaceEnemyFromTile(targetGridPos, board))
        {
            Debug.Log($"Cannot {actionName}: tile occupied by enemy.");
            return false;
        }

        return true;
    }

    private bool CanDisplaceEnemyFromTile(Vector2Int occupiedPos, Board board)
    {
        int knockbackSteps = GetTongQianJianKnockbackSteps();
        if (knockbackSteps <= 0)
        {
            return false;
        }

        Enemy occupyingEnemy = FindAliveEnemyAt(occupiedPos);
        if (occupyingEnemy == null)
        {
            return false;
        }

        return TryFindEnemyDisplacementTarget(occupiedPos, board, out _);
    }

    private bool TryDisplaceEnemyFromTile(Vector2Int occupiedPos, Board board)
    {
        int knockbackSteps = GetTongQianJianKnockbackSteps();
        if (knockbackSteps <= 0)
        {
            return false;
        }

        Enemy occupyingEnemy = FindAliveEnemyAt(occupiedPos);
        if (occupyingEnemy == null)
        {
            return false;
        }

        bool moved = false;
        for (int step = 0; step < knockbackSteps; step++)
        {
            if (!TryFindEnemyDisplacementTarget(occupyingEnemy.gridPosition, board, out Vector2Int knockbackTarget))
            {
                break;
            }

            occupyingEnemy.MoveToPosition(knockbackTarget);
            if (occupyingEnemy.gridPosition != knockbackTarget)
            {
                break;
            }

            moved = true;
        }

        return moved;
    }

    private bool TryFindEnemyDisplacementTarget(Vector2Int occupiedPos, Board board, out Vector2Int targetPos)
    {
        targetPos = Vector2Int.zero;

        List<BoardTile> adjacentTiles = board.GetAdjacentTiles(occupiedPos);
        if (adjacentTiles == null || adjacentTiles.Count == 0)
        {
            return false;
        }

        Vector2 preferred = new Vector2(occupiedPos.x - position.x, occupiedPos.y - position.y);
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
            if (board.IsTileOccupied(candidatePos) || candidatePos == position)
            {
                continue;
            }

            float score = 0f;
            if (hasPreferredDirection)
            {
                Vector2 candidateDirection = new Vector2(candidatePos.x - occupiedPos.x, candidatePos.y - occupiedPos.y);
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

    private int GetTongQianJianKnockbackSteps()
    {
        Player player = GetComponent<Player>();
        if (player == null)
        {
            return 0;
        }

        return Mathf.Max(0, player.GetRelicCount<Relic_TongQianJian>());
    }

    private static Enemy FindAliveEnemyAt(Vector2Int pos)
    {
        IReadOnlyList<Enemy> enemies = BattleRuntimeContext.Active?.Enemies;
        if (enemies == null)
        {
            return null;
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == null || enemy.currentHP <= 0 || enemy.IsDead)
            {
                continue;
            }

            if (enemy.gridPosition == pos)
            {
                return enemy;
            }
        }

        return null;
    }

    private static Board ResolveBoard()
    {
        return BattleRuntimeContext.Active?.Board ?? FindObjectOfType<Board>();
    }
}
