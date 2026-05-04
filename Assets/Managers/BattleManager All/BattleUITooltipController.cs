using UnityEngine;
using UnityEngine.UI;

public class BattleUITooltipController : MonoBehaviour
{
    [Header("Tooltip References")]
    [SerializeField] private RectTransform tooltipRoot;
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;

    [Header("Default Position")]
    [SerializeField] private Vector2 defaultOffset = new Vector2(-260f, 60f);

    private Canvas parentCanvas;
    private RectTransform canvasRect;
    private RectTransform hoveredTarget;
    private Vector2 hoveredOffset;

    private void Awake()
    {
        ResolveReferences();
        Hide();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        Hide();
    }

    private void LateUpdate()
    {
        if (tooltipRoot != null && tooltipRoot.gameObject.activeSelf && hoveredTarget != null)
        {
            RefreshPosition(hoveredTarget);
        }
    }

    public void Show(RectTransform target, string title, string description)
    {
        Show(target, title, description, defaultOffset);
    }

    public void Show(RectTransform target, string title, string description, Vector2 positionOffset)
    {
        if (target == null || string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        ResolveReferences();
        if (tooltipRoot == null)
        {
            return;
        }

        hoveredTarget = target;
        hoveredOffset = positionOffset;

        if (titleText != null)
        {
            titleText.text = title ?? string.Empty;
        }

        if (descriptionText != null)
        {
            descriptionText.text = description;
        }

        RefreshPosition(target, hoveredOffset);
        tooltipRoot.SetAsLastSibling();
        tooltipRoot.gameObject.SetActive(true);
    }

    public void Hide()
    {
        hoveredTarget = null;
        hoveredOffset = defaultOffset;
        if (tooltipRoot != null)
        {
            tooltipRoot.gameObject.SetActive(false);
        }
    }

    private void ResolveReferences()
    {
        CacheCanvasReferences();
        if (tooltipRoot == null)
        {
            return;
        }

        if (titleText == null)
        {
            titleText = tooltipRoot.Find("TitleText")?.GetComponent<Text>();
        }

        if (descriptionText == null)
        {
            descriptionText = tooltipRoot.Find("DescriptionText")?.GetComponent<Text>();
        }

        EnsureTooltipIgnoresRaycasts();
    }

    private void CacheCanvasReferences()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        canvasRect = parentCanvas != null ? parentCanvas.transform as RectTransform : null;
    }

    private void EnsureTooltipIgnoresRaycasts()
    {
        if (tooltipRoot == null)
        {
            return;
        }

        CanvasGroup canvasGroup = tooltipRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = tooltipRoot.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Graphic[] graphics = tooltipRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = false;
            }
        }
    }

    private void RefreshPosition(RectTransform target)
    {
        RefreshPosition(target, hoveredOffset);
    }

    private void RefreshPosition(RectTransform target, Vector2 positionOffset)
    {
        if (tooltipRoot == null || canvasRect == null || parentCanvas == null || target == null)
        {
            return;
        }

        if (target.IsChildOf(canvasRect))
        {
            Bounds targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, target);
            tooltipRoot.anchoredPosition = ClampToCanvas((Vector2)targetBounds.center + positionOffset);
            return;
        }

        Camera eventCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, target.position);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, eventCamera, out Vector2 localPoint))
        {
            localPoint += positionOffset;
            tooltipRoot.anchoredPosition = ClampToCanvas(localPoint);
        }
    }

    private Vector2 ClampToCanvas(Vector2 localPoint)
    {
        if (canvasRect == null || tooltipRoot == null)
        {
            return localPoint;
        }

        Vector2 halfCanvas = canvasRect.rect.size * 0.5f;
        Vector2 halfTooltip = tooltipRoot.rect.size * 0.5f;
        float x = Mathf.Clamp(localPoint.x, -halfCanvas.x + halfTooltip.x, halfCanvas.x - halfTooltip.x);
        float y = Mathf.Clamp(localPoint.y, -halfCanvas.y + halfTooltip.y, halfCanvas.y - halfTooltip.y);
        return new Vector2(x, y);
    }
}
