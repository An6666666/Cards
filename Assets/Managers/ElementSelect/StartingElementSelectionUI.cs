using System;
using System.Collections.Generic; // 使用泛型集合（List、Dictionary 等）
using System.Text;
using UnityEngine; // Unity 核心命名空間
using DG.Tweening; // DOTween 動畫 tween 功能
using UnityEngine.SceneManagement; // 場景切換功能
using UnityEngine.UI; // Unity UI（Text、Button、Image 等）

[System.Serializable] // 讓此類別可在 Inspector 序列化顯示
public class ElementButtonBinding // 元素按鈕綁定資料：元素類型 + 按鈕 + 圖片
{
    public ElementType element; // 這個按鈕代表的元素類型
    public Button button; // 對應的 UI Button 元件
    public Image image; // 對應的 UI Image（通常是按鈕上的圖示）
}

public class StartingElementSelectionUI : MonoBehaviour // 起始元素選擇介面控制器
{
    [Header("Selection")]
    [Min(1)]
    [SerializeField] private int requiredSelections = 3;

    [Header("Navigation")] // Inspector 分組：導航/場景切換
    [SerializeField] private string runSceneName = "RunScene"; // 按下開始後要載入的場景名稱

    [Header("UI References")] // Inspector 分組：UI 參考
    [SerializeField] private Text titleText; // 標題文字（目前程式內未使用到）
    [SerializeField] private List<ElementButtonBinding> elementButtons = new List<ElementButtonBinding>(); // 元素按鈕清單（在 Inspector 逐一綁定）
    [SerializeField] private Button startButton; // 開始按鈕
    [SerializeField] private Image startButtonImage; // 開始按鈕的 Image（用來改顏色）

    [Header("Visuals")] // Inspector 分組：視覺相關設定
    [SerializeField] private Color normalColor = Color.white; // 未特別處理時的按鈕圖示顏色
    [SerializeField] private Sprite selectionRingSprite; // 選擇環的 Sprite（用於顯示選取動畫）
    [SerializeField] private Color selectionRingColor = Color.white; // 選擇環顏色
    [SerializeField] private float selectionRingDrawDuration = 0.45f; // 選擇時畫出選擇環的動畫時間
    [SerializeField] private float selectionRingHideDuration = 0.2f; // 取消選擇時隱藏動畫時間
    [SerializeField] private Vector2 selectionRingPadding = new Vector2(12f, 12f); // 選擇環相對按鈕圖示的內外邊距
    [SerializeField] private Color startEnabledColor = new Color(0.2f, 0.6f, 0.2f); // 可開始時開始按鈕顏色（偏綠）
    [SerializeField] private Color startDisabledColor = new Color(0.5f, 0.5f, 0.5f); // 不可開始時開始按鈕顏色（灰色）

    public event Action<ElementType> ElementSelected;

    // =========================
    // ✅ Left Info Panel (新增)
    // =========================
    [Header("Left Info Panel")]
    [SerializeField] private Text leftInfoText; // 左邊顯示說明的 Text（Inspector 拖進來）

    [System.Serializable]
    public class ElementInfo
    {
        public ElementType element;
        [TextArea(2, 6)] public string description; // 單元素解說
    }

    [System.Serializable]
    public class ReactionInfo
    {
        public ElementType a;
        public ElementType b;
        public string title; // 反應名稱（例如 汽化/融化/導電… 可不填）
        [TextArea(2, 8)] public string description; // 反應解說（需要 a+b 都選到才顯示）
    }

    [SerializeField] private List<ElementInfo> elementInfoTable = new List<ElementInfo>();
    [SerializeField] private List<ReactionInfo> reactionInfoTable = new List<ReactionInfo>();

    private readonly Dictionary<ElementType, string> elementDescLookup = new Dictionary<ElementType, string>();
    private readonly Dictionary<ReactionKey, ReactionInfo> reactionLookup = new Dictionary<ReactionKey, ReactionInfo>();

    private struct ReactionKey : IEquatable<ReactionKey>
    {
        public ElementType x;
        public ElementType y;

