using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 隨風步（1費）：
/// 移動 1 格，成功後獲得護甲。
/// </summary>
[CreateAssetMenu(fileName = "Move_SuiFengBu", menuName = "Cards/Movement/隨風步")]
public class Move_SuiFengBu : MovementCardBase
{
    // 六角棋盤的一步方向。
    private static readonly Vector2Int[] HexDirections =
    {
        new Vector2Int(2, 0),
        new Vector2Int(-2, 0),
        new Vector2Int(-1, -2),
        new Vector2Int(1, -2),
        new Vector2Int(-1, 2),
        new Vector2Int(1, 2)
    };

    [Min(0)] public int blockGain = 4;

    private void OnEnable()
    {
        cardType = CardType.Movement;

        if (cost <= 0)
        {
            cost = 1;
        }

        RebuildRangeOffsets();
    }

    private void OnValidate()
    {
        RebuildRangeOffsets();
    }

    private void RebuildRangeOffsets()
    {
        blockGain = Mathf.Max(0, blockGain);

        List<Vector2Int> offsets = new List<Vector2Int>(HexDirections.Length);
        for (int i = 0; i < HexDirections.Length; i++)
        {
            offsets.Add(HexDirections[i]);
        }

        rangeOffsets = offsets;
    }

    // 選格型移動卡，效果在 ExecuteOnPosition 觸發。
    public override void ExecuteEffect(Player player, Enemy enemy)
    {
    }

    public override void ExecuteOnPosition(Player player, Vector2Int targetGridPos)
    {
        if (player == null)
        {
            return;
        }

        player.MoveToPosition(targetGridPos);

        // 只有真正移動到目標格才給護甲。
        if (player.position == targetGridPos && blockGain > 0)
        {
            player.AddBlock(blockGain);
        }
    }
}
