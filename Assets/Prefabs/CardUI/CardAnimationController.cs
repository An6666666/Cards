using UnityEngine;
using DG.Tweening;

public class CardAnimationController : MonoBehaviour
{
    [Header("抽牌動畫")]
    [SerializeField] private float drawAnimationDuration = 0.35f;
    [SerializeField] private float drawStartScale = 0.5f;
    [SerializeField] private Ease drawAnimationEase = Ease.OutCubic;

    [Header("返回動畫")]
    [SerializeField] private float returnMoveDuration = 0.2f;
    [SerializeField] private Ease returnMoveEase = Ease.InOutQuad;

    [Header("透明度動畫")]
    [SerializeField] private float fadeDuration = 0.15f;

    private CardUI cardUI;
    private CardDragHandler dragHandler;
    private CardRaycastController raycastController;
    private CardHoverEffect hoverEffect;

    private Tweener positionTween;
    private Tweener alphaTween;
    private Tweener scaleTween;

    private bool isPlayingDrawAnimation;
    private int drawAnimationTweenCount;
    private bool allowDraggingBeforeDraw;
    private bool blocksRaycastsBeforeDraw;
    private bool interactableBeforeDraw = true;
    private float originalAlpha = 1f;

    public bool IsPlayingDrawAnimation => isPlayingDrawAnimation;

    public void Initialize(CardUI ui, CardDragHandler drag, CardRaycastController raycast, CardHoverEffect hover)
    {
        cardUI = ui;
        dragHandler = drag;
        raycastController = raycast;
        hoverEffect = hover;

        if (cardUI.CanvasGroup != null)
            originalAlpha = cardUI.CanvasGroup.alpha;
    }

    public void HandleCardEnabled()
    {
        if (cardUI != null && cardUI.VisualRect != null)
        {
            cardUI.VisualRect.localScale = cardUI.OriginalLocalScale;
        }
    }

    public void HandleCardDisabled()
    {
        positionTween?.Kill(); positionTween = null;
        alphaTween?.Kill(); alphaTween = null;
        scaleTween?.Kill(); scaleTween = null;
    }

    public void HandleCardDestroyed()
    {
        positionTween?.Kill();
        alphaTween?.Kill();
        scaleTween?.Kill();
    }

