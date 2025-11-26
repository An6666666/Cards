using System.Collections.Generic;     // 引用集合類別，用於 List<T> 等
using System.Linq;                    // 引用 LINQ，用於快速搜尋與篩選
using UnityEngine;                    // Unity 基本函式庫
using UnityEngine.SceneManagement;    // 用於切換場景
using UnityEngine.UI;                 // 用於 UI 元件操作

// 負責管理商店畫面的主要 UI 控制邏輯
public class ShopUIManager : MonoBehaviour
{
    [Header("Fallback Data")]
    [SerializeField] private ShopInventoryDefinition fallbackInventory;  // 備用商店物件（當目前節點無商店資料時使用）

    [Header("UI References")] // 商店介面中要綁定的 UI 元件
    [SerializeField] private Text shopTitleText;            // 商店標題
    [SerializeField] private Text goldText;                 // 顯示玩家金幣數量
    [SerializeField] private Text messageText;              // 顯示提示文字
    [SerializeField] private Text removalCostText;          // 顯示移除卡片的花費
    [SerializeField] private GameObject cardOfferTemplate;
    [SerializeField] private GameObject cardPrefab;         // 卡片 UI 預製物
    [SerializeField] private Transform cardListParent;      // 卡片列表的父物件
    [SerializeField] private Transform relicListParent;     // 遺物列表的父物件
    [SerializeField] private Transform removalListParent;   // 移除卡片列表的父物件
    [SerializeField] private GameObject removalEntryTemplate;       // 移除項目模板
    [SerializeField] private Button refreshRemovalButton;   // 重新整理移除清單按鈕
    [SerializeField] private Button returnButton;           // 返回按鈕

    private RunManager runManager;   // 遊戲進程管理器
    private Player player;           // 玩家物件
    private ShopInventoryDefinition inventory; // 商店資料來源

    private readonly List<CardBase> availableCards = new(); // 可購買卡片清單
    private readonly List<CardBase> availableRelics = new(); // 可購買遺物清單
    private bool offersGenerated; // 確保單次商店造訪僅生成一次商品
    private const int BaseCardPrice = 50;   // 卡片基本價格
    private const int BaseRelicPrice = 120; // 遺物基本價格

    private void Awake()
    {
        runManager = RunManager.Instance;  // 取得全域執行管理器

        CacheSceneReferences();  // 嘗試在場景中尋找尚未綁定的 UI 元件
        BindButtons();           // 綁定 UI 按鈕事件
        HideTemplates();         // 隱藏模板（避免直接顯示）
    }

    private void Start()
    {
        InitializePlayer();    // 初始化玩家（確保存在）
        LoadInventory();       // 載入商店物件清單
        UpdateShopTitle();     // 更新商店標題
        RefreshGoldDisplay();  // 顯示金幣數量
        RebuildOffers();       // 生成卡片與遺物販售清單
        BuildRemovalList();    // 生成移除卡片清單
    }

    private void InitializePlayer()
    {
        player = FindObjectOfType<Player>(); // 尋找場上玩家
        if (player == null)
        {
            var playerGO = new GameObject("Player"); // 若沒有則建立一個玩家物件
            player = playerGO.AddComponent<Player>();
        }

        if (runManager != null)
        {
            runManager.RegisterPlayer(player); // 註冊玩家到 RunManager
        }
    }

    private void LoadInventory()
    {
        inventory = runManager?.ActiveNode?.ShopInventory; // 優先使用當前節點的商店資料
        if (inventory == null)
            inventory = runManager?.DefaultShopInventory;  // 否則使用預設商店
        if (inventory == null)
            inventory = fallbackInventory;                 // 最後使用備援資料
    }

    private void RebuildOffers()
    {
        ClearChildren(cardListParent);  // 清空卡片區
        ClearChildren(relicListParent); // 清空遺物區

        // 第一次進來或重新載入時，依設定的數量隨機挑選商店商品
        if (!offersGenerated)
        {
            GenerateOffersFromInventory();
        }

        // 為每張卡片建立購買項目
        foreach (var card in availableCards)
        {
            int price = GetCardPrice(card);
            CreateCardOffer(card, price);
        }

        // 為每個遺物建立購買項目
        foreach (var relic in availableRelics)
        {
            int price = GetRelicPrice(relic);
            CreateOfferEntry(removalEntryTemplate, relicListParent, relic.cardName, price, relic.description, () => PurchaseRelic(relic, price));
        }
    }

