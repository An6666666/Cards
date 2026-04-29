using UnityEngine;
using UnityEngine.UI;

public class DeckPanelController : MonoBehaviour
{
    private static readonly string[] AllDeckCounterButtonNames =
    {
        "AllDeck Counter",
        "AllDeck CounterButton"
    };

    [Header("Panels")]
    [SerializeField] private GameObject deckPanel;
    [SerializeField] private GameObject discardPanel;
    [SerializeField] private GameObject allDeckPanel;

    [Header("Data Source")]
    [SerializeField] private DeckObserver deckObserver;

    [Header("Counter Buttons")]
    [SerializeField] private Button deckCounterButton;
    [SerializeField] private Button discardCounterButton;
    [SerializeField] private Button allDeckCounterButton;
    [SerializeField] private Button switchButton;

    private UIFxController _fx;
    private bool _showingDeck = true;
    private bool _allDeckCounterButtonWired;
    private float _autoWireTimer;

    public void Initialize(UIFxController fx)
    {
        _fx = fx;
        WireButtons();
        HideAll();
        UpdateCounters();
    }

    private void WireButtons()
    {
        ResolveAutoReferences();

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

        if (allDeckCounterButton != null)
        {
            WireAllDeckCounterButton();
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
        if (allDeckPanel) _fx?.HidePanel(allDeckPanel);
    }

    private void Update()
    {
        _autoWireTimer += Time.unscaledDeltaTime;
        if (_autoWireTimer < 0.5f)
        {
            return;
        }

        _autoWireTimer = 0f;
        if (allDeckCounterButton == null || !_allDeckCounterButtonWired)
        {
            ResolveAutoReferences();
            WireAllDeckCounterButton();
        }
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
        if (allDeckPanel) _fx?.HidePanel(allDeckPanel);
        if (discardPanel) _fx?.HidePanel(discardPanel);
        if (deckPanel) _fx?.ShowPanel(deckPanel);
    }

    public void OpenDiscardPanel()
    {
        deckObserver?.ForceRefresh();
        if (allDeckPanel) _fx?.HidePanel(allDeckPanel);
        if (deckPanel) _fx?.HidePanel(deckPanel);
        if (discardPanel) _fx?.ShowPanel(discardPanel);
    }

    public void OpenAllDeckPanel()
    {
        RefreshAllDeckData();
        if (deckPanel) _fx?.HidePanel(deckPanel);
        if (discardPanel) _fx?.HidePanel(discardPanel);
        if (allDeckPanel) _fx?.ShowPanel(allDeckPanel);
    }

    public void CloseDeckPanel()
    {
        if (deckPanel) _fx?.HidePanel(deckPanel);
    }

    public void CloseDiscardPanel()
    {
        if (discardPanel) _fx?.HidePanel(discardPanel);
    }

    public void CloseAllDeckPanel()
    {
        if (allDeckPanel) _fx?.HidePanel(allDeckPanel);
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

    private void ResolveAutoReferences()
    {
        if (allDeckCounterButton == null)
        {
            allDeckCounterButton = FindButtonByNames(AllDeckCounterButtonNames);
            _allDeckCounterButtonWired = false;
        }
    }

    private void RefreshAllDeckData()
    {
        if (RunManager.Instance != null && RunManager.Instance.CurrentRunSnapshot != null)
        {
            DeckUIBus.RefreshAll(RunManager.Instance.CurrentRunSnapshot);
            return;
        }

        deckObserver?.ForceRefresh();
    }

    private void WireAllDeckCounterButton()
    {
        if (allDeckCounterButton == null)
        {
            return;
        }

        allDeckCounterButton.onClick.RemoveListener(OpenAllDeckPanel);
        allDeckCounterButton.onClick.AddListener(OpenAllDeckPanel);
        _allDeckCounterButtonWired = true;
    }

    private static Button FindButtonByNames(string[] names)
    {
        if (names == null || names.Length == 0)
        {
            return null;
        }

        Button[] buttons = FindObjectsOfType<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            for (int j = 0; j < names.Length; j++)
            {
                if (button.name == names[j])
                {
                    return button;
                }
            }
        }

        return null;
    }
}
