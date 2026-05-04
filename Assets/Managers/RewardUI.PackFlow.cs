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
        ScheduleSkipButtonReveal(true);

        if (openPackCoroutine != null)
        {
            StopCoroutine(openPackCoroutine);
        }

        openPackCoroutine = StartCoroutine(PlayOpenPackFlow(cardChoices));
    }

    private void ScheduleSkipButtonReveal(bool interactableWhenShown)
    {
        StopSkipButtonReveal();
        HideSkipButton();
        skipButtonRevealCoroutine = StartCoroutine(RevealSkipButtonAfterPanelAnimation(interactableWhenShown));
    }

    private IEnumerator RevealSkipButtonAfterPanelAnimation(bool interactableWhenShown)
    {
        float delay = Mathf.Max(0f, skipButtonRevealDelay);
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        if (skipButton != null && !closeRequested)
        {
            bool wasInteractable = skipButton.interactable;
            skipButton.gameObject.SetActive(true);
            skipButton.interactable = interactableWhenShown || wasInteractable;
        }

        skipButtonRevealCoroutine = null;
    }

    private void HideSkipButton()
    {
        if (skipButton == null)
        {
            return;
        }

        skipButton.gameObject.SetActive(false);
        skipButton.interactable = false;
        SetSkipHoverVisible(false);
    }

    private void StopSkipButtonReveal()
    {
        if (skipButtonRevealCoroutine == null)
        {
            return;
        }

        StopCoroutine(skipButtonRevealCoroutine);
        skipButtonRevealCoroutine = null;
    }

    private IEnumerator PlayOpenPackFlow(List<CardBase> cardChoices)
    {
        packOpenState = PackOpenState.Opening;

        if (packButton != null)
        {
            packButton.interactable = false;
        }

        PlayOpenBurstVisual();
        float baseOpenWaitDuration = Mathf.Max(0f, openBurstDuration + openToRevealDelay);
        float openWaitDuration = Mathf.Max(baseOpenWaitDuration, packOpenAnimationWaitDuration);
        if (waitForEffectBeforeBottom)
        {
            float effectWaitDuration = Mathf.Max(0f, GetVisualAnimatorDuration(effectRoot, effectAnimatorSpeed) + effectToBottomTimeOffset);
            openWaitDuration = Mathf.Max(baseOpenWaitDuration, effectWaitDuration);
        }

        yield return new WaitForSecondsRealtime(openWaitDuration);

        PlayBottomRevealVisual();
        yield return new WaitForSecondsRealtime(bottomRevealDuration);

        float cardGlowDelay = Mathf.Max(0f, cardGlowRevealDelayAfterBottom);
        if (cardGlowDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(cardGlowDelay);
        }

        yield return PlayCardGlowVisual();

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
            TriggerVisualAnimator(cardbagBag, packOpenTriggerName, packIdleStateName);

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
            RestartVisualAnimator(effectRoot, effectAnimatorSpeed);
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
            ScheduleEffectHideAfterBottom();
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

    private void ScheduleEffectHideAfterBottom()
    {
        StopEffectHideCoroutine();

        if (effectRoot == null)
        {
            return;
        }

        float overlapDuration = Mathf.Max(0f, effectBottomOverlapDuration);
        if (overlapDuration <= 0f)
        {
            effectRoot.gameObject.SetActive(false);
            return;
        }

        effectHideCoroutine = StartCoroutine(HideEffectAfterBottomOverlap(overlapDuration));
    }

    private IEnumerator HideEffectAfterBottomOverlap(float overlapDuration)
    {
        yield return new WaitForSecondsRealtime(overlapDuration);

        if (effectRoot != null)
        {
            effectRoot.gameObject.SetActive(false);
        }

        effectHideCoroutine = null;
    }

    private void StopEffectHideCoroutine()
    {
        if (effectHideCoroutine == null)
        {
            return;
        }

        StopCoroutine(effectHideCoroutine);
        effectHideCoroutine = null;
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

        if (replayCardGlowOnEachCardReveal)
        {
            StartCoroutine(PlayCardGlowVisual());
        }
    }

    private IEnumerator PlayCardGlowVisual()
    {
        if (cardGlowRoot == null)
        {
            yield break;
        }

        cardGlowRoot.gameObject.SetActive(true);
        cardGlowRoot.localScale = glowDefaultScale;

        if (restartCardGlowChildAnimators)
        {
            SetCardGlowAnimatorsEnabled(false);
        }

        yield return PlayCardGlowChildrenSpreadVisual();

        RestartVisualAnimator(cardGlowRoot, restartCardGlowChildAnimators);
    }

    private IEnumerator PlayCardGlowChildrenSpreadVisual()
    {
        int childCount = cardGlowRoot.childCount;
        if (childCount == 0)
        {
            yield break;
        }

        List<RectTransform> childRects = new List<RectTransform>(childCount);
        List<Vector2> targetPositions = new List<Vector2>(childCount);
        List<Vector3> targetScales = new List<Vector3>(childCount);
        Vector2 startPosition = ResolveCardGlowStartPosition();

        for (int i = 0; i < childCount; i++)
        {
            RectTransform childRect = cardGlowRoot.GetChild(i) as RectTransform;
            if (childRect == null)
            {
                continue;
            }

            childRect.DOKill();
            childRect.gameObject.SetActive(true);

            CanvasGroup canvasGroup = GetOrAddCanvasGroup(childRect.gameObject);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            childRects.Add(childRect);
            targetPositions.Add(childRect.anchoredPosition);
            targetScales.Add(childRect.localScale);

            childRect.anchoredPosition = startPosition;
            childRect.localScale = childRect.localScale * cardGlowStartScale;
        }

        if (childRects.Count == 0)
        {
            yield break;
        }

        float duration = Mathf.Max(0f, cardGlowSpreadDuration);
        if (duration <= 0f)
        {
            for (int i = 0; i < childRects.Count; i++)
            {
                childRects[i].anchoredPosition = targetPositions[i];
                childRects[i].localScale = targetScales[i];

                CanvasGroup canvasGroup = GetOrAddCanvasGroup(childRects[i].gameObject);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }
            }

            yield break;
        }

        Sequence sequence = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .SetUpdate(true);

        float stagger = Mathf.Max(0f, cardGlowChildStagger);
        for (int i = 0; i < childRects.Count; i++)
        {
            RectTransform childRect = childRects[i];
            CanvasGroup canvasGroup = GetOrAddCanvasGroup(childRect.gameObject);
            float delay = stagger * i;

            sequence.Join(childRect.DOAnchorPos(targetPositions[i], duration)
                .SetDelay(delay)
                .SetEase(cardGlowSpreadEase));
            sequence.Join(childRect.DOScale(targetScales[i], duration)
                .SetDelay(delay)
                .SetEase(cardGlowSpreadEase));

            if (canvasGroup != null)
            {
                sequence.Join(canvasGroup.DOFade(1f, Mathf.Min(0.12f, duration))
                    .SetDelay(delay)
                    .SetEase(Ease.OutQuad));
            }
        }

        yield return sequence.WaitForCompletion();
    }

    private Vector2 ResolveCardGlowStartPosition()
    {
        if (cardGlowRoot == null)
        {
            return Vector2.zero;
        }

        Transform card2 = cardGlowRoot.Find("card2");
        RectTransform card2Rect = card2 as RectTransform;
        return card2Rect != null ? card2Rect.anchoredPosition : Vector2.zero;
    }

    private void SetCardGlowAnimatorsEnabled(bool enabled)
    {
        if (cardGlowRoot == null)
        {
            return;
        }

        Animator[] animators = cardGlowRoot.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].enabled = enabled;
            }
        }
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
        if (stageSelectionCommitted || packOpenState != PackOpenState.Choosing || card == null)
        {
            return;
        }

        stageSelectionCommitted = true;
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
        StopEffectHideCoroutine();

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
