using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("滑鼠懸停效果")]
    [SerializeField] private float hoverMoveDistance = 20f;
    [SerializeField] private Image hoverGlowImage;
    [SerializeField] private Color hoverGlowColor = Color.green;

    [Header("DOTween 設定")]
    [SerializeField] private float hoverMoveDuration = 0.2f;
    [SerializeField] private Ease hoverMoveEase = Ease.OutQuad;
    [SerializeField] private float hoverGlowFadeDuration = 0.2f;

    [Header("獎勵介面懸停效果")]
    [SerializeField] private float rewardHoverScale = 1.05f;
    [SerializeField] private float rewardHoverDuration = 0.15f;
    [SerializeField] private float rewardReturnDuration = 0.15f;
    [SerializeField] private Ease rewardHoverEase = Ease.OutQuad;
    [SerializeField] private Ease rewardReturnEase = Ease.InOutQuad;

    [Header("回位設定")]
    [SerializeField] private float returnMoveDuration = 0.2f;
    [SerializeField] private Ease returnMoveEase = Ease.InOutQuad;

    private CardUI cardUI;
    private CardAnimationController animationController;
    private CardDragHandler dragHandler;
    private CardRaycastController raycastController;

    private Tweener hoverGlowTween;
    private Tweener scaleTween;
    private bool isHovering;
    private bool suppressNextHover;

    public bool IsHovering => isHovering;

    public void Initialize(CardUI ui)
    {
        cardUI = ui;
        animationController = ui.GetComponent<CardAnimationController>();
        dragHandler = ui.GetComponent<CardDragHandler>();
        raycastController = ui.GetComponent<CardRaycastController>();

        if (hoverGlowImage != null)
        {
            var color = hoverGlowColor;
            color.a = 0f;
            hoverGlowImage.color = color;
            hoverGlowImage.gameObject.SetActive(false);
            hoverGlowImage.raycastTarget = false;
        }
    }

    public void HandleCardEnabled()
    {
        suppressNextHover = false;

        if (cardUI == null || cardUI.RectTransform == null)
            return;

        cardUI.RectTransform.localScale = cardUI.OriginalLocalScale;

        Camera targetCamera = cardUI.MainCamera != null ? cardUI.MainCamera : Camera.main;
        if (EventSystem.current != null &&
            RectTransformUtility.RectangleContainsScreenPoint(cardUI.RectTransform, Input.mousePosition, targetCamera))
        {
            suppressNextHover = true;
        }
    }

    public void HandleCardDisabled()
    {
        hoverGlowTween?.Kill();
        hoverGlowTween = null;
        scaleTween?.Kill();
        scaleTween = null;
    }

    public void HandleCardDestroyed()
    {
        hoverGlowTween?.Kill();
        scaleTween?.Kill();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (dragHandler != null && dragHandler.IsDragging) return;
        if (raycastController != null && !raycastController.Interactable) return;

        if (suppressNextHover) { suppressNextHover = false; return; }

        if (cardUI != null && cardUI.CurrentDisplayContext == CardUI.DisplayContext.Reward)
        {
            AnimateRewardHover(true);
        }
        else
        {
            Vector2 targetPosition = cardUI.OriginalAnchoredPosition + Vector2.up * hoverMoveDistance;
            animationController?.TweenCardPosition(targetPosition, hoverMoveDuration, hoverMoveEase);
        }

        isHovering = true;
        SetHoverGlowVisible(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (dragHandler != null && dragHandler.IsDragging) return;

        suppressNextHover = false;
        ResetHoverPosition();
    }

    public void ResetHoverPosition(bool instant = false)
    {
        if (cardUI == null || animationController == null) return;

        if (cardUI.CurrentDisplayContext == CardUI.DisplayContext.Reward)
        {
            AnimateRewardHover(false, instant);
        }
        else
        {
            animationController.TweenCardPosition(cardUI.OriginalAnchoredPosition, instant ? 0f : returnMoveDuration, returnMoveEase);
        }

        if (isHovering || instant)
        {
            isHovering = false;
            SetHoverGlowVisible(false, instant);
        }
    }

    public void ResetHoverInstant()
    {
        ResetHoverPosition(true);
    }

    public void SuppressNextHoverOnce()
    {
        suppressNextHover = true;
    }

    public void SetHoverGlowColor(Color color)
    {
        hoverGlowColor = color;

        if (hoverGlowImage != null)
        {
            var current = hoverGlowImage.color;
            color.a = current.a;
            hoverGlowImage.color = color;
        }
    }

    private void SetHoverGlowVisible(bool visible, bool instant = false)
    {
        if (hoverGlowImage == null) return;

        hoverGlowTween?.Kill();
        hoverGlowTween = null;

        if (visible) hoverGlowImage.gameObject.SetActive(true);

        float targetAlpha = visible ? 1f : 0f;

        if (instant || hoverGlowFadeDuration <= 0f)
        {
            var color = hoverGlowImage.color;
            color.a = targetAlpha;
            hoverGlowImage.color = color;
            if (!visible) hoverGlowImage.gameObject.SetActive(false);
            return;
        }

        hoverGlowTween = hoverGlowImage
            .DOFade(targetAlpha, hoverGlowFadeDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() => hoverGlowTween = null)
            .OnComplete(() => { if (!visible) hoverGlowImage.gameObject.SetActive(false); });
    }

    private void AnimateRewardHover(bool hover, bool instant = false)
    {
        if (cardUI == null || cardUI.RectTransform == null) return;

        scaleTween?.Kill();
        scaleTween = null;

        Vector3 targetScale = hover ? cardUI.OriginalLocalScale * rewardHoverScale : cardUI.OriginalLocalScale;
        float duration = hover ? rewardHoverDuration : rewardReturnDuration;
        Ease ease = hover ? rewardHoverEase : rewardReturnEase;

        if (instant || duration <= 0f)
        {
            cardUI.RectTransform.localScale = targetScale;
            return;
        }

        scaleTween = cardUI.RectTransform
            .DOScale(targetScale, duration)
            .SetEase(ease)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() => scaleTween = null);
    }
}