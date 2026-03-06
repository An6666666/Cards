using System.Linq;          // 提供 FirstOrDefault 等 LINQ 查詢工具
using DG.Tweening;          // 提供 DOTween 動畫 API（淡入淡出）
using UnityEngine;          // Unity 核心型別（MonoBehaviour、GameObject、Mathf...）
using UnityEngine.UI;       // UI 元件（Text、Image、CanvasGroup）

/// <summary>
/// 商店 NPC 對話控制器：
/// 1) 管理進店/購買/金幣不足台詞
/// 2) 管理累積進店次數與累積消費（PlayerPrefs）
/// 3) 管理訊息文字的淡入、停留、淡出與自動隱藏
/// </summary>
public class ShopNpcDialogueController : MonoBehaviour // 掛在場景物件上的對話控制腳本
{
    [Header("UI References")] // UI 參考區塊標題（顯示在 Inspector）
    [SerializeField] private GameObject messageRoot; // 對話根物件（等同 BubbleRoot，負責整體顯示/隱藏/淡入淡出）
    [SerializeField] private Text messageText; // 顯示台詞的文字元件
    [SerializeField] private GameObject shopNpcImage; // NPC 立繪物件（通常是 GuideNPC）
    [SerializeField] private string shopNpcName = "店員"; // 台詞前顯示的角色名稱

    [Header("Message Animation")] // 訊息動畫參數區塊
    [SerializeField, Min(0f)] private float messageFadeInSeconds = 0.2f; // 淡入秒數
    [SerializeField, Min(0f)] private float messageBaseVisibleSeconds = 1.2f; // 基礎停留秒數
    [SerializeField, Min(0f)] private float messageVisibleSecondsPerChar = 0.08f; // 每個字額外增加停留秒數
    [SerializeField, Min(0f)] private float messageFadeOutSeconds = 0.25f; // 淡出秒數

    [Header("Typewriter")] // 打字機效果參數（對齊 DialogueBubbleUI）
    [SerializeField, Min(0f)] private float typewriterDurationPerChar = 0.03f; // 每字打字秒數
    [SerializeField, Min(0f)] private float minTypewriterDuration = 0.15f; // 最短打字秒數
    [SerializeField] private Ease typewriterEase = Ease.Linear; // 打字補間曲線

    private const string ShopNpcTotalSpendKey = "ShopNPC.TotalSpend"; // 累積消費儲存鍵值

    private CanvasGroup messageCanvasGroup; // 控制 messageText 透明度與互動狀態
    private Tween messageVisibilityTween; // 保存目前訊息動畫，方便新訊息來時中斷
    private static int cachedRunSequenceId = int.MinValue; // 記錄上次進店時所屬的冒險序號
    private static int currentAdventureVisitCount; // 只在「當下這場冒險」內累加的進店次數

    private void Awake() // Unity 生命週期：物件初始化時呼叫
    {
        ResolveFallbackReferences(); // 自動補齊 messageText / shopNpcImage 參考
        InitializeMessagePresentation(); // 設定 messageText 初始狀態為隱藏
    }

    private void OnDestroy() // Unity 生命週期：物件被銷毀時呼叫
    {
        if (messageVisibilityTween != null) // 若目前仍有訊息動畫在跑
        {
            messageVisibilityTween.Kill(false); // 停止動畫（不強制完成）
            messageVisibilityTween = null; // 清除引用避免殘留
        }
    }

    public void SetUiReferences(Text messageTextReference, GameObject npcImageReference = null, GameObject messageRootReference = null) // 供外部（如 ShopUIManager）設定 UI 參考
    {
        if (messageTextReference != null) // 如果有傳入新的文字元件
            messageText = messageTextReference; // 就覆蓋掉目前引用

        if (npcImageReference != null) // 如果有傳入新的 NPC 圖像物件
            shopNpcImage = npcImageReference; // 就覆蓋掉目前引用

        if (messageRootReference != null) // 如果有傳入新的對話根物件
            messageRoot = messageRootReference; // 就覆蓋掉目前引用

        messageCanvasGroup = null; // 重新抓取 CanvasGroup（避免 UI 參考改變後仍沿用舊物件）
        ResolveFallbackReferences(); // 再做一次補齊（避免仍有空值）
        InitializeMessagePresentation(); // 重新初始化顯示狀態
    }

