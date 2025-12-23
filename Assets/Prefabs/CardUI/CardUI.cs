using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(CardHoverEffect))]
[RequireComponent(typeof(CardDragHandler))]
[RequireComponent(typeof(CardAnimationController))]
[RequireComponent(typeof(CardUseRouter))]
[RequireComponent(typeof(CardRaycastController))]
public class CardUI : MonoBehaviour
{
    public enum DisplayContext
    {
        Hand,
        Reward
    }

    [Header("UI 參考")]
    public Image cardImage;

    [Header("資料參考")]
    public CardBase cardData;
    public Transform originalParent;

    [Header("Layout可選")]
    [SerializeField] private LayoutElement layoutElement;

    private Canvas canvas;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Transform canvasRoot;
    private Camera mainCamera;
    private DisplayContext displayContext = DisplayContext.Hand;

    private CardHoverEffect hoverEffect;
    private CardDragHandler dragHandler;
    private CardAnimationController animationController;
    private CardUseRouter useRouter;
    private CardRaycastController raycastController;

    public RectTransform RectTransform => rectTransform;
    public Canvas Canvas { get => canvas; set => canvas = value; }
    public CanvasGroup CanvasGroup => canvasGroup;
    public Transform CanvasRoot => canvasRoot;
    public LayoutElement LayoutElement => layoutElement;
    public Camera MainCamera => mainCamera;

    public Vector2 OriginalAnchoredPosition { get; set; }
    public Vector3 OriginalLocalScale { get; private set; }

    public DisplayContext CurrentDisplayContext => displayContext;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        canvasRoot = canvas != null ? canvas.transform : null;
        canvasGroup = GetComponent<CanvasGroup>();
        mainCamera = Camera.main;
        originalParent = transform.parent;

        if (rectTransform != null)
        {
            OriginalAnchoredPosition = rectTransform.anchoredPosition;
            OriginalLocalScale = rectTransform.localScale;
        }

        if (layoutElement == null) layoutElement = GetComponent<LayoutElement>();

        CacheControllers();
    }

    private void OnEnable()
    {
        hoverEffect?.HandleCardEnabled();
        raycastController?.HandleCardEnabled();
        animationController?.HandleCardEnabled();
    }

    private void OnDisable()
    {
        animationController?.HandleCardDisabled();
        hoverEffect?.HandleCardDisabled();
    }

    private void OnDestroy()
    {
        animationController?.HandleCardDestroyed();
        hoverEffect?.HandleCardDestroyed();
    }

    private void CacheControllers()
    {
        hoverEffect = GetComponent<CardHoverEffect>();
        dragHandler = GetComponent<CardDragHandler>();
        animationController = GetComponent<CardAnimationController>();
        useRouter = GetComponent<CardUseRouter>();
        raycastController = GetComponent<CardRaycastController>();

        hoverEffect.Initialize(this);
        dragHandler.Initialize(this, animationController, useRouter, raycastController, hoverEffect);
        animationController.Initialize(this, dragHandler, raycastController, hoverEffect);
        useRouter.Initialize(this);
        raycastController.Initialize(this, animationController);
    }

    public void SetupCard(CardBase data)
    {
        cardData = data;
        if (cardImage != null && data != null && data.cardImage != null)
            cardImage.sprite = data.cardImage;
    }

    public void SetDisplayContext(DisplayContext context)
    {
        displayContext = context;
        dragHandler?.SetDisplayContext(context);
        hoverEffect?.ResetHoverInstant();
    }

    public void ForceResetToHand(Transform newHandParent = null)
    {
        animationController?.ForceResetToHand(newHandParent);
    }

    public void SetInteractable(bool value)
    {
        raycastController?.SetInteractable(value);
    }

    public void PlayDrawAnimation(RectTransform deckOrigin, float? durationOverride = null, float? startScaleOverride = null, DG.Tweening.Ease? easeOverride = null)
    {
        animationController?.PlayDrawAnimation(deckOrigin, durationOverride, startScaleOverride, easeOverride);
    }
}