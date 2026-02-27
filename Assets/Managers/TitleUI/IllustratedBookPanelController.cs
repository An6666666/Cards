using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IllustratedBookPanelController : MonoBehaviour
{
    public enum BookPage
    {
        [InspectorName("卡牌")] Cards,
        [InspectorName("敵人")] Monsters,
        [InspectorName("法器")] Relics
    }

    [Serializable]
    public class UITextBinding
    {
        [SerializeField, InspectorName("Text(舊版UGUI)")] private Text legacyText;
        [SerializeField, InspectorName("TMP_Text")] private TMP_Text tmpText;

        public void SetText(string value)
        {
            string safe = value ?? string.Empty;
            if (legacyText != null) legacyText.text = safe;
            if (tmpText != null) tmpText.text = safe;
        }
    }

    [Serializable]
    public class IllustratedBookSlotBinding
    {
        [SerializeField, InspectorName("格子根物件")] private GameObject root;
        [SerializeField, InspectorName("格子按鈕")] private Button button;
        [SerializeField, InspectorName("Icon圖片")] private Image iconImage;
        [SerializeField, InspectorName("名稱文字")] private UITextBinding nameText;
        [SerializeField, InspectorName("有資料顯示物件")] private GameObject filledVisual;
        [SerializeField, InspectorName("空格顯示物件")] private GameObject emptyVisual;

        public GameObject Root => root;
        public Button Button => button;

        public void SetFilled(Sprite icon, string displayName)
        {
            if (root != null) root.SetActive(true);

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (nameText != null) nameText.SetText(displayName);
            if (filledVisual != null) filledVisual.SetActive(true);
            if (emptyVisual != null) emptyVisual.SetActive(false);
            if (button != null) button.interactable = true;
        }

        public void SetEmpty()
        {
            if (root != null) root.SetActive(true);

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (nameText != null) nameText.SetText(string.Empty);
            if (filledVisual != null) filledVisual.SetActive(false);
            if (emptyVisual != null) emptyVisual.SetActive(true);
            if (button != null) button.interactable = false;
        }

        public void ClearClick()
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }

        public void BindClick(UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }

    [Serializable]
    public class CardDetailBindings
    {
        [SerializeField, InspectorName("詳細頁根物件")] private GameObject root;
        [SerializeField, InspectorName("名稱文字")] private UITextBinding nameText;
        [SerializeField, InspectorName("主圖片")] private Image portraitImage;
        [SerializeField, InspectorName("副圖片")] private Image secondaryImage;
        [SerializeField, InspectorName("描述文字")] private UITextBinding descriptionText;

        public GameObject Root => root;

        public void Fill(CardBookData data)
        {
            if (data == null) return;

            if (nameText != null) nameText.SetText(data.DisplayName);
            if (descriptionText != null) descriptionText.SetText(data.Description);

            SetImage(portraitImage, data.Portrait);
            SetImage(secondaryImage, data.ExtraSprite);
        }

        private static void SetImage(Image target, Sprite sprite)
        {
            if (target == null) return;
            target.sprite = sprite;
            target.enabled = sprite != null;
        }
    }

    [Serializable]
    public class MonsterDetailBindings
    {
        [SerializeField, InspectorName("詳細頁根物件")] private GameObject root;
        [SerializeField, InspectorName("名稱文字")] private UITextBinding nameText;
        [SerializeField, InspectorName("立繪圖片")] private Image portraitImage;
        [SerializeField, InspectorName("介紹文字")] private UITextBinding introText;
        [SerializeField, InspectorName("技能文字")] private UITextBinding skillText;
        [SerializeField, InspectorName("提示文字")] private UITextBinding tipText;

        public GameObject Root => root;

        public void Fill(MonsterBookData data)
        {
            if (data == null) return;

            if (nameText != null) nameText.SetText(data.DisplayName);
            if (introText != null) introText.SetText(data.Intro);
            if (skillText != null) skillText.SetText(data.Skill);
            if (tipText != null) tipText.SetText(data.Tip);

            if (portraitImage != null)
            {
                portraitImage.sprite = data.Portrait;
                portraitImage.enabled = data.Portrait != null;
            }
        }
    }

    [Serializable]
    public class RelicDetailBindings
    {
        [SerializeField, InspectorName("詳細頁根物件")] private GameObject root;
        [SerializeField, InspectorName("名稱文字")] private UITextBinding nameText;
        [SerializeField, InspectorName("立繪圖片")] private Image portraitImage;
        [SerializeField, InspectorName("介紹文字")] private UITextBinding introText;
        [SerializeField, InspectorName("概念文字")] private UITextBinding conceptText;
        [SerializeField, InspectorName("區域圖片(可選/未使用)")] private Image areaImage;

        public GameObject Root => root;

        public void Fill(RelicBookData data)
        {
            if (data == null) return;

            if (nameText != null) nameText.SetText(data.DisplayName);
            if (introText != null) introText.SetText(data.Intro);
            if (conceptText != null) conceptText.SetText(data.Concept);

            if (portraitImage != null)
            {
                portraitImage.sprite = data.Portrait;
                portraitImage.enabled = data.Portrait != null;
            }

            // areaImage / areaSprite is optional and not required by current detail spec (1 image + 2 texts).
            if (areaImage != null && areaImage.sprite == null)
            {
                areaImage.enabled = false;
            }
        }
    }

    [Serializable]
    public class PaginationBindings
    {
        [SerializeField, InspectorName("上一頁按鈕")] private Button prevButton;
        [SerializeField, InspectorName("下一頁按鈕")] private Button nextButton;
        [SerializeField, InspectorName("頁碼文字")] private UITextBinding pageText;

        public Button PrevButton => prevButton;
        public Button NextButton => nextButton;
        public UITextBinding PageText => pageText;
    }

    [Serializable]
    public class BookDataBase
    {
        [SerializeField, InspectorName("ID")] private string id;
        [SerializeField, InspectorName("顯示名稱")] private string displayName;
        [SerializeField, InspectorName("列表Icon")] private Sprite icon;
        [SerializeField, InspectorName("詳細頁立繪")] private Sprite portrait;
        [SerializeField, InspectorName("置頂")] private bool pinned;
        [SerializeField, InspectorName("排序值")] private int order;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public Sprite Icon => icon;
        public Sprite Portrait => portrait;
        public bool Pinned => pinned;
        public int Order => order;
    }

    [Serializable]
    public class CardBookData : BookDataBase
    {
        [SerializeField, InspectorName("第二張圖片(可選)")] private Sprite extraSprite;
        [TextArea(2, 6)]
        [SerializeField, InspectorName("描述")] private string description;

        public Sprite ExtraSprite => extraSprite;
        public string Description => description;
    }

    [Serializable]
    public class MonsterBookData : BookDataBase
    {
        [TextArea(2, 6)]
        [SerializeField, InspectorName("介紹")] private string intro;
        [TextArea(2, 6)]
        [SerializeField, InspectorName("技能")] private string skill;
        [TextArea(2, 6)]
        [SerializeField, InspectorName("提示")] private string tip;

        public string Intro => intro;
        public string Skill => skill;
        public string Tip => tip;
    }

    [Serializable]
    public class RelicBookData : BookDataBase
    {
        [TextArea(2, 6)]
        [SerializeField, InspectorName("介紹")] private string intro;
        [TextArea(2, 6)]
        [SerializeField, InspectorName("概念")] private string concept;
        [SerializeField, InspectorName("區域圖片(可選/未使用)")] private Sprite areaSprite;

        public string Intro => intro;
        public string Concept => concept;
        public Sprite AreaSprite => areaSprite;
    }

    [Header("面板根物件")]
    [SerializeField, InspectorName("圖鑑主面板"), Tooltip("整個圖鑑 Root，用 SetActive 控制")] private GameObject illustratedBookPanel;
    [SerializeField, InspectorName("遮罩(防誤觸)"), Tooltip("可選的透明遮罩，開啟圖鑑時一併顯示")] private GameObject blocker;
    [SerializeField, InspectorName("卡牌大量頁"), Tooltip("Cards 的列表頁根物件")] private GameObject cardsPanel;
    [SerializeField, InspectorName("妖怪大量頁"), Tooltip("Monsters 的列表頁根物件")] private GameObject monstersPanel;
    [SerializeField, InspectorName("法器大量頁"), Tooltip("Relics 的列表頁根物件")] private GameObject relicsPanel;

    [Header("按鈕設定")]
    [SerializeField, InspectorName("關閉按鈕"), Tooltip("關閉圖鑑主面板")] private Button closeButton;
    [SerializeField, InspectorName("卡牌分類按鈕")] private Button cardsTabButton;
    [SerializeField, InspectorName("妖怪分類按鈕")] private Button monstersTabButton;
    [SerializeField, InspectorName("法器分類按鈕")] private Button relicsTabButton;

    [Header("分頁(各分類)")]
    [SerializeField, InspectorName("卡牌分頁")] private PaginationBindings cardsPagination;
    [SerializeField, InspectorName("妖怪分頁")] private PaginationBindings monstersPagination;
    [SerializeField, InspectorName("法器分頁")] private PaginationBindings relicsPagination;

    [Header("分頁(共用)")]
    [SerializeField, InspectorName("使用共用分頁UI")] private bool useSharedPaginationBindings;
    [SerializeField, InspectorName("共用分頁綁定")] private PaginationBindings sharedPagination;

    [Header("固定格子(手動排版)")]
    [SerializeField, InspectorName("卡牌格子"), Tooltip("固定卡牌 slot 清單，數量決定每頁顯示數")] private List<IllustratedBookSlotBinding> cardsSlots = new List<IllustratedBookSlotBinding>();
    [SerializeField, InspectorName("妖怪格子(2x8)")] private List<IllustratedBookSlotBinding> monstersSlots = new List<IllustratedBookSlotBinding>();
    [SerializeField, InspectorName("法器格子(1x3)")] private List<IllustratedBookSlotBinding> relicsSlots = new List<IllustratedBookSlotBinding>();

    [Header("詳細頁根物件")]
    [SerializeField, InspectorName("卡牌詳細頁")] private CardDetailBindings cardDetail;
    [SerializeField, InspectorName("妖怪詳細頁")] private MonsterDetailBindings monsterDetail;
    [SerializeField, InspectorName("法器詳細頁")] private RelicDetailBindings relicDetail;

    [Header("詳細頁返回按鈕(可選)")]
    [SerializeField, InspectorName("卡牌詳細頁返回按鈕")] private Button cardDetailBackButton;
    [SerializeField, InspectorName("妖怪詳細頁返回按鈕")] private Button monsterDetailBackButton;
    [SerializeField, InspectorName("法器詳細頁返回按鈕")] private Button relicDetailBackButton;

    [Header("預設設定")]
    [SerializeField, InspectorName("預設分類")] private BookPage defaultPage = BookPage.Cards;
    [SerializeField, InspectorName("Awake時隱藏面板")] private bool hidePanelOnAwake = true;
    [SerializeField, InspectorName("ESC關閉面板")] private bool closeOnEscape = false;

    [Header("圖鑑資料(暫用/Inspector填入)")]
    [SerializeField, InspectorName("卡牌資料")] private List<CardBookData> cardsData = new List<CardBookData>();
    [SerializeField, InspectorName("妖怪資料")] private List<MonsterBookData> monstersData = new List<MonsterBookData>();
    [SerializeField, InspectorName("法器資料")] private List<RelicBookData> relicsData = new List<RelicBookData>();

    private BookPage _currentPage = BookPage.Cards;
    private int _cardsPageIndex;
    private int _monstersPageIndex;
    private int _relicsPageIndex;
    private bool _isDetailOpen;

    private void Awake()
    {
        WireButtons();

        if (hidePanelOnAwake)
        {
            if (illustratedBookPanel != null) illustratedBookPanel.SetActive(false);
            if (blocker != null) blocker.SetActive(false);
        }

        HideAllDetailRoots();
    }

    private void Update()
    {
        if (!closeOnEscape) return;
        if (illustratedBookPanel == null || !illustratedBookPanel.activeSelf) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    public void Open()
    {
        Open(defaultPage);
    }

    public void Open(BookPage page)
    {
        if (illustratedBookPanel == null)
        {
            Debug.LogWarning("IllustratedBookPanelController: illustratedBookPanel is not assigned.", this);
            return;
        }

        illustratedBookPanel.SetActive(true);
        if (blocker != null) blocker.SetActive(true);

        switch (page)
        {
            case BookPage.Monsters:
                ShowMonsters();
                break;
            case BookPage.Relics:
                ShowRelics();
                break;
            default:
                ShowCards();
                break;
        }
    }

    public void Close()
    {
        HideAllDetailRoots();
        _isDetailOpen = false;

        if (illustratedBookPanel != null)
        {
            illustratedBookPanel.SetActive(false);
        }

        if (blocker != null)
        {
            blocker.SetActive(false);
        }
    }

    public void Toggle()
    {
        if (illustratedBookPanel == null)
        {
            Debug.LogWarning("IllustratedBookPanelController: illustratedBookPanel is not assigned.", this);
            return;
        }

        if (illustratedBookPanel.activeSelf)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void ShowCards()
    {
        _currentPage = BookPage.Cards;
        _cardsPageIndex = 0;
        _isDetailOpen = false;
        HideAllDetailRoots();
        ShowOnlyListPanel(BookPage.Cards);
        BindSharedPaginationIfNeeded(BookPage.Cards);
        RefreshCardsPage();
    }

    public void ShowMonsters()
    {
        _currentPage = BookPage.Monsters;
        _monstersPageIndex = 0;
        _isDetailOpen = false;
        HideAllDetailRoots();
        ShowOnlyListPanel(BookPage.Monsters);
        BindSharedPaginationIfNeeded(BookPage.Monsters);
        RefreshMonstersPage();
    }

    public void ShowRelics()
    {
        _currentPage = BookPage.Relics;
        _relicsPageIndex = 0;
        _isDetailOpen = false;
        HideAllDetailRoots();
        ShowOnlyListPanel(BookPage.Relics);
        BindSharedPaginationIfNeeded(BookPage.Relics);
        RefreshRelicsPage();
    }

    public void BackToList()
    {
        _isDetailOpen = false;
        HideAllDetailRoots();
        ShowOnlyListPanel(_currentPage);

        switch (_currentPage)
        {
            case BookPage.Monsters:
                BindSharedPaginationIfNeeded(BookPage.Monsters);
                RefreshMonstersPage();
                break;
            case BookPage.Relics:
                BindSharedPaginationIfNeeded(BookPage.Relics);
                RefreshRelicsPage();
                break;
            default:
                BindSharedPaginationIfNeeded(BookPage.Cards);
                RefreshCardsPage();
                break;
        }
    }

    public void PrevCardsPage() { ChangePage(BookPage.Cards, -1); }
    public void NextCardsPage() { ChangePage(BookPage.Cards, +1); }
    public void PrevMonstersPage() { ChangePage(BookPage.Monsters, -1); }
    public void NextMonstersPage() { ChangePage(BookPage.Monsters, +1); }
    public void PrevRelicsPage() { ChangePage(BookPage.Relics, -1); }
    public void NextRelicsPage() { ChangePage(BookPage.Relics, +1); }

    public void OpenCardDetailById(string id) { OpenDetail(BookPage.Cards, id); }
    public void OpenMonsterDetailById(string id) { OpenDetail(BookPage.Monsters, id); }
    public void OpenRelicDetailById(string id) { OpenDetail(BookPage.Relics, id); }

    public void RefreshCurrentCategory()
    {
        switch (_currentPage)
        {
            case BookPage.Monsters:
                RefreshMonstersPage();
                break;
            case BookPage.Relics:
                RefreshRelicsPage();
                break;
            default:
                RefreshCardsPage();
                break;
        }
    }

    private void WireButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        if (cardsTabButton != null)
        {
            cardsTabButton.onClick.RemoveAllListeners();
            cardsTabButton.onClick.AddListener(ShowCards);
        }

        if (monstersTabButton != null)
        {
            monstersTabButton.onClick.RemoveAllListeners();
            monstersTabButton.onClick.AddListener(ShowMonsters);
        }

        if (relicsTabButton != null)
        {
            relicsTabButton.onClick.RemoveAllListeners();
            relicsTabButton.onClick.AddListener(ShowRelics);
        }

        WirePerCategoryPaginationButtons();

        if (cardDetailBackButton != null)
        {
            cardDetailBackButton.onClick.RemoveAllListeners();
            cardDetailBackButton.onClick.AddListener(BackToList);
        }

        if (monsterDetailBackButton != null)
        {
            monsterDetailBackButton.onClick.RemoveAllListeners();
            monsterDetailBackButton.onClick.AddListener(BackToList);
        }

        if (relicDetailBackButton != null)
        {
            relicDetailBackButton.onClick.RemoveAllListeners();
            relicDetailBackButton.onClick.AddListener(BackToList);
        }
    }

    private void WirePerCategoryPaginationButtons()
    {
        BindPaginationButton(cardsPagination != null ? cardsPagination.PrevButton : null, PrevCardsPage);
        BindPaginationButton(cardsPagination != null ? cardsPagination.NextButton : null, NextCardsPage);

        BindPaginationButton(monstersPagination != null ? monstersPagination.PrevButton : null, PrevMonstersPage);
        BindPaginationButton(monstersPagination != null ? monstersPagination.NextButton : null, NextMonstersPage);

        BindPaginationButton(relicsPagination != null ? relicsPagination.PrevButton : null, PrevRelicsPage);
        BindPaginationButton(relicsPagination != null ? relicsPagination.NextButton : null, NextRelicsPage);
    }

    private void BindSharedPaginationIfNeeded(BookPage category)
    {
        if (!useSharedPaginationBindings || sharedPagination == null)
        {
            return;
        }

        if (sharedPagination.PrevButton != null)
        {
            sharedPagination.PrevButton.onClick.RemoveAllListeners();
        }

        if (sharedPagination.NextButton != null)
        {
            sharedPagination.NextButton.onClick.RemoveAllListeners();
        }

        switch (category)
        {
            case BookPage.Monsters:
                BindPaginationButton(sharedPagination.PrevButton, PrevMonstersPage);
                BindPaginationButton(sharedPagination.NextButton, NextMonstersPage);
                break;
            case BookPage.Relics:
                BindPaginationButton(sharedPagination.PrevButton, PrevRelicsPage);
                BindPaginationButton(sharedPagination.NextButton, NextRelicsPage);
                break;
            default:
                BindPaginationButton(sharedPagination.PrevButton, PrevCardsPage);
                BindPaginationButton(sharedPagination.NextButton, NextCardsPage);
                break;
        }
    }

    private void BindPaginationButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void ChangePage(BookPage category, int delta)
    {
        switch (category)
        {
            case BookPage.Monsters:
            {
                List<MonsterBookData> sorted = GetSortedMonsters();
                int totalPages = GetTotalPages(sorted.Count, monstersSlots.Count);
                _monstersPageIndex = Mathf.Clamp(_monstersPageIndex + delta, 0, Mathf.Max(0, totalPages - 1));
                RefreshMonstersPage();
                break;
            }
            case BookPage.Relics:
            {
                List<RelicBookData> sorted = GetSortedRelics();
                int totalPages = GetTotalPages(sorted.Count, relicsSlots.Count);
                _relicsPageIndex = Mathf.Clamp(_relicsPageIndex + delta, 0, Mathf.Max(0, totalPages - 1));
                RefreshRelicsPage();
                break;
            }
            default:
            {
                List<CardBookData> sorted = GetSortedCards();
                int totalPages = GetTotalPages(sorted.Count, cardsSlots.Count);
                _cardsPageIndex = Mathf.Clamp(_cardsPageIndex + delta, 0, Mathf.Max(0, totalPages - 1));
                RefreshCardsPage();
                break;
            }
        }
    }

    private void RefreshCardsPage()
    {
        RefreshPage(cardsSlots, GetSortedCards(), _cardsPageIndex, BookPage.Cards, ResolvePagination(BookPage.Cards));
    }

    private void RefreshMonstersPage()
    {
        RefreshPage(monstersSlots, GetSortedMonsters(), _monstersPageIndex, BookPage.Monsters, ResolvePagination(BookPage.Monsters));
    }

    private void RefreshRelicsPage()
    {
        RefreshPage(relicsSlots, GetSortedRelics(), _relicsPageIndex, BookPage.Relics, ResolvePagination(BookPage.Relics));
    }

    private void RefreshPage<T>(
        List<IllustratedBookSlotBinding> slots,
        List<T> sortedItems,
        int pageIndex,
        BookPage category,
        PaginationBindings pagination)
        where T : BookDataBase
    {
        if (slots == null || slots.Count == 0)
        {
            Debug.LogWarning("IllustratedBookPanelController: slots are not assigned for " + category + ".", this);
            UpdatePaginationUi(pagination, 0, 0, 0);
            return;
        }

        int itemsPerPage = slots.Count;
        int totalItems = sortedItems != null ? sortedItems.Count : 0;
        int totalPages = GetTotalPages(totalItems, itemsPerPage);
        int clampedPageIndex = Mathf.Clamp(pageIndex, 0, Mathf.Max(0, totalPages - 1));
        int startIndex = clampedPageIndex * itemsPerPage;

        for (int i = 0; i < slots.Count; i++)
        {
            IllustratedBookSlotBinding slot = slots[i];
            if (slot == null)
            {
                Debug.LogWarning("IllustratedBookPanelController: slot binding is null at index " + i + " for " + category + ".", this);
                continue;
            }

            slot.ClearClick();

            int dataIndex = startIndex + i;
            if (sortedItems != null && dataIndex >= 0 && dataIndex < sortedItems.Count)
            {
                T item = sortedItems[dataIndex];
                slot.SetFilled(item.Icon, item.DisplayName);

                string itemId = item.Id;
                slot.BindClick(() => OpenDetail(category, itemId));
            }
            else
            {
                slot.SetEmpty();
            }
        }

        UpdatePaginationUi(pagination, clampedPageIndex, totalPages, totalItems);

        switch (category)
        {
            case BookPage.Monsters:
                _monstersPageIndex = clampedPageIndex;
                break;
            case BookPage.Relics:
                _relicsPageIndex = clampedPageIndex;
                break;
            default:
                _cardsPageIndex = clampedPageIndex;
                break;
        }
    }

    private void UpdatePaginationUi(PaginationBindings pagination, int pageIndex, int totalPages, int totalItems)
    {
        if (pagination == null) return;

        bool hasPages = totalPages > 0;
        bool canPrev = hasPages && pageIndex > 0;
        bool canNext = hasPages && pageIndex < totalPages - 1;

        if (pagination.PrevButton != null) pagination.PrevButton.interactable = canPrev;
        if (pagination.NextButton != null) pagination.NextButton.interactable = canNext;

        if (pagination.PageText != null)
        {
            if (totalPages <= 0 || totalItems < 0)
            {
                pagination.PageText.SetText("0/0");
            }
            else
            {
                pagination.PageText.SetText((pageIndex + 1).ToString() + "/" + totalPages.ToString());
            }
        }
    }

    private void OpenDetail(BookPage category, string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("IllustratedBookPanelController: clicked item id is null/empty.", this);
            return;
        }

        HideAllDetailRoots();
        HideAllListPanels();

        switch (category)
        {
            case BookPage.Monsters:
            {
                MonsterBookData data = FindById(GetSortedMonsters(), id);
                if (data == null)
                {
                    Debug.LogWarning("IllustratedBookPanelController: monster id not found -> " + id, this);
                    BackToList();
                    return;
                }

                if (monsterDetail.Root != null) monsterDetail.Root.SetActive(true);
                else Debug.LogWarning("IllustratedBookPanelController: monsterDetailRoot is not assigned.", this);
                monsterDetail.Fill(data);
                _currentPage = BookPage.Monsters;
                break;
            }
            case BookPage.Relics:
            {
                RelicBookData data = FindById(GetSortedRelics(), id);
                if (data == null)
                {
                    Debug.LogWarning("IllustratedBookPanelController: relic id not found -> " + id, this);
                    BackToList();
                    return;
                }

                if (relicDetail.Root != null) relicDetail.Root.SetActive(true);
                else Debug.LogWarning("IllustratedBookPanelController: relicDetailRoot is not assigned.", this);
                relicDetail.Fill(data);
                _currentPage = BookPage.Relics;
                break;
            }
            default:
            {
                CardBookData data = FindById(GetSortedCards(), id);
                if (data == null)
                {
                    Debug.LogWarning("IllustratedBookPanelController: card id not found -> " + id, this);
                    BackToList();
                    return;
                }

                if (cardDetail.Root != null) cardDetail.Root.SetActive(true);
                else Debug.LogWarning("IllustratedBookPanelController: cardDetailRoot is not assigned.", this);
                cardDetail.Fill(data);
                _currentPage = BookPage.Cards;
                break;
            }
        }

        _isDetailOpen = true;
    }

    private void ShowOnlyListPanel(BookPage page)
    {
        HideAllListPanels();

        switch (page)
        {
            case BookPage.Monsters:
                if (monstersPanel != null) monstersPanel.SetActive(true);
                else Debug.LogWarning("IllustratedBookPanelController: monstersPanel is not assigned.", this);
                break;
            case BookPage.Relics:
                if (relicsPanel != null) relicsPanel.SetActive(true);
                else Debug.LogWarning("IllustratedBookPanelController: relicsPanel is not assigned.", this);
                break;
            default:
                if (cardsPanel != null) cardsPanel.SetActive(true);
                else Debug.LogWarning("IllustratedBookPanelController: cardsPanel is not assigned.", this);
                break;
        }
    }

    private void HideAllListPanels()
    {
        if (cardsPanel != null) cardsPanel.SetActive(false);
        if (monstersPanel != null) monstersPanel.SetActive(false);
        if (relicsPanel != null) relicsPanel.SetActive(false);
    }

    private void HideAllDetailRoots()
    {
        if (cardDetail != null && cardDetail.Root != null) cardDetail.Root.SetActive(false);
        if (monsterDetail != null && monsterDetail.Root != null) monsterDetail.Root.SetActive(false);
        if (relicDetail != null && relicDetail.Root != null) relicDetail.Root.SetActive(false);
    }

    private PaginationBindings ResolvePagination(BookPage page)
    {
        if (useSharedPaginationBindings && sharedPagination != null)
        {
            return sharedPagination;
        }

        switch (page)
        {
            case BookPage.Monsters: return monstersPagination;
            case BookPage.Relics: return relicsPagination;
            default: return cardsPagination;
        }
    }

    private List<CardBookData> GetSortedCards()
    {
        return SortEntries(cardsData);
    }

    private List<MonsterBookData> GetSortedMonsters()
    {
        return SortEntries(monstersData);
    }

    private List<RelicBookData> GetSortedRelics()
    {
        return SortEntries(relicsData);
    }

    private static List<T> SortEntries<T>(List<T> source) where T : BookDataBase
    {
        if (source == null) return new List<T>();

        return source
            .Where(x => x != null)
            .OrderByDescending(x => x.Pinned)
            .ThenBy(x => x.Order)
            .ThenBy(x => string.IsNullOrEmpty(x.DisplayName) ? x.Id : x.DisplayName, StringComparer.Ordinal)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static T FindById<T>(List<T> source, string id) where T : BookDataBase
    {
        if (source == null || string.IsNullOrEmpty(id)) return null;
        return source.FirstOrDefault(x => x != null && x.Id == id);
    }

    private static int GetTotalPages(int itemCount, int itemsPerPage)
    {
        if (itemsPerPage <= 0) return 0;
        return Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)itemsPerPage));
    }
}
