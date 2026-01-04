using UnityEngine;
using UnityEngine.UI;

public class DeckPanelController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject deckPanel;
    [SerializeField] private GameObject discardPanel;

    [Header("Data Source")]
    [SerializeField] private DeckObserver deckObserver;

    [Header("Counter Buttons")]
    [SerializeField] private Button deckCounterButton;
    [SerializeField] private Button discardCounterButton;
    [SerializeField] private Button switchButton;

    private UIFxController _fx;
    private bool _showingDeck = true;

    public void Initialize(UIFxController fx)
    {
        _fx = fx;
        WireButtons();
        HideAll();
        UpdateCounters();
    }

    private void WireButtons()
    {
        if (deckCounterButton != null)
        {
            deckCounterButton.onClick.RemoveAllListeners();
            deckCounterButton.onClick.AddListener(OpenDeckPanel);
        }

        if (discardCounterButton != null)
        {
            discardCounterButton.onClick.RemoveAllListeners();
            discardCounterButton.onClick.AddListener(OpenDiscardPanel);
        }

        if (switchButton != null)
        {
            switchButton.onClick.RemoveAllListeners();
            switchButton.onClick.AddListener(SwitchPanel);
        }
    }

    public void HideAll()
    {
        if (deckPanel) _fx?.HidePanel(deckPanel);
        if (discardPanel) _fx?.HidePanel(discardPanel);
    }

    public void SwitchPanel()
    {
        if (deckCounterButton == null || discardCounterButton == null) return;

        _showingDeck = !_showingDeck;
        if (_showingDeck)
            _fx?.FadeSwapButtons(deckCounterButton, discardCounterButton);
        else
            _fx?.FadeSwapButtons(discardCounterButton, deckCounterButton);

        UpdateCounters();
        HideAll();
    }

    public void OpenDeckPanel()
    {
        deckObserver?.ForceRefresh();
        if (deckPanel) _fx?.ShowPanel(deckPanel);
    }

    public void OpenDiscardPanel()
    {
        deckObserver?.ForceRefresh();
        if (discardPanel) _fx?.ShowPanel(discardPanel);
    }

    public void CloseDeckPanel()
    {
        if (deckPanel) _fx?.HidePanel(deckPanel);
    }

    public void CloseDiscardPanel()
    {
        if (discardPanel) _fx?.HidePanel(discardPanel);
    }

    private void UpdateCounters()
    {
        if (deckCounterButton != null) deckCounterButton.gameObject.SetActive(_showingDeck);
        if (discardCounterButton != null) discardCounterButton.gameObject.SetActive(!_showingDeck);

        var deckGroup = deckCounterButton ? (deckCounterButton.GetComponent<CanvasGroup>() ?? deckCounterButton.gameObject.AddComponent<CanvasGroup>()) : null;
        var discardGroup = discardCounterButton ? (discardCounterButton.GetComponent<CanvasGroup>() ?? discardCounterButton.gameObject.AddComponent<CanvasGroup>()) : null;

        if (deckGroup != null)
        {
            deckGroup.alpha = _showingDeck ? 1f : 0f;
            deckGroup.blocksRaycasts = _showingDeck;
            deckGroup.interactable = _showingDeck;
        }

        if (discardGroup != null)
        {
            discardGroup.alpha = _showingDeck ? 0f : 1f;
            discardGroup.blocksRaycasts = !_showingDeck;
            discardGroup.interactable = !_showingDeck;
        }
    }
}
