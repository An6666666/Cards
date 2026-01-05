using System.Collections.Generic; // 使用泛型集合（List、Queue、IEnumerable 等）
using DG.Tweening; // DOTween 動畫（用於文字逐字顯示）
using UnityEngine; // Unity 核心 API（MonoBehaviour、GameObject、SerializeField 等）
using UnityEngine.UI; // Unity UI（Text、Button）

public class ElementSelectionGuideNPC : MonoBehaviour // 元素選擇教學 NPC：負責在元素選擇畫面顯示對話泡泡提示
{
    [System.Serializable] // 讓這個類別可以在 Inspector 中序列化顯示/編輯
    public class ElementDialogue // 某個元素對應的一組對話資料
    {
        public ElementType element; // 這組對話所對應的元素類型
        [TextArea(2, 4)] // Inspector 多行文字輸入（最少 2 行高度，最多 4 行顯示高度）
        public List<string> lines = new List<string>(); // 該元素的對話句子清單（依序播放）
    }

    [Header("References")] // Inspector 分組：外部引用
    [SerializeField] private StartingElementSelectionUI selectionUI; // 元素選擇 UI（用來訂閱 ElementSelected 事件）
    [SerializeField] private GameObject bubbleRoot; // 對話泡泡根物件（用來顯示/隱藏整個泡泡 UI）
    [SerializeField] private Text dialogueText; // 泡泡內的文字元件（顯示當前一句）
    [SerializeField] private Button nextButton; // 「下一句」按鈕（點擊切下一句）

    [Header("Intro Lines")] // Inspector 分組：開場對話
    [TextArea(2, 4)] // Inspector 多行文字輸入
    [SerializeField] private List<string> introLines = new List<string>(); // 進入場景時要先播放的對話句子

    [Header("Element Lines")] // Inspector 分組：元素對話
    [SerializeField] private List<ElementDialogue> elementDialogues = new List<ElementDialogue>(); // 每個元素對應的對話集合

    [Header("Options")] // Inspector 分組：選項
    [SerializeField] private bool hideBubbleWhenEmpty = true; // 當沒有內容時是否自動隱藏泡泡 UI
    [Header("Typewriter")] // Inspector 分組：逐字動畫
    [SerializeField, Min(0f)] private float typewriterDurationPerChar = 0.03f; // 單字元顯示時間（越大越慢）
    [SerializeField, Min(0f)] private float minTypewriterDuration = 0.15f; // 最短顯示時間（避免太短）
    [SerializeField] private Ease typewriterEase = Ease.Linear; // 逐字動畫的 Ease

    private readonly Queue<string> queuedLines = new Queue<string>(); // 目前待播放的對話佇列（先進先出）
    private Tween dialogueTween; // 目前的逐字 tween
    private bool isTyping; // 是否正在播放逐字動畫

    private void Awake() // Unity Awake：初始化事件訂閱與按鈕事件
    {
        if (selectionUI != null) // 若有綁定元素選擇 UI
        {
            selectionUI.ElementSelected += HandleElementSelected; // 訂閱「元素被選取」事件（被選就播放該元素對話）
        }

        if (nextButton != null) // 若有綁定下一句按鈕
        {
            nextButton.onClick.RemoveAllListeners(); // 清空舊的 onClick（避免重複綁定）
            nextButton.onClick.AddListener(ShowNextLine); // 綁定按鈕點擊：顯示下一句
        }
    }

    private void Start() // Unity Start：場景開始後播放開場對話
    {
        if (introLines.Count > 0) // 若開場對話有內容
        {
            PlayLines(introLines); // 將開場對話加入佇列並開始播放
        }
    }

    private void OnDestroy() // 物件銷毀時取消訂閱，避免事件引用殘留
    {
        if (selectionUI != null) // 若 selectionUI 存在
        {
            selectionUI.ElementSelected -= HandleElementSelected; // 取消訂閱元素選取事件
        }

        if (nextButton != null) // 若按鈕存在
        {
            nextButton.onClick.RemoveListener(ShowNextLine); // 移除 ShowNextLine 的 listener
        }

        if (dialogueTween != null && dialogueTween.IsActive()) // 若有正在或已存在的 tween
        {
            dialogueTween.Kill(false); // 結束 tween
        }
    }

