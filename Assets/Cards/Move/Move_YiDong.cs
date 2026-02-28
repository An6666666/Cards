using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【移動卡：移動一格（0 費）】
/// </summary>
[CreateAssetMenu(fileName = "Move_YiDong", menuName = "Cards/Movement/移動")]
public class Move_YiDong : MovementCardBase
{

    private void OnEnable()
    {
        cardType = CardType.Movement;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {

    }

    public override void ExecuteOnPosition(Player player, Vector2Int targetGridPos)
    {
        // === 實際移動玩家到 targetGridPos（與原檔一致：不在此做距離或阻擋判定） ===
        player.MoveToPosition(targetGridPos);

    }
}