    public Tweener TweenCardPosition(Vector2 targetPosition, float duration, Ease ease)
    {
        if (cardUI == null || cardUI.VisualRect == null) return null;

        positionTween?.Kill(); positionTween = null;

        if (duration <= 0f)
        {
            cardUI.VisualRect.anchoredPosition = targetPosition;
            return null;
        }

        positionTween = cardUI.VisualRect
            .DOAnchorPos(targetPosition, duration)
            .SetEase(ease)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() => positionTween = null);
        return positionTween;
    }

    public void ReturnToHand(Transform placeholder, Transform originalParent, int originalSiblingIndex)
    {
        FadeCardAlpha(originalAlpha);

        if (placeholder != null)
        {
            var targetParent = placeholder.parent != null ? placeholder.parent : originalParent;
            if (targetParent != null)
            {
                transform.SetParent(targetParent, true);
                transform.SetSiblingIndex(placeholder.GetSiblingIndex());
            }
            DestroyPlaceholder(placeholder);
        }
        else if (originalParent != null)
        {
            transform.SetParent(originalParent, true);
            transform.SetSiblingIndex(originalSiblingIndex);
        }

        cardUI.originalParent = transform.parent;
        int siblingIndex = transform.GetSiblingIndex();

        Vector2 targetPosition = cardUI.OriginalAnchoredPosition;

        var returnTween = TweenCardPosition(targetPosition, returnMoveDuration, returnMoveEase);
        if (returnTween != null)
            returnTween.OnComplete(() => cardUI.OriginalAnchoredPosition = cardUI.VisualRect.anchoredPosition);
        else
            cardUI.OriginalAnchoredPosition = cardUI.VisualRect.anchoredPosition;

        if (cardUI.LayoutElement != null) cardUI.LayoutElement.ignoreLayout = false;

        if (hoverEffect != null) hoverEffect.ResetHoverInstant();
    }

    public void FadeCardAlpha(float alpha, bool instant = false)
    {
        if (cardUI == null || cardUI.CanvasGroup == null) return;

        alphaTween?.Kill(); alphaTween = null;

        if (instant || fadeDuration <= 0f)
        {
            cardUI.CanvasGroup.alpha = alpha;
            return;
        }

        alphaTween = cardUI.CanvasGroup
            .DOFade(alpha, fadeDuration)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() => alphaTween = null);
    }

    public void PlayDrawAnimation(RectTransform deckOrigin, float? durationOverride = null, float? startScaleOverride = null, Ease? easeOverride = null)
    {
        if (cardUI == null)
            return;

        if (cardUI.VisualRect == null || cardUI.RootRect == null)
            return;

        if (cardUI.Canvas == null)
            cardUI.Canvas = GetComponentInParent<Canvas>();

        float duration = durationOverride ?? drawAnimationDuration;
        float startScale = startScaleOverride ?? drawStartScale;
        Ease ease = easeOverride ?? drawAnimationEase;

        Vector2 targetAnchoredPosition = cardUI.VisualRect.anchoredPosition;
        Vector3 targetScale = cardUI.OriginalLocalScale;
        Vector2 startingAnchoredPosition = targetAnchoredPosition;

        if (deckOrigin != null)
        {
            Vector3 deckWorldCenter = deckOrigin.TransformPoint(deckOrigin.rect.center);
            Camera camera = null;

            if (cardUI.Canvas != null && cardUI.Canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                camera = cardUI.Canvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    cardUI.RootRect,
                    RectTransformUtility.WorldToScreenPoint(camera, deckWorldCenter),
                    camera,
                    out Vector2 localPoint))
            {
                startingAnchoredPosition = localPoint;
            }
        }

        positionTween?.Kill();
        scaleTween?.Kill();

        BeginDrawAnimationPhase();

        cardUI.VisualRect.anchoredPosition = startingAnchoredPosition;
        cardUI.VisualRect.localScale = targetScale * startScale;

        if (duration <= 0f)
        {
            cardUI.VisualRect.anchoredPosition = targetAnchoredPosition;
            cardUI.VisualRect.localScale = targetScale;
            CompleteDrawAnimationInstantly();
            return;
        }

        RegisterDrawAnimationTween();
        positionTween = cardUI.VisualRect
            .DOAnchorPos(targetAnchoredPosition, duration)
            .SetEase(ease)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() =>
            {
                positionTween = null;
                cardUI.VisualRect.anchoredPosition = targetAnchoredPosition;
                OnDrawAnimationTweenTerminated();
            });

        RegisterDrawAnimationTween();
        scaleTween = cardUI.VisualRect
            .DOScale(targetScale, duration)
            .SetEase(ease)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() =>
            {
                scaleTween = null;
                cardUI.VisualRect.localScale = targetScale;
                OnDrawAnimationTweenTerminated();
            });
    }

    public void ForceResetToHand(Transform newHandParent = null)
    {
        positionTween?.Kill(); positionTween = null;
        alphaTween?.Kill(); alphaTween = null;
        scaleTween?.Kill(); scaleTween = null;
        isPlayingDrawAnimation = false;
        drawAnimationTweenCount = 0;

        if (cardUI.CanvasGroup != null)
        {
            cardUI.CanvasGroup.alpha = originalAlpha;
            cardUI.CanvasGroup.blocksRaycasts = true;
        }

        hoverEffect?.ResetHoverInstant();

        if (newHandParent != null) cardUI.originalParent = newHandParent;
        if (cardUI.originalParent != null) transform.SetParent(cardUI.originalParent, true);
        if (cardUI.LayoutElement != null) cardUI.LayoutElement.ignoreLayout = false;

        if (dragHandler != null) dragHandler.DestroyPlaceholder();

        if (cardUI.VisualRect != null)
            cardUI.VisualRect.localScale = cardUI.OriginalLocalScale;

        if (cardUI.VisualRect != null)
        {
            cardUI.VisualRect.anchoredPosition = Vector2.zero;
            cardUI.OriginalAnchoredPosition = Vector2.zero;
        }
    }

    public void HandleCardConsumed(Transform placeholder)
    {
        FadeCardAlpha(originalAlpha, true);
        if (cardUI.LayoutElement != null) cardUI.LayoutElement.ignoreLayout = false;
        DestroyPlaceholder(placeholder);
        if (dragHandler != null) dragHandler.DestroyPlaceholder();
        var router = GetComponent<CardUseRouter>();
        if (router != null)
            router.BeginConsumeFlow();
    }

    private void DestroyPlaceholder(Transform placeholder)
    {
        if (placeholder == null) return;
        if (placeholder.gameObject != null) Destroy(placeholder.gameObject);
    }

    private void BeginDrawAnimationPhase()
    {
        if (!isPlayingDrawAnimation)
        {
            interactableBeforeDraw = raycastController != null ? raycastController.Interactable : interactableBeforeDraw;
            allowDraggingBeforeDraw = dragHandler != null && dragHandler.AllowDragging;
            blocksRaycastsBeforeDraw = raycastController != null && raycastController.BlocksRaycasts;

            raycastController?.SetInteractable(false);
            if (dragHandler != null) dragHandler.AllowDragging = false;

            raycastController?.SetBlocksRaycasts(false);

            hoverEffect?.ResetHoverInstant();
            hoverEffect?.SuppressNextHoverOnce();
        }

        isPlayingDrawAnimation = true;
        drawAnimationTweenCount = 0;
    }

    private void RegisterDrawAnimationTween()
    {
        drawAnimationTweenCount++;
    }

    private void OnDrawAnimationTweenTerminated()
    {
        if (!isPlayingDrawAnimation)
            return;

        drawAnimationTweenCount = Mathf.Max(0, drawAnimationTweenCount - 1);
        if (drawAnimationTweenCount > 0)
            return;

        EndDrawAnimationPhase();
    }

    private void CompleteDrawAnimationInstantly()
    {
        if (!isPlayingDrawAnimation)
            return;

        EndDrawAnimationPhase();
    }

    private void EndDrawAnimationPhase()
    {
        isPlayingDrawAnimation = false;
        drawAnimationTweenCount = 0;

        if (dragHandler != null)
            dragHandler.AllowDragging = cardUI.CurrentDisplayContext == CardUI.DisplayContext.Hand && allowDraggingBeforeDraw;

        bool shouldBeInteractable = interactableBeforeDraw;
        if (raycastController != null && raycastController.Interactable)
            shouldBeInteractable = true;

        var router = GetComponent<CardUseRouter>();
        bool isLocked = router != null && router.IsCardInteractionLocked();
        shouldBeInteractable = shouldBeInteractable && !isLocked;

        raycastController?.SetInteractable(shouldBeInteractable);

        raycastController?.SetBlocksRaycasts(blocksRaycastsBeforeDraw);

        hoverEffect?.SuppressNextHoverOnce();
    }
}