using UnityEngine;
using UnityEngine.UI;

public class DeckPanelController : MonoBehaviour
{
    private const string DeckPanelName = "Deck Panel";
    private const string DiscardPanelName = "Discard Panel";
    private const string AllDeckPanelName = "AllDeck Panel";

    private static readonly string[] DeckCounterButtonNames =
    {
        "Deck Counter",
        "Deck CounterButton"
    };

    private static readonly string[] DiscardCounterButtonNames =
    {
        "Discard Counter",
        "Discard CounterButton"
    };

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

    [Header("Close Buttons")]
    [SerializeField] private Button allDeckCloseButton;

    private UIFxController _fx;
    private bool _showingDeck = true;
    private bool _allDeckCounterButtonWired;
    private bool _allDeckCloseButtonWired;
    private bool _initialized;
    private float _autoWireTimer;

    public void Initialize(UIFxController fx)
    {
        _fx = fx;
        _initialized = true;
        WireButtons();
        HideAll();
        UpdateCounters();
    }

    private void WireButtons()
    {
        ResolveAutoReferences();

        if (deckCounterButton != null)
        {
            deckCounterButton.onClick.RemoveListener(OpenDeckPanel);
            deckCounterButton.onClick.AddListener(OpenDeckPanel);
        }

        if (discardCounterButton != null)
        {
            discardCounterButton.onClick.RemoveListener(OpenDiscardPanel);
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

        if (allDeckCloseButton != null)
        {
            WireAllDeckCloseButton();
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
        if (!_initialized)
        {
            return;
        }

        _autoWireTimer += Time.unscaledDeltaTime;
        if (_autoWireTimer < 0.5f)
        {
            return;
        }

        _autoWireTimer = 0f;
        if (NeedsAutoWire())
        {
            ResolveAutoReferences();
            WireButtons();
            WireAllDeckCounterButton();
            WireAllDeckCloseButton();
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
        // Counter visibility is owned by PileVisibilityToggle. Keeping this method
        // as a no-op prevents initialization order differences between Editor and
        // Player builds from hiding Discard Counter permanently.
    }

    private void ResolveAutoReferences()
    {
        if (deckPanel == null) deckPanel = FindSceneGameObject(DeckPanelName);
        if (discardPanel == null) discardPanel = FindSceneGameObject(DiscardPanelName);
        if (allDeckPanel == null) allDeckPanel = FindSceneGameObject(AllDeckPanelName);
        if (allDeckCloseButton == null) allDeckCloseButton = FindDirectChildButton(allDeckPanel);

        if (deckCounterButton == null)
        {
            deckCounterButton = FindButtonByNames(DeckCounterButtonNames);
        }

        if (discardCounterButton == null)
        {
            discardCounterButton = FindButtonByNames(DiscardCounterButtonNames);
        }

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

        allDeckCounterButton.onClick.RemoveAllListeners();
        allDeckCounterButton.onClick.AddListener(OpenAllDeckPanel);
        _allDeckCounterButtonWired = true;
    }

    private bool NeedsAutoWire()
    {
        return deckPanel == null
            || discardPanel == null
            || allDeckPanel == null
            || deckCounterButton == null
            || discardCounterButton == null
            || allDeckCounterButton == null
            || allDeckCloseButton == null
            || !_allDeckCounterButtonWired
            || !_allDeckCloseButtonWired;
    }

    private void WireAllDeckCloseButton()
    {
        if (allDeckCloseButton == null)
        {
            return;
        }

        allDeckCloseButton.onClick.RemoveListener(CloseAllDeckPanel);
        allDeckCloseButton.onClick.AddListener(CloseAllDeckPanel);
        _allDeckCloseButtonWired = true;
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

    private static Button FindDirectChildButton(GameObject parent)
    {
        if (parent == null)
        {
            return null;
        }

        Transform parentTransform = parent.transform;
        for (int i = 0; i < parentTransform.childCount; i++)
        {
            Button button = parentTransform.GetChild(i).GetComponent<Button>();
            if (button != null)
            {
                return button;
            }
        }

        return null;
    }

    private static GameObject FindSceneGameObject(string objectName)
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t != null && t.name == objectName)
            {
                return t.gameObject;
            }
        }

        return null;
    }
}
