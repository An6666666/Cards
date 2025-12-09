using UnityEngine;

public class EnergyObserver : MonoBehaviour
{
    [Header("References")]
    public Player player;

    private void OnEnable()
    {
        UIEventBus.EnergyStateChanged += OnEnergyChanged;
        if (player != null)
        {
            UIEventBus.RaiseEnergyState(new EnergySnapshot(player.energy, player.maxEnergy));
        }
    }

    private void OnDisable()
    {
        UIEventBus.EnergyStateChanged -= OnEnergyChanged;
    }

    private void OnEnergyChanged(EnergySnapshot snapshot)
    {
        EnergyUIBus.RefreshAll(snapshot.Current, snapshot.Max);
    }

    public void ForceRefresh()
    {
        if (player != null)
        {
            UIEventBus.RaiseEnergyState(new EnergySnapshot(player.energy, player.maxEnergy));
        }
    }

}
