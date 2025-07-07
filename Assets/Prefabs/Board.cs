using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// �޲z�Ҧ� BoardTile�A�ô��Ѭd�ߥ\��
/// </summary>
public class Board : MonoBehaviour
{
    // ��Ҧ� Tile �� (x,y) ������
    private Dictionary<Vector2Int, BoardTile> tileDict = new Dictionary<Vector2Int, BoardTile>();
  
    private void Awake()
    {
        // �۰ʦ����l���󤤪� BoardTile
        BoardTile[] tiles = GetComponentsInChildren<BoardTile>();
        foreach (var t in tiles)
        {
            tileDict[t.gridPosition] = t;
        }
    }

    /// <summary>
    /// �̮y�Ш��^ BoardTile�F�Y�L�^�� null
    /// </summary>
    public BoardTile GetTileAt(Vector2Int pos)
    {
        tileDict.TryGetValue(pos, out BoardTile tile);
        return tile;
    }

    /// <summary>
    /// ��Ҧ� Tile �����G�P�i�I������
    /// </summary>
    public void ResetAllTilesSelectable()
    {
        foreach (var kv in tileDict)
        {
            kv.Value.SetSelectable(false);
        }
    }

    // ���o�F��|��
    public List<BoardTile> GetAdjacentTiles(Vector2Int pos)
    {
        List<BoardTile> result = new List<BoardTile>();
        Vector2Int[] offs = { new Vector2Int(4, 0), new Vector2Int(-4, 0), new Vector2Int(-2, -4), new Vector2Int(2, -4), new Vector2Int(-2, 4), new Vector2Int(2, 4) };
        foreach (var o in offs)
        {
            BoardTile t = GetTileAt(pos + o);
            if (t != null) result.Add(t);
        }
        return result;
    }
}