    public void NotifyShopEntered() // 外部通知：玩家進入商店
    {
        ResolveShopNpcReferences(); // 先確保 NPC 圖像物件可見

        int visitCount = ResolveAdventureVisitCount(); // 取得「本次冒險」的進店次數（新冒險會重置）

        int totalSpend = LoadShopNpcStat(ShopNpcTotalSpendKey); // 讀取累積消費
        string entryLine = BuildEntryNpcLine(visitCount, totalSpend); // 依條件組出進店台詞
        ShowNpcLine(entryLine); // 只顯示進店台詞本體

    }

    public void NotifyPurchase(string itemName, int spentGold) // 外部通知：購買成功（卡牌/遺物/移除服務）
    {
        int safeSpend = Mathf.Max(0, spentGold); // 防呆：消費金額至少為 0
        int totalSpend = LoadShopNpcStat(ShopNpcTotalSpendKey); // 讀取目前累積消費

        if (safeSpend > 0) // 只有正消費才累計
        {
            totalSpend += safeSpend; // 增加本次花費
            SaveShopNpcStat(ShopNpcTotalSpendKey, totalSpend); // 寫回累積消費
        }

        ShowNpcLine(BuildPurchaseNpcLine(itemName, safeSpend, totalSpend)); // 依條件組出購買台詞並顯示
    }

    public void NotifyInsufficientGold(int requiredGold, int currentGold) // 外部通知：玩家金幣不足
    {
        ShowNpcLine(BuildInsufficientGoldLine(requiredGold, currentGold)); // 依差額生成台詞並顯示
    }

    private void ResolveFallbackReferences() // 自動補齊缺失的 UI/物件參考
    {
        if (messageText == null) // 如果 messageText 尚未綁定
        {
            Text[] texts = GetComponentsInChildren<Text>(true); // 在自己與子物件（含 inactive）找所有 Text
            messageText = texts.FirstOrDefault(t => // 找第一個符合條件的 Text
                t != null && t.name.ToLowerInvariant().Contains("message")); // 名稱包含 message
        }

        if (shopNpcImage == null) // 如果 NPC 圖像物件尚未綁定
            shopNpcImage = GameObject.Find("GuideNPC"); // 嘗試以名稱在場景中尋找

        if (messageRoot == null && messageText != null) // 若未手動指定 root，則依層級與元件特徵推導
            messageRoot = InferMessageRootFromText(); // 推導出最合理的對話根物件
    }

    private void ResolveShopNpcReferences() // 確保 NPC 立繪可見
    {
        if (shopNpcImage == null) // 若目前沒有引用
            shopNpcImage = GameObject.Find("GuideNPC"); // 再嘗試搜尋一次

        if (shopNpcImage == null) // 仍找不到就直接結束
            return; // 避免後續 NullReference

        if (!shopNpcImage.activeSelf) // 若 NPC 物件被停用
            shopNpcImage.SetActive(true); // 強制啟用

        Image npcPortrait = shopNpcImage.GetComponent<Image>(); // 取得 Image 元件
        if (npcPortrait != null && !npcPortrait.enabled) // 若 Image 元件被關閉
            npcPortrait.enabled = true; // 強制開啟圖片顯示
    }

    private void ShowNpcLine(string line) // 統一對外顯示 NPC 台詞入口
    {
        if (string.IsNullOrWhiteSpace(line)) // 空字串或全空白不顯示
            return; // 直接返回

        ResolveShopNpcReferences(); // 顯示前確保 NPC 圖像狀態正確
        ShowMessage(line); // 只輸出台詞內容
    }

