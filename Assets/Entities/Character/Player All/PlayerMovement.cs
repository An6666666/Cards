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

    public void MoveToPosition(Vector2Int targetGridPos)
    {
        if (!buffController.CanMove())
        {
            Debug.Log("Cannot move: movement is currently restricted.");
            return;
        }

        Board board = FindObjectOfType<Board>();
        if (board == null)
        {
            Debug.LogWarning("Board not found!");
            return;
        }

        if (board.IsTileOccupied(targetGridPos))
        {
            Debug.Log("Cannot move: tile occupied by enemy.");
            return;
        }

        position = targetGridPos;

        BoardTile tile = board.GetTileAt(targetGridPos);
        if (tile == null)
        {
            Debug.LogWarning($"No tile at {targetGridPos}");
            return;
        }

        transform.position = tile.transform.position;

        tile.HandlePlayerEntered(GetComponent<Player>());
    }

    public void TeleportToPosition(Vector2Int targetPos)
    {
        if (!buffController.CanMove())
        {
            Debug.Log("Cannot teleport: movement is currently restricted.");
            return;
        }

        Board board = FindObjectOfType<Board>();
        if (board != null && board.IsTileOccupied(targetPos))
        {
            Debug.Log("Cannot teleport: tile occupied by enemy.");
            return;
        }

        position = targetPos;
        transform.position = new Vector3(targetPos.x, targetPos.y, 0f);

        BoardTile tile = board != null ? board.GetTileAt(targetPos) : null;
        tile?.HandlePlayerEntered(GetComponent<Player>());
    }
}