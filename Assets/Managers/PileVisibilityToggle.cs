using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PileVisibilityToggle : MonoBehaviour
{
    [Header("Pile References")]
    [SerializeField] private GameObject deckPileObject;
    [SerializeField] private GameObject discardPileObject;
    [SerializeField] private BattleManager battleManager;

    [Header("Button Label")]
    [SerializeField] private Text label;
    [SerializeField] private string showDeckText = "顯示牌庫";
    [SerializeField] private string showDiscardText = "顯示棄牌區";

    private bool showingDeck = true;
    private Button toggleButton;
    private bool addedRuntimeListener;

    private void Awake()
    {
        toggleButton = GetComponent<Button>();

        if (label == null && toggleButton != null)
        {
            label = toggleButton.GetComponentInChildren<Text>();
        }

        if (toggleButton != null && toggleButton.onClick.GetPersistentEventCount() == 0)
        {
            toggleButton.onClick.AddListener(TogglePiles);
            addedRuntimeListener = true;
        }

        InitializeStartingPile();
        UpdateLabel();
        UpdateCounters();
    }

    private void OnDestroy()
    {
        if (addedRuntimeListener && toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(TogglePiles);
        }
    }

    public void TogglePiles()
    {
        showingDeck = !showingDeck;

        SetActive(deckPileObject, showingDeck);
        SetActive(discardPileObject, !showingDeck);

        UpdateLabel();

        UpdateCounters();
    }

    private void UpdateLabel()
    {
        if (label == null)
        {
            return;
        }

        label.text = showingDeck ? showDiscardText : showDeckText;
    }

    private void UpdateCounters()
    {
        if (battleManager == null)
        {
            battleManager = FindObjectOfType<BattleManager>();
        }

        if (battleManager == null || battleManager.player == null)
        {
            return;
        }

        if (showingDeck)
        {
            UpdateCounterText(deckPileObject, $"{battleManager.player.deck.Count}");
        }
        else
        {
            UpdateCounterText(discardPileObject, $"{battleManager.player.discardPile.Count}");
        }
    }

    private void UpdateCounterText(GameObject pileRoot, string value)
    {
        if (pileRoot == null)
        {
            return;
        }

        Text counter = pileRoot.GetComponentInChildren<Text>(true);
        if (counter != null)
        {
            counter.text = value;
        }
    }

    private void InitializeStartingPile()
    {
        bool deckActive = IsPileActive(deckPileObject);
        bool discardActive = IsPileActive(discardPileObject);

        if (deckActive && !discardActive)
        {
            showingDeck = true;
            return;
        }

        if (!deckActive && discardActive)
        {
            showingDeck = false;
            return;
        }

        showingDeck = true;
        SetActive(deckPileObject, true);
        SetActive(discardPileObject, false);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private static bool IsPileActive(GameObject target)
    {
        return target != null && target.activeInHierarchy;
    }
}