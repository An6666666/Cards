using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 卡片基底類別：所有卡片（攻擊、技能、移動、遺物等）
/// </summary>
public abstract class CardBase : ScriptableObject
{
    [Header("基本資料")]
    public string cardName;         // 卡片顯示名稱
    public int cost;                // 打出此卡需要消耗的費用
    [TextArea] public string description;   // 卡片的中文敘述
    public Sprite cardImage;        // 卡面的主視覺圖片


    [Header("分類")]
    public CardType cardType;

    /// <summary>
    /// 執行卡片主要效果（以「目標單位」為對象）
    /// </summary>
    /// <param name="player">出牌的玩家</param>
    /// <param name="enemy">主要目標</param>
    public abstract void ExecuteEffect(Player player, Enemy enemy);

    /// <summary>
    /// 預設不做任何事；若你的卡片需要這種施放方式，請在子類別覆寫此方法。
    /// </summary>

    public virtual void ExecuteOnPosition(Player player, Vector2Int targetGridPos)
    {
        // 預設為空實作。需要以座標施放的卡片，請在子類別中覆寫。
    }
}
