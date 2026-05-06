using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class ShopUIManager : MonoBehaviour
{
    private enum PendingShopConfirmation
    {
        None,
        PurchaseCard,
        PurchaseRelic,
        RemoveCard,
        ExitShop
    }

    [Header("Fallback Data")]
    [SerializeField] private ShopInventoryDefinition fallbackInventory;

    [Header("UI References")]
    [SerializeField] private Text shopTitleText;
    [SerializeField] private Text goldText;
    [SerializeField] private Text messageText;
    [SerializeField] private Text removalCostText;

    [Header("Shop NPC")]
    [SerializeField] private ShopNpcDialogueController shopNpcController;

    [SerializeField] private GameObject cardOfferTemplate;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private GameObject relicOfferTemplate;
    [SerializeField] private GameObject relicIconPrefab;
    [SerializeField] private Transform cardListParent;
    [SerializeField] private Transform relicListParent;
    [SerializeField] private Transform removalListParent;

    [SerializeField] private GameObject removalEntryTemplate;
    [SerializeField] private Button refreshRemovalButton;
    [SerializeField] private Button returnButton;

    [Header("Confirm Panel")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private Text confirmTitleText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private string purchaseConfirmTitle = "\u78BA\u5B9A\u8CFC\u8CB7\uFF1F";
    [SerializeField] private string removeConfirmTitle = "\u78BA\u5B9A\u79FB\u9664\uFF1F";
    [SerializeField] private string exitConfirmTitle = "\u78BA\u5B9A\u96E2\u958B\u5546\u5E97\uFF1F";

    private RunManager runManager;
    private Player player;
    private ShopInventoryDefinition inventory;

    private readonly List<CardBase> availableCards = new();
    private readonly List<RelicBase> availableRelics = new();
    private bool offersGenerated;
    private PendingShopConfirmation pendingConfirmation = PendingShopConfirmation.None;
    private CardBase pendingCard;
    private RelicBase pendingRelic;
    private int pendingCardIndex = -1;
    private int pendingPrice;

    private const int BaseCardPrice = 50;
    private const int BaseRelicPrice = 120;

    [Header("Tabs (Optional Buttons)")]
    [SerializeField] private Button btnCards;
    [SerializeField] private Button btnRelics;
    [SerializeField] private Button btnRemoval;

    [Header("Tab Button Images")]
    [SerializeField] private Image btnCardsImage;
    [SerializeField] private Image btnRelicsImage;
    [SerializeField] private Image btnRemovalImage;

    [Header("Tab Button Sprites")]
    [SerializeField] private Sprite btnCardsNormalSprite;
    [SerializeField] private Sprite btnCardsSelectedSprite;
    [SerializeField] private Sprite btnRelicsNormalSprite;
    [SerializeField] private Sprite btnRelicsSelectedSprite;
    [SerializeField] private Sprite btnRemovalNormalSprite;
    [SerializeField] private Sprite btnRemovalSelectedSprite;

    [Header("Lamps (Optional)")]
    [SerializeField] private Image lampCards;
    [SerializeField] private Image lampRelics;
    [SerializeField] private Image lampRemoval;

    [SerializeField] private Color lampNormalColor = Color.white;
    [SerializeField] private Color lampSelectedColor = Color.white;

    [Header("Paging")]
    [SerializeField] private int cardsPerPage = 4;
    [SerializeField] private int relicsPerPage = 4;
    [SerializeField] private int removalPerPage = 6;

    [SerializeField] private Button btnPrev;
    [SerializeField] private Button btnNext;
    [SerializeField] private TMP_Text pageText;

    private int pageCards;
    private int pageRelics;
    private int pageRemoval;

    public GameObject RelicIconPrefab => relicIconPrefab;

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

    private readonly struct PageWindow
    {
        public PageWindow(int pageIndex, int pageCount, int startIndex, int endIndex)
        {
            PageIndex = pageIndex;
            PageCount = pageCount;
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public int PageIndex { get; }
        public int PageCount { get; }
        public int StartIndex { get; }
        public int EndIndex { get; }
    }

    private void Awake()
    {
        runManager = RunManager.Instance;

        CacheSceneReferences();
        ResolveConfirmPanelReferences();
        BindConfirmPanelButtons();
        HideConfirmPanel();
        BindButtons();
        BindTabButtons();
        HideTemplates();
        ResolveShopNpcController();
    }

    private void Start()
    {
        InitializePlayer();
        LoadInventory();
        UpdateShopTitle();
        RefreshGoldDisplay();
        if (ShouldPlayDefaultShopEntryDialogue())
            shopNpcController?.NotifyShopEntered();

        LoadOrGenerateOffers();
        SetTab(ShopTab.Cards);
    }

    private void InitializePlayer()
    {
        player = FindObjectOfType<Player>();
        if (player == null)
        {
            var playerObject = new GameObject("Player");
            player = playerObject.AddComponent<Player>();
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

    private PageWindow GetPageWindow(int totalCount, int itemsPerPage, int currentPage)
    {
        int perPage = Mathf.Max(1, itemsPerPage);
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(totalCount / (float)perPage));
        int clampedPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
        int startIndex = clampedPage * perPage;
        int endIndex = Mathf.Min(startIndex + perPage, totalCount);
        return new PageWindow(clampedPage, pageCount, startIndex, endIndex);
    }

    private void RefreshGoldDisplay()
    {
        if (goldText != null && player != null)
            goldText.text = $"{player.gold}";
    }

    private bool ShouldPlayDefaultShopEntryDialogue()
    {
        if (runManager == null)
            return true;

        return !runManager.ConsumeDefaultShopEntryDialogueSuppression();
    }

    private void ResolveShopNpcController()
    {
        if (shopNpcController == null)
            shopNpcController = GetComponent<ShopNpcDialogueController>();

        if (shopNpcController == null)
            shopNpcController = FindObjectOfType<ShopNpcDialogueController>(true);

        if (shopNpcController == null)
            shopNpcController = gameObject.AddComponent<ShopNpcDialogueController>();

        if (shopNpcController != null)
            shopNpcController.SetUiReferences(messageText);
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
            SceneTransitionLoader.LoadScene("RunScene");
        }
    }

    private void SyncRunState()
    {
        if (runManager != null)
            runManager.SyncPlayerRunState();
    }

    private void LoadOrGenerateOffers()
    {
        if (TryLoadOffersFromActiveNode())
        {
            offersGenerated = true;
            return;
        }

        GenerateOffersFromInventory();
    }

    private bool TryLoadOffersFromActiveNode()
    {
        MapNodeData activeNode = runManager?.ActiveNode;
        if (activeNode == null || !activeNode.ShopOffersGenerated)
        {
            return false;
        }

        availableCards.Clear();
        availableRelics.Clear();
        availableCards.AddRange(activeNode.ShopCardOffers);
        availableRelics.AddRange(activeNode.ShopRelicOffers);
        return true;
    }

    private void SyncOfferStateToActiveNode()
    {
        MapNodeData activeNode = runManager?.ActiveNode;
        if (activeNode == null)
        {
            return;
        }

        activeNode.SetShopOfferState(offersGenerated, availableCards, availableRelics);
    }

    private int GetCardPrice(CardBase card)
    {
        if (card == null)
            return BaseCardPrice;

        if (card.shopPrice > 0)
            return card.shopPrice;

        return Mathf.Max(BaseCardPrice, card.cost * 25);
    }

    private int GetRelicPrice(RelicBase relic)
    {
        if (relic == null)
            return BaseRelicPrice;

        if (relic.shopPrice > 0)
            return relic.shopPrice;

        return BaseRelicPrice;
    }

    private void HideTemplates()
    {
        if (removalEntryTemplate != null)
            removalEntryTemplate.SetActive(false);

        if (cardOfferTemplate != null)
            cardOfferTemplate.SetActive(false);

        if (relicOfferTemplate != null)
            relicOfferTemplate.SetActive(false);
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
