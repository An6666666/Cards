using System.Collections.Generic;

/// <summary>
/// Lightweight hub that fans deck/discard data out to all registered views and
/// replays the latest snapshot when a new view registers (e.g. panel opens).
/// </summary>
public static class DeckUIBus
{
    private static readonly List<DeckDiscardPanelView> s_views = new List<DeckDiscardPanelView>(8);
    private static List<CardBase> s_lastDeck;
    private static List<CardBase> s_lastDiscard;
    private static List<CardBase> s_lastAllDeck;
    private static DeckObserver s_provider;

    /// <summary>
    /// Registers the live data provider (DeckObserver) so we can request a
    /// fresh snapshot when a view wakes up.
    /// </summary>
    public static void SetProvider(DeckObserver provider)
    {
        s_provider = provider;
    }

    public static void Register(DeckDiscardPanelView v)
    {
        if (v != null && !s_views.Contains(v)) s_views.Add(v);

        // Immediately replay the most recent state when a new view shows up
        // (common when the deck/discard panel is lazily enabled on open).
        if (v != null)
        {
            if (s_lastDeck != null) v.RefreshDeck(s_lastDeck);
            if (s_lastDiscard != null) v.RefreshDiscard(s_lastDiscard);
            if (s_lastAllDeck != null) v.RefreshAllDeck(s_lastAllDeck);
        }

        // Immediately request a fresh snapshot so the view isn't stuck with a
        // stale cached state captured before the first draw.
        RequestSync();
    }

    public static void Unregister(DeckDiscardPanelView v)
    {
        if (v != null) s_views.Remove(v);
    }

    public static int ViewCount => s_views.Count;

    public static void RefreshAll(Player player)
    {
        var deck = player != null ? player.deck : null;
        var discard = player != null ? player.discardPile : null;
        var allDeck = BuildOwnedDeck(player);

        // cache latest so late-joining views can replay immediately
        s_lastDeck = deck;
        s_lastDiscard = discard;
        s_lastAllDeck = allDeck;

        if (s_views.Count == 0) return;

        for (int i = 0; i < s_views.Count; i++)
        {
            var v = s_views[i];
            if (v == null) continue;
            if (deck != null) v.RefreshDeck(deck);
            if (discard != null) v.RefreshDiscard(discard);
            v.RefreshAllDeck(allDeck);
        }
    }

    public static void RefreshAll(PlayerRunSnapshot snapshot)
    {
        List<CardBase> deck = snapshot != null && snapshot.deck != null
            ? snapshot.deck
            : new List<CardBase>();
        List<CardBase> discard = new List<CardBase>();
        List<CardBase> allDeck = new List<CardBase>(deck);

        s_lastDeck = deck;
        s_lastDiscard = discard;
        s_lastAllDeck = allDeck;

        if (s_views.Count == 0) return;

        for (int i = 0; i < s_views.Count; i++)
        {
            var v = s_views[i];
            if (v == null) continue;
            v.RefreshDeck(deck);
            v.RefreshDiscard(discard);
            v.RefreshAllDeck(allDeck);
        }
    }

    private static void RequestSync()
    {
        s_provider?.ForceRefresh();
    }

    private static List<CardBase> BuildOwnedDeck(Player player)
    {
        PlayerRunSnapshot snapshot = RunManager.Instance != null ? RunManager.Instance.CurrentRunSnapshot : null;
        if (snapshot != null && snapshot.deck != null)
        {
            return new List<CardBase>(snapshot.deck);
        }

        return player != null && player.deck != null
            ? new List<CardBase>(player.deck)
            : new List<CardBase>();
    }
}
