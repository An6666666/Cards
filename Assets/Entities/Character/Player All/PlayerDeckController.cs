using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerBuffController))]
public class PlayerDeckController : MonoBehaviour
{
    [Header("韏瑕??∠?閮剖?")]
    [SerializeField] private List<CardBase> startingDeck = new List<CardBase>();
    [SerializeField] private List<CardBase> debugStartingDeck = new List<CardBase>();
    [SerializeField] private bool useDebugDeck = false;
    [SerializeField] private StartingDeckDefinition startingDeckDefinition;


    private List<CardBase> deck = new List<CardBase>();
    private List<CardBase> hand = new List<CardBase>();
    private List<CardBase> discardPile = new List<CardBase>();
    private List<CardBase> exhaustPile = new List<CardBase>();

    private readonly Dictionary<CardBase, int> cardCostModifiers = new Dictionary<CardBase, int>();

    public int exhaustCountThisTurn = 0;

    private Player owner;
    private PlayerBuffController buffController;
    private BattleManager battleManager;
    private BattleRuntimeContext runtimeContext;

    public event Action HandChanged;
    public event Action DeckChanged;

    public List<CardBase> Deck
    {
        get => deck;
        set => deck = value ?? new List<CardBase>();
    }

    public List<CardBase> Hand => hand;
    public List<CardBase> DiscardPile => discardPile;
    public List<CardBase> ExhaustPile => exhaustPile;

    public void ConfigureBattleRuntime(BattleManager manager, BattleRuntimeContext context)
    {
        battleManager = manager;
        runtimeContext = context;
    }

    public void ClearBattleRuntime(BattleManager manager)
    {
        if (battleManager == manager)
        {
            battleManager = null;
            runtimeContext = null;
        }
    }

    public void Initialize(Player player, PlayerBuffController buffs)
    {
        owner = player;
        buffController = buffs;
        InitializeDeckFromStartingLists();
        ShuffleDeck();
    }
    
    private void InitializeDeckFromStartingLists()
    {
        List<CardBase> selectedDeck = ResolveStartingDeckFromSelection();

#if UNITY_EDITOR
        if (useDebugDeck && debugStartingDeck != null && debugStartingDeck.Count > 0)
        {
            selectedDeck = debugStartingDeck;
        }
#endif

        deck = selectedDeck != null ? new List<CardBase>(selectedDeck) : new List<CardBase>();
        hand.Clear();
        discardPile.Clear();
        exhaustPile.Clear();
        cardCostModifiers.Clear();
        NotifyDeckChanged();
        NotifyHandChanged();
    }
    
    private List<CardBase> ResolveStartingDeckFromSelection()
    {
        if (StartingDeckSelection.TryGetSelectedElements(out IReadOnlyList<ElementType> selectedElements))
        {
            StartingDeckDefinition definitionToUse = startingDeckDefinition;
            if (definitionToUse == null)
            {
                definitionToUse = Resources.Load<StartingDeckDefinition>("StartingDeckDefinition");
            }

            if (definitionToUse != null)
            {
                List<CardBase> built = definitionToUse.BuildDeck(selectedElements);
                if (built.Count > 0)
                {
                    StartingDeckSelection.ClearSelection();
                    return built;
                }
            }
            else
            {
                Debug.LogWarning("PlayerDeckController: StartingDeckDefinition is missing; using default starting deck.");
            }
        }

        return startingDeck;
    }
    
    public void StartTurn(int baseHandCardCount)
    {
        exhaustCountThisTurn = 0;
        int initialDrawCount = Mathf.Max(0, baseHandCardCount);
        DrawCards(initialDrawCount);
    }

    public void DrawCards(int n)
    {
        if (buffController != null && buffController.drawBlockedThisTurn)
        {
            Debug.Log("DrawCards skipped: drawing is blocked for the rest of this turn.");
            return;
        }

        for (int i = 0; i < n; i++)
        {
            if (deck.Count == 0)
            {
                ReshuffleDiscardIntoDeck();
                if (deck.Count == 0) break;
            }
            CardBase top = deck[0];
            deck.RemoveAt(0);
            hand.Add(top);
        }
        RefreshBattleHandUI(true);
        NotifyDeckChanged();
        NotifyHandChanged();
    }

