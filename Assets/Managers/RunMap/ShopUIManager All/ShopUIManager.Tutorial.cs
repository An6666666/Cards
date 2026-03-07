using System;
using UnityEngine;
using UnityEngine.UI;

public partial class ShopUIManager
{
    private ShopTutorialController tutorialController;
    private bool tutorialInteractionLocked;
    private ShopTutorialAction allowedTutorialAction = ShopTutorialAction.None;

    public ShopNpcDialogueController ShopNpcController => shopNpcController;

    public void AttachTutorialController(ShopTutorialController controller)
    {
        tutorialController = controller;
    }

    public void SetTutorialInteractionState(bool locked, ShopTutorialAction allowedAction = ShopTutorialAction.None)
    {
        tutorialInteractionLocked = locked;
        allowedTutorialAction = locked ? allowedAction : ShopTutorialAction.None;
        RefreshTutorialInteractionState();
    }

    public bool ForceSetTabForTutorial(ShopTab tab)
    {
        SetTab(tab);
        return true;
    }

    public Canvas GetPreferredTutorialCanvas()
    {
        Canvas canvas =
            ResolveCanvas(shopTitleText) ??
            ResolveCanvas(goldText) ??
            ResolveCanvas(messageText) ??
            ResolveCanvas(removalCostText) ??
            ResolveCanvas(pageText) ??
            ResolveCanvas(btnCards) ??
            ResolveCanvas(btnRelics) ??
            ResolveCanvas(btnRemoval) ??
            ResolveCanvas(btnPrev) ??
            ResolveCanvas(btnNext) ??
            ResolveCanvas(returnButton) ??
            ResolveCanvas(refreshRemovalButton) ??
            ResolveCanvas(cardsPanel) ??
            ResolveCanvas(relicsPanel) ??
            ResolveCanvas(removalPanel) ??
            ResolveCanvas(cardListParent) ??
            ResolveCanvas(relicListParent) ??
            ResolveCanvas(removalListParent);

        return canvas != null ? (canvas.rootCanvas != null ? canvas.rootCanvas : canvas) : null;
    }

    public RectTransform GetTutorialAnchor(ShopTutorialAnchor anchor)
    {
        switch (anchor)
        {
            case ShopTutorialAnchor.ShopTitle:
                return shopTitleText != null ? shopTitleText.rectTransform : null;
            case ShopTutorialAnchor.GoldDisplay:
                return goldText != null ? goldText.rectTransform : null;
            case ShopTutorialAnchor.CardsTabButton:
                return GetRectTransform(btnCards);
            case ShopTutorialAnchor.RelicsTabButton:
                return GetRectTransform(btnRelics);
            case ShopTutorialAnchor.RemovalTabButton:
                return GetRectTransform(btnRemoval);
            case ShopTutorialAnchor.PreviousPageButton:
                return GetRectTransform(btnPrev);
            case ShopTutorialAnchor.NextPageButton:
                return GetRectTransform(btnNext);
            case ShopTutorialAnchor.RefreshRemovalButton:
                return GetRectTransform(refreshRemovalButton);
            case ShopTutorialAnchor.ReturnButton:
                return GetRectTransform(returnButton);
            case ShopTutorialAnchor.RemovalCost:
                return removalCostText != null ? removalCostText.rectTransform : null;
            case ShopTutorialAnchor.CardList:
                return GetRectTransform(cardListParent);
            case ShopTutorialAnchor.RelicList:
                return GetRectTransform(relicListParent);
            case ShopTutorialAnchor.RemovalList:
                return GetRectTransform(removalListParent);
            case ShopTutorialAnchor.CardsPanel:
                return GetRectTransform(cardsPanel);
            case ShopTutorialAnchor.RelicsPanel:
                return GetRectTransform(relicsPanel);
            case ShopTutorialAnchor.RemovalPanel:
                return GetRectTransform(removalPanel);
            case ShopTutorialAnchor.MessageText:
                return messageText != null ? messageText.rectTransform : null;
            case ShopTutorialAnchor.None:
            default:
                return null;
        }
    }

