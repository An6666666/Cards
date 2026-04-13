using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public partial class ShopUIManager
{
    private readonly struct OrderedCardEntry
    {
        public OrderedCardEntry(CardBase card, int sourceIndex)
        {
            Card = card;
            SourceIndex = sourceIndex;
        }

        public CardBase Card { get; }
        public int SourceIndex { get; }
    }

    private void RebuildCardPage()
    {
        ClearChildren(cardListParent);

        List<OrderedCardEntry> orderedCards = BuildOrderedCardEntries(availableCards);
        var pageWindow = GetPageWindow(orderedCards.Count, cardsPerPage, pageCards);
        pageCards = pageWindow.PageIndex;

        for (int i = pageWindow.StartIndex; i < pageWindow.EndIndex; i++)
        {
            CardBase card = orderedCards[i].Card;
            int price = GetCardPrice(card);
            CreateCardOffer(card, price);
        }

        UpdatePageUI(pageCards, pageWindow.PageCount);
        RefreshTutorialInteractionState();
    }

    private void RebuildRelicPage()
    {
        ClearChildren(relicListParent);

        var pageWindow = GetPageWindow(availableRelics.Count, relicsPerPage, pageRelics);
        pageRelics = pageWindow.PageIndex;

        for (int i = pageWindow.StartIndex; i < pageWindow.EndIndex; i++)
        {
            var relic = availableRelics[i];
            int price = GetRelicPrice(relic);
            CreateRelicOffer(relic, price);
        }

        UpdatePageUI(pageRelics, pageWindow.PageCount);
        RefreshTutorialInteractionState();
    }

    private void RebuildRemovalPage()
    {
        ClearChildren(removalListParent);

        if (player == null || player.deck == null || inventory == null)
        {
            UpdatePageUI(0, 1);
            RefreshTutorialInteractionState();
            return;
        }

        int removalCost = inventory.CardRemovalCost;
        if (removalCostText != null)
            removalCostText.text = $"移除價格：{removalCost}";

        List<OrderedCardEntry> orderedCards = BuildOrderedCardEntries(player.deck);
        var pageWindow = GetPageWindow(orderedCards.Count, removalPerPage, pageRemoval);
        pageRemoval = pageWindow.PageIndex;

        for (int i = pageWindow.StartIndex; i < pageWindow.EndIndex; i++)
        {
            OrderedCardEntry entry = orderedCards[i];
            CardBase card = entry.Card;
            if (card == null)
                continue;

            CreateRemovalEntry(card, removalCost, entry.SourceIndex);
        }

        UpdatePageUI(pageRemoval, pageWindow.PageCount);
        RefreshTutorialInteractionState();
    }

    private void RebuildOffers()
    {
        if (!offersGenerated)
            GenerateOffersFromInventory();

        RefreshCurrentTabPage();
    }

    private void BuildRemovalList()
    {
        RefreshCurrentTabPage();
    }

    private void GenerateOffersFromInventory()
    {
        availableCards.Clear();
        availableRelics.Clear();
        offersGenerated = false;

        if (inventory == null)
            return;

        availableCards.AddRange(RunCardPoolSelector.GetShopChoices(
            inventory.PurchasableCards,
            inventory.AttackCardOfferCount,
            inventory.SkillCardOfferCount,
            inventory.MovementCardOfferCount));
        AddRandomSelections(inventory.PurchasableRelics, inventory.RelicOfferCount, availableRelics);

        offersGenerated = true;
        SyncOfferStateToActiveNode();
        RefreshCurrentTabPage();
    }

    private void AddRandomSelections(IReadOnlyList<RelicBase> source, int desiredCount, List<RelicBase> target)
    {
        var pool = source?.Where(relic => relic != null).ToList();
        if (pool == null || pool.Count == 0)
            return;

        int count = desiredCount <= 0 ? pool.Count : Mathf.Min(desiredCount, pool.Count);
        for (int i = 0; i < count; i++)
        {
            int selectedIndex = Random.Range(0, pool.Count);
            target.Add(pool[selectedIndex]);
            pool.RemoveAt(selectedIndex);
        }
    }

    private List<OrderedCardEntry> BuildOrderedCardEntries(IReadOnlyList<CardBase> source)
    {
        List<OrderedCardEntry> ordered = new List<OrderedCardEntry>();
        if (source == null)
        {
            return ordered;
        }

        for (int i = 0; i < source.Count; i++)
        {
            CardBase card = source[i];
            if (card != null)
            {
                ordered.Add(new OrderedCardEntry(card, i));
            }
        }

        ordered.Sort(CompareOrderedCardEntries);
        return ordered;
    }

    private int CompareOrderedCardEntries(OrderedCardEntry left, OrderedCardEntry right)
    {
        int categoryCompare = GetCardCategorySortRank(left.Card).CompareTo(GetCardCategorySortRank(right.Card));
        if (categoryCompare != 0)
        {
            return categoryCompare;
        }

        if (left.Card != null &&
            right.Card != null &&
            left.Card.cardType == CardType.Attack &&
            right.Card.cardType == CardType.Attack)
        {
            int elementCompare = GetAttackElementSortRank(left.Card).CompareTo(GetAttackElementSortRank(right.Card));
            if (elementCompare != 0)
            {
                return elementCompare;
            }
        }

        int nameCompare = string.Compare(GetCardSortName(left.Card), GetCardSortName(right.Card), System.StringComparison.Ordinal);
        if (nameCompare != 0)
        {
            return nameCompare;
        }

        int assetNameCompare = string.Compare(GetAssetSortName(left.Card), GetAssetSortName(right.Card), System.StringComparison.Ordinal);
        if (assetNameCompare != 0)
        {
            return assetNameCompare;
        }

        return left.SourceIndex.CompareTo(right.SourceIndex);
    }

    private static int GetCardCategorySortRank(CardBase card)
    {
        if (card == null)
        {
            return int.MaxValue;
        }

        return card.cardType switch
        {
            CardType.Attack => 0,
            CardType.Skill => 1,
            CardType.Movement => 2,
            _ => 3
        };
    }

    private static int GetAttackElementSortRank(CardBase card)
    {
        if (card == null || !card.TryGetElementType(out ElementType elementType))
        {
            return int.MaxValue;
        }

        return elementType switch
        {
            ElementType.Fire => 0,
            ElementType.Water => 1,
            ElementType.Thunder => 2,
            ElementType.Ice => 3,
            ElementType.Wood => 4,
            _ => 5
        };
    }

    private static string GetCardSortName(CardBase card)
    {
        return card != null && !string.IsNullOrWhiteSpace(card.cardName)
            ? card.cardName.Trim()
            : string.Empty;
    }

    private static string GetAssetSortName(CardBase card)
    {
        return card != null && !string.IsNullOrWhiteSpace(card.name)
            ? card.name.Trim()
            : string.Empty;
    }

    private void CreateCardOffer(CardBase card, int price)
    {
        if (card == null || cardListParent == null)
            return;

        GameObject entry = null;
        GameObject cardObject = null;
        Button button = null;

        if (cardOfferTemplate != null)
        {
            entry = Instantiate(cardOfferTemplate, cardListParent);
            entry.name = card.cardName;
            entry.SetActive(true);

            Transform cardContainer = FindCardContainer(entry.transform);
            if (cardPrefab != null)
            {
                cardObject = Instantiate(cardPrefab, cardContainer != null ? cardContainer : entry.transform);
                ResetTransform(cardObject.transform);
            }

            ApplyOfferTexts(
                entry,
                card.cardName,
                price,
                card.description,
                cardObject != null ? cardObject.transform : cardContainer);

            button = entry.GetComponent<Button>() ?? entry.GetComponentInChildren<Button>(true);
        }
        else if (cardPrefab != null)
        {
            cardObject = Instantiate(cardPrefab, cardListParent);
            cardObject.name = card.cardName;
            cardObject.SetActive(true);

            button = cardObject.GetComponent<Button>() ?? cardObject.GetComponentInChildren<Button>(true);
            if (button == null)
                button = cardObject.AddComponent<Button>();
        }

        var cardUi = cardObject?.GetComponent<CardUI>();
        if (cardUi != null)
        {
            cardUi.SetupCard(card);
            cardUi.SetDisplayContext(CardUI.DisplayContext.Reward);
        }

        if (button == null && cardObject != null)
            button = cardObject.GetComponent<Button>() ?? cardObject.GetComponentInChildren<Button>(true);

        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => PurchaseCard(card, price));
    }

    private void CreateRelicOffer(RelicBase relic, int price)
    {
        if (relic == null || relicListParent == null)
            return;

        GameObject template = relicOfferTemplate != null ? relicOfferTemplate : cardOfferTemplate;
        if (template == null)
            return;

        GameObject entry = Instantiate(template, relicListParent);
        entry.name = relic.cardName;
        entry.SetActive(true);

        Transform visualContainer = FindOfferVisualContainer(entry.transform);
        Transform iconExcludeRoot = null;
        Graphic buttonTargetGraphic = null;

        if (relicIconPrefab != null)
        {
            Transform parent = visualContainer != null ? visualContainer : entry.transform;
            GameObject relicObject = Instantiate(relicIconPrefab, parent, false);
            relicObject.name = $"RelicUI_{relic.cardName}";
            relicObject.SetActive(true);

            ConfigureShopRelicInstance(entry, relicObject);

            BattleRelicUIItem relicUiItem = relicObject.GetComponent<BattleRelicUIItem>() ??
                                            relicObject.GetComponentInChildren<BattleRelicUIItem>(true);

            if (relicUiItem != null)
                relicUiItem.Bind(relic);
            else
                ApplyShopRelicPrefabIcon(relicObject, relic.cardImage);

            iconExcludeRoot = relicObject.transform;
            buttonTargetGraphic = FindOfferIconImage(relicObject);
        }
        else
        {
            buttonTargetGraphic = ApplyShopRelicOfferIcon(entry, relic.cardImage);
        }

        ApplyOfferTexts(
            entry,
            relic.cardName,
            price,
            relic.description,
            iconExcludeRoot);

        Button button = entry.GetComponent<Button>() ?? entry.GetComponentInChildren<Button>(true);
        if (button == null)
        {
            button = entry.AddComponent<Button>();
        }

        if (button == null)
            return;

        button.targetGraphic = buttonTargetGraphic;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => PurchaseRelic(relic, price));
    }

    private void CreateRemovalEntry(CardBase card, int price, int cardIndex)
    {
        if (card == null || removalEntryTemplate == null || removalListParent == null)
            return;

        var entry = Instantiate(removalEntryTemplate, removalListParent);
        entry.name = $"Remove {card.cardName}";
        entry.SetActive(true);

        GameObject cardObject = null;
        Transform cardContainer = FindCardContainer(entry.transform);

        if (cardPrefab != null)
        {
            cardObject = Instantiate(cardPrefab, cardContainer != null ? cardContainer : entry.transform);
            ResetTransform(cardObject.transform);
        }

        string description = string.IsNullOrEmpty(card.description) ? string.Empty : card.description;
        ApplyOfferTexts(
            entry,
            $"移除 {card.cardName}",
            price,
            description,
            cardObject != null ? cardObject.transform : cardContainer);

        var button = entry.GetComponent<Button>() ?? entry.GetComponentInChildren<Button>(true);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => RemoveCardAt(cardIndex, price));
        }

        var cardUi = cardObject?.GetComponent<CardUI>();
        if (cardUi != null)
        {
            cardUi.SetupCard(card);
            cardUi.SetDisplayContext(CardUI.DisplayContext.Reward);
        }
    }

    private void PurchaseCard(CardBase card, int price)
    {
        if (IsShopInteractionBlockedByTutorial())
            return;

        if (!TrySpendGold(price))
            return;

        player.deck.Add(Instantiate(card));
        availableCards.Remove(card);
        SyncOfferStateToActiveNode();

        shopNpcController?.NotifyPurchase(card.cardName, price);
        RefreshGoldDisplay();
        RebuildOffers();
        SyncRunState();
    }

    private void PurchaseRelic(RelicBase relic, int price)
    {
        if (IsShopInteractionBlockedByTutorial())
            return;

        if (!TrySpendGold(price))
            return;

        player.AcquireRelic(relic);
        availableRelics.Remove(relic);
        SyncOfferStateToActiveNode();

        shopNpcController?.NotifyPurchase(relic.cardName, price);
        RefreshGoldDisplay();
        RebuildOffers();
        SyncRunState();
    }

    private void RemoveCardAt(int index, int cost)
    {
        if (IsShopInteractionBlockedByTutorial())
            return;

        if (player == null || player.deck == null || index < 0 || index >= player.deck.Count)
            return;

        if (!TrySpendGold(cost))
            return;

        var removedCard = player.deck[index];
        player.deck.RemoveAt(index);

        string removedCardName = removedCard != null ? removedCard.cardName : "卡片";
        shopNpcController?.NotifyCardRemovalPurchase(removedCardName, cost);
        RefreshGoldDisplay();

        pageRemoval = GetPageWindow(player.deck.Count, removalPerPage, pageRemoval).PageIndex;

        BuildRemovalList();
        SyncRunState();
    }

    private bool TrySpendGold(int price)
    {
        if (IsShopInteractionBlockedByTutorial())
            return false;

        if (player == null)
            return false;

        if (player.gold < price)
        {
            shopNpcController?.NotifyInsufficientGold(price, player.gold);
            return false;
        }

        player.gold -= price;
        return true;
    }
}