    public void DrawNewHand(int count)
    {
        if (buffController != null && buffController.drawBlockedThisTurn)
        {
            Debug.Log("DrawNewHand skipped: drawing is blocked for the rest of this turn.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (deck.Count == 0)
            {
                if (discardPile.Count > 0)
                {
                    deck.AddRange(discardPile);
                    discardPile.Clear();
                    ShuffleDeck();
                }
                else
                {
                    break;
                }
            }

            if (deck.Count > 0)
            {
                CardBase drawn = deck[0];
                deck.RemoveAt(0);
                hand.Add(drawn);
            }
        }

        RefreshBattleHandUI(true);
        NotifyDeckChanged();
        NotifyHandChanged();
    }

    public void DiscardCards(int n, List<CardBase> relics)
    {
        if (hand.Count < n) n = hand.Count;
        BattleManager manager = GetBattleManager();
        int actualDiscarded = 0;
        for (int i = 0; i < n; i++)
        {
            if (TryRemoveDiscardableCardFromHand(manager, false, out CardBase c))
            {
                discardPile.Add(c);
                actualDiscarded++;
            }
            else
            {
                break;
            }
        }
        if (actualDiscarded > 0)
        {
            foreach (CardBase r in relics)
            {
                if (r is Relic_LunHuiZhuJian zhujian)
                {
                    zhujian.OnPlayerDiscard(owner, actualDiscarded);
                }
            }

            NotifyHandChanged();
            NotifyDeckChanged();
        }
    }

    public bool DiscardOneCard(List<CardBase> relics)
    {
        BattleManager manager = GetBattleManager();
        if (!TryRemoveDiscardableCardFromHand(manager, false, out CardBase c))
            return false;
        discardPile.Add(c);

        foreach (CardBase r in relics)
        {
            if (r is Relic_LunHuiZhuJian zhujian)
            {
                zhujian.OnPlayerDiscard(owner, 1);
            }
        }

        NotifyHandChanged();
        NotifyDeckChanged();
        return true;
    }

    public bool DiscardRandomCard(List<CardBase> relics, bool triggerRelic = true)
    {
        BattleManager manager = GetBattleManager();
        if (!TryRemoveDiscardableCardFromHand(manager, true, out CardBase c))
        {
            return false;
        }

        discardPile.Add(c);

        if (triggerRelic)
        {
            foreach (CardBase r in relics)
            {
                if (r is Relic_LunHuiZhuJian zhujian)
                {
                    zhujian.OnPlayerDiscard(owner, 1);
                }
            }
        }

        NotifyHandChanged();
        NotifyDeckChanged();
        return true;
    }

    public bool ExhaustRandomCardFromHand(CardBase excludedCard = null)
    {
        BattleManager manager = GetBattleManager();
        if (!TryRemoveDiscardableCardFromHand(manager, true, out CardBase removedCard, excludedCard))
        {
            return false;
        }

        ExhaustCard(removedCard);
        NotifyHandChanged();
        NotifyDeckChanged();
        return true;
    }

    public bool HasExhaustableCardInHand(CardBase excludedCard = null)
    {
        BattleManager manager = GetBattleManager();
        if (manager == null)
        {
            return hand.Exists(card => !ReferenceEquals(card, excludedCard));
        }

        foreach (CardBase card in hand)
        {
            if (!manager.IsGuaranteedMovementCard(card) && !ReferenceEquals(card, excludedCard))
            {
                return true;
            }
        }

        return false;
    }

    public void ExhaustCard(CardBase card)
    {
        if (card == null)
        {
            return;
        }

        if (!exhaustPile.Contains(card))
        {
            exhaustPile.Add(card);
        }

        exhaustCountThisTurn++;
        NotifyDeckChanged();
    }

    public bool CheckExhaustPlan()
    {
        return exhaustCountThisTurn > 0;
    }

    public int GetCardCostModifier(CardBase card)
    {
        if (card == null)
        {
            return 0;
        }

        return cardCostModifiers.TryGetValue(card, out int modifier) ? modifier : 0;
    }

    public void AddCardCostModifier(CardBase card, int modifier)
    {
        if (card == null || modifier == 0)
        {
            return;
        }

        if (cardCostModifiers.TryGetValue(card, out int current))
        {
            int updated = current + modifier;
            if (updated != 0)
            {
                cardCostModifiers[card] = updated;
            }
            else
            {
                cardCostModifiers.Remove(card);
            }
        }
        else
        {
            cardCostModifiers[card] = modifier;
        }
    }

    public void ClearCardCostModifier(CardBase card)
    {
        if (card == null)
        {
            return;
        }

        cardCostModifiers.Remove(card);
    }

    public void ReshuffleDiscardIntoDeck()
    {
        deck.AddRange(discardPile);
        discardPile.Clear();
        ShuffleDeck();
        NotifyDeckChanged();
    }

    public void ShuffleDeck()
    {
        System.Random rnd = new System.Random();
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int r = rnd.Next(0, i + 1);
            CardBase temp = deck[i];
            deck[i] = deck[r];
            deck[r] = temp;
        }

        NotifyDeckChanged();
    }

