using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("拖曳外觀")]
    [SerializeField, Range(0f, 1f)] private float draggingAlpha = 0.5f;

    private static int activeDragCount = 0;
    private CardUI cardUI;
    private CardAnimationController animationController;
    private CardUseRouter useRouter;
    private CardRaycastController raycastController;
    private CardHoverEffect hoverEffect;

    private bool allowDragging = true;
    private bool isDragging;
    private int originalSiblingIndex;
    private Transform placeholder;

    public bool IsDragging => isDragging;
    public static bool IsAnyCardDragging => activeDragCount > 0;

    public bool AllowDragging
    {
        get => allowDragging;
        set => allowDragging = value;
    }

    public void Initialize(CardUI ui, CardAnimationController animation, CardUseRouter router, CardRaycastController raycast, CardHoverEffect hover)
    {
        cardUI = ui;
        animationController = animation;
        useRouter = router;
        raycastController = raycast;
        hoverEffect = hover;
    }

    public void SetDisplayContext(CardUI.DisplayContext context)
    {
        allowDragging = context == CardUI.DisplayContext.Hand;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanDrag()) return;

        isDragging = true;
        activeDragCount++;
        hoverEffect?.ResetHoverInstant();

        cardUI.originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        cardUI.OriginalAnchoredPosition = cardUI.RectTransform.anchoredPosition;

        CreatePlaceholder();

        if (cardUI.LayoutElement != null) cardUI.LayoutElement.ignoreLayout = true;
        if (cardUI.CanvasRoot != null) transform.SetParent(cardUI.CanvasRoot, true);

        animationController?.FadeCardAlpha(draggingAlpha);

        useRouter?.HandleBeginDrag(cardUI.cardData, GetWorldPosition(eventData));

        raycastController?.SetBlocksRaycasts(false);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || cardUI == null || cardUI.RectTransform == null) return;

        float scaleFactor = cardUI.Canvas != null ? cardUI.Canvas.scaleFactor : 1f;
        cardUI.RectTransform.anchoredPosition += eventData.delta / scaleFactor;

        useRouter?.HandleDrag(cardUI.cardData, GetWorldPosition(eventData));
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        CompleteDrag(eventData, null);
    }

    private void OnDisable()
    {
        if (isDragging)
        {
            isDragging = false;
            if (activeDragCount > 0) activeDragCount--;
        }
    }
    
     private void LateUpdate()
    {
        if (!isDragging) return;

        bool mouseReleased = !Input.GetMouseButton(0);
        if (!mouseReleased) return;

        CompleteDrag(null, Input.mousePosition);
    }
    public void DestroyPlaceholder()
    {
        if (placeholder == null) return;

        if (placeholder.gameObject != null)
            Destroy(placeholder.gameObject);

        placeholder = null;
    }

    private bool CanDrag()
    {
        if (raycastController != null && !raycastController.Interactable) return false;
        return allowDragging && cardUI != null && cardUI.RectTransform != null;
    }

    private void CompleteDrag(PointerEventData eventData, Vector2? pointerOverride)
    {
        if (!isDragging) return;

        raycastController?.SetBlocksRaycasts(true);
        isDragging = false;
        if (activeDragCount > 0) activeDragCount--;

        Vector2 pointerPos = pointerOverride ?? (eventData != null ? eventData.position : Input.mousePosition);
        Vector2 worldPos = GetWorldPosition(pointerPos);
        Collider2D hit = Physics2D.OverlapPoint(worldPos);

        bool used = useRouter != null && useRouter.TryHandleDrop(cardUI.cardData, hit, worldPos);

        if (used)
        {
            animationController?.HandleCardConsumed(placeholder);
            placeholder = null;
            return;
        }

        animationController?.ReturnToHand(placeholder, cardUI.originalParent, originalSiblingIndex);
        placeholder = null;
    }

    private void CreatePlaceholder()
    {
        if (placeholder != null || cardUI.originalParent == null) return;

        var placeholderObject = new GameObject($"{name}_Placeholder", typeof(RectTransform));
        placeholder = placeholderObject.transform;
        placeholder.SetParent(cardUI.originalParent, false);
        placeholder.SetSiblingIndex(originalSiblingIndex);
        placeholder.localScale = Vector3.one;

        var placeholderLayoutElement = placeholderObject.AddComponent<LayoutElement>();

        if (cardUI.LayoutElement != null)
        {
            placeholderLayoutElement.preferredWidth = cardUI.LayoutElement.preferredWidth;
            placeholderLayoutElement.preferredHeight = cardUI.LayoutElement.preferredHeight;
            placeholderLayoutElement.minWidth = cardUI.LayoutElement.minWidth;
            placeholderLayoutElement.minHeight = cardUI.LayoutElement.minHeight;
            placeholderLayoutElement.flexibleWidth = cardUI.LayoutElement.flexibleWidth;
            placeholderLayoutElement.flexibleHeight = cardUI.LayoutElement.flexibleHeight;
        }
        else if (cardUI.RectTransform != null)
        {
            var rect = cardUI.RectTransform.rect;
            placeholderLayoutElement.preferredWidth = rect.width;
            placeholderLayoutElement.preferredHeight = rect.height;
        }
    }

    private Vector2 GetWorldPosition(PointerEventData eventData)
    {
        Camera targetCamera = cardUI.MainCamera != null ? cardUI.MainCamera : Camera.main;
        return targetCamera != null
            ? (Vector2)targetCamera.ScreenToWorldPoint(eventData.position)
            : eventData.position;
    }
    private Vector2 GetWorldPosition(Vector2 screenPosition)
    {
        Camera targetCamera = cardUI.MainCamera != null ? cardUI.MainCamera : Camera.main;
        return targetCamera != null
            ? (Vector2)targetCamera.ScreenToWorldPoint(screenPosition)
            : screenPosition;
    }
}