        public ReactionKey(ElementType a, ElementType b)
        {
            // 讓 (火,水) 與 (水,火) 視為同一組（忽略順序）
            if ((int)a <= (int)b) { x = a; y = b; }
            else { x = b; y = a; }
        }

        public bool Equals(ReactionKey other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is ReactionKey other && Equals(other);
        public override int GetHashCode() => ((int)x * 397) ^ (int)y;
    }

    private readonly List<ElementType> selectedElements = new List<ElementType>(3); // 已選取的元素清單（最多 3 個）
    private readonly Dictionary<ElementType, ElementButtonBinding> elementLookup = new Dictionary<ElementType, ElementButtonBinding>(); // 元素 -> 綁定資料 的查表字典
    private readonly Dictionary<ElementType, Image> selectionRings = new Dictionary<ElementType, Image>(); // 元素 -> 選擇環 Image 的字典
    private readonly Dictionary<ElementType, Tween> selectionTweens = new Dictionary<ElementType, Tween>(); // 元素 -> 選擇環動畫 Tween 的字典（方便中止/覆蓋）
    private readonly Dictionary<ElementType, bool> selectionStates = new Dictionary<ElementType, bool>(); // 元素 -> 上一次是否選取（用來避免重複播放動畫）

    private void Awake() // Unity Awake：初始化查表、綁定按鈕事件、刷新 UI 狀態
    {
        requiredSelections = Mathf.Max(1, requiredSelections);
        if (selectedElements.Capacity < requiredSelections)
        {
            selectedElements.Capacity = requiredSelections;
        }

        BuildLookup(); // 建立 elementLookup，並生成每個元素的選擇環
        WireUpButtons(); // 綁定每個元素按鈕與開始按鈕的 onClick 事件

        // ✅ 新增：建立左邊說明查表（不影響原本功能）
        BuildInfoLookup();

        RefreshButtonStates(); // 依目前 selectedElements 刷新 UI（選擇環、開始按鈕可用狀態）

        // ✅ 新增：初始化左邊文字
        UpdateLeftInfoPanel();
    }

    private void BuildLookup() // 建立元素按鈕的查表與選擇環
    {
        elementLookup.Clear(); // 清空舊的查表資料
        foreach (ElementButtonBinding binding in elementButtons) // 走訪 Inspector 設定的元素按鈕清單
        {
            if (binding == null || binding.button == null || binding.image == null) // 防呆：缺少綁定就跳過
                continue; // 跳過無效綁定

            if (!elementLookup.ContainsKey(binding.element)) // 避免同一元素重複加入
            {
                elementLookup.Add(binding.element, binding); // 加入查表
                TryCreateSelectionRing(binding); // 嘗試為這個元素建立選擇環（若有指定 sprite）
            }
        }
    }

    private void WireUpButtons() // 綁定 UI 按鈕點擊事件
    {
        foreach (ElementButtonBinding binding in elementLookup.Values) // 走訪所有有效的元素綁定
        {
            binding.button.onClick.RemoveAllListeners(); // 先移除舊事件（避免重複綁定）
            binding.button.onClick.AddListener(() => ToggleElement(binding.element)); // 點擊時切換該元素是否選取
        }

        if (startButton != null) // 若開始按鈕有綁定
        {
            startButton.onClick.RemoveAllListeners(); // 移除舊事件（避免重複）
            startButton.onClick.AddListener(OnStartClicked); // 綁定開始行為
        }
    }

    private void ToggleElement(ElementType element) // 切換某元素的選取狀態
    {
        if (selectedElements.Contains(element)) // 若已選過
        {
            selectedElements.Remove(element); // 就取消選取
        }
        else if (selectedElements.Count < requiredSelections)
        {
            selectedElements.Add(element); // 就加入選取
            ElementSelected?.Invoke(element);
        }

        RefreshButtonStates(); // 每次變更選取後刷新 UI 狀態

        // ✅ 新增：同步更新左邊文字（不影響原本功能）
        UpdateLeftInfoPanel();
    }

