// 使用泛型集合（List）型別。
using System.Collections.Generic;
// 使用 Unity 常用 API（例如 Vector2Int、Mathf、Debug）。
using UnityEngine;

/// <summary>
/// 縮地成寸（1費）：選擇一個格子，直接移動到該格。
/// </summary>
[CreateAssetMenu(fileName = "Move_SuoDiChengCun", menuName = "Cards/Movement/縮地成寸")]
public class Move_SuoDiChengCun : MovementCardBase
{
    // 可選目標的最大偏移範圍（設大一點即可近似全圖可選）。
    [Min(1)]
    public int maxSelectOffset = 30;

    // 這張牌走「選目標格」流程，因此此方法保留空實作即可。
    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        // 實際效果在 ExecuteOnPosition 內處理。
    }

    // ScriptableObject 啟用時同步卡牌資料（型別與可選位移範圍）。
    private void OnEnable()
    {
        // 設為移動牌，讓系統走移動卡流程。
        cardType = CardType.Movement;
        // 自動重建可選範圍，讓卡牌可選較大範圍內任意格。
        RebuildRangeOffsets();
    }

    // 在 Inspector 修改數值時也同步更新，避免資料不同步。
    private void OnValidate()
    {
        // 維持資料合法並重建範圍偏移。
        RebuildRangeOffsets();
    }

    // 建立大範圍偏移表（排除 0,0），供選格系統標記可選目標。
    private void RebuildRangeOffsets()
    {
        // 防呆：最大偏移至少要 1。
        maxSelectOffset = Mathf.Max(1, maxSelectOffset);

        // 初始化偏移清單。
        List<Vector2Int> offsets = new List<Vector2Int>();

        // 產生 -max~+max 的所有偏移。
        for (int x = -maxSelectOffset; x <= maxSelectOffset; x++)
        {
            for (int y = -maxSelectOffset; y <= maxSelectOffset; y++)
            {
                // 排除原地（不把自己所在格標成目標）。
                if (x == 0 && y == 0)
                {
                    continue;
                }

                // 加入偏移。
                offsets.Add(new Vector2Int(x, y));
            }
        }

        // 寫回移動卡可選偏移表。
        rangeOffsets = offsets;
    }

    // 玩家點擊目標格後，執行位移效果（與移動卡 Move_YiDong 一致）。
    public override void ExecuteOnPosition(Player player, Vector2Int targetGridPos)
    {
        // 使用一般移動流程（非瞬移），套用與 Move_YiDong 相同的移動規則。
        player.MoveToPosition(targetGridPos);
    }
}