    private void ShowMessage(string text) // 負責訊息顯示動畫（淡入->打字->停留->淡出->隱藏）
    {
        if (messageText == null) // 沒有文字元件就無法顯示
            return; // 直接返回

        GameObject root = GetMessageRoot(); // 取得真正要顯示/隱藏的對話根物件
        if (root == null) // 若連根物件都找不到就無法顯示
            return; // 直接返回

        EnsureMessageCanvasGroup(); // 確保 CanvasGroup 存在（控制 alpha）

        if (messageVisibilityTween != null) // 若前一段訊息動畫還在跑
        {
            messageVisibilityTween.Kill(false); // 先停止舊動畫
            messageVisibilityTween = null; // 清空舊動畫引用
        }

        if (string.IsNullOrWhiteSpace(text)) // 若要顯示的內容為空
        {
            HideMessageInstantly(); // 立即隱藏訊息
            return; // 中止後續流程
        }

        string finalText = text.Trim(); // 去掉前後空白
        messageText.text = string.Empty; // 打字開始前先清空文字

        bool reuseVisibleState = root.activeSelf && messageCanvasGroup.alpha > 0.01f; // 若對話框已在顯示中，沿用可見狀態不重播淡入
        if (!root.activeSelf) // 若根物件目前是關閉
            root.SetActive(true); // 先開啟根物件才能看到動畫

        messageCanvasGroup.alpha = reuseVisibleState ? 1f : 0f; // 已顯示則維持可見，否則從透明開始
        messageCanvasGroup.blocksRaycasts = false; // 不阻擋滑鼠事件
        messageCanvasGroup.interactable = false; // 不接受互動

        float holdDuration = Mathf.Max(0.4f, messageBaseVisibleSeconds + (finalText.Length * messageVisibleSecondsPerChar)); // 依字數計算停留時間
        float typewriterDuration = Mathf.Max(minTypewriterDuration, finalText.Length * typewriterDurationPerChar); // 依字數計算打字時間（含最短秒數保底）

        Sequence sequence = DOTween.Sequence(); // 建立 DOTween 序列
        if (!reuseVisibleState && messageFadeInSeconds > 0f) // 僅在非沿用狀態時執行淡入
            sequence.Append(messageCanvasGroup.DOFade(1f, messageFadeInSeconds)); // 執行淡入
        else // 已沿用可見狀態，或淡入秒數為 0
            messageCanvasGroup.alpha = 1f; // 直接顯示為不透明

        if (typewriterDuration > 0f && finalText.Length > 0) // 若需要打字效果
            sequence.Append(messageText.DOText(finalText, typewriterDuration, true, ScrambleMode.None).SetEase(typewriterEase)); // 逐字顯示台詞
        else // 若打字秒數設為 0
            messageText.text = finalText; // 直接顯示完整文字

        sequence.AppendInterval(holdDuration); // 中間停留指定秒數

        if (messageFadeOutSeconds > 0f) // 若淡出秒數大於 0
            sequence.Append(messageCanvasGroup.DOFade(0f, messageFadeOutSeconds)); // 執行淡出
        else // 若淡出秒數為 0
            messageCanvasGroup.alpha = 0f; // 直接變透明

        messageVisibilityTween = sequence.OnComplete(() => // 動畫完成後
        {
            messageVisibilityTween = null; // 清除動畫引用
            if (messageText != null) // 再次確認文字元件仍存在
            {
                messageText.text = string.Empty; // 清空文字內容
            }

            GameObject currentRoot = GetMessageRoot(); // 再取一次根物件（避免期間被替換）
            if (currentRoot != null) // 根物件存在才處理
            {
                currentRoot.SetActive(false); // 關閉根物件以達到整體隱藏
            }
        });
    }

