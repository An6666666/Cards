using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 所有卡牌的抽象基底類別，繼承 ScriptableObject 以便在 Unity 中建立卡牌資產。
/// </summary>
public abstract class CardBase : ScriptableObject
{
    [Header("卡牌基本屬性")]
    public string cardName;         // 卡牌名稱
    public int cost;                // 能量消耗
    [TextArea] public string description;   // 卡牌描述
    public Sprite cardImage;        // 卡面圖示

    [Tooltip("勾選後，這張卡在使用後會進入消耗區（Exhaust）。")]
    public bool exhaustOnUse = false; // 使用後進入消耗區

    [Header("商店設定")]
    [Tooltip("商店販售這張卡時的價格。設定為 0 表示不額外覆寫，交由其他規則處理。")]
    public int shopPrice = 0;

    [Header("卡牌類型")]
    public CardType cardType;

    /// <summary>
    /// 執行卡牌效果，由子類別實作。
    /// </summary>
    /// <param name="player">玩家</param>
    /// <param name="enemy">目標敵人</param>
    public abstract void ExecuteEffect(Player player, Enemy enemy);

    /// <summary>
    /// 讓卡牌回報自己的元素類型；若沒有元素則回傳 false。
    /// </summary>
    public virtual bool TryGetElementType(out ElementType elementType)
    {
        elementType = default;
        return false;
    }

    /// <summary>
    /// 給需要指定格子的位置型卡牌覆寫，例如移動卡或範圍型效果。
    /// </summary>
    public virtual void ExecuteOnPosition(Player player, Vector2Int targetGridPos)
    {
        // 預設不處理位置指定效果，交由需要的卡牌覆寫。
    }
}
