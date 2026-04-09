using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class RewardUI
{
    private void ConfigurePackButtonHover()
    {
        if (packHoverConfigured || packButton == null)
        {
            return;
        }

        EventTrigger trigger = packButton.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = packButton.gameObject.AddComponent<EventTrigger>();
        }

        AddPackHoverEntry(trigger, EventTriggerType.PointerEnter, OnPackPointerEnter);
        AddPackHoverEntry(trigger, EventTriggerType.PointerExit, OnPackPointerExit);
        packHoverConfigured = true;
    }

    private void AddPackHoverEntry(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    private void OnPackPointerEnter(BaseEventData _)
    {
        if (packOpenState != PackOpenState.Idle || cardbagBag == null)
        {
            return;
        }

        bagIdleTween?.Kill();
        bagIdleTween = null;
        bagHoverTween?.Kill();
        bagHoverTween = null;

        bagHoverTween = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .SetUpdate(true)
            .Append(cardbagBag.DOScale(bagDefaultScale * packHoverScale, packHoverDuration).SetEase(Ease.OutQuad))
            .Append(cardbagBag.DOScale(bagDefaultScale, packHoverReturnDuration).SetEase(Ease.InOutSine));
    }

    private void OnPackPointerExit(BaseEventData _)
    {
        if (packOpenState != PackOpenState.Idle || cardbagBag == null)
        {
            return;
        }

        bagHoverTween?.Kill();
        bagHoverTween = null;

        cardbagBag
            .DOScale(bagDefaultScale, packHoverReturnDuration)
            .SetEase(Ease.InOutSine)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .SetUpdate(true)
            .OnComplete(StartIdleBagVisual);
    }

    private void OnPackButtonClicked(List<CardBase> cardChoices)
    {
        if (packOpenState != PackOpenState.Idle || cardChoices == null || cardChoices.Count == 0)
        {
            return;
        }

        StopIdleVisual();

        if (openPackCoroutine != null)
        {
            StopCoroutine(openPackCoroutine);
        }

        openPackCoroutine = StartCoroutine(PlayOpenPackFlow(cardChoices));
    }

    private IEnumerator PlayOpenPackFlow(List<CardBase> cardChoices)
    {
        packOpenState = PackOpenState.Opening;

        if (packButton != null)
        {
            packButton.interactable = false;
        }

        PlayOpenBurstVisual();
        yield return new WaitForSecondsRealtime(openBurstDuration + openToRevealDelay);

        PlayBottomRevealVisual();
        yield return new WaitForSecondsRealtime(bottomRevealDuration);

        DisplayCardChoices(cardChoices);
        yield return new WaitForSecondsRealtime(cardRevealInterval);

        for (int i = 0; i < rewardCardUIs.Count; i++)
        {
            RevealCard(rewardCardUIs[i]);
            yield return new WaitForSecondsRealtime(cardRevealInterval);
        }

        yield return new WaitForSecondsRealtime(cardRevealDuration);

        SetRewardCardsInteractable(true);
        packOpenState = PackOpenState.Choosing;
        openPackCoroutine = null;
    }

    private void StartIdleVisual()
    {
        StartIdleBagVisual();

        if (cardbagLight == null)
        {
            return;
        }

        CanvasGroup lightCanvasGroup = GetOrAddCanvasGroup(cardbagLight.gameObject);
        cardbagLight.localRotation = Quaternion.identity;
        lightCanvasGroup.alpha = 1f;

        lightIdleFadeTween?.Kill();
        lightIdleFadeTween = lightCanvasGroup
            .DOFade(idleLightMinAlpha, idleLightFadeDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .SetUpdate(true);
    }

    private void StopIdleVisual()
    {
        bagIdleTween?.Kill();
        lightIdleFadeTween?.Kill();
        bagHoverTween?.Kill();
        bagIdleTween = null;
        lightIdleFadeTween = null;
        bagHoverTween = null;
    }

    private void StartIdleBagVisual()
    {
        if (packOpenState != PackOpenState.Idle || cardbagBag == null)
        {
            return;
        }

        bagIdleTween?.Kill();
        bagIdleTween = null;
        cardbagBag.localScale = bagDefaultScale;
        bagIdleTween = cardbagBag
            .DOScale(bagDefaultScale * idleBagScale, idleBagDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .SetUpdate(true);
    }

    private void PlayOpenBurstVisual()
    {
        if (cardbagBag != null)
        {
            cardbagBag.gameObject.SetActive(true);
            cardbagBag.localScale = bagDefaultScale;
            RestartVisualAnimator(cardbagBag);

            DOTween.Sequence()
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .SetUpdate(true)
                .Append(cardbagBag.DOScale(bagDefaultScale * 1.15f, openBurstDuration * 0.45f).SetEase(Ease.OutQuad))
                .Append(cardbagBag.DOScale(bagDefaultScale * 0.8f, openBurstDuration * 0.55f).SetEase(Ease.InBack));
        }

        if (cardbagLight != null)
        {
            cardbagLight.gameObject.SetActive(true);
            cardbagLight.localRotation = Quaternion.identity;
            cardbagLight.localScale = lightDefaultScale;
            RestartVisualAnimator(cardbagLight);
        }

        if (effectRoot != null)
        {
            effectRoot.gameObject.SetActive(true);
            effectRoot.localScale = effectDefaultScale;
            RestartVisualAnimator(effectRoot);
        }
    }

    private void PlayBottomRevealVisual()
    {
        if (packButton != null)
        {
            packButton.gameObject.SetActive(false);
        }

        if (cardbagLight != null)
        {
            cardbagLight.gameObject.SetActive(false);
        }

        if (effectRoot != null)
        {
            effectRoot.gameObject.SetActive(false);
        }

        if (cardParent != null)
        {
            cardParent.gameObject.SetActive(true);
        }

        if (bottomRoot == null)
        {
            return;
        }

        bottomRoot.gameObject.SetActive(true);
        bottomRoot.localScale = bottomDefaultScale;
        RestartVisualAnimator(bottomRoot);
    }

    private void DisplayCardChoices(List<CardBase> cardChoices)
    {
        rewardButtons.Clear();
        rewardCardUIs.Clear();

        if (cardParent == null)
        {
            return;
        }

        foreach (CardBase card in cardChoices)
        {
            GameObject cardGO = Instantiate(manager.cardPrefab, cardParent);
            if (useRewardCardScale)
            {
                cardGO.transform.localScale = rewardCardScale;
            }

            if (useRewardCardLayoutSize)
            {
                LayoutElement layoutElement = cardGO.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = cardGO.AddComponent<LayoutElement>();
                }

                layoutElement.preferredWidth = rewardCardPreferredSize.x;
                layoutElement.preferredHeight = rewardCardPreferredSize.y;
            }

            CardUI ui = cardGO.GetComponent<CardUI>();
            if (ui == null)
            {
                continue;
            }

            ui.SetupCard(card);
            ui.SetDisplayContext(CardUI.DisplayContext.Reward);
            ui.SetInteractable(false);

            Button button = cardGO.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                button = cardGO.AddComponent<Button>();
            }

            CardBase capturedCard = card;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnCardSelected(capturedCard));
            button.interactable = false;

            CanvasGroup cardCanvasGroup = GetOrAddCanvasGroup(cardGO);
            cardCanvasGroup.alpha = 0f;
            cardCanvasGroup.blocksRaycasts = false;
            cardCanvasGroup.interactable = false;

            RectTransform visualRect = ui.VisualRect != null ? ui.VisualRect : cardGO.transform as RectTransform;
            if (visualRect != null)
            {
                visualRect.localScale = ui.OriginalLocalScale * cardStartScale;
                visualRect.anchoredPosition = ui.OriginalAnchoredPosition + new Vector2(0f, cardStartYOffset);
            }

            rewardButtons.Add(button);
            rewardCardUIs.Add(ui);
        }

        RefreshRewardLayout();
    }

    private void RevealCard(CardUI ui)
    {
        if (ui == null)
        {
            return;
        }

        CanvasGroup cardCanvasGroup = GetOrAddCanvasGroup(ui.gameObject);
        RectTransform visualRect = ui.VisualRect != null ? ui.VisualRect : ui.transform as RectTransform;

        DOTween.Sequence()
            .SetLink(ui.gameObject, LinkBehaviour.KillOnDisable)
            .SetUpdate(true)
            .Join(cardCanvasGroup.DOFade(1f, cardRevealDuration * 0.75f).SetEase(Ease.OutQuad));

        if (visualRect != null)
        {
            DOTween.Sequence()
                .SetLink(ui.gameObject, LinkBehaviour.KillOnDisable)
                .SetUpdate(true)
                .Join(visualRect.DOScale(ui.OriginalLocalScale, cardRevealDuration).SetEase(Ease.OutBack))
                .Join(visualRect.DOAnchorPos(ui.OriginalAnchoredPosition, cardRevealDuration).SetEase(Ease.OutCubic));
        }

        PlayCardGlowVisual();
    }

    private void PlayCardGlowVisual()
    {
        if (cardGlowRoot == null)
        {
            return;
        }

        cardGlowRoot.gameObject.SetActive(true);
        cardGlowRoot.localScale = glowDefaultScale;
        RestartVisualAnimator(cardGlowRoot);
    }

    private void SetRewardCardsInteractable(bool value)
    {
        for (int i = 0; i < rewardButtons.Count; i++)
        {
            if (rewardButtons[i] != null)
            {
                rewardButtons[i].interactable = value;
            }
        }

        for (int i = 0; i < rewardCardUIs.Count; i++)
        {
            if (rewardCardUIs[i] == null)
            {
                continue;
            }

            rewardCardUIs[i].SetInteractable(value);
            CardRaycastController raycastController = rewardCardUIs[i].GetComponent<CardRaycastController>();
            raycastController?.SetBlocksRaycasts(value);
        }
    }

    private void OnCardSelected(CardBase card)
    {
        if (packOpenState != PackOpenState.Choosing || card == null)
        {
            return;
        }

        SetRewardCardsInteractable(false);
        packOpenState = PackOpenState.Idle;

        if (manager != null && manager.player != null)
        {
            manager.player.deck.Add(Instantiate(card));
        }

        AdvanceAfterCardStage();
    }

    private void StopOpenPackFlow()
    {
        if (openPackCoroutine != null)
        {
            StopCoroutine(openPackCoroutine);
            openPackCoroutine = null;
        }

        StopIdleVisual();
        DOTween.Kill(gameObject);
        StopVisualAnimator(cardbagBag);
        StopVisualAnimator(cardbagLight);
        StopVisualAnimator(effectRoot);
        StopVisualAnimator(bottomRoot);
        StopVisualAnimator(cardGlowRoot);
        packOpenState = PackOpenState.Idle;
    }
}
