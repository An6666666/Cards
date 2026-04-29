using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public partial class ShopUIManager
{
    private void RebuildCardPage()
    {
        ClearChildren(cardListParent);

        List<CardDisplaySortUtility.OrderedCardEntry> orderedCards = CardDisplaySortUtility.BuildOrderedEntries(availableCards);
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

        List<CardDisplaySortUtility.OrderedCardEntry> orderedCards = CardDisplaySortUtility.BuildOrderedEntries(player.deck);
        var pageWindow = GetPageWindow(orderedCards.Count, removalPerPage, pageRemoval);
        pageRemoval = pageWindow.PageIndex;

        for (int i = pageWindow.StartIndex; i < pageWindow.EndIndex; i++)
        {
            CardDisplaySortUtility.OrderedCardEntry entry = orderedCards[i];
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
        button.onClick.AddListener(() => RequestPurchaseCardConfirmation(card, price));
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
        button.onClick.AddListener(() => RequestPurchaseRelicConfirmation(relic, price));
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
            button.onClick.AddListener(() => RequestRemoveCardConfirmation(cardIndex, price));
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
