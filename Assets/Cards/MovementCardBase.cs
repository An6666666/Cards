using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MovementCardBase : CardBase
{
    // 若你希望移動卡可以指定格子執行效果，
    // 可覆寫 ExecuteOnPosition。

    [Header("移動範圍偏移表")]
    public List<Vector2Int> rangeOffsets = new List<Vector2Int>();
}
