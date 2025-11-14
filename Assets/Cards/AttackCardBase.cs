using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 攻擊卡基底：以「偏移座標」定義攻擊範圍的抽象卡片。
/// </summary>
public abstract class AttackCardBase : CardBase
{
    [Header("攻擊範圍偏移表")]
    public List<Vector2Int> rangeOffsets = new List<Vector2Int>();
}
