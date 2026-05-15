using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleHandUIController
{
    private readonly BattleManager battleManager;
    private readonly Player player;
    private readonly GameObject cardPrefab;
    private readonly Transform handPanel;
    private readonly Transform deckPile;
    private readonly Transform discardPile;
    private readonly Transform allDeckPile;
    private readonly Text energyText;
    private readonly Button endTurnButton;
    private readonly float cardUseDelay;
    private readonly HashSet<CardUI> pendingConsumedCardUIs = new HashSet<CardUI>();

    private bool cardInteractionLocked;

    public bool IsCardInteractionLocked => cardInteractionLocked;

    public BattleHandUIController(
        BattleManager battleManager,
        Player player,
        GameObject cardPrefab,
        Transform handPanel,
        Transform deckPile,
        Transform discardPile,
        Transform allDeckPile,
        Text energyText,
        Button endTurnButton,
        float cardUseDelay)
    {
        this.battleManager = battleManager;
        this.player = player;
        this.cardPrefab = cardPrefab;
        this.handPanel = handPanel;
        this.deckPile = deckPile;
        this.discardPile = discardPile;
        this.allDeckPile = allDeckPile;
        this.energyText = energyText;
        this.endTurnButton = endTurnButton;
        this.cardUseDelay = cardUseDelay;
    }

    public void RefreshHandUI(bool playDrawAnimation = false)
    {
        UpdateEnergyUI();
        UpdateDeckDiscardUI();
        SyncHandUI(playDrawAnimation);
    }

    public void UpdateEnergyUI()
    {
        if (energyText != null && player != null)
        {
            energyText.text = $"{player.energy}/{player.maxEnergy}";
        }
    }

    public void UpdateHandMetaUI()
    {
        UpdateEnergyUI();
        UpdateDeckDiscardUI();
    }

    public void HandleCardUsedUI(CardUI usedUI)
    {
        if (usedUI == null || handPanel == null) return;
        pendingConsumedCardUIs.Remove(usedUI);
        Object.Destroy(usedUI.gameObject);

        if (handPanel is RectTransform handRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(handRect);
        }
    }

    public void SyncHandUI(bool playDrawAnimation = false)
    {
        if (handPanel == null || player == null) return;

        List<CardUI> cardsWithMissingData = new List<CardUI>();
        Dictionary<CardBase, Queue<CardUI>> existingByCard = new Dictionary<CardBase, Queue<CardUI>>();

        for (int i = 0; i < handPanel.childCount; i++)
        {
            Transform child = handPanel.GetChild(i);
            CardUI cardUI = child.GetComponent<CardUI>();
            if (cardUI == null) continue;

            if (pendingConsumedCardUIs.Contains(cardUI))
            {
                DetachCardUI(cardUI);
                continue;
            }

            if (cardUI.cardData == null)
            {
                cardsWithMissingData.Add(cardUI);
                continue;
            }

            if (!existingByCard.TryGetValue(cardUI.cardData, out Queue<CardUI> queue))
            {
                queue = new Queue<CardUI>();
                existingByCard.Add(cardUI.cardData, queue);
            }

            queue.Enqueue(cardUI);
        }

        List<CardUI> createdCards = new List<CardUI>();

        foreach (CardBase cardData in player.Hand)
        {
            if (existingByCard.TryGetValue(cardData, out Queue<CardUI> queue) && queue.Count > 0)
            {
                CardUI existing = queue.Dequeue();
                existing.SetInteractable(!cardInteractionLocked);
                continue;
            }

            GameObject cardObj = Object.Instantiate(cardPrefab, handPanel);
            CardUI cardUI = cardObj.GetComponent<CardUI>();
            if (cardUI == null)
            {
                Object.Destroy(cardObj);
                continue;
            }

            cardUI.SetupCard(cardData);
            cardUI.SetInteractable(!cardInteractionLocked);
            cardUI.ForceResetToHand(handPanel);
            createdCards.Add(cardUI);
        }

        foreach (KeyValuePair<CardBase, Queue<CardUI>> pair in existingByCard)
        {
            while (pair.Value.Count > 0)
            {
                CardUI extraCardUI = pair.Value.Dequeue();
                DetachCardUI(extraCardUI);
                Object.Destroy(extraCardUI.gameObject);
            }
        }

        for (int i = 0; i < cardsWithMissingData.Count; i++)
        {
            CardUI cardUI = cardsWithMissingData[i];
            DetachCardUI(cardUI);
            Object.Destroy(cardUI.gameObject);
        }

        if (handPanel is RectTransform handRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(handRect);

            for (int i = 0; i < handPanel.childCount; i++)
            {
                CardUI cardUI = handPanel.GetChild(i).GetComponent<CardUI>();
                RectTransform rt = cardUI != null ? cardUI.VisualRect : null;
                if (cardUI == null || rt == null) continue;

                CardAnimationController animation = cardUI.GetComponent<CardAnimationController>();
                if (animation != null && animation.IsPlayingDrawAnimation) continue;

                CardDragHandler drag = cardUI.GetComponent<CardDragHandler>();
                if (drag != null && drag.IsDragging) continue;

                CardHoverEffect hover = cardUI.GetComponent<CardHoverEffect>();
                if (hover != null && hover.IsHovering)
                {
                    cardUI.OriginalAnchoredPosition = Vector2.zero;
                    continue;
                }

                rt.anchoredPosition = Vector2.zero;
                cardUI.OriginalAnchoredPosition = Vector2.zero;
            }
        }

        if (playDrawAnimation)
        {
            RectTransform deckRect = deckPile as RectTransform;
            for (int i = 0; i < createdCards.Count; i++)
            {
                createdCards[i].PlayDrawAnimation(deckRect);
            }
        }
    }

    public void MarkCardPendingConsume(CardUI cardUI)
    {
        if (cardUI != null)
        {
            pendingConsumedCardUIs.Add(cardUI);
        }
    }

    public void ClearCardPendingConsume(CardUI cardUI)
    {
        if (cardUI != null)
        {
            pendingConsumedCardUIs.Remove(cardUI);
        }
    }

    public void UpdateDeckDiscardUI()
    {
        if (player == null) return;

        SetCounterText(deckPile, player.deck != null ? player.deck.Count : 0);
        SetCounterText(discardPile, player.discardPile != null ? player.discardPile.Count : 0);
        SetCounterText(allDeckPile, GetAllDeckCount());
    }

    private int GetAllDeckCount()
    {
        PlayerRunSnapshot snapshot = RunManager.Instance != null ? RunManager.Instance.CurrentRunSnapshot : null;
        if (snapshot != null && snapshot.deck != null)
        {
            return snapshot.deck.Count;
        }

        if (player == null || player.deck == null)
        {
            return 0;
        }

        return player.deck.Count;
    }

    private static void SetCounterText(Transform root, int value)
    {
        if (root == null)
        {
            return;
        }

        Text text = root.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = $"{value}";
        }
    }

    private void DetachCardUI(CardUI cardUI)
    {
        if (cardUI == null) return;
        if (cardUI.LayoutElement != null) cardUI.LayoutElement.ignoreLayout = true;
        cardUI.transform.SetParent(null, false);
    }

    public void ApplyInteractableToAllCards(bool value)
    {
        CardUI[] cards = Object.FindObjectsOfType<CardUI>();
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].SetInteractable(value);
        }
    }

    public IEnumerator EnableCardsAfterDelay()
    {
        yield return new WaitForSeconds(cardUseDelay);
        cardInteractionLocked = false;
        ApplyInteractableToAllCards(true);
    }

    public void SetEndTurnButtonInteractable(bool value)
    {
        if (endTurnButton != null)
        {
            endTurnButton.interactable = value;
        }
    }

    public void LockCardInteraction()
    {
        cardInteractionLocked = true;
    }
}
