using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RewardUI : MonoBehaviour
{
    private enum PackOpenState
    {
        Idle,
        Opening,
        Choosing
    }

    private BattleManager manager;
    private GameObject skipHoverIndicator;
    private bool skipHoverConfigured;
    private bool packHoverConfigured;
    private Coroutine openPackCoroutine;
    private Tween bagIdleTween;
    private Tween lightIdleFadeTween;
    private Tween bagHoverTween;
    private PackOpenState packOpenState = PackOpenState.Idle;
    private readonly List<Button> rewardButtons = new List<Button>();
    private readonly List<CardUI> rewardCardUIs = new List<CardUI>();

    [SerializeField] private Text goldText;
    [SerializeField] private Button packButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private Transform cardParent;

    [Header("Reward Card Display")]
    [SerializeField] private bool useRewardCardScale = true;
    [SerializeField] private Vector3 rewardCardScale = new Vector3(0.8f, 0.8f, 1f);
    [SerializeField] private bool useRewardCardLayoutSize = false;
    [SerializeField] private Vector2 rewardCardPreferredSize = new Vector2(130f, 270f);

    [Header("Pack Timing")]
    [SerializeField] private float idleBagScale = 1.01f;
    [SerializeField] private float idleBagDuration = 0.8f;
    [SerializeField] private float idleLightFadeDuration = 0.9f;
    [SerializeField] private float idleLightMinAlpha = 0.45f;
    [SerializeField] private float openBurstDuration = 0.5f;
    [SerializeField] private float openToRevealDelay = 0.25f;
    [SerializeField] private float bottomRevealDuration = 0.45f;
    [SerializeField] private float cardRevealDuration = 0.3f;
    [SerializeField] private float cardRevealInterval = 0.14f;
    [SerializeField] private float cardStartYOffset = -24f;
    [SerializeField] private float cardStartScale = 0.75f;
    [SerializeField] private float packHoverScale = 1.04f;
    [SerializeField] private float packHoverDuration = 0.12f;
    [SerializeField] private float packHoverReturnDuration = 0.16f;

    private RectTransform cardbagBag;
    private RectTransform cardbagLight;
    private RectTransform effectRoot;
    private RectTransform bottomRoot;
    private RectTransform cardGlowRoot;

    private Vector3 bagDefaultScale = Vector3.one;
    private Vector3 lightDefaultScale = Vector3.one;
    private Vector3 effectDefaultScale = Vector3.one;
    private Vector3 bottomDefaultScale = Vector3.one;
    private Vector3 glowDefaultScale = Vector3.one;

    private void Awake()
    {
        ResolvePackVisualReferences();
        CacheDefaultVisualState();
        ConfigureSkipButtonHover();
        ConfigurePackButtonHover();
        SetSkipHoverVisible(false);
    }

    private void OnDisable()
    {
        StopOpenPackFlow();
        SetSkipHoverVisible(false);
    }

    public void Show(BattleManager bm, int goldReward, List<CardBase> cardChoices)
    {
        manager = bm;

        ResolvePackVisualReferences();
        CacheDefaultVisualState();
        StopOpenPackFlow();
        ClearRewardCards();

        gameObject.SetActive(true);
        SetSkipHoverVisible(false);

        if (goldText != null)
            goldText.text = $"獲得 {goldReward} 金幣";

        if (goldText != null)
            goldText.text = "獲得 " + goldReward + " 金幣";

        if (goldText != null)
            goldText.text = "\u7372\u5F97 " + goldReward + " \u91D1\u5E63";

        PrepareIdleStage();

        if (packButton != null)
        {
            packButton.onClick.RemoveAllListeners();
            packButton.onClick.AddListener(() => OnPackButtonClicked(cardChoices));
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(Close);
        }

        StartIdleVisual();
    }

    private void ConfigureSkipButtonHover()
    {
        if (skipHoverConfigured || skipButton == null)
            return;

        Transform hoverTransform = skipButton.transform.Find("h");
        if (hoverTransform == null)
            return;

        skipHoverIndicator = hoverTransform.gameObject;
        skipHoverIndicator.SetActive(false);

        Image hoverImage = skipHoverIndicator.GetComponent<Image>();
        if (hoverImage != null)
            hoverImage.raycastTarget = false;

        EventTrigger trigger = skipButton.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = skipButton.gameObject.AddComponent<EventTrigger>();

        AddHoverEntry(trigger, EventTriggerType.PointerEnter, true);
        AddHoverEntry(trigger, EventTriggerType.PointerExit, false);
        skipHoverConfigured = true;
    }

    private void AddHoverEntry(EventTrigger trigger, EventTriggerType eventType, bool visible)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(_ => SetSkipHoverVisible(visible));
        trigger.triggers.Add(entry);
    }

    private void SetSkipHoverVisible(bool visible)
    {
        if (skipHoverIndicator != null)
            skipHoverIndicator.SetActive(visible);
    }

    private void ConfigurePackButtonHover()
    {
        if (packHoverConfigured || packButton == null)
            return;

        EventTrigger trigger = packButton.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = packButton.gameObject.AddComponent<EventTrigger>();

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
            return;

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
            return;

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
            return;

        StopIdleVisual();

        if (openPackCoroutine != null)
            StopCoroutine(openPackCoroutine);

        openPackCoroutine = StartCoroutine(PlayOpenPackFlow(cardChoices));
    }

    private IEnumerator PlayOpenPackFlow(List<CardBase> cardChoices)
    {
        packOpenState = PackOpenState.Opening;

        if (packButton != null)
            packButton.interactable = false;

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

    private void PrepareIdleStage()
    {
        packOpenState = PackOpenState.Idle;

        if (packButton != null)
        {
            packButton.gameObject.SetActive(true);
            packButton.interactable = true;
        }

        if (cardParent != null)
            cardParent.gameObject.SetActive(false);

        ResetRectVisual(cardbagBag, true, bagDefaultScale, 1f);
        ResetRectVisual(cardbagLight, true, lightDefaultScale, 1f);
        ResetRectVisual(effectRoot, false, effectDefaultScale, 1f);
        ResetRectVisual(bottomRoot, false, bottomDefaultScale, 1f);
        ResetRectVisual(cardGlowRoot, false, glowDefaultScale, 1f);

        RestartVisualAnimator(cardbagBag);
        RestartVisualAnimator(cardbagLight);
    }

    private void StartIdleVisual()
    {
        StartIdleBagVisual();

        if (cardbagLight != null)
        {
            CanvasGroup lightCanvasGroup = GetOrAddCanvasGroup(cardbagLight.gameObject);
            cardbagLight.localRotation = Quaternion.identity;
            lightCanvasGroup.alpha = 1f;
        }
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
            return;

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
            packButton.gameObject.SetActive(false);

        if (cardbagLight != null)
            cardbagLight.gameObject.SetActive(false);

        if (effectRoot != null)
            effectRoot.gameObject.SetActive(false);

        if (cardParent != null)
            cardParent.gameObject.SetActive(true);

        if (bottomRoot == null)
            return;

        bottomRoot.gameObject.SetActive(true);
        bottomRoot.localScale = bottomDefaultScale;
        RestartVisualAnimator(bottomRoot);
    }

    private void DisplayCardChoices(List<CardBase> cardChoices)
    {
        rewardButtons.Clear();
        rewardCardUIs.Clear();

        if (cardParent == null)
            return;

        foreach (CardBase card in cardChoices)
        {
            GameObject cardGO = Instantiate(manager.cardPrefab, cardParent);
            if (useRewardCardScale)
                cardGO.transform.localScale = rewardCardScale;

            if (useRewardCardLayoutSize)
            {
                LayoutElement layoutElement = cardGO.GetComponent<LayoutElement>();
                if (layoutElement == null)
                    layoutElement = cardGO.AddComponent<LayoutElement>();

                layoutElement.preferredWidth = rewardCardPreferredSize.x;
                layoutElement.preferredHeight = rewardCardPreferredSize.y;
            }

            CardUI ui = cardGO.GetComponent<CardUI>();
            if (ui == null)
                continue;

            ui.SetupCard(card);
            ui.SetDisplayContext(CardUI.DisplayContext.Reward);
            ui.SetInteractable(false);

            Button button = cardGO.GetComponentInChildren<Button>(true);
            if (button == null)
                button = cardGO.AddComponent<Button>();

            CardBase captured = card;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnCardSelected(captured));
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
    }

    private void RevealCard(CardUI ui)
    {
        if (ui == null)
            return;

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
            return;

        cardGlowRoot.gameObject.SetActive(true);
        cardGlowRoot.localScale = glowDefaultScale;
        RestartVisualAnimator(cardGlowRoot);
    }

    private void SetRewardCardsInteractable(bool value)
    {
        for (int i = 0; i < rewardButtons.Count; i++)
        {
            if (rewardButtons[i] != null)
                rewardButtons[i].interactable = value;
        }

        for (int i = 0; i < rewardCardUIs.Count; i++)
        {
            if (rewardCardUIs[i] != null)
                rewardCardUIs[i].SetInteractable(value);
        }
    }

    private void ClearRewardCards()
    {
        rewardButtons.Clear();
        rewardCardUIs.Clear();

        if (cardParent == null)
            return;

        foreach (Transform child in cardParent)
            Destroy(child.gameObject);
    }

    private void OnCardSelected(CardBase card)
    {
        if (packOpenState != PackOpenState.Choosing || card == null)
            return;

        manager.player.deck.Add(Instantiate(card));
        Close();
    }

    public void Close()
    {
        StopOpenPackFlow();
        gameObject.SetActive(false);
        RunManager.Instance?.ReturnToRunSceneFromBattle();
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

    private void ResolvePackVisualReferences()
    {
        if (cardbagBag == null)
        {
            cardbagBag = FindNamedRect("packImage");
            if (cardbagBag == null)
                cardbagBag = FindNamedRect("cardbag_bag", packButton != null ? packButton.transform : null);
        }

        if (cardbagLight == null)
            cardbagLight = FindNamedRect("cardbag_light");

        if (effectRoot == null)
            effectRoot = FindNamedRect("effect");

        if (bottomRoot == null)
            bottomRoot = FindNamedRect("bottom", cardParent);

        if (cardGlowRoot == null)
            cardGlowRoot = FindNamedRect("card");
    }

    private void CacheDefaultVisualState()
    {
        bagDefaultScale = cardbagBag != null ? cardbagBag.localScale : Vector3.one;
        lightDefaultScale = cardbagLight != null ? cardbagLight.localScale : Vector3.one;
        effectDefaultScale = effectRoot != null ? effectRoot.localScale : Vector3.one;
        bottomDefaultScale = bottomRoot != null ? bottomRoot.localScale : Vector3.one;
        glowDefaultScale = cardGlowRoot != null ? cardGlowRoot.localScale : Vector3.one;
    }

    private RectTransform FindNamedRect(string childName, Transform fallback = null)
    {
        Transform target = transform.Find(childName);
        if (target == null)
            target = FindDeepChild(transform, childName);

        if (target == null)
            target = fallback;

        return target as RectTransform;
    }

    private Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform result = FindDeepChild(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private void ResetRectVisual(RectTransform rectTransform, bool active, Vector3 scale, float alpha)
    {
        if (rectTransform == null)
            return;

        rectTransform.gameObject.SetActive(active);
        rectTransform.localScale = scale;
        rectTransform.localRotation = Quaternion.identity;

        CanvasGroup canvasGroup = GetOrAddCanvasGroup(rectTransform.gameObject);
        canvasGroup.alpha = alpha;

        bool isPackButtonRoot = packButton != null && rectTransform == packButton.transform;
        canvasGroup.blocksRaycasts = isPackButtonRoot;
        canvasGroup.interactable = isPackButtonRoot;
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        if (target == null)
            return null;

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = target.AddComponent<CanvasGroup>();

        return canvasGroup;
    }

    private void StopVisualAnimator(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        Animator animator = rectTransform.GetComponent<Animator>();
        if (animator == null)
            animator = rectTransform.GetComponentInChildren<Animator>(true);

        if (animator == null)
            return;

        animator.speed = 1f;
        animator.enabled = false;
    }

    private void RestartVisualAnimator(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        Animator animator = rectTransform.GetComponent<Animator>();
        if (animator == null)
            animator = rectTransform.GetComponentInChildren<Animator>(true);

        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        animator.enabled = true;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.speed = 1f;
        animator.Rebind();
        animator.Update(0f);

        animator.Play(0, 0, 0f);
        animator.Update(0.0001f);
    }

    private AnimationClip GetPrimaryAnimationClip(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return null;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0)
            return null;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                return clips[i];
        }

        return null;
    }
}
