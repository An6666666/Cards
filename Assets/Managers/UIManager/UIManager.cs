// Assets/Managers/UIManager.cs
// 使用 UIFxController 的「滑入 + 淡入」效果；完全移除按鈕彈跳呼叫
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        EnsureNotDontDestroyOnLoad();
        AutoBindIfMissing();
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

    private void OnEnable()
    {
        EnsureNotDontDestroyOnLoad();
        AutoBindIfMissing();
        // 每次重新啟用（例如進入新關卡時）都重置顯示狀態，避免沿用上一關的互動與透明度
        showingDeck = true;
        if (deckPanel) UIFxController.Instance?.HidePanel(deckPanel);
        if (discardPanel) UIFxController.Instance?.HidePanel(discardPanel);
        UpdateCounterUI();
    }
    
    /// <summary>
    /// UIManager 不需要跨場景保留；若被放入 DontDestroyOnLoad 場景則搬回目前場景，
    /// 以避免在換關時沿用舊的 UI 物件。
    /// </summary>
    private void EnsureNotDontDestroyOnLoad()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (gameObject.scene.name == "DontDestroyOnLoad" && activeScene.IsValid())
        {
            SceneManager.MoveGameObjectToScene(gameObject, activeScene);
        }
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

    private void AutoBindIfMissing()
    {
        var rootObjects = gameObject.scene.IsValid() && gameObject.scene.isLoaded
            ? gameObject.scene.GetRootGameObjects()
            : null;

        Transform Search(string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return null;
            if (rootObjects != null)
            {
                foreach (var go in rootObjects)
                {
                    var found = FindChildContains(go.transform, keyword);
                    if (found != null) return found;
                }
            }
            return FindChildContains(transform, keyword);
        }

        Button FindButton(string keyword) => FindComponentInChildren<Button>(keyword);

        rulePanel ??= Search("Rule")?.gameObject;
        settingsPanel ??= Search("Setting")?.gameObject ?? Search("設定")?.gameObject;
        deckPanel ??= Search("Deck")?.gameObject ?? Search("牌庫")?.gameObject;
        discardPanel ??= Search("Discard")?.gameObject ?? Search("棄牌")?.gameObject;

        settingsButton ??= FindButton("Setting");
        ruleButton ??= FindButton("Rule");
        switchDeckDiscardButton ??= FindButton("Switch") ?? FindButton("切換");
        deckCounterButton ??= FindButton("Deck");
        discardCounterButton ??= FindButton("Discard") ?? FindButton("棄牌");

        ruleImage ??= FindComponentInChildren<Image>("Rule");
        ruleNextButton ??= FindButton("Next") ?? FindButton("下一頁");
        rulePrevButton ??= FindButton("Prev") ?? FindButton("上一頁");
        ruleCloseButton ??= FindButton("Close") ?? FindButton("關閉");
    }

    private T FindComponentInChildren<T>(string keyword) where T : Component
    {
        var comps = GetComponentsInChildren<T>(true);
        foreach (var c in comps)
        {
            if (string.IsNullOrEmpty(keyword) || c.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return c;
        }
        return null;
    }

    private Transform FindChildContains(Transform parent, string keyword)
    {
        if (parent == null || string.IsNullOrEmpty(keyword)) return null;
        if (parent.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindChildContains(parent.GetChild(i), keyword);
            if (found != null) return found;
        }
        return null;
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
        
        // 重新套用互動性與顯示狀態，避免在切換後的關卡中按鈕被隱藏/禁用而無法點擊
        UpdateCounterUI();

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
