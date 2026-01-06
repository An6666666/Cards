using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RunMapUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RunManager runManager;
    [SerializeField] private RectTransform mapContainer;     // ScrollRect/Viewport/Content
    [SerializeField] private Button nodeButtonPrefab;
    [SerializeField] private Image connectionLinePrefab;

    [Header("Scroll / BG")]
    [SerializeField] private RectTransform viewport;         // ScrollRect 的 Viewport（有 Mask 那個）
    [SerializeField] private RectTransform bgRect;           // BG 的 RectTransform（放在 Content 底下）

    [Header("UI Panels")]
    [SerializeField] private GameObject legendPanel;

    [Header("Layout")]
    [SerializeField] private float floorSpacing = 200f;
    [SerializeField] private float nodeSpacing = 160f;
    [SerializeField] private float connectionThickness = 6f;
    [SerializeField] private float horizontalPositionJitter = 40f;
    [SerializeField] private float verticalPositionJitter = 30f;
    [SerializeField] private float connectionAnchorJitter = 20f;

    [Header("Padding (控制上/下留白)")]
    [SerializeField] private float topPadding = 120f;
    [SerializeField] private float bottomPadding = 120f;

    [Header("Colors")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color completedColor = new Color(0.6f, 0.6f, 0.6f);
    [SerializeField] private Color currentColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f);

    [Header("Optional Roots")]
    [SerializeField] private RectTransform nodesRoot;
    [SerializeField] private RectTransform linesRoot;

    [Header("Node Icons")]
    [SerializeField] private Sprite battleIcon;
    [SerializeField] private Sprite eliteIcon;
    [SerializeField] private Sprite shopIcon;
    [SerializeField] private Sprite restIcon;
    [SerializeField] private Sprite eventIcon;
    [SerializeField] private Sprite bossIcon;

    private float _lastTopPadding;
    private float _lastBottomPadding;


    private readonly Dictionary<MapNodeData, RectTransform> nodeRects = new Dictionary<MapNodeData, RectTransform>();
    private readonly Dictionary<MapNodeData, Button> nodeButtons = new Dictionary<MapNodeData, Button>();
    private float refreshTimer;
    private bool _hasBuilt;
    private float _builtMinY;
    private float _builtMaxY;

    private float _currentShiftY;


    private void Awake()
    {
        runManager = RunManager.Instance;
    }

    private void OnEnable()
    {
        runManager = RunManager.Instance;

        if (runManager != null)
        {
            runManager.MapGenerated += HandleMapGenerated;
            runManager.MapStateChanged += HandleMapStateChanged;

            if (runManager.MapFloors != null && runManager.MapFloors.Count > 0)
            {
                HandleMapGenerated(runManager.MapFloors);
            }
            else
            {
                ClearMap();
            }
        }
        _lastTopPadding = topPadding;
        _lastBottomPadding = bottomPadding;
    }

    private void OnDisable()
    {
        if (runManager != null)
        {
            runManager.MapGenerated -= HandleMapGenerated;
            runManager.MapStateChanged -= HandleMapStateChanged;
        }
    }

    private void Update()
    {
        if (!Mathf.Approximately(_lastTopPadding, topPadding) || !Mathf.Approximately(_lastBottomPadding, bottomPadding))
        {
            _lastTopPadding = topPadding;
            _lastBottomPadding = bottomPadding;

            if (runManager != null && runManager.MapFloors != null && runManager.MapFloors.Count > 0 && _hasBuilt)
            {
                if (nodesRoot != null) nodesRoot.anchoredPosition = Vector2.zero;
                if (linesRoot != null) linesRoot.anchoredPosition = Vector2.zero;

                ApplyPaddingAndResize(_builtMinY, _builtMaxY);
            }

            return;
        }

        refreshTimer += Time.deltaTime;
        if (refreshTimer >= 0.25f)
        {
            refreshTimer = 0f;
            RefreshNodeStates();
            RefreshLegendPanel();
        }
    }

    private void HandleMapGenerated(IReadOnlyList<IReadOnlyList<MapNodeData>> floors)
    {
        BuildMap(floors);
        RefreshNodeStates();
        RefreshLegendPanel();
    }

    private void HandleMapStateChanged()
    {
        RefreshNodeStates();
        RefreshLegendPanel();
    }

    private void ClearMap()
    {
        nodeButtons.Clear();
        nodeRects.Clear();

        if (mapContainer == null) return;

        if (nodesRoot != null)
        {
            for (int i = nodesRoot.childCount - 1; i >= 0; i--)
                Destroy(nodesRoot.GetChild(i).gameObject);
        }

        if (linesRoot != null)
        {
            for (int i = linesRoot.childCount - 1; i >= 0; i--)
                Destroy(linesRoot.GetChild(i).gameObject);
        }

        if (nodesRoot == null && linesRoot == null)
        {
            for (int i = mapContainer.childCount - 1; i >= 0; i--)
            {
                var child = mapContainer.GetChild(i);
                // BG 不刪（如果 BG 是 Content 的子物件）
                if (bgRect != null && child == bgRect) continue;
                Destroy(child.gameObject);
            }
        }
    }

    private Sprite GetIcon(MapNodeType type)
    {
        return type switch
        {
            MapNodeType.Battle => battleIcon,
            MapNodeType.EliteBattle => eliteIcon,
            MapNodeType.Shop => shopIcon,
            MapNodeType.Rest => restIcon,
            MapNodeType.Event => eventIcon,
            MapNodeType.Boss => bossIcon,
            _ => null
        };
    }

    private void BuildMap(IReadOnlyList<IReadOnlyList<MapNodeData>> floors)
{
    ClearMap();

    if (mapContainer == null || nodeButtonPrefab == null || floors == null) return;
    if (floors.Count == 0) return;

    // ✅ 讓 Content 用「上邊」當 0（需要你 Inspector pivot=Top）
    // 這樣 y 往下就是負數，符合你現在節點 anchor(0.5,1) 的設計

    float minY = float.PositiveInfinity;  // 最低點（最負）
    float maxY = float.NegativeInfinity;  // 最高點（最接近 0）

    for (int floorIndex = 0; floorIndex < floors.Count; floorIndex++)
    {
        var floorNodes = floors[floorIndex];
        if (floorNodes == null || floorNodes.Count == 0) continue;

        float width = (floorNodes.Count - 1) * nodeSpacing;

        for (int nodeIndex = 0; nodeIndex < floorNodes.Count; nodeIndex++)
        {
            MapNodeData node = floorNodes[nodeIndex];

            Transform nodeParent = nodesRoot != null ? nodesRoot : mapContainer;
            Button buttonInstance = Instantiate(nodeButtonPrefab, nodeParent);
            buttonInstance.name = $"Node_{node.NodeId}";

            RectTransform buttonRect = buttonInstance.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);

            float x = (floorNodes.Count == 1) ? 0f : -width * 0.5f + nodeIndex * nodeSpacing;

            // ✅ 關鍵：先不要套 topPadding（等全部生完再統一 shift）
            float baseY = -(floorIndex * floorSpacing);

            float jitterX = horizontalPositionJitter > 0f ? Random.Range(-horizontalPositionJitter, horizontalPositionJitter) : 0f;
            float jitterY = verticalPositionJitter > 0f ? Random.Range(-verticalPositionJitter, verticalPositionJitter) : 0f;

            Vector2 pos = new Vector2(x + jitterX, baseY + jitterY);
            buttonRect.anchoredPosition = pos;

            float halfH = buttonRect.rect.height * 0.5f;
            minY = Mathf.Min(minY, pos.y - halfH);
            maxY = Mathf.Max(maxY, pos.y + halfH);

            ConfigureButtonVisuals(buttonInstance, node);
            buttonInstance.onClick.AddListener(() => OnNodeClicked(node));

            nodeButtons[node] = buttonInstance;
            nodeRects[node] = buttonRect;
        }
    }

    // 先畫線
