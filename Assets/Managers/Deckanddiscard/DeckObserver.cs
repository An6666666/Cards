// Assets/Managers/DeckObserver.cs
using UnityEngine;

public class DeckObserver : MonoBehaviour
{
    [Header("References")]
    public Player player;                 // 指到當前的 Player
    private void OnEnable()
    {
        DeckUIBus.SetProvider(this);
        UIEventBus.DeckStateChanged += OnDeckChanged;
        if (player != null)
        {
            UIEventBus.RaiseDeckState(new DeckSnapshot(player));
        }
    }

    private void OnDisable()
    {
        UIEventBus.DeckStateChanged -= OnDeckChanged;
        DeckUIBus.SetProvider(null);
    }

    private void OnDeckChanged(DeckSnapshot snapshot)
    {
        if (snapshot.Player != null && player != null && snapshot.Player != player) return;
        if (snapshot.Player == null && player != null) return;
        DeckUIBus.RefreshAll(snapshot.Player ?? player);
    }

    public void ForceRefresh()
    {
        if (player != null)
        {
            UIEventBus.RaiseDeckState(new DeckSnapshot(player));
        }
    }
}
