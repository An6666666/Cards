using System.Collections;
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

    private Coroutine bindRoutine;
    private RunManager subscribedRunManager;

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        Refresh();
        RestartBindRoutine();
    }

    private void OnDisable()
    {
        StopBindRoutine();
        Unsubscribe();
    }

    private void RestartBindRoutine()
    {
        StopBindRoutine();
        bindRoutine = StartCoroutine(BindWhenReady());
    }

    private void StopBindRoutine()
    {
        if (bindRoutine == null)
        {
            return;
        }

        StopCoroutine(bindRoutine);
        bindRoutine = null;
    }

    private IEnumerator BindWhenReady()
    {
        while (isActiveAndEnabled)
        {
            ResolveReferences();
            Subscribe();
            Refresh();

            if (runManager != null && (player != null || runManager.CurrentRunSnapshot != null))
            {
                bindRoutine = null;
                yield break;
            }

            yield return null;
        }
    }

    private void ResolveReferences()
    {
        if (runManager == null)
        {
            runManager = RunManager.Instance;

            if (runManager == null)
            {
                runManager = FindObjectOfType<RunManager>(true);
            }
        }

        if (runManager != null && runManager.RegisteredPlayer != null)
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

        if (subscribedRunManager == runManager)
        {
            return;
        }

        Unsubscribe();
        subscribedRunManager = runManager;
        subscribedRunManager.RunSnapshotChanged += HandleRunSnapshotChanged;
        subscribedRunManager.MapStateChanged += HandleMapStateChanged;
    }

    private void Unsubscribe()
    {
        if (subscribedRunManager == null)
        {
            return;
        }

        subscribedRunManager.RunSnapshotChanged -= HandleRunSnapshotChanged;
        subscribedRunManager.MapStateChanged -= HandleMapStateChanged;
        subscribedRunManager = null;
    }

    private void HandleRunSnapshotChanged(PlayerRunSnapshot snapshot)
    {
        if (snapshot != null)
        {
            RefreshFromSnapshot(snapshot);
            return;
        }

        Refresh();
    }

    private void HandleMapStateChanged()
    {
        ResolveReferences();
        Subscribe();
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