    private BattleManager GetBattleManager()
    {
        if (battleManager != null)
        {
            return battleManager;
        }

        if (runtimeContext != null && runtimeContext.Manager != null)
        {
            battleManager = runtimeContext.Manager;
            return battleManager;
        }

        BattleRuntimeContext active = BattleRuntimeContext.Active;
        if (active != null)
        {
            runtimeContext = active;
            battleManager = active.Manager;
        }

        return battleManager;
    }

    private void RefreshBattleHandUI(bool playDrawAnimation)
    {
        BattleManager manager = GetBattleManager();
        manager?.RefreshHandUI(playDrawAnimation);
    }

    private void NotifyHandChanged()
    {
        HandChanged?.Invoke();
    }

    private void NotifyDeckChanged()
    {
        DeckChanged?.Invoke();
    }

    private bool TryRemoveDiscardableCardFromHand(BattleManager manager, bool randomIndex, out CardBase removedCard, CardBase excludedCard = null)
    {
        removedCard = null;
        if (hand.Count == 0) return false;

        if (manager == null)
        {
            List<int> candidateIndexes = new List<int>();
            for (int i = 0; i < hand.Count; i++)
            {
                if (!ReferenceEquals(hand[i], excludedCard))
                {
                    candidateIndexes.Add(i);
                }
            }

            if (candidateIndexes.Count == 0) return false;

            int index = randomIndex ? candidateIndexes[UnityEngine.Random.Range(0, candidateIndexes.Count)] : candidateIndexes[candidateIndexes.Count - 1];
            removedCard = hand[index];
            hand.RemoveAt(index);
            ClearCardCostModifier(removedCard);
            return true;
        }

        if (randomIndex)
        {
            List<int> candidateIndexes = new List<int>();
            for (int i = 0; i < hand.Count; i++)
            {
                if (!manager.IsGuaranteedMovementCard(hand[i]) && !ReferenceEquals(hand[i], excludedCard))
                {
                    candidateIndexes.Add(i);
                }
            }

            if (candidateIndexes.Count == 0) return false;

            int selectedIndex = candidateIndexes[UnityEngine.Random.Range(0, candidateIndexes.Count)];
            removedCard = hand[selectedIndex];
            hand.RemoveAt(selectedIndex);
            ClearCardCostModifier(removedCard);
            return true;
        }

        for (int i = hand.Count - 1; i >= 0; i--)
        {
            CardBase candidate = hand[i];
            if (!manager.IsGuaranteedMovementCard(candidate) && !ReferenceEquals(candidate, excludedCard))
            {
                removedCard = candidate;
                hand.RemoveAt(i);
                ClearCardCostModifier(removedCard);
                return true;
            }
        }

        return false;
    }
}

