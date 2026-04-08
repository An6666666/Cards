using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 棋盤管理，儲存 BoardTile 字典
/// </summary>
public class Board : MonoBehaviour
{
    private Dictionary<Vector2Int, BoardTile> tileDict = new Dictionary<Vector2Int, BoardTile>();  // 座標->格子映射

    private void Awake()
    {
        // 讀取所有子物件的 BoardTile 並加入字典
        BoardTile[] tiles = GetComponentsInChildren<BoardTile>();
        foreach (var t in tiles) tileDict[t.gridPosition] = t;
    }

    public BoardTile GetTileAt(Vector2Int pos) // 根據座標取得格子
    {
        tileDict.TryGetValue(pos, out BoardTile tile);
        return tile;
    }

    public void ResetAllTilesSelectable()     // 重置所有格子可選
    {
        foreach (var kv in tileDict) kv.Value.SetSelectable(false);
    }
    public void ClearAllTileEffects()         // 清除所有格子的持續效果
    {
        foreach (var kv in tileDict)
        {
            kv.Value.ClearTileEffects();
        }
    }
    // 檢查指定格子是否有敵人佔據
    public bool IsTileOccupied(Vector2Int pos)
    {
        IReadOnlyList<Enemy> enemies = BattleRuntimeContext.Active?.Enemies;
        if (enemies == null) return false;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e.gridPosition == pos) return true;
        }
        return false;
    }

    public List<BoardTile> GetAdjacentTiles(Vector2Int pos) // 取得相鄰格子
    {
        List<BoardTile> result = new List<BoardTile>();
        Vector2Int[] offs = { new Vector2Int(2, 0), new Vector2Int(-2, 0), new Vector2Int(-1, -2), new Vector2Int(1, -2), new Vector2Int(-1, 2), new Vector2Int(1, 2) };
        foreach (var o in offs)
        {
            var t = GetTileAt(pos + o);
            if (t != null) result.Add(t);
        }
        return result;
    }
    
    public IEnumerator PlayYingGeSkillFxSequence(IEnumerable<BoardTile> targetTiles, float columnInterval = 0.25f, float tileVisibleDuration = 0.5f)
    {
        if (targetTiles == null)
        {
            yield break;
        }

        Dictionary<float, List<BoardTile>> columns = new Dictionary<float, List<BoardTile>>();
        foreach (BoardTile tile in targetTiles)
        {
            if (tile == null)
            {
                continue;
            }

            float columnX = Mathf.Round(tile.transform.position.x * 100f) / 100f;
            if (!columns.TryGetValue(columnX, out List<BoardTile> columnTiles))
            {
                columnTiles = new List<BoardTile>();
                columns.Add(columnX, columnTiles);
            }

            if (!columnTiles.Contains(tile))
            {
                columnTiles.Add(tile);
            }
        }

        if (columns.Count == 0)
        {
            yield break;
        }

        List<float> orderedColumns = new List<float>(columns.Keys);
        orderedColumns.Sort((left, right) => right.CompareTo(left));

        float interval = Mathf.Max(0f, columnInterval);
        float visibleDuration = Mathf.Max(0f, tileVisibleDuration);

        for (int i = 0; i < orderedColumns.Count; i++)
        {
            List<BoardTile> columnTiles = columns[orderedColumns[i]];
            for (int j = 0; j < columnTiles.Count; j++)
            {
                columnTiles[j].PlayYingGeSkillFx(visibleDuration);
            }

            if (i < orderedColumns.Count - 1 && interval > 0f)
            {
                yield return new WaitForSeconds(interval);
            }
        }

        float tailDelay = Mathf.Max(0f, visibleDuration - interval);
        if (tailDelay > 0f)
        {
            yield return new WaitForSeconds(tailDelay);
        }
    }

    // 取得所有格子的座標列表
    public List<Vector2Int> GetAllPositions()
    {
        return new List<Vector2Int>(tileDict.Keys);
    }
}
