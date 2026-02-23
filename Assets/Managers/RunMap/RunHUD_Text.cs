using UnityEngine;
using UnityEngine.UI;

public class RunHUD_Text : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text hpText;
    [SerializeField] private Text goldText;

    [Header("References")]
    [SerializeField] private RunManager runManager;
    [SerializeField] private Player player;

    [Header("Options")]
    [SerializeField] private bool showHpAsCurrentSlashMax = true;

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveReferences()
    {
        if (runManager == null)
        {
            runManager = RunManager.Instance;
        }

        if (player == null && runManager != null)
        {
            player = runManager.RegisteredPlayer;
        }
    }

    private void Subscribe()
    {
        if (runManager == null)
        {
            return;
        }

        runManager.RunSnapshotChanged -= HandleRunSnapshotChanged;
        runManager.RunSnapshotChanged += HandleRunSnapshotChanged;
        runManager.MapStateChanged -= HandleMapStateChanged;
        runManager.MapStateChanged += HandleMapStateChanged;
    }

    private void Unsubscribe()
    {
        if (runManager == null)
        {
            return;
        }

        runManager.RunSnapshotChanged -= HandleRunSnapshotChanged;
        runManager.MapStateChanged -= HandleMapStateChanged;
    }

    private void HandleRunSnapshotChanged(PlayerRunSnapshot snapshot)
    {
        RefreshFromSnapshot(snapshot);
    }

    private void HandleMapStateChanged()
    {
        ResolveReferences();
        Refresh();
    }

    private void Refresh()
    {
        if (player != null)
        {
            SetHP(player.currentHP, player.maxHP);
            SetGold(player.gold);
            return;
        }

        if (runManager == null)
        {
            return;
        }

        RefreshFromSnapshot(runManager.CurrentRunSnapshot);
    }

    private void RefreshFromSnapshot(PlayerRunSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        SetHP(snapshot.currentHP, snapshot.maxHP);
        SetGold(snapshot.gold);
    }

    private void SetHP(int current, int max)
    {
        if (hpText == null) return;
        hpText.text = showHpAsCurrentSlashMax ? $"{current}/{max}" : $"HP {current}";
    }

    private void SetGold(int gold)
    {
        if (goldText == null) return;
        goldText.text = $"{gold}";
    }
}

