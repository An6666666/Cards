using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PileVisibilityToggle : MonoBehaviour
{
    private enum VisiblePile
    {
        Deck,
        Discard,
        AllDeck
    }

    [Header("Pile References")]
    [SerializeField] private GameObject deckPileObject;
    [SerializeField] private GameObject discardPileObject;
    [SerializeField] private GameObject allDeckPileObject;
    [SerializeField] private BattleManager battleManager;

    [Header("Button Label")]
    [SerializeField] private Text label;
    [SerializeField] private string showDeckText = "顯示牌庫";
    [SerializeField] private string showDiscardText = "顯示棄牌區";
    [SerializeField] private string showAllDeckText = "顯示總牌庫";

    private VisiblePile visiblePile = VisiblePile.Deck;
    private Button toggleButton;
    private bool addedRuntimeListener;
    private bool pilesAreNested;

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

        ResolveAutoReferences();
        pilesAreNested = ArePilesNested();
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
        visiblePile = GetNextVisiblePile();
        ApplyVisiblePile();
        UpdateLabel();
        UpdateCounters();
    }

    private void UpdateLabel()
    {
        if (label == null)
        {
            return;
        }

        switch (visiblePile)
        {
            case VisiblePile.Deck:
                label.text = showDiscardText;
                break;
            case VisiblePile.Discard:
                label.text = HasAllDeckPile() ? showAllDeckText : showDeckText;
                break;
            default:
                label.text = showDeckText;
                break;
        }
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

        UpdateCounterText(deckPileObject, $"{(battleManager.player.deck != null ? battleManager.player.deck.Count : 0)}");
        UpdateCounterText(discardPileObject, $"{(battleManager.player.discardPile != null ? battleManager.player.discardPile.Count : 0)}");
        UpdateCounterText(allDeckPileObject, $"{GetAllDeckCount()}");
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
        bool allDeckActive = IsPileActive(allDeckPileObject);

        if (deckActive && !discardActive && !allDeckActive)
        {
            visiblePile = VisiblePile.Deck;
            return;
        }

        if (!deckActive && discardActive && !allDeckActive)
        {
            visiblePile = VisiblePile.Discard;
            return;
        }

        if (!deckActive && !discardActive && allDeckActive)
        {
            visiblePile = VisiblePile.AllDeck;
            return;
        }

        visiblePile = VisiblePile.Deck;
        ApplyVisiblePile();
    }

    private bool ArePilesNested()
    {
        return AreNested(deckPileObject, discardPileObject)
            || AreNested(deckPileObject, allDeckPileObject)
            || AreNested(discardPileObject, allDeckPileObject);
    }

    private void ApplyVisibility(GameObject target, bool visible)
    {
        if (target == null) return;

        if (pilesAreNested)
        {
            target.SetActive(true);
            CanvasGroup cg = target.GetComponent<CanvasGroup>() ?? target.AddComponent<CanvasGroup>();
            cg.alpha = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
            cg.interactable = visible;
            return;
        }

        target.SetActive(visible);
    }

    private static bool IsPileActive(GameObject target)
    {
        if (target == null) return false;
        CanvasGroup cg = target.GetComponent<CanvasGroup>();
        if (cg != null) return target.activeSelf && cg.alpha > 0.001f;
        return target.activeSelf;
    }

    private void ApplyVisiblePile()
    {
        ApplyVisibility(deckPileObject, visiblePile == VisiblePile.Deck);
        ApplyVisibility(discardPileObject, visiblePile == VisiblePile.Discard);
        ApplyVisibility(allDeckPileObject, visiblePile == VisiblePile.AllDeck);
    }

    private VisiblePile GetNextVisiblePile()
    {
        switch (visiblePile)
        {
            case VisiblePile.Deck:
                return VisiblePile.Discard;
            case VisiblePile.Discard:
                return HasAllDeckPile() ? VisiblePile.AllDeck : VisiblePile.Deck;
            default:
                return VisiblePile.Deck;
        }
    }

    private bool HasAllDeckPile()
    {
        return allDeckPileObject != null;
    }

    private int GetAllDeckCount()
    {
        PlayerRunSnapshot snapshot = RunManager.Instance != null ? RunManager.Instance.CurrentRunSnapshot : null;
        if (snapshot != null && snapshot.deck != null)
        {
            return snapshot.deck.Count;
        }

        return battleManager.player.deck != null ? battleManager.player.deck.Count : 0;
    }

    private void ResolveAutoReferences()
    {
        if (deckPileObject == null) deckPileObject = FindSceneGameObject("Deck Counter");
        if (discardPileObject == null) discardPileObject = FindSceneGameObject("Discard Counter");
        if (allDeckPileObject == null) allDeckPileObject = FindSceneGameObject("AllDeck Counter");
    }

    private static bool AreNested(GameObject a, GameObject b)
    {
        if (a == null || b == null) return false;
        Transform at = a.transform;
        Transform bt = b.transform;
        return at.IsChildOf(bt) || bt.IsChildOf(at);
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
