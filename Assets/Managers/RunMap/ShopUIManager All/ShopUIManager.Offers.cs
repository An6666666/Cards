using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public partial class ShopUIManager
{
    private void RebuildCardPage()
    {
        ClearChildren(cardListParent);

        var pageWindow = GetPageWindow(availableCards.Count, cardsPerPage, pageCards);
        pageCards = pageWindow.PageIndex;

        for (int i = pageWindow.StartIndex; i < pageWindow.EndIndex; i++)
        {
            var card = availableCards[i];
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

            CreateOfferEntry(
                removalEntryTemplate,
                relicListParent,
                relic.cardName,
                price,
                relic.description,
                () => PurchaseRelic(relic, price));
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

        var pageWindow = GetPageWindow(player.deck.Count, removalPerPage, pageRemoval);
        pageRemoval = pageWindow.PageIndex;

        for (int i = pageWindow.StartIndex; i < pageWindow.EndIndex; i++)
        {
            var card = player.deck[i];
            if (card == null)
                continue;

            CreateRemovalEntry(card, removalCost, i);
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

        AddRandomSelections(inventory.PurchasableCards, inventory.CardOfferCount, availableCards);
        AddRandomSelections(inventory.PurchasableRelics, inventory.RelicOfferCount, availableRelics);

        offersGenerated = true;
        RefreshCurrentTabPage();
    }

    private void AddRandomSelections(IReadOnlyList<CardBase> source, int desiredCount, List<CardBase> target)
    {
        var pool = source?.Where(card => card != null).ToList();
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
        button.onClick.AddListener(() => PurchaseCard(card, price));
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

        shopNpcController?.NotifyPurchase(card.cardName, price);
        RefreshGoldDisplay();
        RebuildOffers();
        SyncRunState();
    }

    private void PurchaseRelic(CardBase relic, int price)
    {
        if (IsShopInteractionBlockedByTutorial())
            return;

        if (!TrySpendGold(price))
            return;

        player.relics.Add(Instantiate(relic));
        availableRelics.Remove(relic);

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
        shopNpcController?.NotifyPurchase($"移除 {removedCardName}", cost);
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
