using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RunLegendTooltipController : MonoBehaviour
{
    [Serializable]
    public class LegendTooltipEntry
    {
        public string targetName;
        public string title;
        [TextArea(2, 5)] public string description;
    }

    [Header("Entries")]
    [SerializeField] private List<LegendTooltipEntry> entries = new List<LegendTooltipEntry>();

    [Header("Tooltip References")]
    [SerializeField] private RectTransform tooltipRoot;
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;

    [Header("Position")]
    [SerializeField] private Vector2 offset = new Vector2(-260f, 60f);

    private readonly Dictionary<string, LegendTooltipEntry> entryLookup = new Dictionary<string, LegendTooltipEntry>(StringComparer.OrdinalIgnoreCase);
    private Canvas parentCanvas;
    private RectTransform canvasRect;
    private RectTransform hoveredTarget;

    private void Reset()
    {
        EnsureDefaultEntries();
        EnsureAllDeckCounterEntry();
    }

    private void OnValidate()
    {
        EnsureDefaultEntries();
        EnsureAllDeckCounterEntry();
        BuildLookup();
    }

    private void Awake()
    {
        EnsureDefaultEntries();
        EnsureAllDeckCounterEntry();
        BuildLookup();
        ResolveTooltipReferences();
        RegisterChildren();
        Hide();
    }

    private void OnEnable()
    {
        ResolveTooltipReferences();
        RegisterChildren();
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

    public void ShowFor(RectTransform target)
    {
        if (target == null)
            return;

        LegendTooltipEntry entry = ResolveEntry(target.name);
        if (entry == null || string.IsNullOrWhiteSpace(entry.description))
            return;

        ResolveTooltipReferences();
        if (tooltipRoot == null)
            return;

        hoveredTarget = target;

        if (titleText != null)
            titleText.text = string.IsNullOrWhiteSpace(entry.title) ? GetDisplayName(target.name) : entry.title;

        if (descriptionText != null)
            descriptionText.text = entry.description;

        RefreshPosition(target);
        tooltipRoot.SetAsLastSibling();
        tooltipRoot.gameObject.SetActive(true);
    }

    public void Hide()
    {
        hoveredTarget = null;
        if (tooltipRoot != null)
            tooltipRoot.gameObject.SetActive(false);
    }

    private void RegisterChildren()
    {
        CacheCanvasReferences();
        Transform searchRoot = canvasRect != null ? canvasRect : transform;
        RectTransform[] targets = searchRoot.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < targets.Length; i++)
        {
            RectTransform childRect = targets[i];
            if (childRect == null || childRect == tooltipRoot)
                continue;

            if (ResolveEntry(childRect.name) == null)
                continue;

            if (string.Equals(NormalizeName(childRect.name), "AllDeck CounterButton", StringComparison.OrdinalIgnoreCase))
            {
                RunAllDeckCounterButton.Attach(childRect);
            }

            RunLegendTooltipTarget target = childRect.GetComponent<RunLegendTooltipTarget>();
            if (target == null)
                target = childRect.gameObject.AddComponent<RunLegendTooltipTarget>();

            target.Bind(this, childRect);
        }
    }

    private LegendTooltipEntry ResolveEntry(string objectName)
    {
        BuildLookup();

        string key = NormalizeName(objectName);
        if (entryLookup.TryGetValue(key, out LegendTooltipEntry entry))
            return entry;

        return null;
    }

    private void BuildLookup()
    {
        entryLookup.Clear();
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            LegendTooltipEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.targetName))
                continue;

            entryLookup[NormalizeName(entry.targetName)] = entry;
        }
    }

    private void EnsureDefaultEntries()
    {
        if (entries == null)
            entries = new List<LegendTooltipEntry>();

        AddDefaultEntry("Battle", "一般戰鬥", "進入普通戰鬥，遭遇一般妖怪。");
        AddDefaultEntry("Elite", "菁英戰鬥", "進入較高難度的菁英戰鬥，通常有更強的妖怪與更好的獎勵。");
        AddDefaultEntry("Shop", "商店", "進入商店，可以購買卡牌、遺物或其他補給。");
        AddDefaultEntry("Rest", "休息", "進入休息節點，恢復生命值並整理狀態。");
        AddDefaultEntry("Event", "事件", "進入事件節點，可能獲得獎勵、觸發選擇或遭遇特殊狀況。");
        AddDefaultEntry("Boss", "首領", "進入本輪路線的首領戰。");
    }

    private void EnsureAllDeckCounterEntry()
    {
        AddDefaultEntry("AllDeck CounterButton", "總牌庫", "查看目前擁有的所有卡牌。");
    }

    private void AddDefaultEntry(string targetName, string title, string description)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            LegendTooltipEntry entry = entries[i];
            if (entry != null && string.Equals(NormalizeName(entry.targetName), targetName, StringComparison.OrdinalIgnoreCase))
                return;
        }

        entries.Add(new LegendTooltipEntry
        {
            targetName = targetName,
            title = title,
            description = description
        });
    }

    private void ResolveTooltipReferences()
    {
        CacheCanvasReferences();
        if (tooltipRoot == null)
            return;

        if (titleText == null)
            titleText = tooltipRoot.Find("TitleText")?.GetComponent<Text>();

        if (descriptionText == null)
            descriptionText = tooltipRoot.Find("DescriptionText")?.GetComponent<Text>();

        EnsureTooltipIgnoresRaycasts();
    }

    private void EnsureTooltipIgnoresRaycasts()
    {
        if (tooltipRoot == null)
            return;

        CanvasGroup canvasGroup = tooltipRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = tooltipRoot.gameObject.AddComponent<CanvasGroup>();

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

    private void CacheCanvasReferences()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        canvasRect = parentCanvas != null ? parentCanvas.transform as RectTransform : null;
    }

    private void RefreshPosition(RectTransform target)
    {
        if (tooltipRoot == null || canvasRect == null || parentCanvas == null || target == null)
            return;

        if (target.IsChildOf(canvasRect))
        {
            Bounds targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, target);
            tooltipRoot.anchoredPosition = ClampToCanvas((Vector2)targetBounds.center + offset);
            return;
        }

        Camera eventCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, target.position);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, eventCamera, out Vector2 localPoint))
        {
            localPoint += offset;
            tooltipRoot.anchoredPosition = ClampToCanvas(localPoint);
        }
    }

    private Vector2 ClampToCanvas(Vector2 localPoint)
    {
        if (canvasRect == null || tooltipRoot == null)
            return localPoint;

        Vector2 halfCanvas = canvasRect.rect.size * 0.5f;
        Vector2 halfTooltip = tooltipRoot.rect.size * 0.5f;
        float x = Mathf.Clamp(localPoint.x, -halfCanvas.x + halfTooltip.x, halfCanvas.x - halfTooltip.x);
        float y = Mathf.Clamp(localPoint.y, -halfCanvas.y + halfTooltip.y, halfCanvas.y - halfTooltip.y);
        return new Vector2(x, y);
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Replace("(Legacy)", string.Empty)
            .Replace("Text", string.Empty)
            .Replace("Image", string.Empty)
            .Trim();
    }

    private static string GetDisplayName(string objectName)
    {
        string normalized = NormalizeName(objectName);
        return string.IsNullOrWhiteSpace(normalized) ? objectName : normalized;
    }
}

public class RunLegendTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RunLegendTooltipController owner;
    private RectTransform rectTransform;

    public void Bind(RunLegendTooltipController tooltipOwner, RectTransform targetRect)
    {
        owner = tooltipOwner;
        rectTransform = targetRect;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.ShowFor(rectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.Hide();
    }
}
