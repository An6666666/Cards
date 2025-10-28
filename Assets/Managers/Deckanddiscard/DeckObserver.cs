// Assets/Managers/DeckObserver.cs
using UnityEngine;

public class DeckObserver : MonoBehaviour
{
    [Header("References")]
    public Player player;                 // 指到當前的 Player
    [Header("Polling")]
    [Tooltip("檢查間隔秒數，避免每幀刷")]
    public float checkInterval = 0.15f;

    private int lastDeckCount = -1;
    private int lastDiscardCount = -1;
    private int lastHandCount = -1;
    private float timer;

    private void Start()
    {
        // 初次同步
        TryAutoFindPlayer();
        ForceRefresh();
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < checkInterval) return;
        timer = 0f;

        if (player == null)
        {
            TryAutoFindPlayer();
            return;
        }

        int deckCount    = player.deck?.Count ?? 0;
        int discardCount = player.discardPile?.Count ?? 0;
        int handCount    = player.Hand?.Count ?? 0;

        // 只要任一數量變了（抽牌、丟棄、重洗）就刷新
        if (deckCount != lastDeckCount || discardCount != lastDiscardCount || handCount != lastHandCount)
        {
            lastDeckCount = deckCount;
            lastDiscardCount = discardCount;
            lastHandCount = handCount;
            DeckUIBus.RefreshAll(player);
        }
    }

    public void ForceRefresh()
    {
        if (player == null) TryAutoFindPlayer();
        lastDeckCount = lastDiscardCount = lastHandCount = -1; // 強制判定變化
        if (player != null) DeckUIBus.RefreshAll(player);
    }

    private void TryAutoFindPlayer()
    {
        player = player ?? FindObjectOfType<Player>();
        if (player != null)
        {
            // 立刻做一次同步
            lastDeckCount = lastDiscardCount = lastHandCount = -1;
            DeckUIBus.RefreshAll(player);
        }
    }
}
