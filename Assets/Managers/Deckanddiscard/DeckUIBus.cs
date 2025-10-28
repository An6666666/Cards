using System.Collections.Generic;

public static class DeckUIBus
{
    private static readonly List<DeckDiscardPanelView> s_views = new List<DeckDiscardPanelView>(8);

    public static void Register(DeckDiscardPanelView v)
    {
        if (v != null && !s_views.Contains(v)) s_views.Add(v);
    }

    public static void Unregister(DeckDiscardPanelView v)
    {
        if (v != null) s_views.Remove(v);
    }

    public static int ViewCount => s_views.Count;

    public static void RefreshAll(Player player)
    {
        if (s_views.Count == 0) return;
        var deck = player != null ? player.deck : null;
        var discard = player != null ? player.discardPile : null;

        for (int i = 0; i < s_views.Count; i++)
        {
            var v = s_views[i];
            if (v == null) continue;
            if (deck != null) v.RefreshDeck(deck);
            if (discard != null) v.RefreshDiscard(discard);
        }
    }
}
