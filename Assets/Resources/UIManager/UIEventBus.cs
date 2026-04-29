using System;

/// <summary>
/// Simple event hub for UI modules. Keeps UI traffic inside a single channel
/// so views do not need to poll or search for data producers.
/// </summary>
public static class UIEventBus
{
    public static event Action<DeckSnapshot> DeckStateChanged;
    public static event Action<EnergySnapshot> EnergyStateChanged;

    public static void RaiseDeckState(DeckSnapshot snapshot) => DeckStateChanged?.Invoke(snapshot);
    public static void RaiseEnergyState(EnergySnapshot snapshot) => EnergyStateChanged?.Invoke(snapshot);
}

public readonly struct DeckSnapshot
{
    public readonly Player Player;
    public readonly int DeckCount;
    public readonly int DiscardCount;
    public readonly int HandCount;
    public readonly int ExhaustCount;
    public readonly int AllDeckCount;

    public readonly System.Collections.Generic.List<CardBase> Deck;
    public readonly System.Collections.Generic.List<CardBase> Discard;
    public readonly System.Collections.Generic.List<CardBase> Hand;
    public readonly System.Collections.Generic.List<CardBase> Exhaust;
    public readonly System.Collections.Generic.List<CardBase> AllDeck;

    public DeckSnapshot(Player player)
    {
        Player = player;
        Deck = player?.deck;
        Discard = player?.discardPile;
        Hand = player?.Hand;
        Exhaust = player?.exhaustPile;
        DeckCount = Deck?.Count ?? 0;
        DiscardCount = Discard?.Count ?? 0;
        HandCount = Hand?.Count ?? 0;
        ExhaustCount = Exhaust?.Count ?? 0;
        PlayerRunSnapshot runSnapshot = RunManager.Instance != null ? RunManager.Instance.CurrentRunSnapshot : null;
        if (runSnapshot != null)
        {
            AllDeck = runSnapshot.deck != null
                ? new System.Collections.Generic.List<CardBase>(runSnapshot.deck)
                : new System.Collections.Generic.List<CardBase>();
        }
        else
        {
            AllDeck = new System.Collections.Generic.List<CardBase>();
            AddCards(AllDeck, Deck);
        }

        AllDeckCount = AllDeck.Count;
    }

    private static void AddCards(System.Collections.Generic.List<CardBase> target, System.Collections.Generic.IEnumerable<CardBase> source)
    {
        if (target == null || source == null)
        {
            return;
        }

        foreach (CardBase card in source)
        {
            if (card != null)
            {
                target.Add(card);
            }
        }
    }
}

public readonly struct EnergySnapshot
{
    public readonly int Current;
    public readonly int Max;

    public EnergySnapshot(int current, int max)
    {
        Current = current;
        Max = max;
    }
}
