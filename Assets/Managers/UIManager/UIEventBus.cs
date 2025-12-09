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

    public readonly System.Collections.Generic.List<CardBase> Deck;
    public readonly System.Collections.Generic.List<CardBase> Discard;

    public DeckSnapshot(Player player)
    {
        Player = player;
        Deck = player?.deck;
        Discard = player?.discardPile;
        DeckCount = Deck?.Count ?? 0;
        DiscardCount = Discard?.Count ?? 0;
        HandCount = player?.Hand?.Count ?? 0;
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