    private void RefreshButtonStates() // 刷新所有按鈕狀態與開始按鈕狀態
    {
        foreach (KeyValuePair<ElementType, ElementButtonBinding> pair in elementLookup) // 逐一處理每個元素按鈕
        {
            bool selected = selectedElements.Contains(pair.Key); // 判斷該元素是否被選取
            pair.Value.image.color = normalColor; // 把按鈕圖示顏色設回 normalColor（目前不做選取變色）
            UpdateSelectionRing(pair.Key, selected); // 更新該元素選擇環顯示與動畫
        }

        bool ready = selectedElements.Count == requiredSelections; // 是否已選滿 requiredSelections 個元素（可開始）
        if (startButton != null) // 若開始按鈕存在
        {
            startButton.interactable = ready; // 設定是否可點擊
        }

        if (startButtonImage != null) // 若開始按鈕的 Image 存在
        {
            startButtonImage.color = ready ? startEnabledColor : startDisabledColor; // 依 ready 切換顏色
        }
    }

    private void TryCreateSelectionRing(ElementButtonBinding binding) // 嘗試建立某元素的選擇環 Image
    {
        if (selectionRingSprite == null || selectionRings.ContainsKey(binding.element)) // 沒有 sprite 或已建立過就不做
            return; // 直接返回

        GameObject ringObject = new GameObject("SelectionRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); // 建立選擇環物件（帶 Image）
        ringObject.transform.SetParent(binding.image.transform, false); // 掛到該按鈕圖示 Image 底下（跟著位置縮放）

        RectTransform rectTransform = ringObject.GetComponent<RectTransform>(); // 取得 RectTransform
        rectTransform.anchorMin = Vector2.zero; // 左下錨點 0,0
        rectTransform.anchorMax = Vector2.one; // 右上錨點 1,1（撐滿父物件）
        rectTransform.offsetMin = -selectionRingPadding; // 左下偏移：向外擴張 padding
        rectTransform.offsetMax = selectionRingPadding; // 右上偏移：向外擴張 padding

        Image ringImage = ringObject.GetComponent<Image>(); // 取得 Image 元件
        ringImage.sprite = selectionRingSprite; // 套用選擇環 Sprite
        ringImage.type = Image.Type.Filled; // 設成 Filled 才能使用 fillAmount 動畫
        ringImage.fillMethod = Image.FillMethod.Radial90; // 使用 Radial90 方式填滿（四分之一圓填法）
        ringImage.fillAmount = 0f; // 初始填滿量為 0（看不到）
        ringImage.color = selectionRingColor; // 設定選擇環顏色
        ringImage.raycastTarget = false; // 不吃 UI 射線（避免擋住按鈕點擊）

        ringObject.SetActive(false); // 預設先關閉（未選取時不顯示）
        selectionRings[binding.element] = ringImage; // 記錄到字典：元素 -> 選擇環 Image
    }

    private string ElementToZh(object elementEnumOrString)
    {
        if (elementEnumOrString == null) return "無";
        string key = elementEnumOrString.ToString();

        return key switch
        {
            "Wood" => "木",
            "Water" => "水",
            "Fire" => "火",
            "Thunder" => "雷",
            "Ice" => "冰",
            _ => key, // 其他就原樣
        };
    }

    private void UpdateSelectionRing(ElementType element, bool selected) // 更新某元素的選擇環顯示/隱藏動畫
    {
        if (!selectionRings.TryGetValue(element, out Image ringImage)) // 若此元素沒有選擇環
            return;

        if (selectionStates.TryGetValue(element, out bool previousSelected) && previousSelected == selected) // 若選取狀態沒有變
        {
            if (selected) // 若仍是選取狀態
            {
                ringImage.gameObject.SetActive(true); // 確保顯示
                ringImage.fillAmount = 1f; // 直接填滿（避免停在半路）
            }
            return; // 狀態沒變就直接返回，不重播動畫
        }

        if (selectionTweens.TryGetValue(element, out Tween tween)) // 若此元素之前有在播放 tween
        {
            tween.Kill(false); // 中止舊 tween（不觸發 complete）
        }

        float duration = selected ? selectionRingDrawDuration : selectionRingHideDuration; // 決定本次動畫時間：選取=畫出，取消=收回

        if (selected) // 若要變成選取
        {
            ringImage.fillAmount = 0f; // 從 0 開始畫
            ringImage.gameObject.SetActive(true); // 先打開物件才能看到動畫
        }

        selectionTweens[element] = ringImage
            .DOFillAmount(selected ? 1f : 0f, duration)
            .SetEase(selected ? Ease.OutCubic : Ease.InCubic)
            .OnComplete(() =>
            {
                if (!selected)
                {
                    ringImage.gameObject.SetActive(false);
                }
            });

        selectionStates[element] = selected;
    }

    private void OnDestroy() // 物件銷毀時清理 tween，避免 DOTween 留下引用
    {
        foreach (Tween tween in selectionTweens.Values)
        {
            tween?.Kill(false);
        }
        selectionTweens.Clear();
    }

    private void OnStartClicked() // 點擊「開始」按鈕時的行為
    {
        if (selectedElements.Count != requiredSelections)
            return;

        StartingDeckSelection.SetSelection(selectedElements, requiredSelections);
        SceneManager.LoadScene(runSceneName);
    }

    // =========================
    // ✅ Left Info Panel methods
    // =========================
    private void BuildInfoLookup()
    {
        elementDescLookup.Clear();
        reactionLookup.Clear();

        foreach (var info in elementInfoTable)
        {
            if (info == null) continue;
            if (!elementDescLookup.ContainsKey(info.element))
                elementDescLookup.Add(info.element, info.description ?? "");
        }

        foreach (var r in reactionInfoTable)
        {
            if (r == null) continue;
            var key = new ReactionKey(r.a, r.b);
            if (!reactionLookup.ContainsKey(key))
                reactionLookup.Add(key, r);
        }
    }

    private void UpdateLeftInfoPanel()
    {
        if (leftInfoText == null) return;

        if (selectedElements.Count == 0)
        {
            leftInfoText.text = "請選擇元素";
            return;
        }

        string ToZh(ElementType e)
        {
            return e switch
            {
                ElementType.Wood => "木",
                ElementType.Water => "水",
                ElementType.Fire => "火",
                ElementType.Thunder => "雷",
                ElementType.Ice => "冰",
                _ => e.ToString()
            };
        }

        var sb = new StringBuilder();

        // ===== 單元素 =====
        sb.AppendLine("【元素】");
        sb.AppendLine();

        for (int i = 0; i < selectedElements.Count; i++)
        {
            var e = selectedElements[i];

            sb.AppendLine($"- {ToZh(e)}");

            if (elementDescLookup.TryGetValue(e, out var desc) && !string.IsNullOrWhiteSpace(desc))
                sb.AppendLine($"  {desc}");
            else
                sb.AppendLine("  （尚未設定解說）");

            sb.AppendLine(); // ✅ 每個元素項目後空一行（更好讀、名稱+說明靠近）
        }

        // ===== 反應：兩個都選到才顯示 =====
        sb.AppendLine("【元素反應】");
        sb.AppendLine();

        bool hasAnyReaction = false;

        for (int i = 0; i < selectedElements.Count; i++)
        {
            for (int j = i + 1; j < selectedElements.Count; j++)
            {
                var a = selectedElements[i];
                var b = selectedElements[j];

                var key = new ReactionKey(a, b);
                if (reactionLookup.TryGetValue(key, out var r) && r != null)
                {
                    hasAnyReaction = true;

                    // 顯示標題：有 title 用 title，沒有就用「木 + 水」這種
                    string defaultTitle = $"{ToZh(a)} + {ToZh(b)}";
                    string title = string.IsNullOrWhiteSpace(r.title) ? defaultTitle : r.title;

                    sb.AppendLine($"- {title}");

                    if (!string.IsNullOrWhiteSpace(r.description))
                        sb.AppendLine($"  {r.description}");
                    else
                        sb.AppendLine("  （尚未設定解說）");

                    sb.AppendLine(); // ✅ 每個反應項目後空一行
                }
            }
        }

        if (!hasAnyReaction)
            sb.AppendLine("（尚未形成反應）");

        leftInfoText.text = sb.ToString();
    }

}
