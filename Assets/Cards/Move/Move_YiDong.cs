using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 移動一格；首次使用免費，之後固定為 1 費。
/// </summary>
[CreateAssetMenu(fileName = "Move_YiDong", menuName = "Cards/Movement/移動")]
public class Move_YiDong : MovementCardBase
{
    private const int InitialCost = 0;
    private const int ActivatedCost = 1;

    [System.NonSerialized] private bool hasActivatedOneCost;

    private void OnEnable()
    {
        cardType = CardType.Movement;
        ApplyRuntimeCost();
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
    }

    public override void ExecuteOnPosition(Player player, Vector2Int targetGridPos)
    {
        player.MoveToPosition(targetGridPos, allowOccupiedTileRelic: true);
    }

    public void MarkUsedOnce()
    {
        if (hasActivatedOneCost)
        {
            return;
        }

        hasActivatedOneCost = true;
        ApplyRuntimeCost();
    }

    private void ApplyRuntimeCost()
    {
        cost = hasActivatedOneCost ? ActivatedCost : InitialCost;
    }
}
