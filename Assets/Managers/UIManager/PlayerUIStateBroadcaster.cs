using UnityEngine;

/// <summary>
/// Centralizes polling of the Player state and pushes snapshots through UIEventBus.
/// Attach once per scene. Keeps UI observers passive and avoids FindObjectOfType calls.
/// </summary>
public class PlayerUIStateBroadcaster : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;

    [Header("Polling")]
    [SerializeField, Tooltip("State check interval in seconds.")] private float checkInterval = 0.1f;

    private int _lastDeck = int.MinValue;
    private int _lastDiscard = int.MinValue;
    private int _lastHand = int.MinValue;
    private int _lastEnergy = int.MinValue;
    private int _lastMaxEnergy = int.MinValue;
    private float _timer;

    private void Awake()
    {
        if (player == null)
        {
            Debug.LogWarning("[PlayerUIStateBroadcaster] Player reference is missing. Assign it in the scene.");
        }
    }

    private void OnEnable()
    {
        PushAll();
    }

    private void Update()
    {
        if (player == null) return;

        _timer += Time.unscaledDeltaTime;
        if (_timer < checkInterval) return;
        _timer = 0f;

        PushDeckIfChanged();
        PushEnergyIfChanged();
    }

    public void SetPlayer(Player target)
    {
        player = target;
        ResetCache();
        PushAll();
    }

    public void PushAll()
    {
        if (player == null) return;
        ResetCache();
        PushDeckIfChanged();
        PushEnergyIfChanged();
    }

    private void PushDeckIfChanged()
    {
        if (player == null) return;
        var deck = player.deck?.Count ?? 0;
        var discard = player.discardPile?.Count ?? 0;
        var hand = player.Hand?.Count ?? 0;

        if (deck == _lastDeck && discard == _lastDiscard && hand == _lastHand) return;

        _lastDeck = deck;
        _lastDiscard = discard;
        _lastHand = hand;
        UIEventBus.RaiseDeckState(new DeckSnapshot(player));
    }

    private void PushEnergyIfChanged()
    {
        if (player == null) return;
        var cur = player.energy;
        var max = player.maxEnergy;
        if (cur == _lastEnergy && max == _lastMaxEnergy) return;

        _lastEnergy = cur;
        _lastMaxEnergy = max;
        UIEventBus.RaiseEnergyState(new EnergySnapshot(cur, max));
    }

    private void ResetCache()
    {
        _lastDeck = _lastDiscard = _lastHand = int.MinValue;
        _lastEnergy = _lastMaxEnergy = int.MinValue;
    }
}
