using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central coordinator for UI modules. Keeps orchestration and wiring here while
/// delegating behavior to specialized controllers.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Controllers")]
    [SerializeField] private RulePanelController ruleController;
    [SerializeField] private SettingsPanelController settingsController;
    [SerializeField] private DeckPanelController deckController;

    [Header("Buttons")]
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private Button openRuleButton;

    private UIFxController _fx;

    private void Awake()
    {
        _fx = UIFxController.Instance ?? GetComponentInChildren<UIFxController>();
        InitializeControllers();
        WireButtons();
    }

    private void InitializeControllers()
    {
        if (ruleController != null) ruleController.Initialize(_fx);
        if (settingsController != null) settingsController.Initialize(_fx);
        if (deckController != null) deckController.Initialize(_fx);
    }

    private void WireButtons()
    {
        if (openSettingsButton != null)
        {
            openSettingsButton.onClick.RemoveAllListeners();
            openSettingsButton.onClick.AddListener(OpenSettingsPanel);
        }

        if (openRuleButton != null)
        {
            openRuleButton.onClick.RemoveAllListeners();
            openRuleButton.onClick.AddListener(OpenRulePanel);
        }
    }

    public void OpenSettingsPanel()
    {
        settingsController?.Open();
        ruleController?.Close();
        deckController?.HideAll();
    }

    public void CloseSettingsPanel() => settingsController?.Close();

    public void OpenRulePanel() => ruleController?.Open();
    public void CloseRulePanel() => ruleController?.Close();

    public void NextRulePage() => ruleController?.NextPage();
    public void PrevRulePage() => ruleController?.PrevPage();

    public void SwitchDeckDiscard() => deckController?.SwitchPanel();
    public void OnDeckCounterClicked() => deckController?.OpenDeckPanel();
    public void CloseDeckPanel() => deckController?.CloseDeckPanel();
    public void OnDiscardCounterClicked() => deckController?.OpenDiscardPanel();
    public void CloseDiscardPanel() => deckController?.CloseDiscardPanel();

    public void CloseAllPanels()
    {
        ruleController?.Close();
        settingsController?.Close();
        deckController?.HideAll();
    }
}
