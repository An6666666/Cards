using UnityEngine;
using UnityEngine.UI;

public class CardInfoTooltip : MonoBehaviour
{
    [Header("Tooltip UI")]
    [SerializeField] private GameObject tooltipRoot;
    [SerializeField] private Text cardNameText;
    [SerializeField] private Text costText;
    [SerializeField] private Text descriptionText;
    [Header("Positioning")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 0f);

    [Header("Sorting")]
    [SerializeField] private int sortingOrder = 5000;
    [SerializeField] private bool overrideSorting = true;
    [SerializeField] private bool matchParentCanvasSettings = true;

    private CardUI cardUI;
    private RectTransform tooltipRect;
    private RectTransform cardRect;
    private Canvas tooltipCanvas;

    public void Initialize(CardUI ui)
    {
        cardUI = ui;
        tooltipRect = tooltipRoot != null ? tooltipRoot.GetComponent<RectTransform>() : null;
        cardRect = cardUI != null ? cardUI.GetComponent<RectTransform>() : null;
        if (tooltipRect != null)
        tooltipRect.localScale = Vector3.one;

        EnsureTooltipCanvas();
        Hide();
    }

    public void SetCardData(CardBase data)
    {
        if (data == null)
        {
            ClearTexts();
            return;
        }

        if (cardNameText != null)
            cardNameText.text = data.cardName;

        if (costText != null)
            costText.text = $"Cost: {data.cost}";

        if (descriptionText != null)
            descriptionText.text = data.description;
        
         RefreshPosition();
    }

    public void RefreshPosition()
    {
        if (tooltipRect == null || cardRect == null)
            return;

        float cardHeight = cardRect.rect.height;
        float tooltipHeight = tooltipRect.rect.height;
        tooltipRect.anchoredPosition = new Vector2(
            offset.x,
            offset.y);
    }

    public void Show()
    {
        if (tooltipRoot == null)
            return;

        EnsureTooltipCanvas();
        RefreshPosition();
        BringToFront();
        tooltipRoot.SetActive(true);
    }

    public void Hide()
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }

    private void ClearTexts()
    {
        if (cardNameText != null)
            cardNameText.text = string.Empty;

        if (costText != null)
            costText.text = string.Empty;

        if (descriptionText != null)
            descriptionText.text = string.Empty;
    }

    private void EnsureTooltipCanvas()
    {
        if (tooltipRoot == null)
            return;

        tooltipCanvas = tooltipRoot.GetComponent<Canvas>();
        if (tooltipCanvas == null)
            tooltipCanvas = tooltipRoot.AddComponent<Canvas>();

        tooltipCanvas.overrideSorting = overrideSorting;
        tooltipCanvas.sortingOrder = sortingOrder;

        if (matchParentCanvasSettings && cardUI != null && cardUI.Canvas != null)
        {
            tooltipCanvas.renderMode = cardUI.Canvas.renderMode;
            tooltipCanvas.worldCamera = cardUI.Canvas.worldCamera;
            tooltipCanvas.sortingLayerID = cardUI.Canvas.sortingLayerID;
        }
    }

    private void BringToFront()
    {
        if (tooltipRect == null)
            return;

        tooltipRect.SetAsLastSibling();
    }
}