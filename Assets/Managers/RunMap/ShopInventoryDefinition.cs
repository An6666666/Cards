using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Run/Shop Inventory", fileName = "ShopInventory")]
public class ShopInventoryDefinition : ScriptableObject
{
    [SerializeField] private List<CardBase> purchasableCards = new List<CardBase>();
    [SerializeField] private List<RelicBase> purchasableRelics = new List<RelicBase>();

    [FormerlySerializedAs("cardOfferCount")]
    [SerializeField] private int attackCardOfferCount = 3;
    [SerializeField] private int skillCardOfferCount = 0;
    [SerializeField] private int movementCardOfferCount = 0;
    [SerializeField] private int relicOfferCount = 1;
    [SerializeField] private int cardRemovalCost = 5;

    public IReadOnlyList<CardBase> PurchasableCards => purchasableCards;
    public IReadOnlyList<RelicBase> PurchasableRelics => purchasableRelics;
    public int AttackCardOfferCount => Mathf.Max(0, attackCardOfferCount);
    public int SkillCardOfferCount => Mathf.Max(0, skillCardOfferCount);
    public int MovementCardOfferCount => Mathf.Max(0, movementCardOfferCount);
    public int TotalCardOfferCount => AttackCardOfferCount + SkillCardOfferCount + MovementCardOfferCount;
    public int RelicOfferCount => Mathf.Max(0, relicOfferCount);
    public int CardRemovalCost => Mathf.Max(0, cardRemovalCost);
}
