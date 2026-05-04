using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class RunMapUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RunManager runManager;
    [SerializeField] private RectTransform mapContainer;
    [SerializeField] private Button nodeButtonPrefab;
    [SerializeField] private Image connectionLinePrefab;

    [Header("Scroll / BG")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform bgRect;

    [Header("UI Panels")]
    [SerializeField] private GameObject legendPanel;

    [Header("Relic UI")]
    [SerializeField] private GameObject relicIconPrefab;

    [Header("Layout")]
    [SerializeField] private float floorSpacing = 200f;
    [SerializeField] private float nodeSpacing = 160f;
    [SerializeField] private float connectionThickness = 6f;
    [SerializeField, Range(0.2f, 0.95f)] private float floorBandFill = 0.72f;
    [SerializeField, Min(0f)] private float nodeOverlapPadding = 12f;
    [SerializeField, Min(0f)] private float sameFloorMinDistance = 96f;
    [SerializeField, Min(0f)] private float differentFloorMinDistance = 24f;
    [SerializeField, Min(1)] private int minHorizontalSlotsPerFloor = 5;
    [SerializeField, Min(0f)] private float maxConnectionHorizontalOffset = 180f;
    [SerializeField, Min(0)] private int relaxedBossApproachFloorCount = 2;
    [SerializeField, Range(0f, 1f)] private float bossApproachChildPullMultiplier = 0.35f;
    [SerializeField, Min(0f)] private float bossApproachSpreadBoost = 0.9f;
    [SerializeField] private float horizontalPositionJitter = 40f;
    [SerializeField] private float verticalPositionJitter = 30f;
    [SerializeField] private float connectionAnchorJitter = 20f;

    [Header("Padding")]
    [SerializeField] private float topPadding = 120f;
    [SerializeField] private float bottomPadding = 120f;
    [SerializeField, Range(0f, 1f)] private float focusedNodeViewportY = 0.5f;
    [SerializeField, Min(0f)] private float focusedNodeForwardPreviewOffset = 160f;

    [Header("Colors")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color completedColor = new Color(0.6f, 0.6f, 0.6f);
    [SerializeField] private Color currentColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f);

    [Header("Node Hover")]
    [SerializeField, Range(1f, 1.3f)] private float selectableNodeHoverScale = 1.08f;

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

    private readonly Dictionary<MapNodeData, RectTransform> nodeRects = new Dictionary<MapNodeData, RectTransform>();
    private readonly Dictionary<MapNodeData, Button> nodeButtons = new Dictionary<MapNodeData, Button>();
    private readonly Dictionary<MapNodeData, Vector3> nodeBaseScales = new Dictionary<MapNodeData, Vector3>();

    private float lastTopPadding;
    private float lastBottomPadding;
    private bool hasBuilt;
    private float builtMinY;
    private float builtMaxY;

    public GameObject RelicIconPrefab => relicIconPrefab;

    private readonly struct ConnectionSegment
    {
        public ConnectionSegment(MapNodeData source, MapNodeData target, Vector2 start, Vector2 end)
        {
            Source = source;
            Target = target;
            Start = start;
            End = end;
        }

        public MapNodeData Source { get; }
        public MapNodeData Target { get; }
        public Vector2 Start { get; }
        public Vector2 End { get; }
    }

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
                HandleMapGenerated(runManager.MapFloors);
            else
                ClearMap();
        }

        lastTopPadding = topPadding;
        lastBottomPadding = bottomPadding;
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
        if (Mathf.Approximately(lastTopPadding, topPadding) && Mathf.Approximately(lastBottomPadding, bottomPadding))
            return;

        lastTopPadding = topPadding;
        lastBottomPadding = bottomPadding;

        if (runManager == null || runManager.MapFloors == null || runManager.MapFloors.Count == 0 || !hasBuilt)
            return;

        if (nodesRoot != null)
            nodesRoot.anchoredPosition = Vector2.zero;
        if (linesRoot != null)
            linesRoot.anchoredPosition = Vector2.zero;

        ApplyPaddingAndResize(builtMinY, builtMaxY);
        SnapMapToFocusedNode();
    }

    private void HandleMapGenerated(IReadOnlyList<IReadOnlyList<MapNodeData>> floors)
    {
        BuildMap(floors);
        RefreshNodeStates();
        RefreshLegendPanel();
        SnapMapToFocusedNode();
    }

    private void HandleMapStateChanged()
    {
        RefreshNodeStates();
        RefreshLegendPanel();
        SnapMapToFocusedNode();
    }

    private void SnapMapToFocusedNode()
    {
        if (!hasBuilt || mapContainer == null || viewport == null)
            return;

        MapNodeData focusNode = GetFocusNode();
        if (focusNode == null || !nodeRects.TryGetValue(focusNode, out RectTransform nodeRect) || nodeRect == null)
            return;

        Canvas.ForceUpdateCanvases();

        Vector3 nodeWorldCenter = nodeRect.TransformPoint(nodeRect.rect.center);
        Vector3 nodeViewportLocal = viewport.InverseTransformPoint(nodeWorldCenter);

        float desiredViewportY = Mathf.Lerp(-viewport.rect.height, 0f, Mathf.Clamp01(focusedNodeViewportY));
        float deltaY = desiredViewportY - nodeViewportLocal.y;
        Vector2 anchoredPosition = mapContainer.anchoredPosition;
        anchoredPosition.y += deltaY + focusedNodeForwardPreviewOffset;

        float maxScrollY = Mathf.Max(0f, mapContainer.rect.height - viewport.rect.height);
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, 0f, maxScrollY);
        mapContainer.anchoredPosition = anchoredPosition;
    }

    private MapNodeData GetFocusNode()
    {
        if (runManager == null)
            return null;

        if (runManager.CurrentNode != null)
            return runManager.CurrentNode;

        if (runManager.ActiveNode != null)
            return runManager.ActiveNode;

        return null;
    }
}
