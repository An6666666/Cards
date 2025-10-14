using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject rulePanel;
    [SerializeField] private GameObject settingsPanel;  
    [SerializeField] private GameObject deckPanel;
    [SerializeField] private GameObject discardPanel;

    [Header("Buttons")]
    [SerializeField] private Button settingsButton;   // 打開 Settings
    [SerializeField] private Button ruleButton;       // 打開 Rule
    [SerializeField] private Button switchDeckDiscardButton; // 切換 Counter

    [Header("Counters (顯示/切換用)")]
    [SerializeField] private Button deckCounterButton;    // 點擊打開 Deck Panel
    [SerializeField] private Button discardCounterButton; // 點擊打開 Discard Panel

    [Header("Rule Page")]
    [SerializeField] private Image ruleImage;
    [SerializeField] private Sprite[] rulePages;
    [SerializeField] private Button ruleNextButton;
    [SerializeField] private Button rulePrevButton;
    [SerializeField] private Button ruleCloseButton;

    private int currentRulePage = 0;
    private bool showingDeck = true;  // 紀錄當前顯示的 Counter

    private void Awake()
    {
        // 一開始關閉所有面板
        if (rulePanel) rulePanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (deckPanel) deckPanel.SetActive(false);
        if (discardPanel) discardPanel.SetActive(false);

        // 初始化 Rule
        if (ruleImage != null && rulePages != null && rulePages.Length > 0)
        {
            currentRulePage = 0;
            ruleImage.sprite = rulePages[currentRulePage];
        }

        // 綁定按鈕事件
        WireUpButtons();

        // 預設顯示 Deck Counter
        UpdateCounterUI();
    }

    private void WireUpButtons()
    {
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OpenSettingsPanel);
        }

        if (ruleButton != null)
        {
            ruleButton.onClick.RemoveAllListeners();
            ruleButton.onClick.AddListener(OpenRulePanel);
        }

        if (switchDeckDiscardButton != null)
        {
            switchDeckDiscardButton.onClick.RemoveAllListeners();
            switchDeckDiscardButton.onClick.AddListener(SwitchDeckDiscard);
        }

        if (deckCounterButton != null)
        {
            deckCounterButton.onClick.RemoveAllListeners();
            deckCounterButton.onClick.AddListener(OnDeckCounterClicked);
        }

        if (discardCounterButton != null)
        {
            discardCounterButton.onClick.RemoveAllListeners();
            discardCounterButton.onClick.AddListener(OnDiscardCounterClicked);
        }

        if (ruleNextButton != null)
        {
            ruleNextButton.onClick.RemoveAllListeners();
            ruleNextButton.onClick.AddListener(NextRulePage);
        }

        if (rulePrevButton != null)
        {
            rulePrevButton.onClick.RemoveAllListeners();
            rulePrevButton.onClick.AddListener(PrevRulePage);
        }

        if (ruleCloseButton != null)
        {
            ruleCloseButton.onClick.RemoveAllListeners();
            ruleCloseButton.onClick.AddListener(CloseRulePanel);
        }
    }

    // ========================
    // Settings
    // ========================
    public void OpenSettingsPanel()
    {
        if (settingsPanel) settingsPanel.SetActive(true);

        // 打開設定時關閉其他
        if (rulePanel) rulePanel.SetActive(false);
        if (deckPanel) deckPanel.SetActive(false);
        if (discardPanel) discardPanel.SetActive(false);
    }

    public void CloseSettingsPanel()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
    }

    // ========================
    // Rule
    // ========================
    public void OpenRulePanel()
    {
        if (rulePanel) rulePanel.SetActive(true);
    }

    public void CloseRulePanel()
    {
        if (rulePanel) rulePanel.SetActive(false);
    }

    public void NextRulePage()
    {
        if (rulePages == null || rulePages.Length == 0) return;
        currentRulePage = (currentRulePage + 1) % rulePages.Length;
        if (ruleImage) ruleImage.sprite = rulePages[currentRulePage];
    }

    public void PrevRulePage()
    {
        if (rulePages == null || rulePages.Length == 0) return;
        currentRulePage = (currentRulePage - 1 + rulePages.Length) % rulePages.Length;
        if (ruleImage) ruleImage.sprite = rulePages[currentRulePage];
    }

    // ========================
    // Deck / Discard
    // ========================
    public void SwitchDeckDiscard()
{
    if (deckCounterButton == null || discardCounterButton == null) return;

    // 狀態反轉
    showingDeck = !showingDeck;

    // 切換顯示
    deckCounterButton.gameObject.SetActive(showingDeck);
    discardCounterButton.gameObject.SetActive(!showingDeck);

    // 切換時順便關掉兩個 Panel，避免殘留
    if (deckPanel) deckPanel.SetActive(false);
    if (discardPanel) discardPanel.SetActive(false);
}


    private void UpdateCounterUI()
    {
        if (deckCounterButton != null)
            deckCounterButton.gameObject.SetActive(showingDeck);

        if (discardCounterButton != null)
            discardCounterButton.gameObject.SetActive(!showingDeck);
    }

    public void OnDeckCounterClicked()
    {
        if (deckPanel) deckPanel.SetActive(true);
    }

    public void CloseDeckPanel()
    {
        if (deckPanel) deckPanel.SetActive(false);
    }

    public void OnDiscardCounterClicked()
    {
        if (discardPanel) discardPanel.SetActive(true);
    }

    public void CloseDiscardPanel()
    {
        if (discardPanel) discardPanel.SetActive(false);
    }

    // ========================
    // 關閉全部
    // ========================
    public void CloseAllPanels()
    {
        if (rulePanel) rulePanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (deckPanel) deckPanel.SetActive(false);
        if (discardPanel) discardPanel.SetActive(false);
    }
}
