using System.Collections.Generic;     // 引用集合類別，用於 List<T> 等
using System.Linq;                    // 引用 LINQ，用於快速搜尋與篩選
using UnityEngine;                    // Unity 基本函式庫
using UnityEngine.SceneManagement;    // 用於切換場景
using UnityEngine.UI;                 // 用於 UI 元件操作
using TMPro;                          // TMP (pageText / tab text 你有用到)
using DG.Tweening;                    // DOTween

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

    [SerializeField] private GameObject cardOfferTemplate;  // 卡片商品「外框模板」(可空；空就用 cardPrefab 本身當入口)
    [SerializeField] private GameObject cardPrefab;         // 卡片 UI 預製物 (你說最好別改它)
    [SerializeField] private Transform cardListParent;      // 卡片列表的父物件
    [SerializeField] private Transform relicListParent;     // 遺物列表的父物件
    [SerializeField] private Transform removalListParent;   // 移除卡片列表的父物件

    [SerializeField] private GameObject removalEntryTemplate; // 移除項目模板
    [SerializeField] private Button refreshRemovalButton;     // 重新整理移除清單按鈕
    [SerializeField] private Button returnButton;             // 返回按鈕

    private RunManager runManager;   // 遊戲進程管理器
    private Player player;           // 玩家物件
    private ShopInventoryDefinition inventory; // 商店資料來源

    private readonly List<CardBase> availableCards = new();  // 可購買卡片清單
    private readonly List<CardBase> availableRelics = new(); // 可購買遺物清單
    private bool offersGenerated; // 確保單次商店造訪僅生成一次商品

    private const int BaseCardPrice = 50;   // 卡片基本價格
    private const int BaseRelicPrice = 120; // 遺物基本價格

    // 你有兩套 Tab 按鈕，我都保留支援（不刪功能）
    [Header("Tabs (Optional Buttons)")]
    [SerializeField] private Button btnCards;
    [SerializeField] private Button btnRelics;
    [SerializeField] private Button btnRemoval;

    [Header("Lamps (Optional)")]
    [SerializeField] private Image lampCards;
    [SerializeField] private Image lampRelics;
    [SerializeField] private Image lampRemoval;

    [SerializeField] private Sprite lampNormalSprite;
    [SerializeField] private Sprite lampSelectedSprite;

    [SerializeField] private Color lampNormalColor = Color.white;
    [SerializeField] private Color lampSelectedColor = Color.white;

    [Header("Paging")]
    [SerializeField] private int cardsPerPage = 4;
    [SerializeField] private int relicsPerPage = 4;
    [SerializeField] private int removalPerPage = 6;

    [SerializeField] private Button btnPrev;
    [SerializeField] private Button btnNext;
    [SerializeField] private TMP_Text pageText; // 可選

    private int pageCards = 0;
    private int pageRelics = 0;
    private int pageRemoval = 0;

    public enum ShopTab
    {
        Cards,
        Relics,
        Removal
    }

    [Header("Tab Panels")]
    [SerializeField] private GameObject cardsPanel;
    [SerializeField] private GameObject relicsPanel;
    [SerializeField] private GameObject removalPanel;

    private ShopTab currentTab = ShopTab.Cards;

    private void Awake()
    {
        runManager = RunManager.Instance;

        CacheSceneReferences();
        BindButtons();
        BindTabButtons();
        HideTemplates();
    }

    private void Start()
    {
        InitializePlayer();
        LoadInventory();
        UpdateShopTitle();
        RefreshGoldDisplay();

        // 只生成一次商品池（保留你的 offersGenerated 規則）
        GenerateOffersFromInventory();

        // 預設顯示卡牌分頁（會同時更新燈籠 + 翻頁 + DOTween）
        SetTab(ShopTab.Cards);
    }

    private void InitializePlayer()
    {
        player = FindObjectOfType<Player>();
        if (player == null)
        {
            var playerGO = new GameObject("Player");
            player = playerGO.AddComponent<Player>();
        }

        if (runManager != null)
        {
            runManager.RegisterPlayer(player);
        }
    }

    private void LoadInventory()
    {
        inventory = runManager?.ActiveNode?.ShopInventory;
        if (inventory == null)
            inventory = runManager?.DefaultShopInventory;
        if (inventory == null)
            inventory = fallbackInventory;
    }

    // ===== UI 綁定 =====
    private void BindButtons()
    {
        if (refreshRemovalButton != null)
        {
            refreshRemovalButton.onClick.RemoveAllListeners();
            refreshRemovalButton.onClick.AddListener(() =>
            {
                // 不刪功能：按下依然是「刷新移除清單」
                // 分頁邏輯：回到第 0 頁再重建
                pageRemoval = 0;
                RefreshCurrentTabPage();
            });
        }

        if (returnButton != null)
        {
            returnButton.onClick.RemoveAllListeners();
            returnButton.onClick.AddListener(ExitShop);
        }

        // 翻頁按鈕
        if (btnPrev != null)
        {
            btnPrev.onClick.RemoveAllListeners();
            btnPrev.onClick.AddListener(() => ChangePage(-1));
        }

        if (btnNext != null)
        {
            btnNext.onClick.RemoveAllListeners();
            btnNext.onClick.AddListener(() => ChangePage(+1));
        }
    }

    private void BindTabButtons()
    {
        // 第一套 tab
        if (btnCards != null)
        {
            btnCards.onClick.RemoveAllListeners();
            btnCards.onClick.AddListener(() => SetTab(ShopTab.Cards));
            }

        if (btnRelics != null)
        {
            btnRelics.onClick.RemoveAllListeners();
            btnRelics.onClick.AddListener(() => SetTab(ShopTab.Relics));
        }

        if (btnRemoval != null)
        {
            btnRemoval.onClick.RemoveAllListeners();
            btnRemoval.onClick.AddListener(() => SetTab(ShopTab.Removal));
        }
    }

    // ===== 分頁切換（含 DOTween / 燈籠）=====
    private void SetTab(ShopTab tab)
    {
        currentTab = tab;

        // 1) 先關掉三個面板
        ShowPanel(cardsPanel, false);
        ShowPanel(relicsPanel, false);
        ShowPanel(removalPanel, false);

        // 2) 打開目標面板 + 淡入
        switch (tab)
        {
            case ShopTab.Cards:   ShowPanel(cardsPanel, true);   break;
            case ShopTab.Relics:  ShowPanel(relicsPanel, true);  break;
            case ShopTab.Removal: ShowPanel(removalPanel, true); break;
        }

        // 3) 更新燈籠狀態 + 動畫（如果你有綁）
        UpdateLamp(lampCards,   tab == ShopTab.Cards);
        UpdateLamp(lampRelics,  tab == ShopTab.Relics);
        UpdateLamp(lampRemoval, tab == ShopTab.Removal);

        // 4) 切到該分頁時，重畫那一區（配合翻頁）
        RefreshCurrentTabPage();
    }

    private void ShowPanel(GameObject panel, bool show)
    {
        if (!panel) return;

        panel.SetActive(show);

        // 用 CanvasGroup 做淡入淡出（沒有就自動補）
        var cg = panel.GetComponent<CanvasGroup>();
        if (!cg) cg = panel.AddComponent<CanvasGroup>();

        cg.DOKill();
        panel.transform.DOKill();

        cg.alpha = show ? 0f : 1f;
        cg.interactable = show;
        cg.blocksRaycasts = show;

        if (show)
        {
            cg.DOFade(1f, 0.2f);
            panel.transform.localScale = Vector3.one * 0.98f;
            panel.transform.DOScale(1f, 0.2f);
        }
    }

    private void UpdateLamp(Image lamp, bool selected)
    {
        if (!lamp) return;

        lamp.DOKill();
        lamp.transform.DOKill();

        // 換圖（若有提供）
        if (lampNormalSprite != null && lampSelectedSprite != null)
            lamp.sprite = selected ? lampSelectedSprite : lampNormalSprite;

        // 變色
        lamp.color = selected ? lampSelectedColor : lampNormalColor;

        // 小動畫：選中的燈籠跳一下
        if (selected)
        {
            lamp.transform.localScale = Vector3.one;
            lamp.transform.DOScale(1.08f, 0.12f).SetEase(Ease.OutQuad)
                .OnComplete(() => lamp.transform.DOScale(1f, 0.12f).SetEase(Ease.InQuad));
        }
        else
        {
            lamp.transform.localScale = Vector3.one;
        }
    }

    // ===== 分頁切換 =====
    private void ChangePage(int delta)
    {
        switch (currentTab)
        {
            case ShopTab.Cards:
                pageCards = Mathf.Max(0, pageCards + delta);
                break;
            case ShopTab.Relics:
                pageRelics = Mathf.Max(0, pageRelics + delta);
                break;
            case ShopTab.Removal:
                pageRemoval = Mathf.Max(0, pageRemoval + delta);
                break;
        }

        RefreshCurrentTabPage();
    }

    private void RefreshCurrentTabPage()
    {
        switch (currentTab)
        {
            case ShopTab.Cards:
                RebuildCardPage();
                break;
            case ShopTab.Relics:
                RebuildRelicPage();
                break;
            case ShopTab.Removal:
                RebuildRemovalPage();
                break;
        }
    }

    private void UpdatePageUI(int pageIndex, int pageCount)
    {
        // pageText 可選
        if (pageText != null)
            pageText.text = $"{pageIndex + 1} / {pageCount}";

        // Prev / Next 可選
        if (btnPrev != null)
            btnPrev.interactable = pageIndex > 0;

        if (btnNext != null)
            btnNext.interactable = pageIndex < pageCount - 1;
    }

    // ====== 生成 / 重建頁面 ======
    private void RebuildCardPage()
    {
        ClearChildren(cardListParent);

        int total = availableCards.Count;
        int perPage = Mathf.Max(1, cardsPerPage);
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(total / (float)perPage));
        pageCards = Mathf.Clamp(pageCards, 0, pageCount - 1);

        int start = pageCards * perPage;
        int end = Mathf.Min(start + perPage, total);

        for (int i = start; i < end; i++)
        {
            var card = availableCards[i];
            int price = GetCardPrice(card);
            CreateCardOffer(card, price);
        }

        UpdatePageUI(pageCards, pageCount);
    }

    private void RebuildRelicPage()
    {
        ClearChildren(relicListParent);

        int total = availableRelics.Count;
        int perPage = Mathf.Max(1, relicsPerPage);
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(total / (float)perPage));
        pageRelics = Mathf.Clamp(pageRelics, 0, pageCount - 1);

        int start = pageRelics * perPage;
        int end = Mathf.Min(start + perPage, total);

        for (int i = start; i < end; i++)
        {
            var relic = availableRelics[i];
            int price = GetRelicPrice(relic);

            // 這裡沿用你的「用模板產生一個可點擊的 entry」功能
            CreateOfferEntry(
                removalEntryTemplate, // 你原本 relic 也用這個（不刪功能）
                relicListParent,
                relic.cardName,
                price,
                relic.description,
                () => PurchaseRelic(relic, price)
            );
        }

        UpdatePageUI(pageRelics, pageCount);
    }

    private void RebuildRemovalPage()
    {
        ClearChildren(removalListParent);

        if (player == null || player.deck == null || inventory == null)
        {
            UpdatePageUI(0, 1);
            return;
        }

        int removalCost = inventory.CardRemovalCost;
        if (removalCostText != null)
            removalCostText.text = $"移除一張卡片需要 {removalCost} 金幣";

        int total = player.deck.Count;
        int perPage = Mathf.Max(1, removalPerPage);
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(total / (float)perPage));
        pageRemoval = Mathf.Clamp(pageRemoval, 0, pageCount - 1);

        int start = pageRemoval * perPage;
        int end = Mathf.Min(start + perPage, total);

        for (int i = start; i < end; i++)
        {
            var card = player.deck[i];
            if (card == null) continue;
            CreateRemovalEntry(card, removalCost, i);
        }

        UpdatePageUI(pageRemoval, pageCount);
    }

    // （保留你原本的功能名：RebuildOffers / BuildRemovalList）
    // 只是現在統一改成：刷新「目前分頁」即可（不會刪功能，只是更符合你要的翻頁模式）
    private void RebuildOffers()
    {
        // 如果你還想保留「首次進店才生成」的規則
        if (!offersGenerated)
            GenerateOffersFromInventory();

        RefreshCurrentTabPage();
    }

    private void BuildRemovalList()
    {
        // 保留功能：生成移除清單（現在是分頁版本）
        RefreshCurrentTabPage();
    }

    // ===== 生成商品池（只做一次）=====
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

        // 若當下正在顯示某頁，刷新畫面
        RefreshCurrentTabPage();
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

    // ===== 建立卡片購買項目 =====
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

    // ===== 移除項目 =====
    private void CreateRemovalEntry(CardBase card, int price, int cardIndex)
    {
        if (card == null || removalEntryTemplate == null || removalListParent == null)
            return;

        var entry = Instantiate(removalEntryTemplate, removalListParent);
        entry.name = $"Remove {card.cardName}";
        entry.SetActive(true);

        GameObject cardGO = null;
        Transform cardContainer = FindCardContainer(entry.transform);

        if (cardPrefab != null)
        {
            cardGO = Instantiate(cardPrefab, cardContainer != null ? cardContainer : entry.transform);
            ResetTransform(cardGO.transform);
        }

        string title = $"移除 {card.cardName}";
        string description = string.IsNullOrEmpty(card.description) ? "" : card.description;

        ApplyOfferTexts(entry, title, price, description, cardGO != null ? cardGO.transform : cardContainer);

        var button = entry.GetComponent<Button>() ?? entry.GetComponentInChildren<Button>(true);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => RemoveCardAt(cardIndex, price));
        }

        var cardUI = cardGO?.GetComponent<CardUI>();
        if (cardUI != null)
        {
            cardUI.SetupCard(card);
            cardUI.SetDisplayContext(CardUI.DisplayContext.Reward);
        }
    }

    // ===== 購買/移除 =====
    private void PurchaseCard(CardBase card, int price)
    {
        if (!TrySpendGold(price))
            return;

        player.deck.Add(Instantiate(card));
        availableCards.Remove(card);

        ShowMessage($"購買 {card.cardName} 成功！");
        RefreshGoldDisplay();
        RebuildOffers();
        SyncRunState();
    }

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

        // 分頁移除後：如果剛好移掉本頁最後一張，避免空頁
        pageRemoval = Mathf.Clamp(pageRemoval, 0, Mathf.Max(0, Mathf.CeilToInt(player.deck.Count / (float)Mathf.Max(1, removalPerPage)) - 1));

        BuildRemovalList();
        SyncRunState();
    }

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

    private void RefreshGoldDisplay()
    {
        if (goldText != null && player != null)
            goldText.text = $"金幣：{player.gold}";
    }

    private void ShowMessage(string text)
    {
        if (messageText != null)
            messageText.text = text;
    }

    private void ExitShop()
    {
        SyncRunState();

        if (runManager != null)
        {
            runManager.CompleteActiveNodeWithoutBattle();
            runManager.ReturnToRunSceneFromBattle();
        }
        else
        {
            SceneManager.LoadScene("RunScene");
        }
    }

    private void SyncRunState()
    {
        if (runManager != null)
            runManager.SyncPlayerRunState();
    }

    // ===== 清除子物件（保留模板）=====
    private void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (ShouldPreserveChild(child))
                continue;

            Destroy(child.gameObject);
        }
    }

    private bool ShouldPreserveChild(Transform child)
    {
        if (child == null)
            return false;

        if (removalCostText != null && child == removalCostText.transform)
            return true;

        if (removalEntryTemplate != null && child == removalEntryTemplate.transform)
            return true;

        if (cardOfferTemplate != null && child == cardOfferTemplate.transform)
            return true;

        return false;
    }

    // ===== 用模板建立 entry（卡片/法器/移除通用）=====
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

        ApplyOfferTexts(entry, title, price, description);
    }

    // ===== 設定文字（同時支援 Text / TMP_Text）=====
    private void ApplyOfferTexts(GameObject entry, string title, int price, string description, Transform excludeRoot = null)
    {
        if (entry == null) return;

        var uiTexts = FilterTexts(entry.GetComponentsInChildren<Text>(true), excludeRoot);
        ApplyOfferTexts(uiTexts, title, price, description,
            t => t.name,
            t => t.gameObject,
            t => t.text,
            (t, value) => t.text = value
        );

        var tmpTexts = FilterTexts(entry.GetComponentsInChildren<TMP_Text>(true), excludeRoot);
        ApplyOfferTexts(tmpTexts, title, price, description,
            t => t.name,
            t => t.gameObject,
            t => t.text,
            (t, value) => t.text = value
        );
    }

    private void ApplyOfferTexts<TText>(
        IEnumerable<TText> texts,
        string title,
        int price,
        string description,
        System.Func<TText, string> nameSelector,
        System.Func<TText, GameObject> goSelector,
        System.Func<TText, string> textGetter,
        System.Action<TText, string> textSetter
    ) where TText : Component
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

    private IEnumerable<TText> FilterTexts<TText>(IEnumerable<TText> texts, Transform excludeRoot) where TText : Component
    {
        if (excludeRoot == null)
            return texts;

        return texts.Where(t => t != null && !IsUnderContainer(t.transform, excludeRoot));
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

    // ===== 自動尋找（保留你的功能）=====
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

    private Text FindTextByName(string partialName)
    {
        var texts = GetComponentsInChildren<Text>(true);
        return texts.FirstOrDefault(t => t.name.ToLowerInvariant().Contains(partialName.ToLowerInvariant()));
    }

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
            if (lower.Contains("cardcontainer") || lower.Contains("cardholder") || lower.Contains("cardroot") || lower == "card")
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

        if (t is RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }
    }

    // ===== 價格計算（保留你的功能）=====
    private int GetCardPrice(CardBase card)
    {
        if (card == null)
            return BaseCardPrice;

        if (card.shopPrice > 0)
            return card.shopPrice;

        return Mathf.Max(BaseCardPrice, card.cost * 25);
    }

    private int GetRelicPrice(CardBase relic)
    {
        if (relic == null)
            return BaseRelicPrice;

        if (relic.shopPrice > 0)
            return relic.shopPrice;

        return Mathf.Max(BaseRelicPrice, 100 + relic.cost * 10);
    }

    // ===== 模板隱藏（保留你的功能）=====
    private void HideTemplates()
    {
        if (removalEntryTemplate != null)
            removalEntryTemplate.SetActive(false);

        if (cardOfferTemplate != null)
            cardOfferTemplate.SetActive(false);
    }

    private void UpdateShopTitle()
    {
        if (shopTitleText == null)
            return;

        if (runManager?.ActiveNode != null)
            shopTitleText.text = $"商店 - {runManager.ActiveNode.NodeId}";
        else
            shopTitleText.text = "商店";
    }
}
