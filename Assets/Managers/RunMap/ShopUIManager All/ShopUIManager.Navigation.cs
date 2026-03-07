using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public partial class ShopUIManager
{
    private void BindButtons()
    {
        if (refreshRemovalButton != null)
        {
            refreshRemovalButton.onClick.RemoveAllListeners();
            refreshRemovalButton.onClick.AddListener(() => ExecuteTutorialAwareAction(ShopTutorialAction.RefreshRemoval, () =>
            {
                pageRemoval = 0;
                RefreshCurrentTabPage();
            }));
        }

        if (returnButton != null)
        {
            returnButton.onClick.RemoveAllListeners();
            returnButton.onClick.AddListener(() => ExecuteTutorialAwareAction(ShopTutorialAction.ReturnToRunMap, ExitShop));
        }

        if (btnPrev != null)
        {
            btnPrev.onClick.RemoveAllListeners();
            btnPrev.onClick.AddListener(() => ExecuteTutorialAwareAction(ShopTutorialAction.PreviousPage, () => ChangePage(-1)));
        }

        if (btnNext != null)
        {
            btnNext.onClick.RemoveAllListeners();
            btnNext.onClick.AddListener(() => ExecuteTutorialAwareAction(ShopTutorialAction.NextPage, () => ChangePage(1)));
        }
    }

    private void BindTabButtons()
    {
        if (btnCards != null)
        {
            btnCards.onClick.RemoveAllListeners();
            btnCards.onClick.AddListener(() => ExecuteTutorialAwareAction(ShopTutorialAction.OpenCardsTab, () => SetTab(ShopTab.Cards)));
        }

        if (btnRelics != null)
        {
            btnRelics.onClick.RemoveAllListeners();
            btnRelics.onClick.AddListener(() => ExecuteTutorialAwareAction(ShopTutorialAction.OpenRelicsTab, () => SetTab(ShopTab.Relics)));
        }

        if (btnRemoval != null)
        {
            btnRemoval.onClick.RemoveAllListeners();
            btnRemoval.onClick.AddListener(() => ExecuteTutorialAwareAction(ShopTutorialAction.OpenRemovalTab, () => SetTab(ShopTab.Removal)));
        }
    }

    private void SetTab(ShopTab tab)
    {
        currentTab = tab;

        ShowPanel(cardsPanel, false);
        ShowPanel(relicsPanel, false);
        ShowPanel(removalPanel, false);

        switch (tab)
        {
            case ShopTab.Cards:
                ShowPanel(cardsPanel, true);
                break;
            case ShopTab.Relics:
                ShowPanel(relicsPanel, true);
                break;
            case ShopTab.Removal:
                ShowPanel(removalPanel, true);
                break;
        }

        UpdateLamp(lampCards, tab == ShopTab.Cards);
        UpdateLamp(lampRelics, tab == ShopTab.Relics);
        UpdateLamp(lampRemoval, tab == ShopTab.Removal);

        RefreshCurrentTabPage();
        RefreshTutorialInteractionState();
    }

    private void ShowPanel(GameObject panel, bool show)
    {
        if (panel == null)
            return;

        panel.SetActive(show);

        var canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = panel.AddComponent<CanvasGroup>();

        canvasGroup.DOKill();
        panel.transform.DOKill();

        canvasGroup.alpha = show ? 0f : 1f;
        canvasGroup.interactable = show;
        canvasGroup.blocksRaycasts = show;

        if (!show)
            return;

        canvasGroup.DOFade(1f, 0.2f);
        panel.transform.localScale = Vector3.one * 0.98f;
        panel.transform.DOScale(1f, 0.2f);
    }

    private void UpdateLamp(Image lamp, bool selected)
    {
        if (lamp == null)
            return;

        lamp.DOKill();
        lamp.transform.DOKill();

        if (lampNormalSprite != null && lampSelectedSprite != null)
            lamp.sprite = selected ? lampSelectedSprite : lampNormalSprite;

        lamp.color = selected ? lampSelectedColor : lampNormalColor;

        if (!selected)
        {
            lamp.transform.localScale = Vector3.one;
            return;
        }

        lamp.transform.localScale = Vector3.one;
        lamp.transform
            .DOScale(1.08f, 0.12f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => lamp.transform.DOScale(1f, 0.12f).SetEase(Ease.InQuad));
    }

    private void ChangePage(int delta)
    {
        switch (currentTab)
        {
            case ShopTab.Cards:
                pageCards = Mathf.Max(0, pageCards + delta);
                break;
            case ShopTab.Relics:
                pageRelics = Mathf.Max(0, pageRelics + delta);
                break;
            case ShopTab.Removal:
                pageRemoval = Mathf.Max(0, pageRemoval + delta);
                break;
        }

        RefreshCurrentTabPage();
    }

    private void RefreshCurrentTabPage()
    {
        switch (currentTab)
        {
            case ShopTab.Cards:
                RebuildCardPage();
                break;
            case ShopTab.Relics:
                RebuildRelicPage();
                break;
            case ShopTab.Removal:
                RebuildRemovalPage();
                break;
        }
    }

    private void UpdatePageUI(int pageIndex, int pageCount)
    {
        if (pageText != null)
            pageText.text = $"{pageIndex + 1} / {pageCount}";

        if (btnPrev != null)
            btnPrev.interactable = CanUseTutorialAction(ShopTutorialAction.PreviousPage, pageIndex > 0);

        if (btnNext != null)
            btnNext.interactable = CanUseTutorialAction(ShopTutorialAction.NextPage, pageIndex < pageCount - 1);
    }
}