ClearLines();
CreateConnections();

// ✅ 套 padding（對齊最高點到 -topPadding）
ApplyPaddingAndResize(minY, maxY);
_hasBuilt = true;

}

    private void ResizeContentAndBG(float minYAfterShift)
{
    if (mapContainer == null) return;

    // ✅ Content 的頂端是 y=0
    // 最低點是 minY（負數），底部要再留 bottomPadding
    float contentHeight = (-minYAfterShift) + bottomPadding;

    if (viewport != null)
        contentHeight = Mathf.Max(contentHeight, viewport.rect.height);

    mapContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

    if (bgRect != null)
    {
        // ✅ BG 寬度建議用 stretch X（Inspector 做），這裡就不硬設寬
        bgRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

        // 保證 BG 貼頂
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.SetAsFirstSibling();
    }
}

    // ✅ 建議：新增一個清線方法（避免 linesRoot 為 null 時線清不乾淨）
private void ClearLines()
{
    if (linesRoot != null)
    {
        for (int i = linesRoot.childCount - 1; i >= 0; i--)
            Destroy(linesRoot.GetChild(i).gameObject);
        return;
    }

    // 如果你沒設 linesRoot，線是生在 mapContainer 底下
    // 就把名字開頭是 "Line_" 的刪掉
    if (mapContainer != null)
    {
        for (int i = mapContainer.childCount - 1; i >= 0; i--)
        {
            var go = mapContainer.GetChild(i).gameObject;
            if (go.name.StartsWith("Line_"))
                Destroy(go);
        }
    }
}

