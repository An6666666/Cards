// Assets/Managers/UIManager.cs
// 使用 UIFxController 的「滑入 + 淡入」效果；完全移除按鈕彈跳呼叫
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
    private bool showingDeck = true;

    private void Awake()
    {
        // 開場先關閉（交給控制器顯示）
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

        WireUpButtons();
        UpdateCounterUI();
    }

    private void WireUpButtons()
    {
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OpenSettingsPanel); // 不再呼叫 PressBounce
        }

        if (ruleButton != null)
        {
            ruleButton.onClick.RemoveAllListeners();
            ruleButton.onClick.AddListener(OpenRulePanel); // 不再呼叫 PressBounce
        }

        if (switchDeckDiscardButton != null)
        {
            switchDeckDiscardButton.onClick.RemoveAllListeners();
            switchDeckDiscardButton.onClick.AddListener(SwitchDeckDiscard); // 不再呼叫 PressBounce
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
        if (settingsPanel) UIFxController.Instance?.ShowPanel(settingsPanel);

        // 打開設定時關閉其他
        if (rulePanel) UIFxController.Instance?.HidePanel(rulePanel);
        if (deckPanel) UIFxController.Instance?.HidePanel(deckPanel);
        if (discardPanel) UIFxController.Instance?.HidePanel(discardPanel);
    }

    public void CloseSettingsPanel()
    {
        if (settingsPanel) UIFxController.Instance?.HidePanel(settingsPanel);
    }

    // ========================
    // Rule
    // ========================
    public void OpenRulePanel()
    {
        if (rulePanel) UIFxController.Instance?.ShowPanel(rulePanel);
    }

    public void CloseRulePanel()
    {
        if (rulePanel) UIFxController.Instance?.HidePanel(rulePanel);
    }

    public void NextRulePage()
    {
        if (rulePages == null || rulePages.Length == 0 || ruleImage == null) return;
        int next = (currentRulePage + 1) % rulePages.Length;
        UIFxController.Instance?.CrossSlideRulePage(ruleImage, rulePages[next], toRight: true);
        currentRulePage = next;
    }

    public void PrevRulePage()
    {
        if (rulePages == null || rulePages.Length == 0 || ruleImage == null) return;
        int next = (currentRulePage - 1 + rulePages.Length) % rulePages.Length;
        UIFxController.Instance?.CrossSlideRulePage(ruleImage, rulePages[next], toRight: false);
        currentRulePage = next;
    }

    // ========================
    // Deck / Discard
    // ========================
    public void SwitchDeckDiscard()
    {
        if (deckCounterButton == null || discardCounterButton == null) return;

        showingDeck = !showingDeck;
        if (showingDeck)
            UIFxController.Instance?.FadeSwapButtons(deckCounterButton, discardCounterButton);
        else
            UIFxController.Instance?.FadeSwapButtons(discardCounterButton, deckCounterButton);

        // 切換時關掉兩個 Panel
        if (deckPanel) UIFxController.Instance?.HidePanel(deckPanel);
        if (discardPanel) UIFxController.Instance?.HidePanel(discardPanel);
    }

    private void UpdateCounterUI()
    {
        if (deckCounterButton != null) deckCounterButton.gameObject.SetActive(showingDeck);
        if (discardCounterButton != null) discardCounterButton.gameObject.SetActive(!showingDeck);

        var d1 = deckCounterButton ? (deckCounterButton.GetComponent<CanvasGroup>() ?? deckCounterButton.gameObject.AddComponent<CanvasGroup>()) : null;
        var d2 = discardCounterButton ? (discardCounterButton.GetComponent<CanvasGroup>() ?? discardCounterButton.gameObject.AddComponent<CanvasGroup>()) : null;
        if (d1 != null) { d1.alpha = showingDeck ? 1f : 0f; d1.blocksRaycasts = showingDeck; d1.interactable = showingDeck; }
        if (d2 != null) { d2.alpha = showingDeck ? 0f : 1f; d2.blocksRaycasts = !showingDeck; d2.interactable = !showingDeck; }
    }

    public void OnDeckCounterClicked()
    {
        if (deckPanel) UIFxController.Instance?.ShowPanel(deckPanel);
    }

    public void CloseDeckPanel()
    {
        if (deckPanel) UIFxController.Instance?.HidePanel(deckPanel);
    }

    public void OnDiscardCounterClicked()
    {
        if (discardPanel) UIFxController.Instance?.ShowPanel(discardPanel);
    }

    public void CloseDiscardPanel()
    {
        if (discardPanel) UIFxController.Instance?.HidePanel(discardPanel);
    }

    // ========================
    // 關閉全部
    // ========================
    public void CloseAllPanels()
    {
        if (rulePanel) UIFxController.Instance?.HidePanel(rulePanel);
        if (settingsPanel) UIFxController.Instance?.HidePanel(settingsPanel);
        if (deckPanel) UIFxController.Instance?.HidePanel(deckPanel);
        if (discardPanel) UIFxController.Instance?.HidePanel(discardPanel);
    }
}
