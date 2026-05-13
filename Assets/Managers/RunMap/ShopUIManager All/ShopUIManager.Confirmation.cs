using UnityEngine;
using UnityEngine.UI;

public partial class ShopUIManager
{
    private const int ConfirmPanelSortingOrder = 10000;

    private void ResolveConfirmPanelReferences()
    {
        if (confirmPanel == null)
            confirmPanel = FindSceneObject("ConfirmPanel");

        if (confirmPanel == null)
            return;

        if (confirmTitleText == null)
            confirmTitleText = FindChildComponent<Text>(confirmPanel.transform, "TitleText");

        if (confirmButton == null)
            confirmButton = FindChildComponent<Button>(confirmPanel.transform, "ConfirmButton");

        if (cancelButton == null)
            cancelButton = FindChildComponent<Button>(confirmPanel.transform, "CancelButton");
    }

    private void BindConfirmPanelButtons()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(ConfirmPendingShopAction);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(CancelPendingShopAction);
        }
    }

    private T FindChildComponent<T>(Transform root, string childName) where T : Component
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        Transform child = root.Find(childName);
        if (child != null)
            return child.GetComponent<T>();

        T[] components = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].name == childName)
                return components[i];
        }

        return null;
    }

    private GameObject FindSceneObject(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        GameObject activeObject = GameObject.Find(objectName);
        if (activeObject != null)
            return activeObject;

        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject current = objects[i];
            if (current != null && current.name == objectName && current.scene.IsValid())
                return current;
        }

        return null;
    }

    private void RequestPurchaseCardConfirmation(CardBase card, int price)
    {
        if (IsShopInteractionBlockedByTutorial())
            return;

        if (player == null || player.gold < price)
        {
            TrySpendGold(price);
            return;
        }

        pendingConfirmation = PendingShopConfirmation.PurchaseCard;
        pendingCard = card;
        pendingRelic = null;
        pendingCardIndex = -1;
        pendingPrice = price;
        ShowConfirmPanel(purchaseConfirmTitle);
    }

    private void RequestPurchaseRelicConfirmation(RelicBase relic, int price)
    {
        if (IsShopInteractionBlockedByTutorial())
            return;

        if (player == null || player.gold < price)
        {
            TrySpendGold(price);
            return;
        }

        pendingConfirmation = PendingShopConfirmation.PurchaseRelic;
        pendingCard = null;
        pendingRelic = relic;
        pendingCardIndex = -1;
        pendingPrice = price;
        ShowConfirmPanel(purchaseConfirmTitle);
    }

    private void RequestRemoveCardConfirmation(int cardIndex, int price)
    {
        if (IsShopInteractionBlockedByTutorial())
            return;

        if (player == null || player.deck == null || cardIndex < 0 || cardIndex >= player.deck.Count)
            return;

        if (player.gold < price)
        {
            TrySpendGold(price);
            return;
        }

        pendingConfirmation = PendingShopConfirmation.RemoveCard;
        pendingCard = null;
        pendingRelic = null;
        pendingCardIndex = cardIndex;
        pendingPrice = price;
        ShowConfirmPanel(removeConfirmTitle);
    }

    private void RequestExitShopConfirmation()
    {
        pendingConfirmation = PendingShopConfirmation.ExitShop;
        pendingCard = null;
        pendingRelic = null;
        pendingCardIndex = -1;
        pendingPrice = 0;
        ShowConfirmPanel(exitConfirmTitle);
    }

    private void ShowConfirmPanel(string title)
    {
        if (confirmPanel == null || confirmButton == null)
        {
            ConfirmPendingShopAction();
            return;
        }

        SetShopRelicTooltipsSuppressed(true);

        if (confirmTitleText != null)
            confirmTitleText.text = title;

        EnsureConfirmPanelTopSorting();
        confirmPanel.transform.SetAsLastSibling();
        confirmPanel.SetActive(true);
    }

    private void HideConfirmPanel()
    {
        SetShopRelicTooltipsSuppressed(false);

        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    private void CancelPendingShopAction()
    {
        ClearPendingShopAction();
        HideConfirmPanel();
    }

    private void ConfirmPendingShopAction()
    {
        PendingShopConfirmation action = pendingConfirmation;
        CardBase card = pendingCard;
        RelicBase relic = pendingRelic;
        int cardIndex = pendingCardIndex;
        int price = pendingPrice;

        ClearPendingShopAction();
        HideConfirmPanel();

        switch (action)
        {
            case PendingShopConfirmation.PurchaseCard:
                PurchaseCard(card, price);
                break;
            case PendingShopConfirmation.PurchaseRelic:
                PurchaseRelic(relic, price);
                break;
            case PendingShopConfirmation.RemoveCard:
                RemoveCardAt(cardIndex, price);
                break;
            case PendingShopConfirmation.ExitShop:
                ExitShop();
                break;
        }
    }

    private void ClearPendingShopAction()
    {
        pendingConfirmation = PendingShopConfirmation.None;
        pendingCard = null;
        pendingRelic = null;
        pendingCardIndex = -1;
        pendingPrice = 0;
    }

    private void SetShopRelicTooltipsSuppressed(bool suppressed)
    {
        BattleRelicUIItem[] relicItems = Resources.FindObjectsOfTypeAll<BattleRelicUIItem>();
        for (int i = 0; i < relicItems.Length; i++)
        {
            BattleRelicUIItem relicItem = relicItems[i];
            if (relicItem == null || !relicItem.gameObject.scene.IsValid())
            {
                continue;
            }

            relicItem.SetTooltipSuppressed(suppressed);
        }
    }

    private void EnsureConfirmPanelTopSorting()
    {
        if (confirmPanel == null)
        {
            return;
        }

        Canvas confirmCanvas = confirmPanel.GetComponent<Canvas>();
        if (confirmCanvas == null)
        {
            confirmCanvas = confirmPanel.AddComponent<Canvas>();
        }

        Canvas parentCanvas = ResolveParentCanvas(confirmPanel.transform, confirmCanvas);
        if (parentCanvas != null)
        {
            confirmCanvas.renderMode = parentCanvas.renderMode;
            confirmCanvas.worldCamera = parentCanvas.worldCamera;
            confirmCanvas.sortingLayerID = parentCanvas.sortingLayerID;
        }

        confirmCanvas.overrideSorting = true;
        confirmCanvas.sortingOrder = ConfirmPanelSortingOrder;

        if (confirmPanel.GetComponent<GraphicRaycaster>() == null)
        {
            confirmPanel.AddComponent<GraphicRaycaster>();
        }
    }

    private static Canvas ResolveParentCanvas(Transform target, Canvas excludedCanvas)
    {
        if (target == null)
        {
            return null;
        }

        Canvas[] canvases = target.GetComponentsInParent<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas != excludedCanvas)
            {
                return canvas;
            }
        }

        return null;
    }
}