    public void HandleElementSelected(ElementType element) // 當 UI 通知「某元素被選取」時呼叫
    {
        List<string> lines = GetLinesForElement(element); // 取得該元素對應的對話句子清單
        if (lines != null && lines.Count > 0) // 若有找到且內容不為空
        {
            PlayLines(lines); // 播放這組句子（覆蓋/重置目前佇列）
        }
    }

    public void ShowNextLine() // 顯示佇列中的下一句
    {
        if (dialogueText == null) // 若沒有綁定文字元件
            return; // 無法顯示，直接返回

        if (dialogueTween != null && dialogueTween.IsActive() && dialogueTween.IsPlaying()) // 若正在逐字播放
        {
            dialogueTween.Complete(); // 先完成當前文字
            return; // 不往下切句
        }

        if (dialogueTween != null) // 若有舊 tween
        {
            dialogueTween.Kill(false); // 結束舊 tween
            dialogueTween = null; // 清空參考
            isTyping = false; // 重置狀態
        }
        if (queuedLines.Count == 0) // 若佇列已經沒有句子了
        {
            isTyping = false; // 確保狀態重置
            dialogueText.text = string.Empty; // 清空顯示文字
            UpdateBubbleVisibility(); // 依設定更新泡泡/按鈕可見性
            return; // 結束
        }

        PlayTypewriter(queuedLines.Dequeue()); // 逐字播放下一句
    }

    public void PlayLines(IEnumerable<string> lines) // 將一組句子放入佇列並從第一句開始播放
    {
        if (dialogueTween != null) // 若有正在播放的舊對話
        {
            dialogueTween.Kill(false); // 直接結束
            dialogueTween = null; // 清空參考
            isTyping = false; // 重置狀態
        }

        if (dialogueText != null) // 清空舊文字
        {
            dialogueText.text = string.Empty;
        }
        queuedLines.Clear(); // 清空舊的佇列（新對話會覆蓋舊對話）
        foreach (string line in lines) // 走訪每一句
        {
            if (!string.IsNullOrWhiteSpace(line)) // 若不是 null/空字串/只有空白
            {
                queuedLines.Enqueue(line); // 加入佇列等待播放
            }
        }

        ShowNextLine(); // 立刻顯示第一句（或若沒有句子則清空並更新顯示）
    }

    private List<string> GetLinesForElement(ElementType element) // 從 elementDialogues 找出對應元素的句子清單
    {
        foreach (ElementDialogue dialogue in elementDialogues) // 走訪所有元素對話設定
        {
            if (dialogue != null && dialogue.element == element) // 若此筆資料存在且元素相符
            {
                return dialogue.lines; // 回傳該元素的句子清單
            }
        }

        return null; // 找不到就回傳 null
    }

    private void UpdateBubbleVisibility() // 依照目前狀態顯示/隱藏「下一句按鈕」與「泡泡根物件」
    {
        bool hasMoreLines = queuedLines.Count > 0; // 是否還有下一句可播
        if (nextButton != null) // 若下一句按鈕存在
        {
            bool canAdvanceOrSkip = hasMoreLines || isTyping; // 有下一句或正在逐字播放都顯示按鈕
            nextButton.gameObject.SetActive(canAdvanceOrSkip); // 有下一句才顯示按鈕，沒有就隱藏
        }

        if (hideBubbleWhenEmpty && bubbleRoot != null) // 若啟用「沒內容就隱藏泡泡」且泡泡根物件存在
        {
            bool hasContent = hasMoreLines || isTyping || !string.IsNullOrEmpty(dialogueText?.text); // 有下一句、正在播放或目前文字非空就算有內容
            bubbleRoot.SetActive(hasContent); // 依是否有內容決定泡泡顯示/隱藏
        }
    }

    private void PlayTypewriter(string line) // 使用 DOTween 逐字顯示文字
    {
        isTyping = true; // 標記正在逐字播放
        dialogueText.text = string.Empty; // 先清空

        float duration = Mathf.Max(minTypewriterDuration, line.Length * typewriterDurationPerChar); // 根據字數決定動畫時間
        dialogueTween = dialogueText // 對文字進行 tween
            .DOText(line, duration, true, ScrambleMode.None) // 逐字顯示完整句子
            .SetEase(typewriterEase) // 套用指定的 Ease
            .OnComplete(() => // 播放完畢的回呼
            {
                isTyping = false; // 標記結束
                UpdateBubbleVisibility(); // 更新按鈕/泡泡顯示
            });

        UpdateBubbleVisibility(); // 開始播放時立即刷新顯示狀態
    }
}