    private void ExecuteTutorialAwareAction(ShopTutorialAction action, Action execute)
    {
        if (!CanExecuteTutorialAction(action))
            return;

        execute?.Invoke();
        tutorialController?.HandleAction(action);
        RefreshTutorialInteractionState();
    }

    private bool IsShopInteractionBlockedByTutorial()
    {
        return tutorialInteractionLocked;
    }

    private bool CanExecuteTutorialAction(ShopTutorialAction action)
    {
        return !tutorialInteractionLocked || allowedTutorialAction == action;
    }

    private bool CanUseTutorialAction(ShopTutorialAction action, bool baseInteractable = true)
    {
        if (!baseInteractable)
            return false;

        return !tutorialInteractionLocked || allowedTutorialAction == action;
    }

    private void RefreshTutorialInteractionState()
    {
        if (btnCards != null)
            btnCards.interactable = CanUseTutorialAction(ShopTutorialAction.OpenCardsTab);

        if (btnRelics != null)
            btnRelics.interactable = CanUseTutorialAction(ShopTutorialAction.OpenRelicsTab);

        if (btnRemoval != null)
            btnRemoval.interactable = CanUseTutorialAction(ShopTutorialAction.OpenRemovalTab);

        if (refreshRemovalButton != null)
            refreshRemovalButton.interactable = CanUseTutorialAction(
                ShopTutorialAction.RefreshRemoval,
                removalPanel == null || removalPanel.activeInHierarchy);

        if (returnButton != null)
            returnButton.interactable = CanUseTutorialAction(ShopTutorialAction.ReturnToRunMap);

        RefreshPagingInteractivity();

        SetDynamicEntryButtonsInteractable(cardListParent, !tutorialInteractionLocked);
        SetDynamicEntryButtonsInteractable(relicListParent, !tutorialInteractionLocked);
        SetDynamicEntryButtonsInteractable(removalListParent, !tutorialInteractionLocked);
    }

    private void RefreshPagingInteractivity()
    {
        int pageIndex = 0;
        int pageCount = 1;

        switch (currentTab)
        {
            case ShopTab.Cards:
                PageWindow cardsWindow = GetPageWindow(availableCards.Count, cardsPerPage, pageCards);
                pageIndex = cardsWindow.PageIndex;
                pageCount = cardsWindow.PageCount;
                break;
            case ShopTab.Relics:
                PageWindow relicsWindow = GetPageWindow(availableRelics.Count, relicsPerPage, pageRelics);
                pageIndex = relicsWindow.PageIndex;
                pageCount = relicsWindow.PageCount;
                break;
            case ShopTab.Removal:
                int removalCount = player != null && player.deck != null ? player.deck.Count : 0;
                PageWindow removalWindow = GetPageWindow(removalCount, removalPerPage, pageRemoval);
                pageIndex = removalWindow.PageIndex;
                pageCount = removalWindow.PageCount;
                break;
        }

        UpdatePageUI(pageIndex, pageCount);
    }

    private void SetDynamicEntryButtonsInteractable(Transform parent, bool interactable)
    {
        if (parent == null)
            return;

        Button[] buttons = parent.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button != null)
                button.interactable = interactable;
        }
    }

    private static RectTransform GetRectTransform(Component component)
    {
        return component != null ? component.transform as RectTransform : null;
    }

    private static RectTransform GetRectTransform(GameObject gameObject)
    {
        return gameObject != null ? gameObject.transform as RectTransform : null;
    }

    private static Canvas ResolveCanvas(Component component)
    {
        return component != null ? component.GetComponentInParent<Canvas>(true) : null;
    }

    private static Canvas ResolveCanvas(GameObject gameObject)
    {
        return gameObject != null ? gameObject.GetComponentInParent<Canvas>(true) : null;
    }
}
