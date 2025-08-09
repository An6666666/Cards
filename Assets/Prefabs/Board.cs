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

    // 檢查指定格子是否有敵人佔據
    public bool IsTileOccupied(Vector2Int pos)
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (var e in enemies)
        {
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
    
    // 取得所有格子的座標列表
    public List<Vector2Int> GetAllPositions()
    {
        return new List<Vector2Int>(tileDict.Keys);
    }
}