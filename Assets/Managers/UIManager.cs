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
    [SerializeField] private Button settingsButton;           // 打開 Settings
    [SerializeField] private Button ruleButton;               // 打開 Rule
    [SerializeField] private Button switchDeckDiscardButton;  // 切換 Counter

    [Header("Counters (顯示/切換用)")]
    [SerializeField] private Button deckCounterButton;        // 點擊打開 Deck Panel
    [SerializeField] private Button discardCounterButton;     // 點擊打開 Discard Panel

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

        // 確保一開始四個面板都在 Canvas 的最下方（會被畫在最上層）
        EnsurePanelsOnTopLayer();
        MovePanelsToCanvasRoot(); 
    }

    private void MovePanelsToCanvasRoot()
{
    var canvas = GetComponentInParent<Canvas>();
    if (canvas == null) return;

    Transform root = canvas.transform;
    if (rulePanel)     rulePanel.transform.SetParent(root, false);
    if (settingsPanel) settingsPanel.transform.SetParent(root, false);
    if (deckPanel)     deckPanel.transform.SetParent(root, false);
    if (discardPanel)  discardPanel.transform.SetParent(root, false);
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

    /// <summary>
    /// 把四個面板都推到 Canvas 子物件列表的最後面（畫在最上層）
    /// </summary>
    private void EnsurePanelsOnTopLayer()
    {
        if (rulePanel) rulePanel.transform.SetAsLastSibling();
        if (settingsPanel) settingsPanel.transform.SetAsLastSibling();
        if (deckPanel) deckPanel.transform.SetAsLastSibling();
        if (discardPanel) discardPanel.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 顯示指定面板：關閉其它面板 + 把它移到最上層
    /// </summary>
    private void ShowPanelOnTop(GameObject panel)
    {
        if (panel == null) return;

        // 1. 先關閉其它面板（避免重疊）
        if (panel != rulePanel && rulePanel) UIFxController.Instance?.HidePanel(rulePanel);
        if (panel != settingsPanel && settingsPanel) UIFxController.Instance?.HidePanel(settingsPanel);
        if (panel != deckPanel && deckPanel) UIFxController.Instance?.HidePanel(deckPanel);
        if (panel != discardPanel && discardPanel) UIFxController.Instance?.HidePanel(discardPanel);

        // 2. 把這個面板拉到兄弟節點的最後 → 在同一個 Canvas 裡會畫在最上面
        panel.transform.SetAsLastSibling();

        // 3. 顯示這個面板（滑入＋淡入）
        UIFxController.Instance?.ShowPanel(panel);
    }

    // ========================
    // Settings
    // ========================
    public void OpenSettingsPanel()
    {
        if (settingsPanel)
        {
            ShowPanelOnTop(settingsPanel);
        }
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
        if (rulePanel)
        {
            ShowPanelOnTop(rulePanel);
        }
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

        // 切換時關掉兩個 Panel（維持你原本的習慣）
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
        if (deckPanel)
        {
            ShowPanelOnTop(deckPanel);
        }
    }

    public void CloseDeckPanel()
    {
        if (deckPanel) UIFxController.Instance?.HidePanel(deckPanel);
    }

    public void OnDiscardCounterClicked()
    {
        if (discardPanel)
        {
            ShowPanelOnTop(discardPanel);
        }
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
