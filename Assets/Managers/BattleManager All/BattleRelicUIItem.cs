using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Binds a battle relic icon and its optional counter / tooltip UI.
/// </summary>
public class BattleRelicUIItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private bool renameToRelicName = true;
    [SerializeField] private Text counterText;

    [Header("Tooltip UI")]
    [SerializeField] private GameObject tooltipRoot;
    [SerializeField] private Text tooltipNameText;
    [SerializeField] private Text tooltipDescriptionText;

    [Header("Relic Hover Sorting")]
    [SerializeField] private int hoveredRelicSortingOrder = 1;

    [Header("Tooltip Sorting")]
    [SerializeField] private int tooltipSortingOrder = 2;
    [SerializeField] private bool overrideTooltipSorting = true;
    [SerializeField] private bool matchParentCanvasSettings = true;

    private BattleManager battleManager;
    private RelicBase boundRelic;
    private RectTransform tooltipRect;
    private Canvas selfCanvas;
    private GraphicRaycaster selfGraphicRaycaster;
    private Canvas parentCanvas;
    private Canvas tooltipCanvas;
    private Vector2 initialTooltipAnchoredPosition;
    private Vector3 initialTooltipLocalScale = Vector3.one;
    private bool hasCachedTooltipTransform;
    private bool hasTooltipTransformOverride;
    private Vector2 tooltipAnchoredPositionOverride;
    private Vector3 tooltipLocalScaleOverride = Vector3.one;
    private RectTransform relicRect;
    private bool isPointerHoveringRelic;

    private void Awake()
    {
        ResolveIconImage();
        ResolveCounterText();
        ResolveTooltipReferences();
        EnsureSelfCanvas();
        HideTooltip();
    }

    private void LateUpdate()
    {
        RefreshCounterText();
        RefreshTooltipHoverState();
    }

    private void OnDisable()
    {
        HideTooltip();
    }

    private void OnDestroy()
    {
        HideTooltip();
    }

    public void Bind(RelicBase relic)
    {
        boundRelic = relic;
        ResolveIconImage();
        ResolveCounterText();
        ResolveTooltipReferences();
        EnsureSelfCanvas();

        if (renameToRelicName && relic != null && !string.IsNullOrWhiteSpace(relic.cardName))
            gameObject.name = $"RelicUI_{relic.cardName}";

        if (iconImage != null)
        {
            iconImage.sprite = relic != null ? relic.cardImage : null;
            iconImage.enabled = iconImage.sprite != null;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
        }

        RefreshCounterText();
        RefreshTooltipContent();
        HideTooltip();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerHoveringRelic = true;
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerHoveringRelic = false;
        HideTooltip();
    }

    private void ResolveIconImage()
    {
        if (iconImage != null && iconImage.transform != transform)
            return;

        Transform iconTransform = transform.Find("Image");
        if (iconTransform != null)
        {
            iconImage = iconTransform.GetComponent<Image>();
            if (iconImage != null)
                return;
        }

        iconTransform = transform.Find("Icon");
        if (iconTransform != null)
        {
            iconImage = iconTransform.GetComponent<Image>();
            if (iconImage != null)
                return;
        }

        iconImage = GetComponentInChildren<Image>(true);
        if (iconImage == GetComponent<Image>())
        {
            Image[] childImages = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < childImages.Length; i++)
            {
                if (childImages[i] != null && childImages[i].transform != transform)
                {
                    iconImage = childImages[i];
                    break;
                }
            }
        }
    }

    private void ResolveCounterText()
    {
        if (counterText != null)
            return;

        Transform existing = transform.Find("CounterText");
        if (existing != null)
            counterText = existing.GetComponent<Text>();
    }

    private void ResolveTooltipReferences()
    {
        if (tooltipRoot == null)
        {
            Transform tooltipTransform = transform.Find("RelicTooltip");
            if (tooltipTransform != null)
                tooltipRoot = tooltipTransform.gameObject;
        }

        if (tooltipRoot == null)
            return;

        if (tooltipRect == null)
            tooltipRect = tooltipRoot.GetComponent<RectTransform>();

        if (!hasCachedTooltipTransform && tooltipRect != null)
        {
            initialTooltipAnchoredPosition = tooltipRect.anchoredPosition;
            initialTooltipLocalScale = tooltipRect.localScale;
            hasCachedTooltipTransform = true;
        }

        if (tooltipNameText == null)
        {
            Transform nameTransform = tooltipRoot.transform.Find("TooltipNameText");
            if (nameTransform != null)
                tooltipNameText = nameTransform.GetComponent<Text>();
        }

        if (tooltipDescriptionText == null)
        {
            Transform descriptionTransform = tooltipRoot.transform.Find("TooltipDescriptionText");
            if (descriptionTransform != null)
                tooltipDescriptionText = descriptionTransform.GetComponent<Text>();
        }
    }

    private void RefreshCounterText()
    {
        if (counterText == null)
            return;

        if (!IsBattleStarted())
        {
            counterText.enabled = false;
            return;
        }

        if (boundRelic == null || !boundRelic.TryGetBattleUiCounter(out string counter))
        {
            counterText.enabled = false;
            return;
        }

        counterText.enabled = true;
        counterText.text = string.IsNullOrWhiteSpace(counter) ? "0" : counter;
    }

    private void ShowTooltip()
    {
        if (tooltipRoot == null)
            return;

        ApplyHoveredSorting(true);
        RefreshTooltipContent();
        EnsureTooltipCanvas();
        RefreshTooltipPosition();
        BringTooltipToFront();
        tooltipRoot.SetActive(true);
    }

    private void HideTooltip()
    {
        ApplyHoveredSorting(false);
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }

    private void RefreshTooltipContent()
    {
        if (boundRelic == null)
        {
            ClearTooltipTexts();
            return;
        }

        if (tooltipNameText != null)
            tooltipNameText.text = boundRelic.cardName;

        if (tooltipDescriptionText != null)
            tooltipDescriptionText.text = boundRelic.description;
    }

    private void ClearTooltipTexts()
    {
        if (tooltipNameText != null)
            tooltipNameText.text = string.Empty;

        if (tooltipDescriptionText != null)
            tooltipDescriptionText.text = string.Empty;
    }

    private void RefreshTooltipPosition()
    {
        if (tooltipRect == null)
            return;

        if (hasTooltipTransformOverride)
        {
            tooltipRect.localScale = tooltipLocalScaleOverride;
            tooltipRect.anchoredPosition = tooltipAnchoredPositionOverride;
            return;
        }

        tooltipRect.localScale = initialTooltipLocalScale;
        tooltipRect.anchoredPosition = initialTooltipAnchoredPosition;
    }

    public void SetTooltipTransformOverride(RectTransform referenceRect)
    {
        SetTooltipTransformOverride(referenceRect, 1f, false, Vector2.zero);
    }

    public void SetTooltipTransformOverride(RectTransform referenceRect, float scaleMultiplier, bool useUniformScale)
    {
        SetTooltipTransformOverride(referenceRect, scaleMultiplier, useUniformScale, Vector2.zero);
    }

    public void SetTooltipTransformOverride(RectTransform referenceRect, float scaleMultiplier, bool useUniformScale, Vector2 positionOffset)
    {
        ResolveTooltipReferences();
        if (tooltipRect == null || referenceRect == null)
        {
            hasTooltipTransformOverride = false;
            return;
        }

        tooltipAnchoredPositionOverride = referenceRect.anchoredPosition + positionOffset;

        Vector2 sourceSize = tooltipRect.sizeDelta;
        Vector2 targetSize = referenceRect.sizeDelta;

        float scaleX = Mathf.Approximately(sourceSize.x, 0f) ? 1f : targetSize.x / sourceSize.x;
        float scaleY = Mathf.Approximately(sourceSize.y, 0f) ? 1f : targetSize.y / sourceSize.y;
        float clampedScaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);

        if (useUniformScale)
        {
            float uniformScale = Mathf.Max(scaleX, scaleY) * clampedScaleMultiplier;
            tooltipLocalScaleOverride = new Vector3(uniformScale, uniformScale, initialTooltipLocalScale.z);
        }
        else
        {
            tooltipLocalScaleOverride = new Vector3(scaleX * clampedScaleMultiplier, scaleY * clampedScaleMultiplier, initialTooltipLocalScale.z);
        }

        hasTooltipTransformOverride = true;
    }

    public void ClearTooltipTransformOverride()
    {
        hasTooltipTransformOverride = false;
    }

    private void EnsureTooltipCanvas()
    {
        if (tooltipRoot == null)
            return;

        parentCanvas = ResolveParentCanvas();

        if (tooltipCanvas == null)
            tooltipCanvas = tooltipRoot.GetComponent<Canvas>();
        if (tooltipCanvas == null)
            tooltipCanvas = tooltipRoot.AddComponent<Canvas>();

        tooltipCanvas.overrideSorting = overrideTooltipSorting;
        tooltipCanvas.sortingOrder = tooltipSortingOrder;

        if (!matchParentCanvasSettings || parentCanvas == null)
            return;

        tooltipCanvas.renderMode = parentCanvas.renderMode;
        tooltipCanvas.worldCamera = parentCanvas.worldCamera;
        tooltipCanvas.sortingLayerID = parentCanvas.sortingLayerID;
    }

    private void EnsureSelfCanvas()
    {
        if (selfCanvas == null)
            selfCanvas = GetComponent<Canvas>();
        if (selfCanvas == null)
            selfCanvas = gameObject.AddComponent<Canvas>();

        if (selfGraphicRaycaster == null)
            selfGraphicRaycaster = GetComponent<GraphicRaycaster>();
        if (selfGraphicRaycaster == null)
            selfGraphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();

        parentCanvas = ResolveParentCanvas();

        if (parentCanvas != null)
        {
            selfCanvas.renderMode = parentCanvas.renderMode;
            selfCanvas.worldCamera = parentCanvas.worldCamera;
            selfCanvas.sortingLayerID = parentCanvas.sortingLayerID;
        }

        selfCanvas.overrideSorting = false;
        selfCanvas.sortingOrder = 0;
    }

    private void ApplyHoveredSorting(bool isHovered)
    {
        EnsureSelfCanvas();
        if (selfCanvas == null)
            return;

        selfCanvas.overrideSorting = isHovered;
        selfCanvas.sortingOrder = isHovered ? hoveredRelicSortingOrder : 0;
    }

    private void BringTooltipToFront()
    {
        if (tooltipRect != null)
            tooltipRect.SetAsLastSibling();
    }

    private void RefreshTooltipHoverState()
    {
        if (tooltipRoot == null || !isActiveAndEnabled)
            return;

        bool shouldHover = IsPointerHoveringRelic();
        if (shouldHover == isPointerHoveringRelic)
            return;

        isPointerHoveringRelic = shouldHover;
        if (isPointerHoveringRelic)
            ShowTooltip();
        else
            HideTooltip();
    }

    private bool IsPointerHoveringRelic()
    {
        if (PointerUiBlocker.IsPointerBlockedByOtherUi(transform))
            return false;

        if (relicRect == null)
            relicRect = transform as RectTransform;
        if (relicRect == null)
            return false;

        parentCanvas = ResolveParentCanvas();

        Camera eventCamera = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = parentCanvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(relicRect, Input.mousePosition, eventCamera);
    }

    private Canvas ResolveParentCanvas()
    {
        Canvas[] canvases = GetComponentsInParent<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas candidate = canvases[i];
            if (candidate != null && candidate != selfCanvas)
                return candidate;
        }

        return null;
    }

    private bool IsBattleStarted()
    {
        if (battleManager == null)
        {
            battleManager = BattleRuntimeContext.Active != null
                ? BattleRuntimeContext.Active.Manager
                : FindObjectOfType<BattleManager>();
        }

        return battleManager != null && battleManager.BattleStarted;
    }

}