private void ApplyPaddingAndResize(float minY, float maxY)
{
    // 記住「原始」min/max（未套 padding 前）
    _builtMinY = minY;
    _builtMaxY = maxY;

    // 最高點要到 -topPadding
    float shiftY = (-topPadding) - maxY;

    // ✅ 移動 root（優先），不要一顆顆移
    if (nodesRoot != null)
        nodesRoot.anchoredPosition += new Vector2(0f, shiftY);
    else
        ShiftAllNodes(shiftY);

    if (linesRoot != null)
        linesRoot.anchoredPosition += new Vector2(0f, shiftY);

    // ✅ 因為線是用 anchoredPosition 算好的
    // 如果你移動的是 root，線會跟著一起動，不必重畫
    // 如果你是 ShiftAllNodes（一顆顆移），線就必須重畫
    if (nodesRoot == null || linesRoot == null)
    {
        ClearLines();
        CreateConnections();
    }

    // 套完 shift 後的 minY
    float minYAfterShift = minY + shiftY;

    ResizeContentAndBG(minYAfterShift);
}

    private void CreateConnections()
    {
        if (connectionLinePrefab == null) return;

        foreach (var pair in nodeButtons)
        {
            MapNodeData node = pair.Key;
            if (!nodeRects.TryGetValue(node, out RectTransform startRect))
                continue;

            var nextNodes = node.NextNodes;
            if (nextNodes == null) continue;

            for (int i = 0; i < nextNodes.Count; i++)
            {
                MapNodeData nextNode = nextNodes[i];
                if (!nodeRects.TryGetValue(nextNode, out RectTransform endRect))
                    continue;

                Transform lineParent = linesRoot != null ? linesRoot : mapContainer;
                Image lineInstance = Instantiate(connectionLinePrefab, lineParent);
                lineInstance.name = $"Line_{node.NodeId}_to_{nextNode.NodeId}";

                RectTransform lineRect = lineInstance.rectTransform;
                lineRect.anchorMin = new Vector2(0.5f, 1f);
                lineRect.anchorMax = new Vector2(0.5f, 1f);
                lineRect.pivot = new Vector2(0.5f, 0.5f);

                Vector2 start = startRect.anchoredPosition;
                Vector2 end = endRect.anchoredPosition;

                if (connectionAnchorJitter > 0f)
                {
                    start += Random.insideUnitCircle * connectionAnchorJitter;
                    end += Random.insideUnitCircle * connectionAnchorJitter;
                }

                Vector2 dir = end - start;
                float dist = dir.magnitude;

                lineRect.sizeDelta = new Vector2(dist, connectionThickness);
                lineRect.anchoredPosition = start + dir * 0.5f;

                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }

    private void ConfigureButtonVisuals(Button buttonInstance, MapNodeData node)
    {
        if (buttonInstance == null || node == null) return;

        Image frameImage = buttonInstance.targetGraphic as Image;
        if (frameImage != null) frameImage.color = defaultColor;

        // 找子 Image 當 icon（避免抓到 frame）
        Image[] images = buttonInstance.GetComponentsInChildren<Image>(true);
        Image iconImage = null;

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != frameImage)
            {
                iconImage = images[i];
                break;
            }
        }

        if (iconImage != null)
        {
            iconImage.sprite = GetIcon(node.NodeType);
            iconImage.preserveAspect = true;
            iconImage.enabled = iconImage.sprite != null;
        }
    }

    private void RefreshNodeStates()
    {
        if (runManager == null) return;

        foreach (var pair in nodeButtons)
        {
            MapNodeData node = pair.Key;
            Button button = pair.Value;
            if (button == null) continue;

            bool isSelectable = runManager.IsNodeSelectable(node);
            button.interactable = isSelectable;

            Image image = button.targetGraphic as Image;
            if (image == null) continue;

            if (runManager.CurrentNode == node) image.color = currentColor;
            else if (node.IsCompleted) image.color = completedColor;
            else if (isSelectable) image.color = defaultColor;
            else image.color = lockedColor;
        }
    }
    private void ShiftAllNodes(float shiftY)
{
    foreach (var kv in nodeRects)
    {
        if (kv.Value == null) continue;
        kv.Value.anchoredPosition += new Vector2(0f, shiftY);
    }
}


    private void RefreshLegendPanel()
    {
        if (legendPanel == null || runManager == null) return;

        bool isInEvent = runManager.ActiveNode != null
                         && runManager.ActiveNode.NodeType == MapNodeType.Event;

        legendPanel.SetActive(!isInEvent);
    }

    private void OnNodeClicked(MapNodeData node)
    {
        if (runManager == null || node == null) return;
        runManager.TryEnterNode(node);
    }
}