    private void InitializeMessagePresentation() // 初始化訊息文字狀態（預設隱藏）
    {
        if (messageText == null) // 沒有文字元件就不處理
            return; // 直接返回

        EnsureMessageCanvasGroup(); // 確保 CanvasGroup 可用
        messageCanvasGroup.alpha = 0f; // 初始化為透明
        messageCanvasGroup.blocksRaycasts = false; // 不阻擋射線
        messageCanvasGroup.interactable = false; // 不可互動
        messageText.text = string.Empty; // 初始內容為空

        GameObject root = GetMessageRoot(); // 取得根物件
        if (root != null && root.activeSelf) // 若根物件目前啟用
            root.SetActive(false); // 預設關閉（沒有台詞就隱藏）
    }

    private void EnsureMessageCanvasGroup() // 保證 messageText 上有 CanvasGroup
    {
        GameObject root = GetMessageRoot(); // 取得要掛 CanvasGroup 的根物件
        if (root == null) // 沒有根物件就無法掛 CanvasGroup
            return; // 直接返回

        if (messageCanvasGroup == null) // 若尚未抓到 CanvasGroup
        {
            messageCanvasGroup = root.GetComponent<CanvasGroup>(); // 先嘗試讀取既有元件
            if (messageCanvasGroup == null) // 若原本沒有 CanvasGroup
                messageCanvasGroup = root.AddComponent<CanvasGroup>(); // 在根物件動態新增一個
        }
    }

    private void HideMessageInstantly() // 立即隱藏訊息（不播動畫）
    {
        if (messageText == null) // 沒有文字元件就結束
            return; // 直接返回

        EnsureMessageCanvasGroup(); // 確保 CanvasGroup 可控制
        messageCanvasGroup.alpha = 0f; // 設為透明
        messageCanvasGroup.blocksRaycasts = false; // 不阻擋射線
        messageCanvasGroup.interactable = false; // 不可互動
        messageText.text = string.Empty; // 清空文字內容

        GameObject root = GetMessageRoot(); // 取得根物件
        if (root != null) // 根物件存在才關閉
            root.SetActive(false); // 直接關閉整個對話 root
    }

    private GameObject GetMessageRoot() // 取得真正用來顯示/隱藏對話的 root 物件
    {
        if (messageRoot != null) // 若 Inspector 有手動指定
            return messageRoot; // 直接使用指定物件

        if (messageText == null) // 若沒有 messageText 就無法推導 root
            return null; // 回傳 null

        messageRoot = InferMessageRootFromText(); // 依目前 messageText 狀態動態推導 root
        return messageRoot; // 回傳推導結果（若推導失敗會是 null）
    }

    private GameObject InferMessageRootFromText() // 根據 messageText 所在位置推導 BubbleRoot（或等效父物件）
    {
        if (messageText == null) // 沒有 messageText 就無法推導
            return null; // 回傳 null

        Transform textTransform = messageText.transform; // 取得文字物件 Transform
        Transform parent = textTransform.parent; // 取得父節點（可能是 BubbleRoot）
        if (parent == null) // 若沒有父節點
            return messageText.gameObject; // 退回使用 messageText 自身

        string parentName = parent.name.ToLowerInvariant(); // 父物件名稱轉小寫方便比對
        bool nameLooksLikeBubble = // 判斷父物件名稱是否像對話框容器
            parentName.Contains("bubble") ||
            parentName.Contains("message") ||
            parentName.Contains("dialog") ||
            parentName.Contains("talk");

        bool parentLooksLikeBubble = // 判斷父物件是否具有常見對話框容器元件
            parent.GetComponent<CanvasGroup>() != null ||
            parent.GetComponent<Image>() != null;

        if (nameLooksLikeBubble || parentLooksLikeBubble) // 父層看起來就是對話框容器
            return parent.gameObject; // 直接使用父層作為 root

        return messageText.gameObject; // 否則使用 messageText 自身，避免抓到更外層容器
    }

    private int LoadShopNpcStat(string key) // 讀取 NPC 統計資料（進店次數/累積消費）
    {
        return Mathf.Max(0, PlayerPrefs.GetInt(key, 0)); // 從 PlayerPrefs 讀取，最小值保底 0
    }

