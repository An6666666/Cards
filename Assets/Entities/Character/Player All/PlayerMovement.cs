using System.Collections;
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

    BoardTile tile = board.GetTileAt(targetGridPos);
    if (tile == null)
    {
        Debug.LogWarning($"No tile at {targetGridPos}");
        return;
    }

    // 更新格子位置（你要保守可以放到到位後再更新，這裡先沿用你原本）
    position = targetGridPos;

    // 平移 + Move動畫
    StopAllCoroutines();
    StartCoroutine(MoveRoutine(tile.transform.position, 0.2f, tile));
}

private IEnumerator MoveRoutine(Vector3 targetWorldPos, float duration, BoardTile tile)
{
    Player p = GetComponent<Player>();
    p?.SetMovingAnim(true);

    Vector3 start = transform.position;
    float t = 0f;

    while (t < 1f)
    {
        t += Time.deltaTime / Mathf.Max(0.0001f, duration);
        transform.position = Vector3.Lerp(start, targetWorldPos, t);
        yield return null;
    }

    transform.position = targetWorldPos;
    p?.SetMovingAnim(false);

    tile?.HandlePlayerEntered(p);
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