using System.Collections.Generic;                      // 要用 List<>
using UnityEngine;                                     // Unity 命名空間

// 建立商店庫存用的 ScriptableObject，可以在 Unity 建資產
[CreateAssetMenu(menuName = "Run/Shop Inventory", fileName = "ShopInventory")]
public class ShopInventoryDefinition : ScriptableObject   // 這個資產描述一間商店可以賣什麼
{
    // 可以被買的卡片清單
    [SerializeField] private List<CardBase> purchasableCards = new List<CardBase>();
    // 可以被買的遺物清單（這裡也用 CardBase 存，之後可換成真正的 Relic 類型）
    [SerializeField] private List<CardBase> purchasableRelics = new List<CardBase>();
    // 玩家要移除卡片時需要花的錢
    // 每次商店可供購買的卡片數量（0 代表不限制，會全部列出）
    [SerializeField] private int cardOfferCount = 3;
    // 每次商店可供購買的遺物數量（0 代表不限制，會全部列出）
    [SerializeField] private int relicOfferCount = 1;
    [SerializeField] private int cardRemovalCost = 5;

    // 對外的唯讀屬性：商店有哪些卡可以買
    public IReadOnlyList<CardBase> PurchasableCards => purchasableCards;
    // 對外的唯讀屬性：商店有哪些「遺物」可以買
    public IReadOnlyList<CardBase> PurchasableRelics => purchasableRelics;
    // 對外的費用，保證至少是 0
    // 每次商店隨機提供的卡片數量
    public int CardOfferCount => Mathf.Max(0, cardOfferCount);
    // 每次商店隨機提供的遺物數量
    public int RelicOfferCount => Mathf.Max(0, relicOfferCount);
    public int CardRemovalCost => Mathf.Max(0, cardRemovalCost);
}