    private void SaveShopNpcStat(string key, int value) // 儲存 NPC 統計資料
    {
        PlayerPrefs.SetInt(key, Mathf.Max(0, value)); // 寫入整數，保底不小於 0
        PlayerPrefs.Save(); // 立即保存到磁碟
    }

    private int ResolveAdventureVisitCount() // 只計算當下這場冒險的進店次數
    {
        int activeRunSequenceId = RunManager.Instance != null ? RunManager.Instance.RunSequenceId : 0; // 讀取目前冒險序號

        if (cachedRunSequenceId != activeRunSequenceId) // 若序號不同，代表已切到新冒險
        {
            cachedRunSequenceId = activeRunSequenceId; // 更新快取序號
            currentAdventureVisitCount = 0; // 新冒險從 0 開始重新計算
        }

        currentAdventureVisitCount++; // 本次進店 +1
        return currentAdventureVisitCount; // 回傳當下冒險內的累積進店次數
    }

    private string BuildEntryNpcLine(int visitCount, int totalSpend) // 依進店次數與累積消費生成進店台詞
    {
        if (visitCount <= 1) // 第一次進店
            return "第一次來嗎？慢慢挑，今天有不少好貨。"; // 新客台詞
        if (totalSpend >= 120) // 累積消費高
            return "我的大股東！貴人能來我這小店，小店真是蓬蓽生輝呀!"; // 高消費台詞
        if (totalSpend >= 80) // 中高消費
            return "歡迎回來小財主，這次需要點什麼?"; // 中高消費台詞

        return "又見面了，這次要買什麼?"; // 一般回訪台詞
    }

    private string BuildPurchaseNpcLine(string itemName, int spentGold, int totalSpend) // 依購買條件生成成交台詞
    {
        string safeItemName = string.IsNullOrWhiteSpace(itemName) ? "這件商品" : itemName.Trim(); // 安全商品名稱（避免空字串）

        if (totalSpend >= 150) // 累積消費很高
            return PickRandomLine( // 從高消費台詞池隨機抽一句
                $"成交！{safeItemName} 配得上你，尊爵不凡。",
                $"老主顧果然有眼光，{safeItemName} 我幫你包好了。",
                $"{safeItemName} 跟你很搭，拿去讓它發揮真正價值吧。",
                $"你一出手就是精品，{safeItemName} 歸你了。");

        if (spentGold >= 12) // 本次消費偏高
            return PickRandomLine( // 從高價台詞池隨機抽一句
                $"真有眼光，{safeItemName} 值這 {spentGold} 金幣。",
                $"這件 {safeItemName} 很搶手呢。",
                $"好選擇，{safeItemName} 是我們店裡的精品。");

        if (spentGold >= 8) // 本次消費中等
            return PickRandomLine( // 從中價台詞池隨機抽一句
                $"成交，{safeItemName} 已為你準備好。",
                $"好，{safeItemName} 我這就交給你。",
                $"{safeItemName} 已經是你的了，收好。");

        return PickRandomLine( // 其餘視為一般消費
            $"好交易，這是你的 {safeItemName}，拿好了。",
            $"成交，{safeItemName} 給你了。",
            $"謝謝光顧，{safeItemName} 歸你。");
    }

    private string BuildInsufficientGoldLine(int requiredGold, int currentGold) // 依「差額」生成金幣不足台詞
    {
        int shortfall = Mathf.Max(0, requiredGold - currentGold); // 計算差額，最小為 0

        if (shortfall >= 12) // 差額很大
            return "這商品很貴的，沒有要買就別碰。"; // 大差額台詞

        if (shortfall >= 8) // 差額中等
            return "這已經是最便宜的價錢了。"; // 中差額台詞

        return "小本經營，不能賒帳。"; // 小差額台詞
    }

    private string PickRandomLine(params string[] lines) // 從給定台詞池隨機抽一句
    {
        if (lines == null || lines.Length == 0) // 若台詞池為空
            return string.Empty; // 回傳空字串

        int index = Random.Range(0, lines.Length); // 產生隨機索引（含 0，不含上限）
        return lines[index]; // 回傳對應台詞
    }
}
