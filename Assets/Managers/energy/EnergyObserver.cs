using UnityEngine;

public class EnergyObserver : MonoBehaviour
{
    [Header("References")]
    public Player player;

    [Header("Polling")]
    public float checkInterval = 0.1f;

    private int lastCurrent = int.MinValue;
    private int lastMax = int.MinValue;
    private float timer;

    private void Start()
    {
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

        int cur = player.energy;
        int max = player.maxEnergy;

        if (cur != lastCurrent || max != lastMax)
        {
            lastCurrent = cur;
            lastMax = max;
            EnergyUIBus.RefreshAll(cur, max);
        }
    }

    public void ForceRefresh()
    {
        if (player == null) TryAutoFindPlayer();
        if (player != null)
        {
            lastCurrent = int.MinValue;
            lastMax = int.MinValue;
            EnergyUIBus.RefreshAll(player.energy, player.maxEnergy);
        }
    }

    private void TryAutoFindPlayer()
    {
        player = player ?? FindObjectOfType<Player>();
    }
}