    private void BuildRemovalList()
    {
        ClearChildren(removalListParent);  // 清空移除區

        if (player == null || player.deck == null || inventory == null)
            return;

        int removalCost = inventory.CardRemovalCost;  // 取得移除卡片費用
        if (removalCostText != null)
            removalCostText.text = $"移除一張卡片需要 {removalCost} 金幣";

        // 為玩家每張卡建立移除按鈕
        for (int i = 0; i < player.deck.Count; i++)
        {
            int index = i;
            CardBase card = player.deck[index];
            if (card == null)
                continue;

            string title = $"移除 {card.cardName}";
            string description = string.IsNullOrEmpty(card.description) ? "" : card.description;
            CreateOfferEntry(removalEntryTemplate, removalListParent, title, removalCost, description, () => RemoveCardAt(index, removalCost));
        }
    }

    // 生成卡片購買項目
    private void CreateCardOffer(CardBase card, int price)
    {
        if (card == null || cardListParent == null)
            return;

        GameObject entry = null;
        GameObject cardGO = null;
        Button button = null;

        Transform cardContainer = null;

        if (cardOfferTemplate != null)
        {
            entry = Instantiate(cardOfferTemplate, cardListParent);
            entry.name = card.cardName;
            entry.SetActive(true);

            cardContainer = FindCardContainer(entry.transform);

            if (cardPrefab != null)
            {
                cardGO = Instantiate(cardPrefab, cardContainer != null ? cardContainer : entry.transform);
                ResetTransform(cardGO.transform);
            }

            ApplyOfferTexts(entry, card.cardName, price, card.description, cardGO != null ? cardGO.transform : cardContainer);


            button = entry.GetComponent<Button>() ?? entry.GetComponentInChildren<Button>(true);
        }
        else if (cardPrefab != null)
        {
            cardGO = Instantiate(cardPrefab, cardListParent);
            cardGO.name = card.cardName;
            cardGO.SetActive(true);

            button = cardGO.GetComponent<Button>() ?? cardGO.GetComponentInChildren<Button>(true);
            if (button == null)
                button = cardGO.AddComponent<Button>();
        }

        var cardUI = cardGO?.GetComponent<CardUI>();
        if (cardUI != null)
        {
            cardUI.SetupCard(card);
            cardUI.SetDisplayContext(CardUI.DisplayContext.Reward);
        }

        if (button == null && cardGO != null)
            button = cardGO.GetComponent<Button>() ?? cardGO.GetComponentInChildren<Button>(true);

        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => PurchaseCard(card, price));

    }

    // 執行購買卡片
    private void PurchaseCard(CardBase card, int price)
    {
        if (!TrySpendGold(price))
            return;

        player.deck.Add(Instantiate(card));       // 加入玩家卡組
        availableCards.Remove(card);              // 從商店移除
        ShowMessage($"購買 {card.cardName} 成功！");
        RefreshGoldDisplay();                     // 更新金幣
        RebuildOffers();                          // 重新生成清單
        SyncRunState();                           // 同步遊戲狀態
    }

    // 購買遺物
    private void PurchaseRelic(CardBase relic, int price)
    {
        if (!TrySpendGold(price))
            return;

        player.relics.Add(Instantiate(relic));
        availableRelics.Remove(relic);
        ShowMessage($"購買 {relic.cardName} 成功！");
        RefreshGoldDisplay();
        RebuildOffers();
        SyncRunState();
    }

    // 移除指定索引的卡
    private void RemoveCardAt(int index, int cost)
    {
        if (player == null || player.deck == null || index < 0 || index >= player.deck.Count)
            return;

        if (!TrySpendGold(cost))
            return;

        var removed = player.deck[index];
        player.deck.RemoveAt(index);
        ShowMessage(removed != null ? $"已移除 {removed.cardName}" : "已移除卡片");
        RefreshGoldDisplay();
        BuildRemovalList();
        SyncRunState();
    }

    // 嘗試扣除金幣
    private bool TrySpendGold(int price)
    {
        if (player == null)
            return false;

        if (player.gold < price)
        {
            ShowMessage("金幣不足");
            return false;
        }

        player.gold -= price;
        return true;
    }

    // 更新金幣顯示
    private void RefreshGoldDisplay()
    {
        if (goldText != null && player != null)
        {
            goldText.text = $"金幣：{player.gold}";
        }
    }

    // 顯示提示文字
    private void ShowMessage(string text)
    {
        if (messageText != null)
            messageText.text = text;
    }

    // 離開商店
    private void ExitShop()
    {
        SyncRunState();
        if (runManager != null)
        {
            runManager.CompleteActiveNodeWithoutBattle();  // 標記該節點完成
            runManager.ReturnToRunSceneFromBattle();       // 返回地圖畫面
        }
        else
        {
            SceneManager.LoadScene("RunScene");
        }
    }

    private void SyncRunState()
    {
        if (runManager != null)
        {
            runManager.SyncPlayerRunState();  // 將玩家資料同步回當前進程
        }
    }

    // 清除子物件
    private void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (removalCostText != null && child == removalCostText.transform)
                continue;

            Destroy(child.gameObject);
        }
    }

    // 以模板建立一個商品項目（卡片、遺物、移除卡）
    private void CreateOfferEntry(GameObject template, Transform parent, string title, int price, string description, UnityEngine.Events.UnityAction onClick)
    {
        if (template == null || parent == null)
            return;

        var entry = Instantiate(template, parent);
        entry.name = title;
        entry.SetActive(true);

        var button = entry.GetComponent<Button>() ?? entry.GetComponentInChildren<Button>(true);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onClick != null)
                button.onClick.AddListener(onClick);
        }

        ApplyOfferTexts(entry, title, price, description); // 設定文字內容
    }

    // 設定項目文字內容（名稱、價格、描述）
    private void ApplyOfferTexts(GameObject entry, string title, int price, string description, Transform excludeRoot = null)
    {
        ApplyOfferTexts(FilterTexts(entry.GetComponentsInChildren<Text>(true), excludeRoot), title, price, description, t => t.name, t => t.gameObject, t => t.text, (t, value) => t.text = value);

#if TMP_PRESENT || TMPRO_PRESENT || UNITY_TEXTMESHPRO
        ApplyOfferTexts(FilterTexts(entry.GetComponentsInChildren<TMPro.TMP_Text>(true), excludeRoot), title, price, description, t => t.name, t => t.gameObject, t => t.text, (t, value) => t.text = value);
#endif
    }

    private void ApplyOfferTexts<TText>(IEnumerable<TText> texts, string title, int price, string description, System.Func<TText, string> nameSelector, System.Func<TText, GameObject> goSelector, System.Func<TText, string> textGetter, System.Action<TText, string> textSetter) where TText : Component
    {
        var textList = texts?.Where(t => t != null).ToList();
        if (textList == null || textList.Count == 0)
            return;

        TText titleText = default;
        TText priceText = default;
        TText descriptionText = default;

        foreach (var text in textList)
        {
            string lowerName = nameSelector(text).ToLowerInvariant();
            if (EqualityComparer<TText>.Default.Equals(titleText, default) && (lowerName.Contains("title") || lowerName.Contains("name")))
                titleText = text;
            else if (EqualityComparer<TText>.Default.Equals(priceText, default) && (lowerName.Contains("price") || lowerName.Contains("cost")))
                priceText = text;
            else if (EqualityComparer<TText>.Default.Equals(descriptionText, default) && (lowerName.Contains("description") || lowerName.Contains("desc")))
                descriptionText = text;
        }

        if (EqualityComparer<TText>.Default.Equals(priceText, default))
        {
            priceText = textList.FirstOrDefault(t =>
            {
                var content = textGetter(t);
                if (string.IsNullOrEmpty(content))
                    return false;

                string lowerContent = content.ToLowerInvariant();
                return lowerContent.Contains("price") || lowerContent.Contains("cost") || lowerContent.Contains("gold") || lowerContent.Contains("金") || lowerContent.Contains("價");
            });
        }

        if (EqualityComparer<TText>.Default.Equals(priceText, default))
            priceText = textList.FirstOrDefault(t => !EqualityComparer<TText>.Default.Equals(t, titleText));

        if (!EqualityComparer<TText>.Default.Equals(titleText, default))
            textSetter(titleText, EqualityComparer<TText>.Default.Equals(titleText, priceText) ? $"{title} - {price} 金幣" : title);

        if (!EqualityComparer<TText>.Default.Equals(priceText, default) && !EqualityComparer<TText>.Default.Equals(priceText, titleText))
            textSetter(priceText, $"{price} 金幣");

        if (!EqualityComparer<TText>.Default.Equals(descriptionText, default))
        {
            goSelector(descriptionText).SetActive(!string.IsNullOrWhiteSpace(description));
            textSetter(descriptionText, description);
        }

        if (EqualityComparer<TText>.Default.Equals(priceText, default) && !EqualityComparer<TText>.Default.Equals(titleText, default))
            textSetter(titleText, $"{title} - {price} 金幣");
    }

    private IEnumerable<TText> FilterTexts<TText>(IEnumerable<TText> texts, Transform cardContainer) where TText : Component
    {
        if (cardContainer == null)
            return texts;

        return texts.Where(t => t != null && !IsUnderContainer(t.transform, cardContainer));
    }

    private bool IsUnderContainer(Transform target, Transform container)
    {
        if (target == null || container == null)
            return false;

        var current = target;
        while (current != null)
        {
            if (current == container)
                return true;

            current = current.parent;
        }

        return false;
    }

    // 自動尋找場景中未綁定的 UI 元件
    private void CacheSceneReferences()
    {
        if (goldText == null)
            goldText = FindTextByName("Gold");
        if (messageText == null)
            messageText = FindTextByName("Message");
        if (removalCostText == null)
            removalCostText = FindTextByName("RemovalCost");
        if (cardListParent == null)
            cardListParent = FindContainerByName("CardList");
        if (relicListParent == null)
            relicListParent = FindContainerByName("RelicList");
        if (removalListParent == null)
            removalListParent = FindContainerByName("RemovalList");
    }

    // 綁定 UI 按鈕事件
    private void BindButtons()
    {
        if (refreshRemovalButton != null)
        {
            refreshRemovalButton.onClick.RemoveAllListeners();
            refreshRemovalButton.onClick.AddListener(BuildRemovalList); // 點擊刷新移除清單
        }

        if (returnButton != null)
        {
            returnButton.onClick.RemoveAllListeners();
            returnButton.onClick.AddListener(ExitShop); // 點擊返回地圖
        }
    }

    // 隱藏模板（避免預設顯示）
    private void HideTemplates()
    {  
        if (removalEntryTemplate != null)
            removalEntryTemplate.SetActive(false);
        if (cardOfferTemplate != null)
            cardOfferTemplate.SetActive(false);
    }

    // 更新商店標題
    private void UpdateShopTitle()
    {
        if (shopTitleText == null)
            return;

        if (runManager?.ActiveNode != null)
            shopTitleText.text = $"商店 - {runManager.ActiveNode.NodeId}";
    }

    // 根據名稱尋找 Text 元件
    private Text FindTextByName(string partialName)
    {
        var texts = GetComponentsInChildren<Text>(true);
        return texts.FirstOrDefault(t => t.name.ToLowerInvariant().Contains(partialName.ToLowerInvariant()));
    }

    // 根據名稱尋找容器 Transform
    private Transform FindContainerByName(string partialName)
    {
        var transforms = GetComponentsInChildren<Transform>(true);
        return transforms.FirstOrDefault(t => t != transform && t.name.ToLowerInvariant().Contains(partialName.ToLowerInvariant()));
    }

    private Transform FindCardContainer(Transform root)
    {
        if (root == null)
            return null;

        var transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in transforms)
        {
            if (t == root)
                continue;

            string lower = t.name.ToLowerInvariant();
            if (lower.Contains("cardcontainer") || lower.Contains("cardholder") || lower.Contains("cardroot") || lower.Contains("card"))
                return t;
        }

        return null;
    }

    private void ResetTransform(Transform t)
    {
        if (t == null)
            return;

        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        var rect = t as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }
    }

    // 計算卡片價格（基於卡片 cost）
    private int GetCardPrice(CardBase card)
    {
        if (card == null)
            return BaseCardPrice;
        
        if (card.shopPrice > 0)
            return card.shopPrice;

        return Mathf.Max(BaseCardPrice, card.cost * 25);
    }

    // 計算遺物價格（基於 cost）
    private int GetRelicPrice(CardBase relic)
    {
        if (relic == null)
            return BaseRelicPrice;

        if (relic.shopPrice > 0)
            return relic.shopPrice;

        return Mathf.Max(BaseRelicPrice, 100 + relic.cost * 10);
    }

    // 依商店設定的數量，從可購買清單中隨機挑選卡片與遺物
    private void GenerateOffersFromInventory()
    {
        availableCards.Clear();
        availableRelics.Clear();

        offersGenerated = false;

        if (inventory == null)
            return;

        AddRandomSelections(inventory.PurchasableCards, inventory.CardOfferCount, availableCards);
        AddRandomSelections(inventory.PurchasableRelics, inventory.RelicOfferCount, availableRelics);

        offersGenerated = true;
    }

    private void AddRandomSelections(IReadOnlyList<CardBase> source, int desiredCount, List<CardBase> target)
    {
        var pool = source?.Where(c => c != null).ToList();
        if (pool == null || pool.Count == 0)
            return;

        int count = desiredCount <= 0 ? pool.Count : Mathf.Min(desiredCount, pool.Count);

        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, pool.Count);
            target.Add(pool[index]);
            pool.RemoveAt(index);
        }
    }
}
