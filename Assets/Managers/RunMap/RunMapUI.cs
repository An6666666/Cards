using System.Collections.Generic;            // 要用 Dictionary、List
using TMPro;                                 // 用來抓 TMP_Text 顯示節點文字
using UnityEngine;
using UnityEngine.UI;

public class RunMapUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RunManager runManager;          // 引用你的核心 RunManager，用來拿地圖資料、問節點能不能點
    [SerializeField] private RectTransform mapContainer;     // UI 上擺節點跟連線的父物件（通常是一個空的 RectTransform）
    [SerializeField] private Button nodeButtonPrefab;        // 每一個節點要用的按鈕 Prefab
    [SerializeField] private Image connectionLinePrefab;     // 節點之間的連線 Prefab（用 Image 來當線）

    [Header("Layout")]
    [SerializeField] private float floorSpacing = 200f;      // 一層跟一層之間的垂直距離
    [SerializeField] private float nodeSpacing = 160f;       // 同一層節點之間的水平距離
    [SerializeField] private float connectionThickness = 6f; // 連線的粗細

    [Header("Colors")]
    [SerializeField] private Color defaultColor = Color.white;                    // 一般節點的顏色
    [SerializeField] private Color completedColor = new Color(0.6f, 0.6f, 0.6f);  // 已經完成的節點顏色
    [SerializeField] private Color currentColor = new Color(1f, 0.85f, 0.3f);     // 玩家目前所在節點顏色
    [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f);     // 還不能點的節點顏色

    // 把每個 MapNodeData 對應到它的 Button，之後要更新顏色/可互動性會用到
    private readonly Dictionary<MapNodeData, Button> nodeButtons = new Dictionary<MapNodeData, Button>();
    // 把每個 MapNodeData 對應到它的 RectTransform，之後畫連線要知道位置
    private readonly Dictionary<MapNodeData, RectTransform> nodeRects = new Dictionary<MapNodeData, RectTransform>();
    private float refreshTimer;  // 每隔一小段時間刷新節點狀態，避免每幀都跑

    private void Awake()
    {
        // 如果 Inspector 沒手動指定 RunManager，就嘗試抓單例
        if (runManager == null)
        {
            runManager = RunManager.Instance;
        }
    }

    private void OnEnable()
    {
        // 再保險一次，OnEnable 時也抓 RunManager
        if (runManager == null)
        {
            runManager = RunManager.Instance;
        }

        if (runManager != null)
        {
            // 訂閱「地圖生成」事件，地圖一生成就重畫 UI
            runManager.MapGenerated += HandleMapGenerated;
            // 訂閱「地圖狀態改變」事件（例如走到下一個節點）就刷新顏色/互動
            runManager.MapStateChanged += HandleMapStateChanged;

            // 如果這時候 RunManager 已經有地圖了，直接畫一次
            if (runManager.MapFloors != null && runManager.MapFloors.Count > 0)
            {
                HandleMapGenerated(runManager.MapFloors);
            }
            else
            {
                // 沒有地圖就清空
                ClearMap();
            }
        }
    }

    private void OnDisable()
    {
        // 解除事件訂閱，避免物件被關掉還一直收到事件
        if (runManager != null)
        {
            runManager.MapGenerated -= HandleMapGenerated;
            runManager.MapStateChanged -= HandleMapStateChanged;
        }
    }

    private void Update()
    {
        // 每 0.25 秒刷新一次節點狀態，避免一直刷新造成負擔
        refreshTimer += Time.deltaTime;
        if (refreshTimer >= 0.25f)
        {
            refreshTimer = 0f;
            RefreshNodeStates();
        }
    }

    // 收到「有一張新地圖生成了」的事件時呼叫
    private void HandleMapGenerated(IReadOnlyList<IReadOnlyList<MapNodeData>> floors)
    {
        BuildMap(floors);      // 重新生成 UI
        RefreshNodeStates();   // 再刷新一次互動與顏色
    }

    // 收到「地圖狀態有變（例如走到下一格）」的事件時呼叫
    private void HandleMapStateChanged()
    {
        RefreshNodeStates();
    }

    // 把 mapContainer 底下的東西全部清掉
    private void ClearMap()
    {
        nodeButtons.Clear();
        nodeRects.Clear();

        if (mapContainer == null)
            return;

        // 從最後一個 child 開始刪，避免遍歷時數量變動問題
        for (int i = mapContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(mapContainer.GetChild(i).gameObject);
        }
    }

    // 根據 RunManager 給的 floors（多層節點）去生成 UI
    private void BuildMap(IReadOnlyList<IReadOnlyList<MapNodeData>> floors)
    {
        ClearMap(); // 先清空舊的

        // 基本防呆：沒有容器、沒有 prefab、沒有資料就不做
        if (mapContainer == null || nodeButtonPrefab == null || floors == null)
            return;

        int floorCount = floors.Count;
        if (floorCount == 0)
            return;

        // 一層一層畫
        for (int floorIndex = 0; floorIndex < floorCount; floorIndex++)
        {
            IReadOnlyList<MapNodeData> floorNodes = floors[floorIndex];
            if (floorNodes == null || floorNodes.Count == 0)
                continue;

            // 為了讓這一層置中，先算這層總寬度
            float width = (floorNodes.Count - 1) * nodeSpacing;
            for (int nodeIndex = 0; nodeIndex < floorNodes.Count; nodeIndex++)
            {
                MapNodeData node = floorNodes[nodeIndex];
                // 生成一個節點按鈕
                Button buttonInstance = Instantiate(nodeButtonPrefab, mapContainer);
                buttonInstance.name = $"Node_{node.NodeId}";

                // 抓它的 RectTransform 來定位
                RectTransform buttonRect = buttonInstance.GetComponent<RectTransform>();
                // 設定錨點與 pivot 到上方中間，方便用 anchoredPosition 定位置
                buttonRect.anchorMin = new Vector2(0.5f, 1f);
                buttonRect.anchorMax = new Vector2(0.5f, 1f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);

                // 算這個節點的 X 位置：如果這層只有一個，就放 0；不然就從左到右排
                float x = floorNodes.Count == 1 ? 0f : -width * 0.5f + nodeIndex * nodeSpacing;
                // Y 位置是樓層 * 間距，往下排
                float y = -floorIndex * floorSpacing;
                buttonRect.anchoredPosition = new Vector2(x, y);

                // 初始外觀（顏色 + 文字）設定
                ConfigureButtonVisuals(buttonInstance, node);

                // 加按鈕事件：點了這顆節點就去請 RunManager 處理
                buttonInstance.onClick.AddListener(() => OnNodeClicked(node));

                // 存起來之後要更新顏色、畫線要用
                nodeButtons[node] = buttonInstance;
                nodeRects[node] = buttonRect;
            }
        }

        // 全部節點都生完後才畫連線，這樣每個節點位置都知道了
        CreateConnections();
    }

    // 依照 MapNodeData.NextNodes 去畫線
    private void CreateConnections()
    {
        if (connectionLinePrefab == null)
            return;

        // 對每一個節點去看它連到哪些下一層節點
        foreach (KeyValuePair<MapNodeData, Button> pair in nodeButtons)
        {
            MapNodeData node = pair.Key;
            if (!nodeRects.TryGetValue(node, out RectTransform startRect))
                continue;

            IReadOnlyList<MapNodeData> nextNodes = node.NextNodes;
            if (nextNodes == null)
                continue;

            for (int i = 0; i < nextNodes.Count; i++)
            {
                MapNodeData nextNode = nextNodes[i];
                if (!nodeRects.TryGetValue(nextNode, out RectTransform endRect))
                    continue;

                // 生成一條線
                Image lineInstance = Instantiate(connectionLinePrefab, mapContainer);
                lineInstance.name = $"Line_{node.NodeId}_to_{nextNode.NodeId}";

                RectTransform lineRect = lineInstance.rectTransform;
                lineRect.anchorMin = new Vector2(0.5f, 1f);
                lineRect.anchorMax = new Vector2(0.5f, 1f);
                lineRect.pivot = new Vector2(0.5f, 0.5f);

                // 起點跟終點的位置
                Vector2 start = startRect.anchoredPosition;
                Vector2 end = endRect.anchoredPosition;
                Vector2 direction = end - start;
                float distance = direction.magnitude;

                // 線的長度設成起點到終點的距離，寬度就是你設定的粗細
                lineRect.sizeDelta = new Vector2(distance, connectionThickness);
                // 把線放在起點跟終點的中間
                lineRect.anchoredPosition = start + direction * 0.5f;

                // 算出線的旋轉角度，讓它對準終點
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }

    // 初始時幫按鈕上色、填文字（戰鬥/商店/事件）
    private void ConfigureButtonVisuals(Button buttonInstance, MapNodeData node)
    {
        if (buttonInstance == null || node == null)
            return;

        // 抓按鈕本體的 Image
        Image image = buttonInstance.targetGraphic as Image;
        if (image != null)
        {
            image.color = defaultColor;
        }

        // 看按鈕底下有沒有 TMP_Text，用來顯示節點類型
        TMP_Text tmpText = buttonInstance.GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = node.NodeType.ToString();
        }
        else
        {
            // 如果不是用 TextMeshPro，試試看舊的 Text
            Text legacyText = buttonInstance.GetComponentInChildren<Text>();
            if (legacyText != null)
            {
                legacyText.text = node.NodeType.ToString();
            }
        }
    }

    // 根據現在 RunManager 的狀態，刷新每顆節點的顏色跟可互動性
    private void RefreshNodeStates()
    {
        if (runManager == null)
            return;

        foreach (KeyValuePair<MapNodeData, Button> pair in nodeButtons)
        {
            MapNodeData node = pair.Key;
            Button button = pair.Value;
            if (button == null)
                continue;

            // 問 RunManager 這個節點現在能不能點
            bool isSelectable = runManager.IsNodeSelectable(node);
            button.interactable = isSelectable;

            Image image = button.targetGraphic as Image;
            if (image != null)
            {
                // 目前所在節點
                if (runManager.CurrentNode == node)
                {
                    image.color = currentColor;
                }
                // 已打過的節點
                else if (node.IsCompleted)
                {
                    image.color = completedColor;
                }
                // 可以點的節點
                else if (isSelectable)
                {
                    image.color = defaultColor;
                }
                // 其他都視為鎖住
                else
                {
                    image.color = lockedColor;
                }
            }
        }
    }

    // 按下某個節點按鈕時呼叫
    private void OnNodeClicked(MapNodeData node)
    {
        if (runManager == null || node == null)
            return;

        // 交給 RunManager 去做真正的場景切換與流程控制
        runManager.TryEnterNode(node);
    }
}
